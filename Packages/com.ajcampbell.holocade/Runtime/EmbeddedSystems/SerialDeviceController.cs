/*
 * HoloCade Serial Device Controller (Unity)
 * 
 * Secure bidirectional communication with embedded microcontrollers
 * Supports: AES-128-CTR encryption, HMAC-SHA1 authentication, UDP/Serial protocols
 * 
 * Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace HoloCade.EmbeddedSystems
{
    // =====================================
    // Enums
    // =====================================

    public enum MicrocontrollerType
    {
        Arduino,
        ESP32,
        STM32,
        RaspberryPi,
        JetsonNano,
        Custom
    }

    public enum CommProtocol
    {
        Serial,
        WiFi,
        Bluetooth,
        Ethernet
    }

    public enum SecurityLevel
    {
        None,       // Development only
        HMAC,       // Authentication only
        Encrypted   // AES-128 + HMAC (recommended)
    }

    public enum DataType : byte
    {
        Bool = 0,
        Int32 = 1,
        Float = 2,
        String = 3,
        Bytes = 4,
        Struct = 5
    }

    // =====================================
    // Configuration
    // =====================================

    [System.Serializable]
    public class EmbeddedDeviceConfig
    {
        [Header("Device")]
        public MicrocontrollerType deviceType = MicrocontrollerType.ESP32;
        public CommProtocol protocol = CommProtocol.WiFi;
        public string deviceAddress = "192.168.1.50";
        public int port = 8888;
        public int baudRate = 115200;

        [Header("Channels")]
        public int inputChannelCount = 8;
        public int outputChannelCount = 8;

        [Header("Security")]
        public SecurityLevel securityLevel = SecurityLevel.Encrypted;
        [Tooltip("Shared secret key for encryption (must match ESP32 firmware)")]
        public string sharedSecret = "CHANGE_ME_IN_PRODUCTION_2025";

        [Header("Debug")]
        [Tooltip("Use JSON packets for Wireshark inspection (disables encryption!)")]
        public bool debugMode = false;
    }

    // =====================================
    // Events
    // =====================================

    [System.Serializable] public class BoolEvent : UnityEvent<int, bool> { }
    [System.Serializable] public class Int32Event : UnityEvent<int, int> { }
    [System.Serializable] public class FloatEvent : UnityEvent<int, float> { }
    [System.Serializable] public class StringEvent : UnityEvent<int, string> { }
    [System.Serializable] public class BytesEvent : UnityEvent<int, byte[]> { }

    // =====================================
    // Main Controller
    // =====================================

    public class SerialDeviceController : MonoBehaviour
    {
        [Header("Configuration")]
        public EmbeddedDeviceConfig config = new EmbeddedDeviceConfig();

        [Header("Events")]
        public BoolEvent onBoolReceived = new BoolEvent();
        public Int32Event onInt32Received = new Int32Event();
        public FloatEvent onFloatReceived = new FloatEvent();
        public StringEvent onStringReceived = new StringEvent();
        public BytesEvent onBytesReceived = new BytesEvent();

        // Networking
        private UdpClient udpClient;
        private IPEndPoint remoteEndPoint;
        private bool isConnected = false;

        // Cryptography
        private byte[] derivedAESKey = new byte[16];   // 128-bit
        private byte[] derivedHMACKey = new byte[32];  // 256-bit
        private RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        // Protocol
        private const byte PACKET_START_MARKER = 0xAA;
        private Dictionary<int, float> inputValueCache = new Dictionary<int, float>();

        // =====================================
        // Lifecycle
        // =====================================

        void Start()
        {
            InitializeDevice(config);
        }

        void Update()
        {
            if (isConnected)
            {
                ProcessIncomingData();
            }
        }

        void OnDestroy()
        {
            DisconnectDevice();
        }

        // =====================================
        // Initialization
        // =====================================

        public bool InitializeDevice(EmbeddedDeviceConfig cfg)
        {
            config = cfg;

            // Security warning if debug mode conflicts with security
            if (config.debugMode && config.securityLevel != SecurityLevel.None)
            {
                Debug.LogWarning("========================================");
                Debug.LogWarning("⚠️  SECURITY WARNING ⚠️");
                Debug.LogWarning("========================================");
                Debug.LogWarning("Debug mode DISABLES encryption for Wireshark packet inspection!");
                Debug.LogWarning($"SecurityLevel is set to '{config.securityLevel}' but will be IGNORED in debug mode.");
                Debug.LogWarning("All packets will be sent as PLAIN JSON (no encryption).");
                Debug.LogWarning("");
                Debug.LogWarning("⛔ NEVER USE DEBUG MODE IN PRODUCTION! ⛔");
                Debug.LogWarning("========================================");
            }

            // Derive encryption keys (only if not in debug mode)
            if (config.securityLevel != SecurityLevel.None && !config.debugMode)
            {
                DeriveKeysFromSecret();
                Debug.Log($"EmbeddedDeviceController: Security enabled ({config.securityLevel})");
            }
            else if (config.securityLevel == SecurityLevel.None)
            {
                Debug.LogWarning("EmbeddedDeviceController: Security DISABLED (Development Only)");
            }

            // Establish connection
            bool success = false;
            switch (config.protocol)
            {
                case CommProtocol.WiFi:
                case CommProtocol.Ethernet:
                    success = InitializeWiFiConnection();
                    break;

                case CommProtocol.Serial:
                    Debug.LogWarning("Serial communication not yet implemented");
                    break;

                case CommProtocol.Bluetooth:
                    Debug.LogWarning("Bluetooth not yet implemented");
                    break;
            }

            if (!success)
            {
                Debug.LogError($"Failed to initialize {config.protocol} connection");
                return false;
            }

            // Initialize input cache
            inputValueCache.Clear();
            for (int i = 0; i < config.inputChannelCount; i++)
            {
                inputValueCache[i] = 0f;
            }

            isConnected = true;
            Debug.Log($"EmbeddedDeviceController: Initialized successfully ({(config.debugMode ? "JSON Debug" : "Binary")} mode, {config.securityLevel})");
            return true;
        }

        private bool InitializeWiFiConnection()
        {
            try
            {
                // Create UDP client
                udpClient = new UdpClient();
                udpClient.Client.Blocking = false;  // Non-blocking
                udpClient.Client.ReceiveBufferSize = 8192;

                // Parse remote endpoint
                IPAddress ipAddress;
                if (!IPAddress.TryParse(config.deviceAddress, out ipAddress))
                {
                    Debug.LogError($"Invalid IP address: {config.deviceAddress}");
                    return false;
                }

                remoteEndPoint = new IPEndPoint(ipAddress, config.port);

                Debug.Log($"UDP socket created successfully (target: {config.deviceAddress}:{config.port})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create UDP socket: {e.Message}");
                return false;
            }
        }

        public void DisconnectDevice()
        {
            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }

            isConnected = false;
            inputValueCache.Clear();
            Debug.Log("EmbeddedDeviceController: Disconnected");
        }

        public bool IsDeviceConnected()
        {
            return isConnected;
        }

        /// <summary>
        /// Get digital input state (button press)
        /// </summary>
        /// <param name="channel">Channel/pin number</param>
        /// <returns>True if button is pressed, false otherwise</returns>
        public bool GetDigitalInput(int channel)
        {
            if (inputValueCache.TryGetValue(channel, out float value))
            {
                // Digital input: > 0.5 = pressed
                return value > 0.5f;
            }
            return false;
        }

        /// <summary>
        /// Get analog input value (0.0 to 1.0)
        /// </summary>
        /// <param name="channel">Channel/pin number</param>
        /// <returns>Analog value from 0.0 to 1.0</returns>
        public float GetAnalogInput(int channel)
        {
            if (inputValueCache.TryGetValue(channel, out float value))
            {
                return value;
            }
            return 0.0f;
        }

        // =====================================
        // Cryptography - Key Derivation
        // =====================================

        private void DeriveKeysFromSecret()
        {
            byte[] secretBytes = Encoding.UTF8.GetBytes(config.sharedSecret);

            // Derive AES key: SHA1(Secret + "AES128_HoloCade_2025")
            using (var sha1 = SHA1.Create())
            {
                byte[] aesInput = CombineBytes(secretBytes, Encoding.UTF8.GetBytes("AES128_HoloCade_2025"));
                byte[] aesHash = sha1.ComputeHash(aesInput);
                Array.Copy(aesHash, derivedAESKey, 16);  // First 16 bytes
            }

            // Derive HMAC key: SHA1(Secret + "HMAC_HoloCade_2025")
            using (var sha1 = SHA1.Create())
            {
                byte[] hmacInput = CombineBytes(secretBytes, Encoding.UTF8.GetBytes("HMAC_HoloCade_2025"));
                byte[] hmacHash = sha1.ComputeHash(hmacInput);
                Array.Copy(hmacHash, derivedHMACKey, 20);  // First 20 bytes
            }

            Debug.Log("Derived AES and HMAC keys from shared secret", this);
        }

        // =====================================
        // Cryptography - AES-128-CTR
        // =====================================

        private byte[] EncryptAES128(byte[] plaintext, uint iv)
        {
            if (plaintext.Length == 0) return new byte[0];

            byte[] ciphertext = new byte[plaintext.Length];

            using (var aes = Aes.Create())
            {
                aes.Key = derivedAESKey;
                aes.Mode = CipherMode.ECB;  // We implement CTR manually
                aes.Padding = PaddingMode.None;

                using (var encryptor = aes.CreateEncryptor())
                {
                    int blockCount = (plaintext.Length + 15) / 16;

                    for (int blockIdx = 0; blockIdx < blockCount; blockIdx++)
                    {
                        // Create counter block
                        byte[] counterBlock = new byte[16];
                        uint currentCounter = iv + (uint)blockIdx;
                        
                        counterBlock[0] = (byte)(currentCounter & 0xFF);
                        counterBlock[1] = (byte)((currentCounter >> 8) & 0xFF);
                        counterBlock[2] = (byte)((currentCounter >> 16) & 0xFF);
                        counterBlock[3] = (byte)((currentCounter >> 24) & 0xFF);
                        counterBlock[4] = (byte)(blockIdx & 0xFF);
                        counterBlock[5] = (byte)((blockIdx >> 8) & 0xFF);
                        counterBlock[6] = (byte)((blockIdx >> 16) & 0xFF);
                        counterBlock[7] = (byte)((blockIdx >> 24) & 0xFF);

                        // Encrypt counter block
                        byte[] encryptedCounter = new byte[16];
                        encryptor.TransformBlock(counterBlock, 0, 16, encryptedCounter, 0);

                        // XOR with plaintext
                        int bytesInBlock = Math.Min(16, plaintext.Length - blockIdx * 16);
                        for (int i = 0; i < bytesInBlock; i++)
                        {
                            ciphertext[blockIdx * 16 + i] = (byte)(plaintext[blockIdx * 16 + i] ^ encryptedCounter[i]);
                        }
                    }
                }
            }

            return ciphertext;
        }

        private byte[] DecryptAES128(byte[] ciphertext, uint iv)
        {
            // CTR mode decryption is identical to encryption
            return EncryptAES128(ciphertext, iv);
        }

        // =====================================
        // Cryptography - HMAC-SHA1
        // =====================================

        private byte[] CalculateHMAC(byte[] data)
        {
            using (var hmac = new HMACSHA1(derivedHMACKey))
            {
                byte[] hash = hmac.ComputeHash(data);
                byte[] truncated = new byte[8];
                Array.Copy(hash, truncated, 8);  // Truncate to 8 bytes
                return truncated;
            }
        }

        private bool ValidateHMAC(byte[] data, byte[] expectedHMAC)
        {
            if (expectedHMAC.Length != 8) return false;

            byte[] calculatedHMAC = CalculateHMAC(data);

            // Constant-time comparison
            byte diff = 0;
            for (int i = 0; i < 8; i++)
            {
                diff |= (byte)(calculatedHMAC[i] ^ expectedHMAC[i]);
            }

            return diff == 0;
        }

        // =====================================
        // Random Number Generation
        // =====================================

        private uint GenerateRandomIV()
        {
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        // =====================================
        // Sending - Primitive API
        // =====================================

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

        public void SendFloat(int channel, float value)
        {
            byte[] payload = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(payload);
            SendPacket(DataType.Float, channel, payload);
        }

        public void SendString(int channel, string value)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(value);
            int length = Math.Min(utf8Bytes.Length, 255);

            byte[] payload = new byte[length + 1];
            payload[0] = (byte)length;
            Array.Copy(utf8Bytes, 0, payload, 1, length);

            SendPacket(DataType.String, channel, payload);
        }

        public void SendBytes(int channel, byte[] data)
        {
            int length = Math.Min(data.Length, 255);

            byte[] payload = new byte[length + 1];
            payload[0] = (byte)length;
            Array.Copy(data, 0, payload, 1, length);

            SendPacket(DataType.Bytes, channel, payload);
        }

        // =====================================
        // Packet Building
        // =====================================

        private void SendPacket(DataType type, int channel, byte[] payload)
        {
            byte[] packet;

            if (config.debugMode)
            {
                // JSON mode (always plain, ignore SecurityLevel)
                packet = BuildJSONPacket(type, channel, payload);
            }
            else
            {
                // Binary mode (respects SecurityLevel)
                packet = BuildBinaryPacket(type, channel, payload);
            }

            SendDataToDevice(packet);
        }

        private byte[] BuildBinaryPacket(DataType type, int channel, byte[] payload)
        {
            List<byte> packet = new List<byte>();

            if (config.securityLevel == SecurityLevel.Encrypted)
            {
                // Encrypted format: [0xAA][IV:4][Encrypted(Type|Ch|Payload):N][HMAC:8]

                // Build plaintext
                List<byte> plaintext = new List<byte>();
                plaintext.Add((byte)type);
                plaintext.Add((byte)channel);
                plaintext.AddRange(payload);

                // Generate random IV
                uint iv = GenerateRandomIV();

                // Encrypt
                byte[] ciphertext = EncryptAES128(plaintext.ToArray(), iv);

                // Build packet
                packet.Add(PACKET_START_MARKER);
                packet.Add((byte)(iv & 0xFF));
                packet.Add((byte)((iv >> 8) & 0xFF));
                packet.Add((byte)((iv >> 16) & 0xFF));
                packet.Add((byte)((iv >> 24) & 0xFF));
                packet.AddRange(ciphertext);

                // Calculate and append HMAC
                byte[] hmac = CalculateHMAC(packet.ToArray());
                packet.AddRange(hmac);
            }
            else if (config.securityLevel == SecurityLevel.HMAC)
            {
                // HMAC-only format: [0xAA][Type][Ch][Payload][HMAC:8]

                packet.Add(PACKET_START_MARKER);
                packet.Add((byte)type);
                packet.Add((byte)channel);
                packet.AddRange(payload);

                // Calculate and append HMAC
                byte[] hmac = CalculateHMAC(packet.ToArray());
                packet.AddRange(hmac);
            }
            else
            {
                // No security: [0xAA][Type][Ch][Payload][CRC:1]

                packet.Add(PACKET_START_MARKER);
                packet.Add((byte)type);
                packet.Add((byte)channel);
                packet.AddRange(payload);

                // Calculate and append CRC
                byte crc = CalculateCRC(packet.ToArray());
                packet.Add(crc);
            }

            return packet.ToArray();
        }

        private byte[] BuildJSONPacket(DataType type, int channel, byte[] payload)
        {
            string typeStr = type.ToString().ToLower();
            string valueStr = "";

            switch (type)
            {
                case DataType.Bool:
                    valueStr = (payload[0] != 0) ? "true" : "false";
                    break;
                case DataType.Int32:
                    int intVal = BitConverter.ToInt32(payload, 0);
                    valueStr = intVal.ToString();
                    break;
                case DataType.Float:
                    float floatVal = BitConverter.ToSingle(payload, 0);
                    valueStr = floatVal.ToString("F3");
                    break;
                case DataType.String:
                    string strVal = Encoding.UTF8.GetString(payload, 1, payload[0]);
                    valueStr = $"\"{strVal.Replace("\"", "\\\"")}\"";
                    break;
                case DataType.Bytes:
                    string hexStr = BitConverter.ToString(payload, 1, payload[0]).Replace("-", "");
                    valueStr = $"\"{hexStr}\"";
                    break;
            }

            string json = $"{{\"ch\":{channel},\"type\":\"{typeStr}\",\"val\":{valueStr}}}";
            return Encoding.UTF8.GetBytes(json);
        }

        private void SendDataToDevice(byte[] data)
        {
            if (!isConnected || udpClient == null || data.Length == 0)
                return;

            try
            {
                udpClient.Send(data, data.Length, remoteEndPoint);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to send data: {e.Message}");
            }
        }

        // =====================================
        // Packet Parsing
        // =====================================

        private void ProcessIncomingData()
        {
            if (udpClient == null || udpClient.Available == 0)
                return;

            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref sender);

                if (config.debugMode)
                {
                    ParseJSONPacket(data);
                }
                else
                {
                    ParseBinaryPacket(data);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error receiving data: {e.Message}");
            }
        }

        private void ParseBinaryPacket(byte[] data)
        {
            if (data.Length < 1 || data[0] != PACKET_START_MARKER)
            {
                Debug.LogWarning("Invalid start marker");
                return;
            }

            DataType type;
            int channel;
            byte[] payloadData;

            if (config.securityLevel == SecurityLevel.Encrypted)
            {
                // Encrypted format: [0xAA][IV:4][Encrypted(Type|Ch|Payload):N][HMAC:8]
                if (data.Length < 15)
                {
                    Debug.LogWarning($"Encrypted packet too small ({data.Length} bytes)");
                    return;
                }

                // Extract and validate HMAC
                byte[] receivedHMAC = new byte[8];
                Array.Copy(data, data.Length - 8, receivedHMAC, 0, 8);

                byte[] dataForHMAC = new byte[data.Length - 8];
                Array.Copy(data, dataForHMAC, data.Length - 8);

                if (!ValidateHMAC(dataForHMAC, receivedHMAC))
                {
                    Debug.LogWarning("HMAC validation failed");
                    return;
                }

                // Extract IV
                uint iv = (uint)(data[1] | (data[2] << 8) | (data[3] << 16) | (data[4] << 24));

                // Extract and decrypt ciphertext
                int ciphertextLen = data.Length - 13;
                byte[] ciphertext = new byte[ciphertextLen];
                Array.Copy(data, 5, ciphertext, 0, ciphertextLen);

                byte[] plaintext = DecryptAES128(ciphertext, iv);

                if (plaintext.Length < 2)
                {
                    Debug.LogWarning("Decrypted payload too small");
                    return;
                }

                type = (DataType)plaintext[0];
                channel = plaintext[1];
                payloadData = new byte[plaintext.Length - 2];
                Array.Copy(plaintext, 2, payloadData, 0, payloadData.Length);
            }
            else if (config.securityLevel == SecurityLevel.HMAC)
            {
                // HMAC-only format: [0xAA][Type][Ch][Payload][HMAC:8]
                if (data.Length < 12)
                {
                    Debug.LogWarning($"HMAC packet too small ({data.Length} bytes)");
                    return;
                }

                // Extract and validate HMAC
                byte[] receivedHMAC = new byte[8];
                Array.Copy(data, data.Length - 8, receivedHMAC, 0, 8);

                byte[] dataForHMAC = new byte[data.Length - 8];
                Array.Copy(data, dataForHMAC, data.Length - 8);

                if (!ValidateHMAC(dataForHMAC, receivedHMAC))
                {
                    Debug.LogWarning("HMAC validation failed");
                    return;
                }

                type = (DataType)data[1];
                channel = data[2];
                payloadData = new byte[data.Length - 11];
                Array.Copy(data, 3, payloadData, 0, payloadData.Length);
            }
            else
            {
                // No security: [0xAA][Type][Ch][Payload][CRC:1]
                if (data.Length < 5)
                {
                    Debug.LogWarning($"Packet too small ({data.Length} bytes)");
                    return;
                }

                // Validate CRC
                byte receivedCRC = data[data.Length - 1];
                byte[] dataForCRC = new byte[data.Length - 1];
                Array.Copy(data, dataForCRC, data.Length - 1);

                if (!ValidateCRC(dataForCRC, receivedCRC))
                {
                    Debug.LogWarning("CRC validation failed");
                    return;
                }

                type = (DataType)data[1];
                channel = data[2];
                payloadData = new byte[data.Length - 4];
                Array.Copy(data, 3, payloadData, 0, payloadData.Length);
            }

            // Dispatch based on type
            HandleReceivedData(type, channel, payloadData);
        }

        private void ParseJSONPacket(byte[] data)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                var dict = MiniJSON.Json.Deserialize(json) as Dictionary<string, object>;

                if (dict == null) return;

                int channel = Convert.ToInt32(dict["ch"]);
                string typeStr = dict["type"] as string;

                DataType type = DataType.Bool;
                byte[] payloadData = null;

                switch (typeStr)
                {
                    case "bool":
                        type = DataType.Bool;
                        bool boolVal = Convert.ToBoolean(dict["val"]);
                        payloadData = new byte[] { (byte)(boolVal ? 1 : 0) };
                        break;

                    case "int":
                    case "int32":
                        type = DataType.Int32;
                        int intVal = Convert.ToInt32(dict["val"]);
                        payloadData = BitConverter.GetBytes(intVal);
                        break;

                    case "float":
                        type = DataType.Float;
                        float floatVal = Convert.ToSingle(dict["val"]);
                        payloadData = BitConverter.GetBytes(floatVal);
                        break;

                    case "string":
                        type = DataType.String;
                        string strVal = dict["val"] as string;
                        byte[] strBytes = Encoding.UTF8.GetBytes(strVal);
                        payloadData = new byte[strBytes.Length + 1];
                        payloadData[0] = (byte)strBytes.Length;
                        Array.Copy(strBytes, 0, payloadData, 1, strBytes.Length);
                        break;
                }

                HandleReceivedData(type, channel, payloadData);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to parse JSON: {e.Message}");
            }
        }

        private void HandleReceivedData(DataType type, int channel, byte[] payload)
        {
            switch (type)
            {
                case DataType.Bool:
                    if (payload.Length >= 1)
                    {
                        bool value = payload[0] != 0;
                        inputValueCache[channel] = value ? 1f : 0f;
                        onBoolReceived?.Invoke(channel, value);
                    }
                    break;

                case DataType.Int32:
                    if (payload.Length >= 4)
                    {
                        int value = BitConverter.ToInt32(payload, 0);
                        inputValueCache[channel] = value;
                        onInt32Received?.Invoke(channel, value);
                    }
                    break;

                case DataType.Float:
                    if (payload.Length >= 4)
                    {
                        float value = BitConverter.ToSingle(payload, 0);
                        inputValueCache[channel] = value;
                        onFloatReceived?.Invoke(channel, value);
                    }
                    break;

                case DataType.String:
                    if (payload.Length >= 1)
                    {
                        int length = payload[0];
                        if (payload.Length >= 1 + length)
                        {
                            string value = Encoding.UTF8.GetString(payload, 1, length);
                            onStringReceived?.Invoke(channel, value);
                        }
                    }
                    break;

                case DataType.Bytes:
                    if (payload.Length >= 1)
                    {
                        int length = payload[0];
                        if (payload.Length >= 1 + length)
                        {
                            byte[] bytes = new byte[length];
                            Array.Copy(payload, 1, bytes, 0, length);
                            onBytesReceived?.Invoke(channel, bytes);
                        }
                    }
                    break;
            }
        }

        // =====================================
        // Utility
        // =====================================

        private byte CalculateCRC(byte[] data)
        {
            byte crc = 0;
            foreach (byte b in data)
            {
                crc ^= b;
            }
            return crc;
        }

        private bool ValidateCRC(byte[] data, byte expectedCRC)
        {
            return CalculateCRC(data) == expectedCRC;
        }

        private byte[] CombineBytes(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length + b.Length];
            Array.Copy(a, result, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}
