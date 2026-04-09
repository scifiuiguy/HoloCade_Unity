// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.ProAudio
{
    /// <summary>
    /// Lightweight OSC client implementation for Unity
    /// Uses UDP (same as HoloCade's other network modules) with OSC message encoding
    /// </summary>
    public class HoloCadeOSCClient : MonoBehaviour
    {
        private UdpClient udpClient;
        private IPEndPoint targetEndpoint;
        private bool isInitialized = false;

        /// <summary>
        /// Initialize OSC client connection
        /// </summary>
        public bool Initialize(string serverIP, int serverPort = 10023)
        {
            if (isInitialized)
            {
                Debug.LogWarning("[HoloCadeOSCClient] Already initialized");
                return false;
            }

            try
            {
                udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                IPAddress serverAddr;
                if (!IPAddress.TryParse(serverIP, out serverAddr))
                {
                    Debug.LogError($"[HoloCadeOSCClient] Invalid server IP: {serverIP}");
                    return false;
                }

                targetEndpoint = new IPEndPoint(serverAddr, serverPort);
                isInitialized = true;

                Debug.Log($"[HoloCadeOSCClient] Initialized (target: {serverIP}:{serverPort})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCadeOSCClient] Failed to initialize: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send OSC message with string address and float value
        /// </summary>
        public void SendOSCMessage(string address, float value)
        {
            if (!isInitialized || udpClient == null)
            {
                Debug.LogWarning("[HoloCadeOSCClient] Not initialized");
                return;
            }

            try
            {
                byte[] packet = BuildOSCPacket(address, value);
                udpClient.Send(packet, packet.Length, targetEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HoloCadeOSCClient] Failed to send OSC message: {ex.Message}");
            }
        }

        /// <summary>
        /// Send OSC message with string address and int value
        /// </summary>
        public void SendOSCMessage(string address, int value)
        {
            if (!isInitialized || udpClient == null)
            {
                Debug.LogWarning("[HoloCadeOSCClient] Not initialized");
                return;
            }

            try
            {
                byte[] packet = BuildOSCPacket(address, value);
                udpClient.Send(packet, packet.Length, targetEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HoloCadeOSCClient] Failed to send OSC message: {ex.Message}");
            }
        }

        /// <summary>
        /// Send OSC message with string address and bool value
        /// </summary>
        public void SendOSCMessage(string address, bool value)
        {
            SendOSCMessage(address, value ? 1 : 0);
        }

        /// <summary>
        /// Build OSC packet: [address][type tag][data]
        /// OSC uses null-terminated strings aligned to 4-byte boundaries
        /// </summary>
        private byte[] BuildOSCPacket(string address, float value)
        {
            List<byte> packet = new List<byte>();

            // OSC Address (null-terminated, 4-byte aligned)
            byte[] addressBytes = Encoding.UTF8.GetBytes(address);
            packet.AddRange(addressBytes);
            packet.Add(0);  // Null terminator
            PadToFourBytes(packet);

            // Type Tag String (",f" for float, null-terminated, 4-byte aligned)
            byte[] typeTag = Encoding.UTF8.GetBytes(",f");
            packet.AddRange(typeTag);
            packet.Add(0);  // Null terminator
            PadToFourBytes(packet);

            // Float value (4 bytes, big-endian per OSC spec)
            byte[] floatBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(floatBytes);
            packet.AddRange(floatBytes);

            return packet.ToArray();
        }

        /// <summary>
        /// Build OSC packet for int value
        /// </summary>
        private byte[] BuildOSCPacket(string address, int value)
        {
            List<byte> packet = new List<byte>();

            // OSC Address
            byte[] addressBytes = Encoding.UTF8.GetBytes(address);
            packet.AddRange(addressBytes);
            packet.Add(0);
            PadToFourBytes(packet);

            // Type Tag String (",i" for int)
            byte[] typeTag = Encoding.UTF8.GetBytes(",i");
            packet.AddRange(typeTag);
            packet.Add(0);
            PadToFourBytes(packet);

            // Int value (4 bytes, big-endian)
            byte[] intBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            packet.AddRange(intBytes);

            return packet.ToArray();
        }

        /// <summary>
        /// Pad packet to 4-byte boundary (OSC requirement)
        /// </summary>
        private void PadToFourBytes(List<byte> packet)
        {
            int remainder = packet.Count % 4;
            if (remainder != 0)
            {
                int padding = 4 - remainder;
                for (int i = 0; i < padding; i++)
                {
                    packet.Add(0);
                }
            }
        }

        /// <summary>
        /// Shutdown OSC client
        /// </summary>
        public void Shutdown()
        {
            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }
            targetEndpoint = null;
            isInitialized = false;
            Debug.Log("[HoloCadeOSCClient] Shutdown");
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}

