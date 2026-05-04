// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Listens for HoloCade-compatible UDP packets (same framing as <c>HoloCadeUDPTransport</c>)
    /// from HyperCube vision — float/int32 channels for atlas-normalized pose stubs.
    /// </summary>
    [DisallowMultipleComponent]
    public class HyperCubeUdpPoseReceiver : MonoBehaviour
    {
        const byte PacketStartMarker = 0xAA;

        enum DataType : byte
        {
            Bool = 0,
            Int32 = 1,
            Float = 2,
            String = 3,
            Bytes = 4
        }

        [SerializeField] int listenPort = 18100;
        [SerializeField] string bindAddress = "0.0.0.0";

        UdpClient _udp;
        readonly Dictionary<int, float> _floatCache = new Dictionary<int, float>();
        readonly Dictionary<int, int> _intCache = new Dictionary<int, int>();

        public int ListenPort => listenPort;

        void OnEnable()
        {
            Shutdown();
            try
            {
                if (!IPAddress.TryParse(bindAddress, out var addr))
                    addr = IPAddress.Any;
                _udp = new UdpClient(new IPEndPoint(addr, listenPort));
                _udp.Client.Blocking = false;
                _udp.Client.ReceiveBufferSize = 65536;
            }
            catch (Exception e)
            {
                Debug.LogError($"[HyperCubeUdpPoseReceiver] bind failed: {e.Message}");
                _udp = null;
            }
        }

        void OnDisable()
        {
            Shutdown();
        }

        void Shutdown()
        {
            if (_udp != null)
            {
                try
                {
                    _udp.Close();
                    _udp.Dispose();
                }
                catch (Exception) { /* ignore */ }
                _udp = null;
            }
        }

        void Update()
        {
            if (_udp == null)
                return;

            while (_udp.Available > 0)
            {
                IPEndPoint remote = null;
                byte[] data;
                try
                {
                    data = _udp.Receive(ref remote);
                }
                catch (SocketException)
                {
                    break;
                }

                if (data == null || data.Length < 5)
                    continue;

                if (!TryParsePacket(data, data.Length, out var type, out var channel, out var payload))
                    continue;

                switch (type)
                {
                    case DataType.Float when payload.Length >= 4:
                        if (!BitConverter.IsLittleEndian)
                            Array.Reverse(payload, 0, 4);
                        _floatCache[channel] = BitConverter.ToSingle(payload, 0);
                        break;
                    case DataType.Int32 when payload.Length >= 4:
                        if (!BitConverter.IsLittleEndian)
                            Array.Reverse(payload, 0, 4);
                        _intCache[channel] = BitConverter.ToInt32(payload, 0);
                        break;
                }
            }
        }

        public bool TryGetFloat(int channel, out float value)
        {
            return _floatCache.TryGetValue(channel, out value);
        }

        public bool TryGetInt32(int channel, out int value)
        {
            return _intCache.TryGetValue(channel, out value);
        }

        static bool TryParsePacket(byte[] data, int length, out DataType type, out int channel, out byte[] payload)
        {
            type = 0;
            channel = 0;
            payload = null;

            if (length < 5 || data[0] != PacketStartMarker)
                return false;

            byte receivedCrc = data[length - 1];
            if (CalculateCrc(data, length - 1) != receivedCrc)
                return false;

            type = (DataType)data[1];
            channel = data[2];
            int payloadLen = length - 4;
            if (payloadLen < 0)
                return false;
            payload = new byte[payloadLen];
            Buffer.BlockCopy(data, 3, payload, 0, payloadLen);
            return true;
        }

        static byte CalculateCrc(byte[] data, int length)
        {
            byte crc = 0;
            for (int i = 0; i < length; i++)
                crc ^= data[i];
            return crc;
        }
    }
}
