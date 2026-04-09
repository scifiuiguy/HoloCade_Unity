// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Net;

namespace HoloCade.ProAudio
{
    /// <summary>
    /// Pro Audio Console Types
    /// </summary>
    public enum ProAudioConsole
    {
        BehringerX32,
        BehringerM32,
        BehringerWing,
        YamahaQL,
        YamahaCL,
        YamahaTF,
        YamahaDM7,
        AllenHeathSQ,
        AllenHeathDLive,
        SoundcraftSi,
        PresonusStudioLive,
        Other,
        Custom
    }

    /// <summary>
    /// ProAudio Controller Configuration
    /// </summary>
    [System.Serializable]
    public class ProAudioConfig
    {
        [Tooltip("Console manufacturer/model")]
        public ProAudioConsole ConsoleType = ProAudioConsole.BehringerX32;

        [Tooltip("Sound board IP address")]
        public string BoardIPAddress = "192.168.1.100";

        [Tooltip("OSC port (default: 10023 for X32, varies by manufacturer)")]
        public int OSCPort = 10023;

        [Tooltip("Enable receive mode (listen for OSC messages from board)")]
        public bool EnableReceive = false;

        [Tooltip("OSC receive port (for bidirectional communication)")]
        public int ReceivePort = 8000;

        [Tooltip("Channel number offset for OSC addressing (0 = 1-based, -1 = 0-based)")]
        public int ChannelOffset = 0;

        [Tooltip("Custom OSC patterns (only used when ConsoleType = Custom). Use XX for channel, YY for bus.")]
        public string CustomFaderPattern = "/ch/XX/fader";

        [Tooltip("Custom OSC mute pattern")]
        public string CustomMutePattern = "/ch/XX/mute";

        [Tooltip("Custom OSC bus send pattern (YY = bus number)")]
        public string CustomBusSendPattern = "/ch/XX/bus/YY/level";

        [Tooltip("Custom OSC master fader pattern")]
        public string CustomMasterPattern = "/master/fader";
    }

    /// <summary>
    /// HoloCade ProAudio Controller
    /// 
    /// Hardware-agnostic professional audio console control via OSC.
    /// Supports all major manufacturers with a unified API.
    /// 
    /// Uses lightweight custom OSC client (no external dependencies).
    /// </summary>
    public class ProAudioController : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Pro Audio console configuration")]
        public ProAudioConfig Config = new ProAudioConfig();

        [Header("Status (Read-Only)")]
        [Tooltip("Connection status")]
        public bool IsConnected = false;

        // Events for bidirectional sync (bind UI Toolkit widgets to these)
        public event Action<int, float> OnChannelFaderChanged;      // (virtualChannel, level)
        public event Action<int, bool> OnChannelMuteChanged;         // (virtualChannel, mute)
        public event Action<float> OnMasterFaderChanged;             // (level)

        private HoloCadeOSCClient oscClient;
        private HoloCadeOSCServer oscServer; // For receiving OSC messages from physical board

        // Virtual-to-physical channel mapping
        private Dictionary<int, int> virtualToPhysicalChannelMap = new Dictionary<int, int>();
        private HashSet<int> registeredChannelsForSync = new HashSet<int>();

        private void Awake()
        {
            oscClient = gameObject.AddComponent<HoloCadeOSCClient>();
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(Config.BoardIPAddress))
                InitializeConsole(Config);
        }

        private void Update()
        {
            // OSC message processing happens in HoloCadeOSCServer thread, dispatched to main thread
            // No polling needed here
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        /// <summary>
        /// Initialize connection to pro audio console
        /// </summary>
        public bool InitializeConsole(ProAudioConfig config)
        {
            Config = config;

            if (oscClient == null)
            {
                Debug.LogError("[ProAudioController] OSC client not available");
                return false;
            }

            if (oscClient.Initialize(Config.BoardIPAddress, Config.OSCPort))
            {
                IsConnected = true;

                // Start OSC server for bidirectional sync (if enabled)
                if (Config.EnableReceive)
                {
                    // Ensure main thread dispatcher exists (must be created on main thread)
                    UnityMainThreadDispatcher dispatcher = UnityMainThreadDispatcher.Instance;
                    
                    oscServer = gameObject.AddComponent<HoloCadeOSCServer>();
                    if (oscServer != null)
                    {
                        oscServer.OnFloatMessageReceived += HandleIncomingOSCMessage;
                        oscServer.OnIntMessageReceived += HandleIncomingOSCMessage;
                        oscServer.StartListening(Config.ReceivePort);
                        Debug.Log($"[ProAudioController] OSC Server listening on port {Config.ReceivePort} (bidirectional sync enabled)");
                    }
                }

                Debug.Log($"[ProAudioController] Initialized (Console: {Config.ConsoleType}, IP: {Config.BoardIPAddress}:{Config.OSCPort})");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Set channel fader level (0.0 to 1.0, where 1.0 = 0dB)
        /// </summary>
        /// <param name="channel">Virtual channel number (1-based: 1, 2, 3, etc.)</param>
        /// <param name="level">Fader level (0.0 = -inf, 1.0 = 0dB)</param>
        public void SetChannelFader(int channel, float level)
        {
            if (!IsConnected || oscClient == null)
            {
                Debug.LogWarning("[ProAudioController] Not connected");
                return;
            }

            // Map virtual channel to physical channel
            int physicalChannel = GetPhysicalChannel(channel);
            if (physicalChannel <= 0)
            {
                // If not explicitly mapped, assume 1:1 (for backward compatibility)
                physicalChannel = channel;
            }

            string oscPath = BuildOSCPath("fader", physicalChannel);
            oscClient.SendOSCMessage(oscPath, level);
            Debug.Log($"[ProAudioController] Set fader - Virtual CH {channel} -> Physical CH {physicalChannel} = {level:F3}");
        }

        /// <summary>
        /// Mute/unmute a channel
        /// </summary>
        /// <param name="channel">Virtual channel number (1-based)</param>
        /// <param name="mute">true to mute, false to unmute</param>
        public void SetChannelMute(int channel, bool mute)
        {
            if (!IsConnected || oscClient == null)
            {
                Debug.LogWarning("[ProAudioController] Not connected");
                return;
            }

            // Map virtual channel to physical channel
            int physicalChannel = GetPhysicalChannel(channel);
            if (physicalChannel <= 0)
            {
                physicalChannel = channel; // Default to 1:1
            }

            string oscPath = BuildOSCPath("mute", physicalChannel);
            oscClient.SendOSCMessage(oscPath, mute ? 1 : 0);
            Debug.Log($"[ProAudioController] Set mute - Virtual CH {channel} -> Physical CH {physicalChannel} = {(mute ? "Muted" : "Unmuted")}");
        }

        /// <summary>
        /// Set bus send level (e.g., reverb send, monitor send)
        /// </summary>
        /// <param name="channel">Virtual channel number (1-based)</param>
        /// <param name="bus">Bus number (1-based)</param>
        /// <param name="level">Send level (0.0 to 1.0)</param>
        public void SetChannelBusSend(int channel, int bus, float level)
        {
            if (!IsConnected || oscClient == null)
            {
                Debug.LogWarning("[ProAudioController] Not connected");
                return;
            }

            // Map virtual channel to physical channel
            int physicalChannel = GetPhysicalChannel(channel);
            if (physicalChannel <= 0)
            {
                physicalChannel = channel; // Default to 1:1
            }

            string oscPath = BuildOSCPath("bus", physicalChannel, bus);
            oscClient.SendOSCMessage(oscPath, level);
            Debug.Log($"[ProAudioController] Set bus send - Virtual CH {channel} -> Physical CH {physicalChannel}, Bus {bus} = {level:F3}");
        }

        /// <summary>
        /// Set master fader level
        /// </summary>
        /// <param name="level">Fader level (0.0 to 1.0)</param>
        public void SetMasterFader(float level)
        {
            if (!IsConnected || oscClient == null)
            {
                Debug.LogWarning("[ProAudioController] Not connected");
                return;
            }

            string oscPath = BuildOSCPath("master", -1);
            oscClient.SendOSCMessage(oscPath, level);
        }

        /// <summary>
        /// Check if console is connected
        /// </summary>
        public bool IsConsoleConnected() => IsConnected && oscClient != null;

        /// <summary>
        /// Shutdown connection
        /// </summary>
        public void Shutdown()
        {
            if (oscServer != null)
            {
                oscServer.OnFloatMessageReceived -= HandleIncomingOSCMessage;
                oscServer.OnIntMessageReceived -= HandleIncomingOSCMessage;
                oscServer.StopListening();
                Destroy(oscServer);
                oscServer = null;
            }

            if (oscClient != null)
            {
                oscClient.Shutdown();
            }

            IsConnected = false;
            Debug.Log("[ProAudioController] Shutdown");
        }

        // ========================================
        // UMG WIDGET AUTO-MAPPING (For Command Console)
        // ========================================

        /// <summary>
        /// Register a channel for bidirectional sync
        /// </summary>
        /// <param name="virtualChannel">Virtual channel number (1-based) used by UI Toolkit widgets</param>
        /// <param name="physicalChannel">Physical hardware channel number (1-based). MUST be specified and valid.</param>
        /// <returns>True if registration succeeded, false if PhysicalChannel is invalid</returns>
        public bool RegisterChannelForSync(int virtualChannel, int physicalChannel)
        {
            if (virtualChannel <= 0)
            {
                Debug.LogError($"[ProAudioController] Invalid virtual channel number {virtualChannel}");
                return false;
            }

            if (physicalChannel <= 0)
            {
                Debug.LogError($"[ProAudioController] Physical channel number must be specified and greater than 0 (received {physicalChannel})");
                return false;
            }

            // Validate physical channel is within console's supported range
            int maxChannels = GetMaxChannelsForConsole();
            if (physicalChannel > maxChannels)
            {
                Debug.LogError($"[ProAudioController] Physical channel {physicalChannel} exceeds maximum for {Config.ConsoleType} (max: {maxChannels} channels)");
                return false;
            }

            // Check if physical channel is already mapped (allow override, but warn)
            foreach (var kvp in virtualToPhysicalChannelMap)
            {
                if (kvp.Value == physicalChannel && kvp.Key != virtualChannel)
                {
                    Debug.LogWarning($"[ProAudioController] Physical channel {physicalChannel} already mapped to virtual channel {kvp.Key}. Virtual channel {virtualChannel} will override.");
                    break;
                }
            }

            // Store mapping
            virtualToPhysicalChannelMap[virtualChannel] = physicalChannel;
            registeredChannelsForSync.Add(virtualChannel);

            Debug.Log($"[ProAudioController] Registered virtual channel {virtualChannel} -> physical channel {physicalChannel} for bidirectional sync");
            return true;
        }

        /// <summary>
        /// Unregister a channel (stop syncing)
        /// </summary>
        /// <param name="virtualChannel">Virtual channel number to unregister</param>
        public void UnregisterChannelForSync(int virtualChannel)
        {
            registeredChannelsForSync.Remove(virtualChannel);
            virtualToPhysicalChannelMap.Remove(virtualChannel);
            Debug.Log($"[ProAudioController] Unregistered virtual channel {virtualChannel} from sync");
        }

        /// <summary>
        /// Get the physical hardware channel number for a virtual channel
        /// </summary>
        /// <param name="virtualChannel">Virtual channel number</param>
        /// <returns>Physical hardware channel number, or -1 if not mapped</returns>
        public int GetPhysicalChannel(int virtualChannel)
        {
            return virtualToPhysicalChannelMap.ContainsKey(virtualChannel) 
                ? virtualToPhysicalChannelMap[virtualChannel] 
                : -1;
        }

        /// <summary>
        /// Get maximum number of channels supported by the configured console type
        /// </summary>
        /// <returns>Maximum channel number (e.g., 32 for X32, 48 for Wing, etc.)</returns>
        public int GetMaxChannelsForConsole()
        {
            switch (Config.ConsoleType)
            {
                case ProAudioConsole.BehringerX32:
                case ProAudioConsole.BehringerM32:
                    return 32;  // X32/M32 have 32 input channels

                case ProAudioConsole.BehringerWing:
                    return 48;  // Wing has 48 input channels

                case ProAudioConsole.YamahaQL:
                case ProAudioConsole.YamahaCL:
                case ProAudioConsole.YamahaTF:
                    return 64;  // QL/CL/TF series can have up to 64 channels

                case ProAudioConsole.YamahaDM7:
                    return 96;  // DM7 can have up to 96 channels

                case ProAudioConsole.AllenHeathSQ:
                    return 64;  // SQ series can have up to 64 channels

                case ProAudioConsole.AllenHeathDLive:
                    return 128;  // dLive can have up to 128 channels

                case ProAudioConsole.SoundcraftSi:
                    return 64;  // Si Expression/Impact: typically 32-64 channels

                case ProAudioConsole.PresonusStudioLive:
                    return 32;  // StudioLive Series III: typically 32 channels

                case ProAudioConsole.Other:
                    return 64;  // "Other" option - assume 64 channels

                case ProAudioConsole.Custom:
                default:
                    return 64;  // Unknown/Custom console - use conservative default
            }
        }

        /// <summary>
        /// Check if bidirectional sync is enabled and ready
        /// </summary>
        public bool IsBidirectionalSyncEnabled() => IsConnected && Config.EnableReceive && oscServer != null;

        /// <summary>
        /// Handle incoming OSC message from physical board (called by OSC server)
        /// </summary>
        private void HandleIncomingOSCMessage(string address, float value)
        {
            int physicalChannel = ExtractChannelFromOSCAddress(address);
            if (physicalChannel <= 0)
                return;

            // Find which virtual channel(s) map to this physical channel
            List<int> matchingVirtualChannels = new List<int>();
            foreach (var kvp in virtualToPhysicalChannelMap)
            {
                if (kvp.Value == physicalChannel)
                    matchingVirtualChannels.Add(kvp.Key);
            }

            if (matchingVirtualChannels.Count == 0)
                return; // No virtual channels registered for this physical channel

            // Determine command type from address
            string lowerAddress = address.ToLower();
            float normalizedValue = Mathf.Clamp01(value);

            if (lowerAddress.Contains("/fader") || lowerAddress.Contains("/level"))
            {
                // Fader update
                foreach (int virtualChannel in matchingVirtualChannels)
                {
                    if (registeredChannelsForSync.Contains(virtualChannel))
                    {
                        OnChannelFaderChanged?.Invoke(virtualChannel, normalizedValue);
                        Debug.Log($"[ProAudioController] Received fader update - Physical CH {physicalChannel} -> Virtual CH {virtualChannel} = {normalizedValue:F3}");
                    }
                }
            }
            else if (address.Contains("/master") || address.Contains("/main"))
            {
                // Master fader update
                OnMasterFaderChanged?.Invoke(normalizedValue);
                Debug.Log($"[ProAudioController] Received master fader update = {normalizedValue:F3}");
            }
        }

        /// <summary>
        /// Handle incoming OSC message with int value (for mute)
        /// </summary>
        private void HandleIncomingOSCMessage(string address, int value)
        {
            int physicalChannel = ExtractChannelFromOSCAddress(address);
            if (physicalChannel <= 0)
                return;

            // Find which virtual channel(s) map to this physical channel
            List<int> matchingVirtualChannels = new List<int>();
            foreach (var kvp in virtualToPhysicalChannelMap)
            {
                if (kvp.Value == physicalChannel)
                    matchingVirtualChannels.Add(kvp.Key);
            }

            if (matchingVirtualChannels.Count == 0)
                return;

            string lowerAddress = address.ToLower();
            bool muted = (value != 0);

            if (lowerAddress.Contains("/mute") || lowerAddress.Contains("/on"))
            {
                // Mute update
                foreach (int virtualChannel in matchingVirtualChannels)
                {
                    if (registeredChannelsForSync.Contains(virtualChannel))
                    {
                        OnChannelMuteChanged?.Invoke(virtualChannel, muted);
                        Debug.Log($"[ProAudioController] Received mute update - Physical CH {physicalChannel} -> Virtual CH {virtualChannel} = {(muted ? "Muted" : "Unmuted")}");
                    }
                }
            }
        }

        /// <summary>
        /// Extract physical channel number from OSC address (e.g., /ch/01/mix/fader -> 1)
        /// Returns the raw OSC channel number (physical hardware channel)
        /// </summary>
        private int ExtractChannelFromOSCAddress(string oscAddress)
        {
            string lowerAddress = oscAddress.ToLower();
            int oscChannelNumber = -1;

            // Behringer X32/M32 format: /ch/XX/mix/...
            if (lowerAddress.Contains("/ch/"))
            {
                int chPos = lowerAddress.IndexOf("/ch/");
                if (chPos != -1)
                {
                    string afterCh = lowerAddress.Substring(chPos + 4); // After "/ch/"
                    int nextSlash = afterCh.IndexOf('/');
                    if (nextSlash != -1)
                    {
                        string channelStr = afterCh.Substring(0, nextSlash);
                        if (int.TryParse(channelStr, out int channel))
                            oscChannelNumber = channel;
                    }
                }
            }
            // Yamaha format: /mix/chan/XX/...
            else if (lowerAddress.Contains("/mix/chan/"))
            {
                int chanPos = lowerAddress.IndexOf("/mix/chan/");
                if (chanPos != -1)
                {
                    string afterChan = lowerAddress.Substring(chanPos + 10); // After "/mix/chan/"
                    int nextSlash = afterChan.IndexOf('/');
                    if (nextSlash != -1)
                    {
                        string channelStr = afterChan.Substring(0, nextSlash);
                        if (int.TryParse(channelStr, out int channel))
                            oscChannelNumber = channel;
                    }
                }
            }

            // Return raw OSC channel number (physical hardware channel)
            // The virtual-to-physical mapping will be used to find which virtual channel this corresponds to
            return oscChannelNumber;
        }

        /// <summary>
        /// Build OSC address path for console-specific commands
        /// </summary>
        private string BuildOSCPath(string command, int channel, int bus = -1)
        {
            // Apply channel offset (for 0-based vs 1-based indexing)
            int oscChannel = channel + Config.ChannelOffset;
            int oscBus = (bus > 0) ? (bus + Config.ChannelOffset) : -1;

            // Behringer X32/M32/Wing OSC paths (1-based by default)
            if (Config.ConsoleType == ProAudioConsole.BehringerX32 ||
                Config.ConsoleType == ProAudioConsole.BehringerM32 ||
                Config.ConsoleType == ProAudioConsole.BehringerWing)
            {
                if (command == "fader")
                    return $"/ch/{oscChannel:D2}/mix/fader";
                else if (command == "mute")
                    return $"/ch/{oscChannel:D2}/mix/on";
                else if (command == "bus")
                    return $"/ch/{oscChannel:D2}/mix/{oscBus:D2}/level";
                else if (command == "master")
                    return "/main/st/mix/fader";
            }
            // Yamaha QL/CL/TF OSC paths (1-based by default)
            else if (Config.ConsoleType == ProAudioConsole.YamahaQL ||
                     Config.ConsoleType == ProAudioConsole.YamahaCL ||
                     Config.ConsoleType == ProAudioConsole.YamahaTF)
            {
                if (command == "fader")
                    return $"/ch/{oscChannel:D2}/level";
                else if (command == "mute")
                    return $"/ch/{oscChannel:D2}/mute";
                else if (command == "bus")
                    return $"/ch/{oscChannel:D2}/mix/{oscBus:D2}/level";
                else if (command == "master")
                    return "/main/st/level";
            }
            // "Other" - use generic OSC path structure (assumes standard /ch/XX/ format)
            else if (Config.ConsoleType == ProAudioConsole.Other)
            {
                if (command == "fader")
                    return $"/ch/{oscChannel:D2}/fader";
                else if (command == "mute")
                    return $"/ch/{oscChannel:D2}/mute";
                else if (command == "bus")
                    return $"/ch/{oscChannel:D2}/bus/{oscBus:D2}";
                else if (command == "master")
                    return "/master/fader";
            }
            // Custom - use user-provided patterns with XX/YY placeholders
            else if (Config.ConsoleType == ProAudioConsole.Custom)
            {
                string pattern;
                if (command == "fader")
                    pattern = Config.CustomFaderPattern;
                else if (command == "mute")
                    pattern = Config.CustomMutePattern;
                else if (command == "bus")
                    pattern = Config.CustomBusSendPattern;
                else if (command == "master")
                    return Config.CustomMasterPattern; // Master doesn't have channel number
                else
                {
                    Debug.LogWarning($"[ProAudioController] Unknown command for Custom console: {command}");
                    return "";
                }

                // Replace XX with zero-padded channel number (e.g., 5 -> 05, 15 -> 15)
                string path = pattern.Replace("XX", oscChannel.ToString("D2"));
                
                // Replace YY with zero-padded bus number if present
                if (oscBus > 0)
                {
                    path = path.Replace("YY", oscBus.ToString("D2"));
                }

                return path;
            }
            // Default fallback (generic OSC path structure)
            else
            {
                if (command == "fader")
                    return $"/ch/{oscChannel:D2}/fader";
                else if (command == "mute")
                    return $"/ch/{oscChannel:D2}/mute";
                else if (command == "bus")
                    return $"/ch/{oscChannel:D2}/bus/{oscBus:D2}";
                else if (command == "master")
                    return "/master/fader";
            }

            return command;
        }
    }
}


