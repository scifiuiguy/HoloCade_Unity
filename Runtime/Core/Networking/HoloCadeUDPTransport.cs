// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Events;

namespace HoloCade.Core.Networking
{
    /// <summary>
    /// HoloCade UDP Transport Component
    /// 
    /// Provides channel-based UDP communication using the HoloCade binary protocol.
    /// Used by HapticPlatformController and other systems that need simple UDP IO.
    /// 
    /// Protocol: [0xAA][Type][Channel][Payload...][CRC]
    /// - No encryption (for performance)
    /// - Simple CRC checksum
    /// - Channel-based API for easy integration
    /// </summary>
    public class HoloCadeUDPTransport : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string remoteIP = "192.168.1.100";
        [SerializeField] private int remotePort = 8888;
        [SerializeField] private string socketName = "HoloCade_UDP";

        // Code-only: not serialized so inspector wiring cannot persist broken listener lists on prefabs.
        private readonly IntEvent _onFloatReceived = new IntEvent();
        private readonly BoolEvent _onBoolReceived = new BoolEvent();
        private readonly Int32Event _onInt32Received = new Int32Event();
        private readonly BytesEvent _onBytesReceived = new BytesEvent();

        /// <summary>Invoked when a float payload is received for a channel (subscribe in code).</summary>
        public IntEvent onFloatReceived => _onFloatReceived;

        /// <summary>Invoked when a bool payload is received for a channel (subscribe in code).</summary>
        public BoolEvent onBoolReceived => _onBoolReceived;

        /// <summary>Invoked when an int32 payload is received for a channel (subscribe in code).</summary>
        public Int32Event onInt32Received => _onInt32Received;

        /// <summary>Invoked when a bytes payload is received for a channel (subscribe in code).</summary>
        public BytesEvent onBytesReceived => _onBytesReceived;

        // Base UDP transport (handles raw socket management)
        private UDPTransportBase udpTransport;

        // Cache of most recent received values per channel
        private Dictionary<int, float> receivedFloatCache = new Dictionary<int, float>();
        private Dictionary<int, bool> receivedBoolCache = new Dictionary<int, bool>();
        private Dictionary<int, int> receivedInt32Cache = new Dictionary<int, int>();
        private Dictionary<int, byte[]> receivedBytesCache = new Dictionary<int, byte[]>();

        // Protocol constants
        private const byte PACKET_START_MARKER = 0xAA;

        private enum DataType : byte
        {
            Bool = 0,
            Int32 = 1,
            Float = 2,
            String = 3,
            Bytes = 4
        }

        // UnityEvents for received data
        [System.Serializable] public class IntEvent : UnityEvent<int, float> { }
        [System.Serializable] public class BoolEvent : UnityEvent<int, bool> { }
        [System.Serializable] public class Int32Event : UnityEvent<int, int> { }
        [System.Serializable] public class BytesEvent : UnityEvent<int, byte[]> { }

        void Start()
        {
            InitializeUDPConnection(remoteIP, remotePort, socketName);
        }

        void Update()
        {
            if (udpTransport != null && udpTransport.IsUDPConnected())
            {
                ProcessIncomingUDPData();
            }
        }

        void OnDestroy()
        {
            ShutdownUDPConnection();
        }

        /// <summary>
        /// Initialize UDP connection
        /// </summary>
        public bool InitializeUDPConnection(string ip, int port, string name = "HoloCade_UDP")
        {
            remoteIP = ip;
            remotePort = port;
            socketName = name;

            udpTransport = new UDPTransportBase();
            bool success = udpTransport.InitializeUDPConnection(ip, port, name, false);

            if (success)
            {
                receivedFloatCache.Clear();
                receivedBoolCache.Clear();
                receivedInt32Cache.Clear();
                receivedBytesCache.Clear();
            }

            return success;
        }

        /// <summary>
        /// Shutdown UDP connection
        /// </summary>
        public void ShutdownUDPConnection()
        {
            if (udpTransport != null)
            {
                udpTransport.ShutdownUDPConnection();
                udpTransport = null;
            }

            receivedFloatCache.Clear();
            receivedBoolCache.Clear();
            receivedInt32Cache.Clear();
            receivedBytesCache.Clear();
        }

        /// <summary>
        /// Check if UDP connection is active
        /// </summary>
        public bool IsUDPConnected() => udpTransport != null && udpTransport.IsUDPConnected();

        // =====================================
        // Channel-Based Send API
        // =====================================

        public void SendFloat(int channel, float value)
        {
            byte[] payload = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(payload);
            SendPacket(DataType.Float, channel, payload);
        }

        public void SendBool(int channel, bool value)
        {
            byte[] payload = new byte[] { (byte)(value ? 1 : 0) };
            SendPacket(DataType.Bool, channel, payload);
        }

        public void SendInt32(int channel, int value)
        {
            byte[] payload = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(payload);
            SendPacket(DataType.Int32, channel, payload);
        }

        public void SendBytes(int channel, byte[] data)
        {
            int length = Math.Min(data.Length, 255);
            byte[] payload = new byte[length + 1];
            payload[0] = (byte)length;
            Array.Copy(data, 0, payload, 1, length);
            SendPacket(DataType.Bytes, channel, payload);
        }

        /// <summary>
        /// Send struct as bytes packet (for MVC pattern)
        /// </summary>
        public void SendStruct<T>(int channel, T data) where T : struct
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            byte[] bytes = new byte[size];
            System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }
            SendBytes(channel, bytes);
        }

        // =====================================
        // Channel-Based Receive API
        // =====================================

        public float GetReceivedFloat(int channel)
        {
            return receivedFloatCache.TryGetValue(channel, out float value) ? value : 0f;
        }

        public bool GetReceivedBool(int channel)
        {
            return receivedBoolCache.TryGetValue(channel, out bool value) ? value : false;
        }

        public int GetReceivedInt32(int channel)
        {
            return receivedInt32Cache.TryGetValue(channel, out int value) ? value : 0;
        }

        public byte[] GetReceivedBytes(int channel)
        {
            return receivedBytesCache.TryGetValue(channel, out byte[] value) ? value : new byte[0];
        }

        // =====================================
        // Protocol Implementation
        // =====================================

        private void SendPacket(DataType type, int channel, byte[] payload)
        {
            byte[] packet = BuildBinaryPacket(type, channel, payload);
            if (udpTransport != null)
            {
                udpTransport.SendUDPData(packet);
            }
        }

        private byte[] BuildBinaryPacket(DataType type, int channel, byte[] payload)
        {
            List<byte> packet = new List<byte>();
            packet.Add(PACKET_START_MARKER);
            packet.Add((byte)type);
            packet.Add((byte)channel);
            packet.AddRange(payload);

            // Calculate and append CRC
            byte crc = CalculateCRC(packet.ToArray());
            packet.Add(crc);

            return packet.ToArray();
        }

        private void ProcessIncomingUDPData()
        {
            if (udpTransport == null)
                return;

            byte[] data;
            int bytesRead;
            IPEndPoint sender;

            while (udpTransport.ReceiveUDPData(out data, out bytesRead, out sender))
            {
                if (data != null && bytesRead > 0)
                {
                    ParseBinaryPacket(data, bytesRead);
                }
            }
        }

        private void ParseBinaryPacket(byte[] data, int length)
        {
            // Validate start marker
            if (length < 1 || data[0] != PACKET_START_MARKER)
            {
                Debug.LogWarning("HoloCadeUDPTransport: Invalid start marker");
                return;
            }

            // Minimum: Marker(1) + Type(1) + Channel(1) + Payload(1) + CRC(1) = 5 bytes
            if (length < 5)
            {
                Debug.LogWarning($"HoloCadeUDPTransport: Packet too small ({length} bytes)");
                return;
            }

            // Validate CRC
            byte receivedCRC = data[length - 1];
            byte calculatedCRC = CalculateCRC(data, length - 1);
            if (receivedCRC != calculatedCRC)
            {
                Debug.LogWarning("HoloCadeUDPTransport: CRC mismatch");
                return;
            }

            // Parse packet: [0xAA][Type][Channel][Payload...][CRC]
            DataType type = (DataType)data[1];
            int channel = data[2];
            byte[] payloadData = new byte[length - 4]; // Exclude marker, type, channel, CRC
            Array.Copy(data, 3, payloadData, 0, length - 4);

            // Handle based on type
            switch (type)
            {
                case DataType.Bool:
                    if (payloadData.Length >= 1)
                    {
                        bool boolValue = payloadData[0] != 0;
                        receivedBoolCache[channel] = boolValue;
                        _onBoolReceived.Invoke(channel, boolValue);
                    }
                    break;

                case DataType.Int32:
                    if (payloadData.Length >= 4)
                    {
                        if (!BitConverter.IsLittleEndian)
                            Array.Reverse(payloadData, 0, 4);
                        int intValue = BitConverter.ToInt32(payloadData, 0);
                        receivedInt32Cache[channel] = intValue;
                        _onInt32Received.Invoke(channel, intValue);
                    }
                    break;

                case DataType.Float:
                    if (payloadData.Length >= 4)
                    {
                        if (!BitConverter.IsLittleEndian)
                            Array.Reverse(payloadData, 0, 4);
                        float floatValue = BitConverter.ToSingle(payloadData, 0);
                        receivedFloatCache[channel] = floatValue;
                        _onFloatReceived.Invoke(channel, floatValue);
                    }
                    break;

                case DataType.Bytes:
                    if (payloadData.Length >= 1)
                    {
                        int byteLength = payloadData[0];
                        if (payloadData.Length >= 1 + byteLength)
                        {
                            byte[] bytes = new byte[byteLength];
                            Array.Copy(payloadData, 1, bytes, 0, byteLength);
                            receivedBytesCache[channel] = bytes;
                            _onBytesReceived.Invoke(channel, bytes);
                        }
                    }
                    break;

                default:
                    Debug.LogWarning($"HoloCadeUDPTransport: Unknown data type ({type})");
                    break;
            }
        }

        private byte CalculateCRC(byte[] data, int length = -1)
        {
            if (length < 0)
                length = data.Length;

            byte crc = 0;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
            }
            return crc;
        }
    }
}


