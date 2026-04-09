// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HoloCade.VOIP
{
    /// <summary>
    /// HoloCade VOIP Manager Component
    /// 
    /// Main component for VOIP functionality. Attach to HMD object or player GameObject.
    /// 
    /// Handles:
    /// - Mumble connection management
    /// - Per-user audio source creation and management
    /// - Steam Audio spatialization setup
    /// - Automatic audio routing based on player positions
    /// 
    /// Usage:
    /// 1. Add component to HMD/Player GameObject
    /// 2. Set Server IP and Port
    /// 3. Call Connect() to start VOIP
    /// 4. Audio is automatically spatialized based on player positions
    /// 
    /// Replication:
    /// - Uses Unity Netcode for GameObjects replication system
    /// - Player positions are replicated automatically
    /// - Audio data is streamed via Mumble (not replicated)
    /// </summary>
    [AddComponentMenu("HoloCade/VOIP Manager")]
    public class VOIPManager : MonoBehaviour
    {
        [Header("Connection")]
        [Tooltip("Server IP address for Mumble connection")]
        public string serverIP = "192.168.1.100";

        [Tooltip("Server port for Mumble connection (default: 64738)")]
        public int serverPort = 64738;

        [Tooltip("Player name/identifier for Mumble (auto-generated if empty)")]
        public string playerName = "";

        [Tooltip("Enable automatic connection on Start")]
        public bool autoConnect = true;

        [Header("Audio")]
        [Tooltip("Microphone mute state")]
        public bool microphoneMuted = false;

        [Tooltip("Audio output volume (0.0 to 1.0)")]
        [Range(0f, 1f)]
        public float outputVolume = 1.0f;

        [Header("Events")]
        [Tooltip("Fired when connection state changes")]
        public UnityEvent<VOIPConnectionState> OnConnectionStateChanged = new UnityEvent<VOIPConnectionState>();

        [Tooltip("Fired when remote player audio is received")]
        public UnityEvent<int, Vector3> OnRemotePlayerAudioReceived = new UnityEvent<int, Vector3>();

        /// <summary>
        /// Current connection state
        /// </summary>
        public VOIPConnectionState ConnectionState { get; private set; } = VOIPConnectionState.Disconnected;

        /// <summary>
        /// Mumble client instance
        /// </summary>
        private MumbleClient mumbleClient;

        /// <summary>
        /// Map of user IDs to audio source components
        /// </summary>
        private Dictionary<int, SteamAudioSourceComponent> audioSourceMap = new Dictionary<int, SteamAudioSourceComponent>();

        /// <summary>
        /// Registered audio visitors (for decoupled module integration)
        /// </summary>
        private List<IVOIPAudioVisitor> audioVisitors = new List<IVOIPAudioVisitor>();

        private void Start()
        {
            // Auto-generate player name if not set
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = $"Player_{gameObject.GetInstanceID()}";
            }

            // Create Mumble client
            mumbleClient = gameObject.AddComponent<MumbleClient>();
            if (mumbleClient != null)
            {
                mumbleClient.OnAudioReceived.AddListener(OnMumbleAudioReceived);
                mumbleClient.OnConnectionStateChanged.AddListener(OnMumbleConnectionStateChanged);
            }

            // Auto-connect if enabled
            if (autoConnect)
            {
                Connect();
            }
        }

        private void Update()
        {
            // Update audio source positions
            if (ConnectionState == VOIPConnectionState.Connected)
            {
                UpdateAudioSourcePositions();
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        /// <summary>
        /// Connect to Mumble server
        /// </summary>
        public bool Connect()
        {
            if (ConnectionState == VOIPConnectionState.Connected || 
                ConnectionState == VOIPConnectionState.Connecting)
            {
                Debug.LogWarning("VOIPManager: Already connected or connecting");
                return false;
            }

            if (mumbleClient == null)
            {
                Debug.LogError("VOIPManager: MumbleClient not initialized");
                return false;
            }

            Debug.Log($"VOIPManager: Connecting to Mumble server {serverIP}:{serverPort} as {playerName}");

            ConnectionState = VOIPConnectionState.Connecting;
            OnConnectionStateChanged?.Invoke(ConnectionState);

            bool success = mumbleClient.Connect(serverIP, serverPort, playerName);

            if (!success)
            {
                ConnectionState = VOIPConnectionState.Error;
                OnConnectionStateChanged?.Invoke(ConnectionState);
                Debug.LogError("VOIPManager: Failed to connect to Mumble server");
            }

            return success;
        }

        /// <summary>
        /// Disconnect from Mumble server
        /// </summary>
        public void Disconnect()
        {
            if (ConnectionState == VOIPConnectionState.Disconnected)
            {
                return;
            }

            Debug.Log("VOIPManager: Disconnecting from Mumble server");

            if (mumbleClient != null)
            {
                mumbleClient.Disconnect();
            }

            // Clean up all audio sources
            foreach (var pair in audioSourceMap)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }
            audioSourceMap.Clear();

            ConnectionState = VOIPConnectionState.Disconnected;
            OnConnectionStateChanged?.Invoke(ConnectionState);
        }

        /// <summary>
        /// Check if currently connected
        /// </summary>
        public bool IsConnected()
        {
            return ConnectionState == VOIPConnectionState.Connected;
        }

        /// <summary>
        /// Get current player count
        /// </summary>
        public int GetPlayerCount()
        {
            return audioSourceMap.Count;
        }

        /// <summary>
        /// Set microphone mute state
        /// </summary>
        public void SetMicrophoneMuted(bool muted)
        {
            microphoneMuted = muted;
            if (mumbleClient != null)
            {
                mumbleClient.SetMicrophoneMuted(muted);
            }
            Debug.Log($"VOIPManager: Microphone {(muted ? "muted" : "unmuted")}");
        }

        /// <summary>
        /// Check if microphone is muted
        /// </summary>
        public bool IsMicrophoneMuted()
        {
            return microphoneMuted;
        }

        /// <summary>
        /// Set audio output volume (0.0 to 1.0)
        /// </summary>
        public void SetOutputVolume(float volume)
        {
            outputVolume = Mathf.Clamp01(volume);

            // Update all audio sources
            foreach (var pair in audioSourceMap)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetVolume(outputVolume);
                }
            }
        }

        /// <summary>
        /// Get audio output volume
        /// </summary>
        public float GetOutputVolume()
        {
            return outputVolume;
        }

        /// <summary>
        /// Handle remote audio received from Mumble
        /// </summary>
        private void OnMumbleAudioReceived(int userId, byte[] opusData, Vector3 position)
        {
            // Get or create audio source for this user
            SteamAudioSourceComponent audioSource = GetOrCreateAudioSource(userId);
            if (audioSource == null)
            {
                Debug.LogWarning($"VOIPManager: Failed to create audio source for user {userId}");
                return;
            }

            // Process audio through Steam Audio spatialization
            audioSource.ProcessAudioData(opusData, position);

            // Broadcast event
            OnRemotePlayerAudioReceived?.Invoke(userId, position);

            // Notify all registered visitors
            // NOOP: TODO - Decode Opus to PCM before passing to visitors
            // For now, pass empty PCM data - visitor will need to decode Opus themselves
            // Or we decode here and pass PCM
            float[] decodedPCM = new float[0];  // Placeholder - actual implementation will decode Opus
            // TODO: Decode opusData to decodedPCM using Opus decoder
            // Typical Mumble sample rate is 48000 Hz
            
            foreach (IVOIPAudioVisitor visitor in audioVisitors)
            {
                if (visitor != null)
                {
                    visitor.OnPlayerAudioReceived(userId, decodedPCM, 48000, position);  // Mumble uses 48kHz
                }
            }
        }

        /// <summary>
        /// Handle connection state change from Mumble
        /// </summary>
        private void OnMumbleConnectionStateChanged(VOIPConnectionState newState)
        {
            ConnectionState = newState;
            OnConnectionStateChanged?.Invoke(newState);

            if (newState == VOIPConnectionState.Connected)
            {
                Debug.Log("VOIPManager: Connected to Mumble server");
            }
            else if (newState == VOIPConnectionState.Disconnected)
            {
                Debug.Log("VOIPManager: Disconnected from Mumble server");
            }
            else if (newState == VOIPConnectionState.Error)
            {
                Debug.LogError("VOIPManager: Connection error");
            }
        }

        /// <summary>
        /// Create or get audio source for a user
        /// </summary>
        private SteamAudioSourceComponent GetOrCreateAudioSource(int userId)
        {
            // Check if audio source already exists
            if (audioSourceMap.TryGetValue(userId, out SteamAudioSourceComponent existingSource))
            {
                if (existingSource != null)
                {
                    return existingSource;
                }
            }

            // Create new audio source GameObject
            GameObject audioSourceGO = new GameObject($"VOIPAudioSource_{userId}");
            audioSourceGO.transform.SetParent(transform);
            SteamAudioSourceComponent audioSource = audioSourceGO.AddComponent<SteamAudioSourceComponent>();
            audioSource.SetVolume(outputVolume);

            // Add to map
            audioSourceMap[userId] = audioSource;

            Debug.Log($"VOIPManager: Created audio source for user {userId}");

            return audioSource;
        }

        /// <summary>
        /// Remove audio source for a user
        /// </summary>
        private void RemoveAudioSource(int userId)
        {
            if (audioSourceMap.TryGetValue(userId, out SteamAudioSourceComponent audioSource))
            {
                if (audioSource != null)
                {
                    Destroy(audioSource.gameObject);
                }
                audioSourceMap.Remove(userId);
                Debug.Log($"VOIPManager: Removed audio source for user {userId}");
            }
        }

        /// <summary>
        /// Register an audio visitor to receive audio events
        /// Visitors are notified when player audio is received
        /// </summary>
        /// <param name="visitor">Visitor implementing IVOIPAudioVisitor interface</param>
        public void RegisterAudioVisitor(IVOIPAudioVisitor visitor)
        {
            if (visitor == null)
            {
                Debug.LogWarning("VOIPManager: Attempted to register null audio visitor");
                return;
            }

            if (!audioVisitors.Contains(visitor))
            {
                audioVisitors.Add(visitor);
                Debug.Log($"VOIPManager: Registered audio visitor {visitor.GetType().Name}");
            }
        }

        /// <summary>
        /// Unregister an audio visitor
        /// </summary>
        /// <param name="visitor">Visitor to remove</param>
        public void UnregisterAudioVisitor(IVOIPAudioVisitor visitor)
        {
            if (visitor == null)
            {
                return;
            }

            if (audioVisitors.Remove(visitor))
            {
                Debug.Log($"VOIPManager: Unregistered audio visitor {visitor.GetType().Name}");
            }
        }

        /// <summary>
        /// Update audio source positions based on player locations
        /// </summary>
        private void UpdateAudioSourcePositions()
        {
            // Get local player position (for HRTF calculation)
            Vector3 localPlayerPosition = Vector3.zero;
            Quaternion localPlayerRotation = Quaternion.identity;

            if (Camera.main != null)
            {
                localPlayerPosition = Camera.main.transform.position;
                localPlayerRotation = Camera.main.transform.rotation;
            }
            else
            {
                localPlayerPosition = transform.position;
                localPlayerRotation = transform.rotation;
            }

            // Update all audio source positions
            // Note: Remote player positions should come from replicated player states
            // This is a placeholder - actual implementation will query player positions from game state
            foreach (var pair in audioSourceMap)
            {
                if (pair.Value != null)
                {
                    // Get remote player position (this should come from replicated player state)
                    // For now, we'll use the position from Mumble if available
                    // TODO: Integrate with player replication system
                    Vector3 remotePlayerPosition = Vector3.zero; // Get from player state
                    pair.Value.UpdatePosition(remotePlayerPosition, localPlayerPosition, localPlayerRotation);
                }
            }
        }
    }
}

