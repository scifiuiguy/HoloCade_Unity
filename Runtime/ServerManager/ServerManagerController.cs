// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using HoloCade.Core;

namespace HoloCade.ServerManager
{
    /// <summary>
    /// Server configuration data
    /// </summary>
    [System.Serializable]
    public class ServerConfiguration
    {
        public string ExperienceType = "AIFacemask";
        public string ServerName = "HoloCade Server";
        public int MaxPlayers = 4;
        public int Port = 7777;
        public string SceneName = "HoloCadeScene";
    }

    /// <summary>
    /// Connection mode for Server Manager
    /// </summary>
    public enum ConnectionMode
    {
        Local,   // Launch server locally
        Remote   // Connect to remote server
    }

    /// <summary>
    /// Server runtime status
    /// </summary>
    [System.Serializable]
    public class ServerStatus
    {
        public bool IsRunning = false;
        public int CurrentPlayers = 0;
        public string ExperienceState = "Idle";
        public float Uptime = 0.0f;
        public int ProcessID = 0;
    }

    /// <summary>
    /// Omniverse Audio2Face status
    /// </summary>
    [System.Serializable]
    public class OmniverseStatus
    {
        public bool IsConnected = false;
        public string StreamStatus = "Inactive";
        public int ActiveFaceStreams = 0;
    }

    /// <summary>
    /// HoloCade Server Manager Controller
    /// 
    /// Unity implementation of the server management system.
    /// Manages dedicated game servers, monitors status, and integrates with Omniverse.
    /// </summary>
    public class ServerManagerController : MonoBehaviour
    {
        [Header("Configuration")]
        public ConnectionMode connectionMode = ConnectionMode.Local;
        public ServerConfiguration ServerConfig = new ServerConfiguration();

        [Header("Remote Server Configuration")]
        [Tooltip("Only used in Remote mode")]
        public string RemoteServerIP = "127.0.0.1";
        [Tooltip("Only used in Remote mode")]
        public int RemoteServerPort = 7777;
        [Tooltip("Only used in Remote mode")]
        public int RemoteCommandPort = 7779;

        [Header("Security")]
        [Tooltip("Enable authentication for remote connections (not needed for local same-desk setups)")]
        public bool EnableAuthentication = false;
        [Tooltip("Shared secret for authentication (must match server configuration)")]
        public string SharedSecret = "CHANGE_ME_IN_PRODUCTION";

        [Header("Status (Read-Only)")]
        public ServerStatus ServerStatus = new ServerStatus();
        public OmniverseStatus OmniverseStatus = new OmniverseStatus();

        [Header("Settings")]
        [SerializeField] private float statusPollInterval = 1.0f;

        // Events for UI updates
        public event Action<string> OnLogMessageAdded;
        public event Action<ServerStatus> OnServerStatusChanged;

        // Private members
        private Process serverProcess;
        private HoloCadeServerBeacon serverBeacon;
        private HoloCadeServerCommandProtocol commandProtocol;
        private float statusPollTimer = 0.0f;
        private string expectedServerIP = "";
        private int expectedServerPort = 0;

        private void Awake()
        {
            // Initialize server beacon for real-time status updates
            serverBeacon = gameObject.AddComponent<HoloCadeServerBeacon>();
            serverBeacon.OnServerDiscovered += OnServerStatusReceived;
            serverBeacon.OnServerDiscovered += OnServerDiscoveredForConnection;
            serverBeacon.StartClientDiscovery();

            // Initialize command protocol for remote server control
            commandProtocol = gameObject.AddComponent<HoloCadeServerCommandProtocol>();
            if (commandProtocol != null)
            {
                // Configure authentication settings (only applies in Remote mode)
                commandProtocol.EnableAuthentication = EnableAuthentication;
                commandProtocol.SharedSecret = SharedSecret;
                commandProtocol.CommandPort = RemoteCommandPort;

                if (EnableAuthentication)
                    AddLogMessage("Command protocol initialized with authentication enabled (port 7779)");
                else
                    AddLogMessage("Command protocol initialized (port 7779, authentication disabled)");
            }

            AddLogMessage($"Server Manager initialized (Mode: {connectionMode})");
            AddLogMessage("Server status beacon initialized (listening on port 7778)");
        }

        private void Update()
        {
            // Poll server status
            statusPollTimer += Time.deltaTime;
            if (statusPollTimer >= statusPollInterval)
            {
                statusPollTimer = 0.0f;
                PollServerStatus();
            }

            // Tick command protocol in Remote mode
            if (connectionMode == ConnectionMode.Remote && commandProtocol != null)
                commandProtocol.TickClient();

            // Update uptime if server is running
            if (ServerStatus.IsRunning)
            {
                ServerStatus.Uptime += Time.deltaTime;
                OnServerStatusChanged?.Invoke(ServerStatus);
            }
        }

        /// <summary>
        /// Start the dedicated server with current configuration
        /// </summary>
        public bool StartServer()
        {
            if (ServerStatus.IsRunning)
            {
                AddLogMessage("ERROR: Server is already running");
                return false;
            }

            if (connectionMode == ConnectionMode.Local)
                return StartServerLocal();
            else
                return StartServerRemote();
        }

        private bool StartServerLocal()
        {
            string serverPath = GetServerExecutablePath();
            if (!System.IO.File.Exists(serverPath))
            {
                AddLogMessage($"ERROR: Server executable not found at {serverPath}");
                AddLogMessage("Please build the dedicated server target first.");
                return false;
            }

            string commandLine = BuildServerCommandLine();
            AddLogMessage($"Starting server: {serverPath} {commandLine}");

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = serverPath,
                    Arguments = commandLine,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                serverProcess = Process.Start(startInfo);

                if (serverProcess != null)
                {
                    ServerStatus.IsRunning = true;
                    ServerStatus.Uptime = 0.0f;
                    ServerStatus.ExperienceState = "Starting...";
                    ServerStatus.ProcessID = serverProcess.Id;

                    AddLogMessage($"Server started successfully (PID: {ServerStatus.ProcessID})");
                    AddLogMessage("Listening for server status broadcasts...");
                    OnServerStatusChanged?.Invoke(ServerStatus);
                    return true;
                }
                else
                {
                    AddLogMessage("ERROR: Failed to start server process");
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"ERROR: Exception starting server: {ex.Message}");
                return false;
            }
        }

        private bool StartServerRemote()
        {
            // Connect to remote server if not already connected
            if (!ConnectToRemoteServer())
                return false;

            // Send StartServer command
            if (commandProtocol != null)
            {
                ServerResponseMessage response = commandProtocol.SendCommand(ServerCommand.StartServer, "");
                if (response.Success)
                {
                    AddLogMessage($"Start server command sent to {RemoteServerIP}:{RemoteServerPort}");
                    ServerStatus.IsRunning = true;
                    ServerStatus.ExperienceState = "Starting...";
                    OnServerStatusChanged?.Invoke(ServerStatus);
                    return true;
                }
                else
                {
                    AddLogMessage($"ERROR: Failed to send start command: {response.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Stop the running dedicated server
        /// </summary>
        public bool StopServer()
        {
            if (!ServerStatus.IsRunning)
            {
                AddLogMessage("ERROR: No server is currently running");
                return false;
            }

            if (connectionMode == ConnectionMode.Local)
                return StopServerLocal();
            else
                return StopServerRemote();
        }

        private bool StopServerLocal()
        {
            if (serverProcess == null || serverProcess.HasExited)
            {
                AddLogMessage("ERROR: Server process not found or already exited");
                ServerStatus.IsRunning = false;
                OnServerStatusChanged?.Invoke(ServerStatus);
                return false;
            }

            AddLogMessage("Stopping server...");

            try
            {
                serverProcess.Kill();
                serverProcess.WaitForExit(5000); // Wait up to 5 seconds
                serverProcess.Dispose();
                serverProcess = null;

                ServerStatus.IsRunning = false;
                ServerStatus.CurrentPlayers = 0;
                ServerStatus.ExperienceState = "Stopped";
                ServerStatus.ProcessID = 0;

                AddLogMessage("Server stopped");
                OnServerStatusChanged?.Invoke(ServerStatus);
                return true;
            }
            catch (Exception ex)
            {
                AddLogMessage($"ERROR: Exception stopping server: {ex.Message}");
                return false;
            }
        }

        private bool StopServerRemote()
        {
            // Send StopServer command
            if (commandProtocol != null && commandProtocol.IsActive())
            {
                ServerResponseMessage response = commandProtocol.SendCommand(ServerCommand.StopServer, "");
                if (response.Success)
                {
                    AddLogMessage($"Stop server command sent to {RemoteServerIP}:{RemoteServerPort}");
                    ServerStatus.IsRunning = false;
                    ServerStatus.CurrentPlayers = 0;
                    ServerStatus.ExperienceState = "Stopped";
                    OnServerStatusChanged?.Invoke(ServerStatus);
                    return true;
                }
                else
                {
                    AddLogMessage($"ERROR: Failed to send stop command: {response.Message}");
                    return false;
                }
            }
            AddLogMessage("ERROR: Not connected to remote server");
            return false;
        }

        /// <summary>
        /// Connect to remote server
        /// </summary>
        public bool ConnectToRemoteServer()
        {
            if (connectionMode != ConnectionMode.Remote)
            {
                AddLogMessage("ERROR: Connection mode is not set to Remote");
                return false;
            }

            if (commandProtocol != null && commandProtocol.IsActive())
            {
                AddLogMessage("Already connected to remote server");
                return true;
            }

            if (commandProtocol == null)
            {
                commandProtocol = gameObject.AddComponent<HoloCadeServerCommandProtocol>();
                if (commandProtocol == null)
                {
                    AddLogMessage("ERROR: Failed to create command protocol");
                    return false;
                }
            }

            // Sync authentication settings before connecting
            commandProtocol.EnableAuthentication = EnableAuthentication;
            commandProtocol.SharedSecret = SharedSecret;
            commandProtocol.CommandPort = RemoteCommandPort;

            // Initialize client connection
            if (commandProtocol.InitializeClient(RemoteServerIP, RemoteCommandPort))
            {
                expectedServerIP = RemoteServerIP;
                expectedServerPort = RemoteServerPort;
                AddLogMessage($"Connected to remote server at {RemoteServerIP}:{RemoteServerPort} (command port: {RemoteCommandPort})");
                return true;
            }
            else
            {
                AddLogMessage($"ERROR: Failed to connect to remote server at {RemoteServerIP}:{RemoteCommandPort}");
                return false;
            }
        }

        /// <summary>
        /// Disconnect from remote server
        /// </summary>
        public void DisconnectFromRemoteServer()
        {
            if (connectionMode != ConnectionMode.Remote)
                return;

            if (commandProtocol != null && commandProtocol.IsActive())
            {
                commandProtocol.ShutdownClient();
                AddLogMessage("Disconnected from remote server");
            }
        }

        /// <summary>
        /// Check if connected to remote server
        /// </summary>
        public bool IsRemoteConnected() =>
            connectionMode == ConnectionMode.Remote &&
            commandProtocol != null &&
            commandProtocol.IsActive();

        private void OnServerDiscoveredForConnection(ServerInfo serverInfo)
        {
            // Auto-fill remote server info if not set
            if (connectionMode == ConnectionMode.Remote && string.IsNullOrEmpty(RemoteServerIP))
            {
                RemoteServerIP = serverInfo.ServerIP;
                RemoteServerPort = serverInfo.ServerPort;
                AddLogMessage($"Auto-filled remote server info from discovery: {RemoteServerIP}:{RemoteServerPort}");
            }
        }

        /// <summary>
        /// Get available experience types
        /// </summary>
        public string[] GetAvailableExperienceTypes()
        {
            return new string[]
            {
                "AIFacemask",
                "MovingPlatform",
                "Gunship",
                "CarSim",
                "FlightSim"
            };
        }

        /// <summary>
        /// Add a log message
        /// </summary>
        public void AddLogMessage(string message)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            UnityEngine.Debug.Log($"[ServerManager] {message}");
            OnLogMessageAdded?.Invoke(logEntry);
        }

        /// <summary>
        /// Open Omniverse configuration panel
        /// </summary>
        public void OpenOmniverseConfig()
        {
            AddLogMessage("Omniverse configuration not yet implemented");
            // NOOP: TODO - Open Omniverse configuration dialog
        }

        private void PollServerStatus()
        {
            if (!ServerStatus.IsRunning)
                return;

            // Check if process is still running
            if (serverProcess != null && serverProcess.HasExited)
            {
                AddLogMessage("WARNING: Server process terminated unexpectedly");
                ServerStatus.IsRunning = false;
                ServerStatus.CurrentPlayers = 0;
                ServerStatus.ExperienceState = "Crashed";
                OnServerStatusChanged?.Invoke(ServerStatus);
            }
            // Real-time status updates come via OnServerStatusReceived callback
            // from the network beacon. This function just verifies process is alive.
        }

        private void OnServerStatusReceived(ServerInfo serverInfo)
        {
            // Only process status for our managed server
            // (Match by port since IP might be reported differently for localhost)
            if (serverInfo.ServerPort != ServerConfig.Port)
                return; // This is a different server on the network

            // Only process if our server is marked as running
            if (!ServerStatus.IsRunning)
                return;

            // Update real-time status from server broadcast
            bool stateChanged = false;

            if (ServerStatus.CurrentPlayers != serverInfo.CurrentPlayers)
            {
                ServerStatus.CurrentPlayers = serverInfo.CurrentPlayers;
                stateChanged = true;
                AddLogMessage($"Player count changed to: {ServerStatus.CurrentPlayers}/{ServerConfig.MaxPlayers}");
            }

            if (ServerStatus.ExperienceState != serverInfo.ExperienceState)
            {
                ServerStatus.ExperienceState = serverInfo.ExperienceState;
                stateChanged = true;
                AddLogMessage($"Server state changed to: {ServerStatus.ExperienceState}");
            }

            if (stateChanged)
                OnServerStatusChanged?.Invoke(ServerStatus);
        }

        private string GetServerExecutablePath()
        {
            // Build path to dedicated server executable
            string projectDir = Application.dataPath.Replace("/Assets", "");
            string buildDir = System.IO.Path.Combine(projectDir, "Builds", "Server");

#if UNITY_STANDALONE_WIN
            string executableName = "HoloCade_UnityServer.exe";
#elif UNITY_STANDALONE_LINUX
            string executableName = "HoloCade_UnityServer.x86_64";
#elif UNITY_STANDALONE_OSX
            string executableName = "HoloCade_UnityServer.app";
#else
            string executableName = "HoloCade_UnityServer";
#endif

            return System.IO.Path.Combine(buildDir, executableName);
        }

        private string BuildServerCommandLine()
        {
            // Unity dedicated server command-line arguments
            string args = $"-batchmode -nographics";
            args += $" -port {ServerConfig.Port}";
            args += $" -scene {ServerConfig.SceneName}";
            args += $" -experienceType {ServerConfig.ExperienceType}";
            args += $" -maxPlayers {ServerConfig.MaxPlayers}";
            args += $" -logFile ServerLog.txt";

            return args;
        }

        private void OnDestroy()
        {
            // Clean up
            if (serverProcess != null && !serverProcess.HasExited)
            {
                serverProcess.Kill();
                serverProcess.Dispose();
            }

            if (serverBeacon != null)
                serverBeacon.Stop();

            if (commandProtocol != null)
                commandProtocol.Shutdown();
        }
    }
}
