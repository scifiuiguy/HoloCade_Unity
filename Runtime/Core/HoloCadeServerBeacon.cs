// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace HoloCade.Core
{
    /// <summary>
    /// Server information broadcast over LAN
    /// </summary>
    [System.Serializable]
    public class ServerInfo
    {
        public string ServerIP = "";
        public int ServerPort = 7777;
        public string ExperienceType = "";
        public string ServerName = "";
        public int CurrentPlayers = 0;
        public int MaxPlayers = 8;
        public string ExperienceState = "";
        public string ServerVersion = "1.0.0";
        public bool bAcceptingConnections = true;
        
        // Internal tracking
        public float LastBeaconTime = 0.0f;
    }

    /// <summary>
    /// HoloCade Server Beacon
    /// 
    /// Handles automatic server discovery on LAN using UDP broadcasting.
    /// 
    /// SERVER MODE:
    /// - Broadcasts server presence every X seconds
    /// - Includes server metadata (experience type, player count, etc.)
    /// - Runs on dedicated server to advertise availability
    /// 
    /// CLIENT MODE:
    /// - Listens for server broadcasts
    /// - Maintains list of available servers
    /// - Auto-connects to appropriate server
    /// - Detects when servers go offline
    /// 
    /// Perfect for LBE installations with multiple concurrent experiences.
    /// </summary>
    public class HoloCadeServerBeacon : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private int broadcastPort = 7778;
        [SerializeField] private float broadcastInterval = 2.0f;
        [SerializeField] private float serverTimeoutDuration = 10.0f;

        // Events
        public event Action<ServerInfo> OnServerDiscovered;
        public event Action<string> OnServerLost;

        // Private members
        private UdpClient udpClient;
        private bool isActive = false;
        private bool isServerMode = false;

        // Server mode
        private ServerInfo serverInfo;
        private float broadcastTimer = 0.0f;

        // Client mode
        private Dictionary<string, ServerInfo> discoveredServers = new Dictionary<string, ServerInfo>();
        private readonly object serverLock = new object();

        public bool IsActive => isActive;

        /// <summary>
        /// Start broadcasting server presence on the LAN
        /// </summary>
        public bool StartServerBroadcast(ServerInfo info, int port = 7778, float interval = 2.0f)
        {
            if (isActive)
            {
                Debug.LogWarning("[HoloCadeServerBeacon] Already active");
                return false;
            }

            broadcastPort = port;
            broadcastInterval = interval;
            serverInfo = info;
            isServerMode = true;

            try
            {
                udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;
                isActive = true;
                broadcastTimer = 0.0f;

                Debug.Log($"[HoloCadeServerBeacon] Server broadcast started on port {broadcastPort}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCadeServerBeacon] Failed to start server broadcast: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Start listening for server broadcasts on the LAN
        /// </summary>
        public bool StartClientDiscovery(int port = 7778, float timeoutDuration = 10.0f)
        {
            if (isActive)
            {
                Debug.LogWarning("[HoloCadeServerBeacon] Already active");
                return false;
            }

            broadcastPort = port;
            serverTimeoutDuration = timeoutDuration;
            isServerMode = false;

            try
            {
                udpClient = new UdpClient(broadcastPort);
                udpClient.BeginReceive(OnDataReceived, null);
                isActive = true;

                Debug.Log($"[HoloCadeServerBeacon] Client discovery started, listening on port {broadcastPort}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCadeServerBeacon] Failed to start client discovery: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stop broadcasting or listening
        /// </summary>
        public void Stop()
        {
            if (!isActive)
            {
                return;
            }

            isActive = false;

            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }

            discoveredServers.Clear();
            Debug.Log("[HoloCadeServerBeacon] Stopped");
        }

        /// <summary>
        /// Get list of currently discovered servers (client mode only)
        /// </summary>
        public List<ServerInfo> GetDiscoveredServers()
        {
            lock (serverLock)
            {
                return new List<ServerInfo>(discoveredServers.Values);
            }
        }

        /// <summary>
        /// Tick function (called every frame)
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!isActive)
            {
                return;
            }

            if (isServerMode)
            {
                // Server mode: broadcast periodically
                broadcastTimer += deltaTime;
                if (broadcastTimer >= broadcastInterval)
                {
                    broadcastTimer = 0.0f;
                    SendBroadcast();
                }
            }
            else
            {
                // Client mode: check for server timeouts
                ProcessDiscoveredServers(deltaTime);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void SendBroadcast()
        {
            if (udpClient == null || serverInfo == null)
            {
                return;
            }

            try
            {
                // Build JSON packet
                string json = JsonUtility.ToJson(serverInfo);
                byte[] data = Encoding.UTF8.GetBytes(json);

                // Send broadcast
                IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
                udpClient.Send(data, data.Length, broadcastEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCadeServerBeacon] Failed to send broadcast: {ex.Message}");
            }
        }

        private void OnDataReceived(IAsyncResult result)
        {
            if (udpClient == null || !isActive)
            {
                return;
            }

            try
            {
                IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.EndReceive(result, ref remoteEndpoint);

                // Parse JSON packet
                string json = Encoding.UTF8.GetString(data);
                ServerInfo info = JsonUtility.FromJson<ServerInfo>(json);

                if (info != null)
                {
                    // Override ServerIP with actual sender IP (more reliable than self-reported)
                    info.ServerIP = remoteEndpoint.Address.ToString();
                    info.LastBeaconTime = Time.time;

                    // Store/update discovered server
                    string serverKey = $"{info.ServerIP}:{info.ServerPort}";
                    bool isNewServer = false;

                    lock (serverLock)
                    {
                        isNewServer = !discoveredServers.ContainsKey(serverKey);
                        discoveredServers[serverKey] = info;
                    }

                    // Notify on main thread
                    if (isNewServer)
                    {
                        UnityMainThreadDispatcher.Enqueue(() =>
                        {
                            OnServerDiscovered?.Invoke(info);
                        });
                    }
                }

                // Continue receiving
                udpClient.BeginReceive(OnDataReceived, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCadeServerBeacon] Error receiving data: {ex.Message}");

                // Try to restart receiver
                if (udpClient != null && isActive)
                {
                    try
                    {
                        udpClient.BeginReceive(OnDataReceived, null);
                    }
                    catch { }
                }
            }
        }

        private void ProcessDiscoveredServers(float deltaTime)
        {
            List<string> serversToRemove = new List<string>();

            lock (serverLock)
            {
                foreach (var kvp in discoveredServers)
                {
                    ServerInfo info = kvp.Value;
                    float timeSinceLastBeacon = Time.time - info.LastBeaconTime;

                    if (timeSinceLastBeacon > serverTimeoutDuration)
                    {
                        serversToRemove.Add(kvp.Key);
                    }
                }

                // Remove timed-out servers
                foreach (string key in serversToRemove)
                {
                    ServerInfo lostServer = discoveredServers[key];
                    discoveredServers.Remove(key);

                    Debug.Log($"[HoloCadeServerBeacon] Server lost: {lostServer.ServerName} ({lostServer.ServerIP})");
                    OnServerLost?.Invoke(lostServer.ServerIP);
                }
            }
        }

        private void OnDestroy()
        {
            Stop();
        }
    }

    /// <summary>
    /// Helper class for dispatching actions to the main Unity thread
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> executionQueue = new Queue<Action>();
        private static UnityMainThreadDispatcher instance;

        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }

            // Create instance if needed
            if (instance == null)
            {
                GameObject go = new GameObject("UnityMainThreadDispatcher");
                instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }

        private void Update()
        {
            lock (executionQueue)
            {
                while (executionQueue.Count > 0)
                {
                    executionQueue.Dequeue()?.Invoke();
                }
            }
        }
    }
}



