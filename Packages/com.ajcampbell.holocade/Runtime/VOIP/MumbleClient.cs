// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HoloCade.VOIP
{
    /// <summary>
    /// Mumble Client Wrapper
    /// 
    /// Wraps Mumble protocol implementation for Unity.
    /// Handles connection, audio encoding/decoding, and user management.
    /// 
    /// This class will interface with the MumbleLink plugin (git submodule)
    /// to provide low-latency VOIP functionality.
    /// 
    /// Protocol:
    /// - Uses Mumble's native protocol (TCP for control, UDP for audio)
    /// - Opus codec for audio compression
    /// - Positional audio support
    /// 
    /// Note: Actual Mumble implementation will be provided by MumbleLink plugin.
    /// This class provides a clean Unity-friendly interface.
    /// </summary>
    public class MumbleClient : MonoBehaviour
    {
        [Header("Connection")]
        [Tooltip("Server IP address")]
        public string serverIP = "192.168.1.100";

        [Tooltip("Server port (default: 64738)")]
        public int serverPort = 64738;

        [Tooltip("Username for connection")]
        public string userName = "";

        [Header("Audio")]
        [Tooltip("Microphone mute state")]
        public bool microphoneMuted = false;

        [Header("Events")]
        [Tooltip("Fired when audio data is received from a remote user")]
        public UnityEvent<int, byte[], Vector3> OnAudioReceived = new UnityEvent<int, byte[], Vector3>();

        [Tooltip("Fired when connection state changes")]
        public UnityEvent<VOIPConnectionState> OnConnectionStateChanged = new UnityEvent<VOIPConnectionState>();

        /// <summary>
        /// Is currently connected
        /// </summary>
        public bool IsConnected { get; private set; } = false;

        /// <summary>
        /// User ID assigned by server
        /// </summary>
        public int UserId { get; private set; } = -1;

        private VOIPConnectionState connectionState = VOIPConnectionState.Disconnected;

        /// <summary>
        /// Connect to Mumble server
        /// </summary>
        public bool Connect(string ip, int port, string name)
        {
            if (IsConnected)
            {
                Debug.LogWarning("MumbleClient: Already connected");
                return false;
            }

            serverIP = ip;
            serverPort = port;
            userName = name;

            Debug.Log($"MumbleClient: Connecting to {serverIP}:{serverPort} as {userName}");

            // Initialize MumbleLink plugin
            if (!InitializeMumbleLink())
            {
                Debug.LogError("MumbleClient: Failed to initialize MumbleLink plugin");
                SetConnectionState(VOIPConnectionState.Error);
                return false;
            }

            // TODO: Call MumbleLink plugin's Connect function
            // if (MumbleLinkInterface != null)
            // {
            //     bool success = MumbleLinkInterface.Connect(serverIP, serverPort, userName);
            //     if (success)
            //     {
            //         SetConnectionState(VOIPConnectionState.Connecting);
            //     }
            //     return success;
            // }

            // Placeholder: For now, simulate connection
            SetConnectionState(VOIPConnectionState.Connecting);

            // Simulate successful connection after a delay
            // In real implementation, this will be called by MumbleLink plugin callback
            Invoke(nameof(SimulateConnection), 1.0f);

            return true;
        }

        private void SimulateConnection()
        {
            SetConnectionState(VOIPConnectionState.Connected);
            UserId = 1; // Placeholder - will be assigned by server
        }

        /// <summary>
        /// Disconnect from Mumble server
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }

            Debug.Log("MumbleClient: Disconnecting from server");

            // TODO: Call MumbleLink plugin's Disconnect function
            // if (MumbleLinkInterface != null)
            // {
            //     MumbleLinkInterface.Disconnect();
            // }

            CleanupMumbleLink();

            IsConnected = false;
            UserId = -1;
            SetConnectionState(VOIPConnectionState.Disconnected);
        }

        /// <summary>
        /// Set microphone mute state
        /// </summary>
        public void SetMicrophoneMuted(bool muted)
        {
            microphoneMuted = muted;

            // TODO: Call MumbleLink plugin's SetMute function
            // if (MumbleLinkInterface != null)
            // {
            //     MumbleLinkInterface.SetMicrophoneMuted(muted);
            // }

            Debug.Log($"MumbleClient: Microphone {(muted ? "muted" : "unmuted")}");
        }

        /// <summary>
        /// Send audio data to server
        /// Called automatically from microphone input
        /// 
        /// Note: Microphone capture uses Unity's audio system, which accesses
        /// any microphone device recognized by the OS (WASAPI on Windows).
        /// HMD microphones (Oculus, Vive, etc.) appear as standard audio input
        /// devices and are automatically accessible.
        /// </summary>
        public void SendAudioData(float[] pcmData, Vector3 position)
        {
            if (!IsConnected || microphoneMuted)
            {
                return;
            }

            // TODO: Encode PCM to Opus and send via MumbleLink plugin
            // if (MumbleLinkInterface != null)
            // {
            //     byte[] opusData;
            //     if (EncodeOpus(pcmData, out opusData))
            //     {
            //         MumbleLinkInterface.SendAudio(opusData, position);
            //     }
            // }
        }

        /// <summary>
        /// Process incoming audio data
        /// Called by MumbleLink plugin when audio is received
        /// </summary>
        public void ProcessIncomingAudio(int userId, byte[] opusData, Vector3 position)
        {
            // Broadcast to listeners
            OnAudioReceived?.Invoke(userId, opusData, position);
        }

        /// <summary>
        /// Update connection state
        /// </summary>
        public void SetConnectionState(VOIPConnectionState newState)
        {
            if (newState == VOIPConnectionState.Connected)
            {
                IsConnected = true;
            }
            else if (newState == VOIPConnectionState.Disconnected || newState == VOIPConnectionState.Error)
            {
                IsConnected = false;
            }

            connectionState = newState;
            OnConnectionStateChanged?.Invoke(newState);
        }

        /// <summary>
        /// Get current connection state
        /// </summary>
        public VOIPConnectionState GetConnectionState()
        {
            return connectionState;
        }

        private bool InitializeMumbleLink()
        {
            // TODO: Load MumbleLink plugin and get interface
            // This will be implemented when MumbleLink submodule is added
            // 
            // Example:
            // MumbleLinkInterface = ...;
            // return MumbleLinkInterface != null;

            Debug.LogWarning("MumbleClient: MumbleLink plugin not yet integrated. Using placeholder.");
            return true; // Placeholder - return true for now
        }

        private void CleanupMumbleLink()
        {
            // TODO: Cleanup MumbleLink plugin interface
            // MumbleLinkInterface = null;
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}



