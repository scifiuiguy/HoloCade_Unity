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
    /// Lightweight OSC server implementation for Unity
    /// Listens for OSC messages from physical audio boards (for bidirectional sync)
    /// </summary>
    public class HoloCadeOSCServer : MonoBehaviour
    {
        private UdpClient udpListener;
        private int listenPort;
        private bool isListening = false;
        private System.Threading.Thread receiveThread;

        // Callback when OSC message received: (address, value)
        public Action<string, float> OnFloatMessageReceived;
        public Action<string, int> OnIntMessageReceived;

        /// <summary>
        /// Start listening for OSC messages
        /// </summary>
        public bool StartListening(int port)
        {
            if (isListening)
            {
                Debug.LogWarning("[HoloCadeOSCServer] Already listening");
                return false;
            }

            try
            {
                listenPort = port;
                udpListener = new UdpClient(port);
                udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                isListening = true;

                // Start receive thread
                receiveThread = new System.Threading.Thread(ReceiveLoop);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                Debug.Log($"[HoloCadeOSCServer] Started listening on port {port}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCadeOSCServer] Failed to start listening: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stop listening
        /// </summary>
        public void StopListening()
        {
            isListening = false;

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(1000); // Wait up to 1 second
            }

            if (udpListener != null)
            {
                udpListener.Close();
                udpListener = null;
            }

            Debug.Log("[HoloCadeOSCServer] Stopped listening");
        }

        /// <summary>
        /// Receive loop (runs in background thread)
        /// </summary>
        private void ReceiveLoop()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (isListening && udpListener != null)
            {
                try
                {
                    byte[] data = udpListener.Receive(ref remoteEndPoint);
                    if (data != null && data.Length > 0)
                    {
                        // Copy data to avoid thread safety issues
                        byte[] messageData = new byte[data.Length];
                        Array.Copy(data, messageData, data.Length);
                        
                        // Parse OSC message on main thread
                        if (UnityMainThreadDispatcher.Instance != null)
                        {
                            UnityMainThreadDispatcher.Instance.Enqueue(() => ParseOSCMessage(messageData));
                        }
                    }
                }
                catch (SocketException ex)
                {
                    if (isListening)
                    {
                        Debug.LogWarning($"[HoloCadeOSCServer] Receive error: {ex.Message}");
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HoloCadeOSCServer] Receive loop error: {ex.Message}");
                    break;
                }
            }
        }

        /// <summary>
        /// Parse OSC message and extract address + value
        /// </summary>
        private void ParseOSCMessage(byte[] data)
        {
            try
            {
                int offset = 0;

                // Read OSC address (null-terminated string, 4-byte aligned)
                string address = ReadOSCString(data, ref offset);
                if (string.IsNullOrEmpty(address))
                    return;

                // Read type tag string (null-terminated, 4-byte aligned)
                string typeTag = ReadOSCString(data, ref offset);
                if (string.IsNullOrEmpty(typeTag) || typeTag.Length < 2 || typeTag[0] != ',')
                    return;

                char type = typeTag[1];
                
                // Read value based on type
                if (type == 'f' && offset + 4 <= data.Length)
                {
                    // Float value (big-endian, 4 bytes)
                    byte[] floatBytes = new byte[4];
                    Array.Copy(data, offset, floatBytes, 0, 4);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(floatBytes);
                    float value = BitConverter.ToSingle(floatBytes, 0);
                    
                    OnFloatMessageReceived?.Invoke(address, value);
                }
                else if (type == 'i' && offset + 4 <= data.Length)
                {
                    // Int value (big-endian, 4 bytes)
                    byte[] intBytes = new byte[4];
                    Array.Copy(data, offset, intBytes, 0, 4);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(intBytes);
                    int value = BitConverter.ToInt32(intBytes, 0);
                    
                    OnIntMessageReceived?.Invoke(address, value);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HoloCadeOSCServer] Failed to parse OSC message: {ex.Message}");
            }
        }

        /// <summary>
        /// Read null-terminated string from OSC packet (4-byte aligned)
        /// </summary>
        private string ReadOSCString(byte[] data, ref int offset)
        {
            if (offset >= data.Length)
                return null;

            int startOffset = offset;
            
            // Find null terminator
            while (offset < data.Length && data[offset] != 0)
                offset++;

            string str = Encoding.UTF8.GetString(data, startOffset, offset - startOffset);
            offset++; // Skip null terminator

            // Align to 4-byte boundary
            while (offset % 4 != 0 && offset < data.Length)
                offset++;

            return str;
        }

        private void OnDestroy()
        {
            StopListening();
        }
    }

    /// <summary>
    /// Simple main thread dispatcher for Unity (to process OSC messages on main thread)
    /// Must be initialized on main thread before use
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher instance;
        private static readonly Queue<Action> actionQueue = new Queue<Action>();
        private static readonly object lockObject = new object();

        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (instance == null)
                {
                    // Must be called on main thread (from MonoBehaviour)
                    if (UnityEngine.Application.isPlaying)
                    {
                        GameObject go = new GameObject("UnityMainThreadDispatcher");
                        instance = go.AddComponent<UnityMainThreadDispatcher>();
                        UnityEngine.Object.DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        public void Enqueue(Action action)
        {
            if (action == null)
                return;
                
            lock (lockObject)
            {
                actionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (lockObject)
            {
                while (actionQueue.Count > 0)
                {
                    Action action = actionQueue.Dequeue();
                    action?.Invoke();
                }
            }
        }

        private void OnDestroy()
        {
            instance = null;
        }
    }
}

