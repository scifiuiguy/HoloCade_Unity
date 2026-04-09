// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace HoloCade.ProLighting
{
    /// <summary>Consolidated manager for Art-Net transport + discovery</summary>
    public class ArtNetManager : IDMXTransport
    {
        public event Action<HoloCadeArtNetNode> OnNodeDiscovered;

        private ArtNetTransport transport;
        private UdpClient discoverySocket;
        private IPEndPoint sendAddr;
        private int artNetPort = 6454;
        private float pollIntervalSeconds = 2f;
        private float accumulated = 0f;
        private readonly Dictionary<string, HoloCadeArtNetNode> discoveredNodes = new Dictionary<string, HoloCadeArtNetNode>();

        public bool IsConnected => transport != null && transport.IsConnected;

        public Dictionary<string, HoloCadeArtNetNode> GetDiscoveredArtNetNodes() => discoveredNodes;

        public List<HoloCadeArtNetNode> GetNodes() => discoveredNodes.Values.ToList();

        public bool Initialize(string ip, int port, int net, int subnet)
        {
            artNetPort = port;
            transport = new ArtNetTransport(ip, port, net, subnet);
            return true;
        }

        bool IDMXTransport.Initialize()
        {
            if (transport == null) return false;
            if (!transport.Initialize()) return false;
            if (!InitializeDiscovery((ushort)artNetPort))
                Debug.LogWarning("ArtNetManager: Discovery init failed; transport only");
            return true;
        }

        public void Shutdown()
        {
            discoverySocket?.Close();
            discoverySocket?.Dispose();
            discoverySocket = null;
            sendAddr = null;
            discoveredNodes.Clear();
            transport?.Shutdown();
            transport = null;
        }

        public void SendDMX(int universe, byte[] dmxData) => transport?.SendDMX(universe, dmxData);

        public void Tick(float deltaTime)
        {
            ProcessIncoming();
            accumulated += deltaTime;
            if (accumulated >= pollIntervalSeconds)
            {
                SendArtPoll();
                accumulated = 0f;
            }
        }

        public void SendArtPoll()
        {
            if (discoverySocket == null || sendAddr == null) return;
            var packet = BuildArtPollPacket();
            try
            {
                discoverySocket.Send(packet, packet.Length, sendAddr);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ArtNetManager: ArtPoll send failed - {ex.Message}");
            }
        }

        bool InitializeDiscovery(ushort port)
        {
            try
            {
                discoverySocket = new UdpClient(port);
                discoverySocket.EnableBroadcast = true;
                sendAddr = new IPEndPoint(IPAddress.Broadcast, port);
                Debug.Log($"ArtNetManager: Discovery initialized on port {port}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"ArtNetManager: Discovery init failed - {ex.Message}");
                discoverySocket?.Close();
                discoverySocket?.Dispose();
                discoverySocket = null;
                return false;
            }
        }

        void ProcessIncoming()
        {
            if (discoverySocket == null) return;
            while (discoverySocket.Available > 0)
            {
                try
                {
                    IPEndPoint remoteEP = null;
                    var data = discoverySocket.Receive(ref remoteEP);
                    if (data != null && data.Length > 0)
                    {
                        var sourceIP = remoteEP.Address.ToString();
                        var node = new HoloCadeArtNetNode();
                        if (ParseArtPollReply(data, ref node))
                        {
                            if (!discoveredNodes.ContainsKey(sourceIP))
                            {
                                node.IPAddress = sourceIP;
                                node.LastSeenTimestamp = DateTime.Now;
                                discoveredNodes[sourceIP] = node;
                                OnNodeDiscovered?.Invoke(node);
                                Debug.Log($"ArtNetManager: Discovered node: {node.NodeName} ({sourceIP})");
                            }
                            else
                            {
                                var existing = discoveredNodes[sourceIP];
                                existing.LastSeenTimestamp = DateTime.Now;
                                discoveredNodes[sourceIP] = existing;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"ArtNetManager: ProcessIncoming error - {ex.Message}");
                    break;
                }
            }
        }

        byte[] BuildArtPollPacket()
        {
            var packet = new byte[14];
            var offset = 0;
            var artNet = Encoding.ASCII.GetBytes("Art-Net\0");
            Array.Copy(artNet, 0, packet, offset, 8);
            offset += 8;
            packet[offset++] = 0x20;
            packet[offset++] = 0x00; // OpCode ArtPoll
            packet[offset++] = 0x0E;
            packet[offset++] = 0x00; // ProtVer 14
            packet[offset++] = 0x02; // Flags
            packet[offset++] = 0x07; // TalkToMe
            packet[offset++] = 0x00;
            packet[offset++] = 0x00;
            return packet;
        }

        bool ParseArtPollReply(byte[] packetData, ref HoloCadeArtNetNode node)
        {
            if (packetData.Length < 240) return false;
            var header = Encoding.ASCII.GetString(packetData, 0, 8);
            if (header != "Art-Net\0") return false;
            var opCode = (ushort)(packetData[8] | (packetData[9] << 8));
            if (opCode != 0x0021) return false; // ArtPollReply
            var nameOffset = 26;
            node.NodeName = Encoding.ASCII.GetString(packetData, nameOffset, 18).TrimEnd('\0');
            nameOffset = 44;
            var longName = Encoding.ASCII.GetString(packetData, nameOffset, 64).TrimEnd('\0');
            if (longName.Length > 0) node.NodeType = longName;
            var numPortsHi = (packetData[173] >> 4) & 0x0F;
            var numPortsLo = packetData[173] & 0x0F;
            node.OutputCount = Mathf.Max(1, numPortsHi * 16 + numPortsLo);
            node.UniversesPerOutput = 1;
            return true;
        }
    }
}

