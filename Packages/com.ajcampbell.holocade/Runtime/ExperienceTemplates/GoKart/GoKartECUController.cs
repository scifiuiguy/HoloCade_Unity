// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core.Networking;
using HoloCade.ExperienceTemplates.GoKart.Models;
using System;

namespace HoloCade.ExperienceTemplates.GoKart
{
    /// <summary>
    /// GoKart ECU Controller
    /// 
    /// Handles UDP communication with GoKartExperience_ECU firmware.
    /// Manages throttle control (man-in-the-middle), button events, and vehicle state.
    /// 
    /// Similar to HapticPlatformController but specialized for GoKart hardware:
    /// - Throttle control (boost/reduction)
    /// - Horn button with LED
    /// - Shield button (long-press)
    /// - Vehicle telemetry
    /// 
    /// Communication Protocol:
    /// - Server → ECU: Throttle commands, game state
    /// - ECU → Server: Button events, throttle feedback, vehicle telemetry
    /// </summary>
    public class GoKartECUController : MonoBehaviour
    {
        [Header("ECU Configuration")]
        [SerializeField] private string ecuIPAddress = "192.168.1.100";
        [SerializeField] private int ecuPort = 8888;

        [Header("Connection")]
        [SerializeField] private bool bECUConnected = false;
        [SerializeField] private float connectionTimeout = 2.0f;

        private HoloCadeUDPTransport udpTransport;
        private float lastButtonEventTime = 0.0f;
        private float lastThrottleFeedbackTime = 0.0f;

        private GoKartButtonEvents lastButtonEvents = new GoKartButtonEvents();
        private GoKartThrottleState lastThrottleState = new GoKartThrottleState();

        void Start()
        {
            // NOOP: ECU initialization should be called explicitly by experience
        }

        void Update()
        {
            // NOOP: UDP data processing will be implemented
            // Check for connection timeout
            if (bECUConnected)
            {
                float currentTime = Time.time;
                if (currentTime - lastButtonEventTime > connectionTimeout &&
                    currentTime - lastThrottleFeedbackTime > connectionTimeout)
                {
                    // Connection lost
                    bECUConnected = false;
                    Debug.LogWarning("[HoloCade] GoKartECU: Connection timeout");
                }
            }
        }

        /// <summary>
        /// Initialize connection to GoKart ECU
        /// </summary>
        public bool InitializeECU(string ipAddress, int port = 8888)
        {
            ecuIPAddress = ipAddress;
            ecuPort = port;

            if (udpTransport == null)
            {
                udpTransport = gameObject.AddComponent<HoloCadeUDPTransport>();
            }

            if (!udpTransport.InitializeUDPConnection(ecuIPAddress, ecuPort, "GoKart_ECU"))
            {
                Debug.LogError($"[HoloCade] GoKartECU: Failed to initialize UDP connection to {ecuIPAddress}:{ecuPort}");
                return false;
            }

            // Subscribe to received data events
            udpTransport.onBytesReceived.AddListener(OnBytesReceived);

            bECUConnected = true;
            Debug.Log($"[HoloCade] GoKartECU: Connected to {ecuIPAddress}:{ecuPort}");
            return true;
        }

        /// <summary>
        /// Shutdown ECU connection
        /// </summary>
        public void ShutdownECU()
        {
            if (udpTransport != null)
            {
                udpTransport.onBytesReceived.RemoveListener(OnBytesReceived);
                udpTransport.ShutdownUDPConnection();
            }
            bECUConnected = false;
        }

        /// <summary>
        /// Check if ECU is connected
        /// </summary>
        public bool IsECUConnected()
        {
            return bECUConnected && udpTransport != null && udpTransport.IsUDPConnected();
        }

        // =====================================
        // Throttle Control (Server → ECU)
        // =====================================

        /// <summary>
        /// Send throttle multiplier to ECU (man-in-the-middle control)
        /// </summary>
        /// <param name="multiplier">Throttle multiplier (1.0 = normal, >1.0 = boost, <1.0 = reduction)</param>
        public void SetThrottleMultiplier(float multiplier)
        {
            // NOOP: Will send throttle multiplier to ECU via Channel 0
            if (udpTransport != null)
            {
                udpTransport.SendFloat(0, Mathf.Clamp(multiplier, 0.0f, 2.0f));
            }
        }

        /// <summary>
        /// Send complete throttle state to ECU
        /// </summary>
        /// <param name="throttleState">Complete throttle state (input, multiplier, output)</param>
        public void SendThrottleState(GoKartThrottleState throttleState)
        {
            // NOOP: Will send complete throttle state struct to ECU via Channel 100
            if (udpTransport != null)
            {
                // Serialize struct to bytes
                byte[] structData = SerializeThrottleState(throttleState);
                udpTransport.SendBytes(100, structData);
            }
        }

        // =====================================
        // Game State (Server → ECU)
        // =====================================

        /// <summary>
        /// Set play session active state (controls whether kart can operate)
        /// </summary>
        /// <param name="active">True if play session is active</param>
        public void SetPlaySessionActive(bool active)
        {
            // NOOP: Will send play session state to ECU via Channel 9
            if (udpTransport != null)
            {
                udpTransport.SendBool(9, active);
            }
        }

        /// <summary>
        /// Send emergency stop command
        /// </summary>
        public void EmergencyStop()
        {
            // NOOP: Will send emergency stop to ECU via Channel 7
            if (udpTransport != null)
            {
                udpTransport.SendBool(7, true);
            }
        }

        // =====================================
        // Button Events (ECU → Server)
        // =====================================

        /// <summary>
        /// Get button events from ECU (Channel 310)
        /// </summary>
        /// <param name="outButtonEvents">Output button events</param>
        /// <returns>True if valid button events were received</returns>
        public bool GetButtonEvents(out GoKartButtonEvents outButtonEvents)
        {
            outButtonEvents = lastButtonEvents;
            return (Time.time - lastButtonEventTime) < connectionTimeout;
        }

        /// <summary>
        /// Get throttle state feedback from ECU (Channel 311)
        /// </summary>
        /// <param name="outThrottleState">Output throttle state</param>
        /// <returns>True if valid throttle state was received</returns>
        public bool GetThrottleStateFeedback(out GoKartThrottleState outThrottleState)
        {
            outThrottleState = lastThrottleState;
            return (Time.time - lastThrottleFeedbackTime) < connectionTimeout;
        }

        // =====================================
        // Internal Methods
        // =====================================

        private void OnBytesReceived(int channel, byte[] data)
        {
            // NOOP: Will process incoming UDP data and route to appropriate channels
            if (channel == 310)
            {
                // Parse button events
                if (data != null && data.Length >= GetButtonEventsSize())
                {
                    lastButtonEvents = DeserializeButtonEvents(data);
                    lastButtonEventTime = Time.time;
                }
            }
            else if (channel == 311)
            {
                // Parse throttle state
                if (data != null && data.Length >= GetThrottleStateSize())
                {
                    lastThrottleState = DeserializeThrottleState(data);
                    lastThrottleFeedbackTime = Time.time;
                }
            }
        }

        private byte[] SerializeThrottleState(GoKartThrottleState state)
        {
            // NOOP: Serialize struct to byte array
            // This should match firmware struct layout exactly
            byte[] data = new byte[GetThrottleStateSize()];
            int offset = 0;
            System.BitConverter.GetBytes(state.RawThrottleInput).CopyTo(data, offset); offset += sizeof(float);
            System.BitConverter.GetBytes(state.ThrottleMultiplier).CopyTo(data, offset); offset += sizeof(float);
            System.BitConverter.GetBytes(state.FinalThrottleOutput).CopyTo(data, offset); offset += sizeof(float);
            System.BitConverter.GetBytes(state.Timestamp).CopyTo(data, offset);
            return data;
        }

        private GoKartThrottleState DeserializeThrottleState(byte[] data)
        {
            // NOOP: Deserialize byte array to struct
            GoKartThrottleState state = new GoKartThrottleState();
            int offset = 0;
            state.RawThrottleInput = System.BitConverter.ToSingle(data, offset); offset += sizeof(float);
            state.ThrottleMultiplier = System.BitConverter.ToSingle(data, offset); offset += sizeof(float);
            state.FinalThrottleOutput = System.BitConverter.ToSingle(data, offset); offset += sizeof(float);
            state.Timestamp = System.BitConverter.ToInt32(data, offset);
            return state;
        }

        private GoKartButtonEvents DeserializeButtonEvents(byte[] data)
        {
            // NOOP: Deserialize byte array to struct
            GoKartButtonEvents events = new GoKartButtonEvents();
            int offset = 0;
            events.HornButtonState = System.BitConverter.ToBoolean(data, offset); offset += sizeof(bool);
            events.HornLEDState = System.BitConverter.ToBoolean(data, offset); offset += sizeof(bool);
            events.ShieldButtonState = System.BitConverter.ToBoolean(data, offset); offset += sizeof(bool);
            events.Timestamp = System.BitConverter.ToInt32(data, offset);
            return events;
        }

        private int GetThrottleStateSize()
        {
            return sizeof(float) * 3 + sizeof(int); // RawThrottleInput, ThrottleMultiplier, FinalThrottleOutput, Timestamp
        }

        private int GetButtonEventsSize()
        {
            return sizeof(bool) * 3 + sizeof(int); // HornButtonState, HornLEDState, ShieldButtonState, Timestamp
        }

        void OnDestroy()
        {
            ShutdownECU();
        }
    }
}

