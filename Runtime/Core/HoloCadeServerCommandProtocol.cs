// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace HoloCade.Core
{
    /// <summary>
    /// Server command types that can be sent from Command Console to Server Manager
    /// </summary>
    public enum ServerCommand
    {
        None = 0,
        StartServer,
        StopServer,
        AdvanceState,
        RetreatState,
        SetMaxPlayers,
        SetPort,
        RequestStatus,
        Shutdown
    }

    /// <summary>
    /// Server command message structure
    /// </summary>
    [System.Serializable]
    public class ServerCommandMessage
    {
        public ServerCommand Command = ServerCommand.None;
        public string Parameter = "";
        public float Timestamp = 0.0f;
        public uint SequenceNumber = 0;
        public string AuthToken = "";

        public ServerCommandMessage()
        {
            Command = ServerCommand.None;
            Timestamp = Time.time;
            SequenceNumber = 0;
        }

        public ServerCommandMessage(ServerCommand command, string parameter = "", uint sequenceNumber = 0)
        {
            Command = command;
            Parameter = parameter;
            Timestamp = Time.time;
            SequenceNumber = sequenceNumber;
        }
    }

    /// <summary>
    /// Server response message structure
    /// </summary>
    [System.Serializable]
    public class ServerResponseMessage
    {
        public bool Success = false;
        public string Message = "";
        public string Data = "";

        public ServerResponseMessage()
        {
            Success = false;
        }

        public ServerResponseMessage(bool success, string message, string data = "")
        {
            Success = success;
            Message = message;
            Data = data;
        }
    }

    /// <summary>
    /// HoloCade Server Command Protocol
    /// 
    /// UDP-based command protocol for remote server control.
    /// Allows Command Console to send commands to Server Manager over network.
    /// 
    /// CLIENT MODE (Command Console):
    /// - Sends commands to Server Manager via UDP
    /// - Sends commands (start/stop, state changes, etc.)
    /// - Optionally receives responses
    /// 
    /// SERVER MODE (Server Manager):
    /// - Listens for incoming command packets on UDP
    /// - Receives and processes commands
    /// - Sends responses back
    /// 
    /// Protocol:
    /// - UDP packets on port 7779 (default)
    /// - Messages are JSON-serialized
    /// - Commands include sequence numbers for reliability
    /// - Responses are optional (fire-and-forget or request-response)
    /// 
    /// Note: Consistent with HoloCade architecture (all networking is UDP-based:
    ///       Server Beacon on 7778, Embedded Systems on 8888, Commands on 7779)
    /// </summary>
    public class HoloCadeServerCommandProtocol : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Command port (default: 7779, separate from game port 7777 and beacon port 7778)")]
        public int CommandPort = 7779;

        [Header("Security")]
        [Tooltip("Enable authentication for remote connections (not needed for local same-desk setups)")]
        public bool EnableAuthentication = false;

        [Tooltip("Shared secret for authentication (must match between client and server)")]
        public string SharedSecret = "CHANGE_ME_IN_PRODUCTION";

        // Events
        public event Action<ServerCommandMessage, IPEndPoint> OnCommandReceived;

        // Client mode
        private UdpClient clientSocket;
        private IPEndPoint remoteServerEndpoint;
        private uint nextSequenceNumber = 0;
        private bool isActive = false;

        // Server mode
        private UdpClient listenSocket;
        private IPEndPoint lastSenderAddress;
        private bool isListening = false;

        private void OnDestroy() => Shutdown();

        #region Client Mode

        /// <summary>
        /// Initialize connection to remote Server Manager
        /// </summary>
        public bool InitializeClient(string serverIP, int serverPort = 7779)
        {
            if (isActive)
            {
                Debug.LogWarning("[HoloCadeServerCommandProtocol] Client already active");
                return false;
            }

            try
            {
                clientSocket = new UdpClient();
                clientSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                IPAddress serverAddr;
                if (!IPAddress.TryParse(serverIP, out serverAddr))
                {
                    Debug.LogError($"[HoloCadeServerCommandProtocol] Invalid server IP: {serverIP}");
                    return false;
                }

                remoteServerEndpoint = new IPEndPoint(serverAddr, serverPort);
                isActive = true;
                nextSequenceNumber = 0;

                Debug.Log($"[HoloCadeServerCommandProtocol] Client initialized (target: {serverIP}:{serverPort})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCadeServerCommandProtocol] Failed to initialize client: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shutdown client mode
        /// </summary>
        public void ShutdownClient()
        {
            if (clientSocket != null)
            {
                clientSocket.Close();
                clientSocket = null;
            }
            remoteServerEndpoint = null;
            isActive = false;
            Debug.Log("[HoloCadeServerCommandProtocol] Client shutdown");
        }

        /// <summary>
        /// Send a command to the server
        /// </summary>
        public ServerResponseMessage SendCommand(ServerCommand command, string parameter = "")
        {
            if (!isActive || clientSocket == null || remoteServerEndpoint == null)
                return new ServerResponseMessage(false, "Not connected to server");

            try
            {
                // Create command message
                ServerCommandMessage commandMsg = new ServerCommandMessage(command, parameter, nextSequenceNumber++);

                // Generate auth token if enabled
                if (EnableAuthentication && !string.IsNullOrEmpty(SharedSecret))
                    commandMsg.AuthToken = GenerateAuthToken(commandMsg);

                // Serialize to JSON
                string json = JsonUtility.ToJson(commandMsg);
                byte[] data = Encoding.UTF8.GetBytes(json);

                // Send via UDP
                clientSocket.Send(data, data.Length, remoteServerEndpoint);

                Debug.Log($"[HoloCadeServerCommandProtocol] Sent command {(int)command} (seq: {commandMsg.SequenceNumber})");

                // For UDP, we return a generic success - actual response would come via ReceiveClientResponse
                return new ServerResponseMessage(true, "Command sent");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HoloCadeServerCommandProtocol] Failed to send command: {ex.Message}");
                return new ServerResponseMessage(false, $"Failed to send command: {ex.Message}");
            }
        }

        /// <summary>
        /// Process incoming response packets (call from Update in client mode)
        /// </summary>
        public void TickClient()
        {
            if (!isActive || clientSocket == null)
                return;

            // Check for incoming responses
            if (clientSocket.Available > 0)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = clientSocket.Receive(ref sender);
                    string json = Encoding.UTF8.GetString(data);
                    ServerResponseMessage response = JsonUtility.FromJson<ServerResponseMessage>(json);

                    if (response != null)
                        Debug.Log($"[HoloCadeServerCommandProtocol] Received response: {response.Message}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[HoloCadeServerCommandProtocol] Error receiving response: {ex.Message}");
                }
            }
        }

        #endregion

        #region Server Mode

        /// <summary>
        /// Start listening for incoming command connections
        /// </summary>
        public bool StartListening()
        {
            if (isListening)
            {
                Debug.LogWarning("[HoloCadeServerCommandProtocol] Already listening");
                return false;
            }

            try
            {
                listenSocket = new UdpClient(CommandPort);
                listenSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listenSocket.BeginReceive(OnDataReceived, null);

                isListening = true;
                Debug.Log($"[HoloCadeServerCommandProtocol] Started listening on port {CommandPort}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCadeServerCommandProtocol] Failed to start listening: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stop listening for connections
        /// </summary>
        public void StopListening()
        {
            if (!isListening)
                return;

            if (listenSocket != null)
            {
                listenSocket.Close();
                listenSocket = null;
            }
            isListening = false;
            Debug.Log("[HoloCadeServerCommandProtocol] Stopped listening");
        }

        /// <summary>
        /// Process incoming command packets (call from Update in server mode)
        /// </summary>
        public void Tick()
        {
            // Processing happens asynchronously via OnDataReceived
        }

        private void OnDataReceived(IAsyncResult result)
        {
            if (listenSocket == null || !isListening)
                return;

            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listenSocket.EndReceive(result, ref sender);

                // Continue listening for next packet
                listenSocket.BeginReceive(OnDataReceived, null);

                if (data.Length == 0)
                    return;

                // Parse JSON
                string json = Encoding.UTF8.GetString(data);
                ServerCommandMessage command = JsonUtility.FromJson<ServerCommandMessage>(json);

                if (command != null)
                {
                    // Validate authentication if enabled
                    if (EnableAuthentication && !ValidateAuthToken(command))
                    {
                        Debug.LogWarning($"[HoloCadeServerCommandProtocol] Authentication failed for command {(int)command.Command} from {sender}");
                        SendResponse(new ServerResponseMessage(false, "Authentication failed"), sender);
                        return;
                    }

                    Debug.Log($"[HoloCadeServerCommandProtocol] Received command {(int)command.Command} (seq: {command.SequenceNumber}) from {sender}");
                    lastSenderAddress = sender;
                    OnCommandReceived?.Invoke(command, sender);
                }
                else
                    Debug.LogWarning($"[HoloCadeServerCommandProtocol] Failed to deserialize command from {sender}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HoloCadeServerCommandProtocol] Error receiving data: {ex.Message}");
                
                // Continue listening even if there was an error
                if (isListening && listenSocket != null)
                    listenSocket.BeginReceive(OnDataReceived, null);
            }
        }

        /// <summary>
        /// Send a response back to the client
        /// </summary>
        public void SendResponse(ServerResponseMessage response, IPEndPoint clientAddress)
        {
            if (listenSocket == null || !isListening)
                return;

            try
            {
                string json = JsonUtility.ToJson(response);
                byte[] data = Encoding.UTF8.GetBytes(json);

                listenSocket.Send(data, data.Length, clientAddress);
                Debug.Log($"[HoloCadeServerCommandProtocol] Sent response to {clientAddress}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HoloCadeServerCommandProtocol] Failed to send response to {clientAddress}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the last sender address (for sending responses)
        /// </summary>
        public IPEndPoint GetLastSenderAddress() => lastSenderAddress;

        #endregion

        #region Authentication

        /// <summary>
        /// Generate authentication token from command data
        /// </summary>
        private string GenerateAuthToken(ServerCommandMessage command)
        {
            // Simple HMAC-like approach: hash(command + timestamp + sequence + secret)
            // For production, consider using proper HMAC-SHA256
            string dataToHash = $"{(int)command.Command}_{command.Timestamp}_{command.SequenceNumber}_{SharedSecret}";
            
            // Simple hash (for production, use proper crypto library)
            int hash = dataToHash.GetHashCode();
            return hash.ToString("X8");
        }

        /// <summary>
        /// Validate authentication token
        /// </summary>
        private bool ValidateAuthToken(ServerCommandMessage command)
        {
            if (string.IsNullOrEmpty(command.AuthToken))
                return false;
            return GenerateAuthToken(command) == command.AuthToken;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Check if currently active (client mode)
        /// </summary>
        public bool IsActive() => isActive && clientSocket != null;

        /// <summary>
        /// Check if currently listening (server mode)
        /// </summary>
        public bool IsListening() => isListening && listenSocket != null;

        /// <summary>
        /// Shutdown both client and server modes
        /// </summary>
        public void Shutdown()
        {
            ShutdownClient();
            StopListening();
        }

        #endregion
    }
}
