// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using Unity.Netcode;

namespace HoloCade.Core
{
    /// <summary>
    /// Manages LAN multiplayer networking for HoloCade experiences
    /// Uses Unity NetCode for GameObjects (NGO) for reliable local network play
    /// </summary>
    public class HoloCadeNetworkManager : MonoBehaviour
    {
        [Header("Network Configuration")]
        [SerializeField] private string ipAddress = "192.168.1.100";
        [SerializeField] private ushort port = 7777;
        [SerializeField] private int maxPlayers = 4;

        private NetworkManager networkManager;
        private bool isInitialized = false;

        #region Initialization

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                networkManager = gameObject.AddComponent<NetworkManager>();
            }
        }

        /// <summary>
        /// Initialize the network system for LAN play
        /// </summary>
        public bool InitializeNetwork()
        {
            if (isInitialized)
            {
                return true;
            }

            // Configure NetworkManager for LAN
            if (networkManager != null)
            {
                isInitialized = true;
                Debug.Log($"[HoloCade] Network manager initialized - Max players: {maxPlayers}");
                return true;
            }

            Debug.LogError("[HoloCade] Failed to initialize network manager");
            return false;
        }

        #endregion

        #region Host/Client Control

        /// <summary>
        /// Start as network host (server + client)
        /// </summary>
        public bool StartHost()
        {
            if (!isInitialized)
            {
                InitializeNetwork();
            }

            if (networkManager != null && networkManager.StartHost())
            {
                Debug.Log($"[HoloCade] Started as host on port {port}");
                return true;
            }

            Debug.LogError("[HoloCade] Failed to start host");
            return false;
        }

        /// <summary>
        /// Start as network server only
        /// </summary>
        public bool StartServer()
        {
            if (!isInitialized)
            {
                InitializeNetwork();
            }

            if (networkManager != null && networkManager.StartServer())
            {
                Debug.Log($"[HoloCade] Started as server on port {port}");
                return true;
            }

            Debug.LogError("[HoloCade] Failed to start server");
            return false;
        }

        /// <summary>
        /// Start as network client and connect to host
        /// </summary>
        public bool StartClient()
        {
            if (!isInitialized)
            {
                InitializeNetwork();
            }

            if (networkManager != null && networkManager.StartClient())
            {
                Debug.Log($"[HoloCade] Started as client, connecting to {ipAddress}:{port}");
                return true;
            }

            Debug.LogError("[HoloCade] Failed to start client");
            return false;
        }

        /// <summary>
        /// Shutdown network connection
        /// </summary>
        public void Shutdown()
        {
            if (networkManager != null)
            {
                networkManager.Shutdown();
                Debug.Log("[HoloCade] Network shutdown");
            }
        }

        #endregion

        #region Network State

        /// <summary>
        /// Check if this instance is the server/host
        /// </summary>
        public bool IsServer()
        {
            return networkManager != null && networkManager.IsServer;
        }

        /// <summary>
        /// Check if this instance is a client
        /// </summary>
        public bool IsClient()
        {
            return networkManager != null && networkManager.IsClient;
        }

        /// <summary>
        /// Check if this instance is the host (server + client)
        /// </summary>
        public bool IsHost()
        {
            return networkManager != null && networkManager.IsHost;
        }

        /// <summary>
        /// Check if network is currently connected
        /// </summary>
        public bool IsConnected()
        {
            return networkManager != null && (networkManager.IsServer || networkManager.IsClient);
        }

        /// <summary>
        /// Get the number of currently connected clients
        /// </summary>
        public int GetConnectedClientsCount()
        {
            if (networkManager != null && networkManager.IsServer)
            {
                return (int)networkManager.ConnectedClientsIds.Count;
            }
            return 0;
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Set the IP address for client connections
        /// </summary>
        public void SetIPAddress(string address)
        {
            ipAddress = address;
        }

        /// <summary>
        /// Set the network port
        /// </summary>
        public void SetPort(ushort newPort)
        {
            port = newPort;
        }

        /// <summary>
        /// Set the maximum number of players
        /// </summary>
        public void SetMaxPlayers(int max)
        {
            maxPlayers = Mathf.Clamp(max, 1, 16);
        }

        #endregion
    }
}



