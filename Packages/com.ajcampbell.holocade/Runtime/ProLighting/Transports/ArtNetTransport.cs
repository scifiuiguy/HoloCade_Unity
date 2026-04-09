// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace HoloCade.ProLighting
{
    /// <summary>Minimal Art-Net transport (UDP)</summary>
    public class ArtNetTransport : IDMXTransport
    {
        private readonly string targetIP;
        private readonly int port;
        private readonly int net;
        private readonly int subnet;
        private UdpClient socket;
        private IPEndPoint address;

        public bool IsConnected => socket != null && address != null;

        public ArtNetTransport(string ip, int port, int net, int subnet)
        {
            targetIP = ip;
            this.port = port;
            this.net = net;
            this.subnet = subnet;
        }

        public bool Initialize()
        {
            try
            {
                if (!IPAddress.TryParse(targetIP, out var ipAddr))
                {
                    Debug.LogError($"ArtNetTransport: Invalid IP address {targetIP}");
                    return false;
                }
                address = new IPEndPoint(ipAddr, port);
                socket = new UdpClient();
                socket.EnableBroadcast = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"ArtNetTransport: Initialize failed - {ex.Message}");
                Shutdown();
                return false;
            }
        }

        public void Shutdown()
        {
            socket?.Close();
            socket?.Dispose();
            socket = null;
            address = null;
        }

        public void SendDMX(int universe, byte[] dmxData)
        {
            if (!IsConnected) return;
            var packet = BuildArtDmxPacket(universe, dmxData);
            try
            {
                socket.Send(packet, packet.Length, address);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ArtNetTransport: SendDMX failed - {ex.Message}");
            }
        }

        byte[] BuildArtDmxPacket(int universe, byte[] dmxData)
        {
            var packet = new byte[18 + 512];
            var offset = 0;
            var artNet = System.Text.Encoding.ASCII.GetBytes("Art-Net\0");
            Array.Copy(artNet, 0, packet, offset, 8);
            offset += 8;
            packet[offset++] = 0x00;
            packet[offset++] = 0x50; // OpDmx (0x5000 LE)
            packet[offset++] = 0x0E;
            packet[offset++] = 0x00; // ProtVer 14
            packet[offset++] = 0x00; // Sequence
            packet[offset++] = 0x00; // Physical
            var artUniverse = (subnet * 16) + universe;
            packet[offset++] = (byte)(artUniverse & 0xFF);
            packet[offset++] = (byte)((artUniverse >> 8) & 0xFF);
            packet[offset++] = 0x02; // Length hi (512)
            packet[offset++] = 0x00; // Length lo
            Array.Clear(packet, offset, 512);
            var copyLen = Mathf.Min(512, dmxData?.Length ?? 0);
            if (dmxData != null && copyLen > 0)
                Array.Copy(dmxData, 0, packet, offset, copyLen);
            return packet;
        }
    }
}

