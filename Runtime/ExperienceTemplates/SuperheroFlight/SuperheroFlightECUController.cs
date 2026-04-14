// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using System;
using System.Runtime.InteropServices;
using HoloCade.Core.Networking;
using HoloCade.ExperienceTemplates.SuperheroFlight.Models;

namespace HoloCade.ExperienceTemplates.SuperheroFlight
{
    /// <summary>
    /// Superhero Flight ECU Controller
    /// 
    /// Handles UDP communication with SuperheroFlightExperience_ECU firmware.
    /// Manages dual-winch control (front and rear winches), game state, and safety interlocks.
    /// 
    /// Communication Protocol (Binary HoloCade over UDP):
    /// - Server → ECU: Winch positions/speeds, game state, parameters (Channels 0-13)
    /// - ECU → Server: Dual-winch telemetry (Channel 310), system telemetry (Channel 311)
    /// 
    /// Channel Mapping (Server → ECU):
    /// - Channel 0: Front winch position (inches, relative to standingGroundHeight)
    /// - Channel 1: Front winch speed (inches/second)
    /// - Channel 2: Rear winch position (inches, relative to standingGroundHeight)
    /// - Channel 3: Rear winch speed (inches/second)
    /// - Channel 6: Game state (0=standing, 1=hovering, 2=flight-up, 3=flight-forward, 4=flight-down)
    /// - Channel 7: Emergency stop (true = stop all systems, return to standing)
    /// - Channel 9: Play session active (true = winches can operate)
    /// - Channel 10: Standing ground height acknowledgment (current winch position becomes new baseline)
    /// - Channel 11: Air height parameter (inches)
    /// - Channel 12: Prone height parameter (inches)
    /// - Channel 13: Player height compensation (multiplier)
    /// </summary>
    public class SuperheroFlightECUController : MonoBehaviour
    {
        [Header("ECU Configuration")]
        [SerializeField] private string ecuIPAddress = "192.168.1.100";
        [SerializeField] private int ecuPort = 8888;
        [SerializeField] private float connectionTimeout = 2.0f;

        private HoloCadeUDPTransport udpTransport;
        private bool bECUConnected = false;

        private SuperheroFlightDualWinchState lastWinchState;
        private SuperheroFlightTelemetry lastTelemetry;

        private float lastWinchStateTime = 0.0f;
        private float lastTelemetryTime = 0.0f;

        void Awake()
        {
            udpTransport = GetComponent<HoloCadeUDPTransport>();
            if (udpTransport == null)
            {
                udpTransport = gameObject.AddComponent<HoloCadeUDPTransport>();
            }
        }

        void Update()
        {
            // Check for connection timeout
            if (bECUConnected)
            {
                float currentTime = Time.time;
                if (currentTime - lastWinchStateTime > connectionTimeout &&
                    currentTime - lastTelemetryTime > connectionTimeout)
                {
                    // Connection lost
                    bECUConnected = false;
                    Debug.LogWarning("[HoloCade] SuperheroFlightECU: Connection timeout");
                }
            }
        }

        void OnDestroy()
        {
            ShutdownECU();
        }

        /// <summary>
        /// Initialize connection to Superhero Flight ECU
        /// </summary>
        public bool InitializeECU(string ipAddress, int port)
        {
            ecuIPAddress = ipAddress;
            ecuPort = port;

            if (!udpTransport.InitializeUDPConnection(ecuIPAddress, ecuPort, "SuperheroFlight_ECU"))
            {
                Debug.LogError($"[HoloCade] SuperheroFlightECU: Failed to initialize UDP connection to {ecuIPAddress}:{ecuPort}");
                return false;
            }

            // Subscribe to UDP data reception events
            udpTransport.onBytesReceived.AddListener(OnBytesReceived);

            bECUConnected = true;
            Debug.Log($"[HoloCade] SuperheroFlightECU: Connected to {ecuIPAddress}:{ecuPort}");
            return true;
        }

        /// <summary>
        /// Shutdown ECU connection
        /// </summary>
        public void ShutdownECU()
        {
            if (udpTransport != null)
            {
                // Unsubscribe from UDP data reception events
                udpTransport.onBytesReceived.RemoveListener(OnBytesReceived);
                udpTransport.ShutdownUDPConnection();
            }
            bECUConnected = false;
        }

        /// <summary>
        /// Check if ECU is connected
        /// </summary>
        public bool IsECUConnected() => bECUConnected && udpTransport != null && udpTransport.IsUDPConnected();

        // =====================================
        // Winch Control (Server → ECU)
        // =====================================

        /// <summary>
        /// Set front winch position (Channel 0)
        /// </summary>
        public void SetFrontWinchPosition(float position)
        {
            udpTransport?.SendFloat(0, position);
        }

        /// <summary>
        /// Set front winch speed (Channel 1)
        /// </summary>
        public void SetFrontWinchSpeed(float speed)
        {
            udpTransport?.SendFloat(1, Mathf.Max(0.0f, speed));
        }

        /// <summary>
        /// Set rear winch position (Channel 2)
        /// </summary>
        public void SetRearWinchPosition(float position)
        {
            udpTransport?.SendFloat(2, position);
        }

        /// <summary>
        /// Set rear winch speed (Channel 3)
        /// </summary>
        public void SetRearWinchSpeed(float speed)
        {
            udpTransport?.SendFloat(3, Mathf.Max(0.0f, speed));
        }

        /// <summary>
        /// Set both winch positions simultaneously
        /// </summary>
        public void SetDualWinchPositions(float frontPosition, float rearPosition, float speed)
        {
            SetFrontWinchPosition(frontPosition);
            SetRearWinchPosition(rearPosition);
            SetFrontWinchSpeed(speed);
            SetRearWinchSpeed(speed);
        }

        // =====================================
        // Game State (Server → ECU)
        // =====================================

        /// <summary>
        /// Set game state (Channel 6)
        /// </summary>
        public void SetGameState(SuperheroFlightGameState gameState)
        {
            int stateValue = (int)gameState;
            udpTransport?.SendInt32(6, stateValue);
        }

        /// <summary>
        /// Set play session active state (Channel 9)
        /// </summary>
        public void SetPlaySessionActive(bool active)
        {
            udpTransport?.SendBool(9, active);
        }

        /// <summary>
        /// Send emergency stop command (Channel 7)
        /// </summary>
        public void EmergencyStop()
        {
            udpTransport?.SendBool(7, true);
        }

        // =====================================
        // Parameters (Server → ECU)
        // =====================================

        /// <summary>
        /// Acknowledge standing ground height (Channel 10)
        /// </summary>
        public void AcknowledgeStandingGroundHeight(float height)
        {
            udpTransport?.SendFloat(10, height);
        }

        /// <summary>
        /// Set air height parameter (Channel 11)
        /// </summary>
        public void SetAirHeight(float height)
        {
            udpTransport?.SendFloat(11, Mathf.Max(0.0f, height));
        }

        /// <summary>
        /// Set prone height parameter (Channel 12)
        /// </summary>
        public void SetProneHeight(float height)
        {
            udpTransport?.SendFloat(12, Mathf.Max(0.0f, height));
        }

        /// <summary>
        /// Set player height compensation multiplier (Channel 13)
        /// </summary>
        public void SetPlayerHeightCompensation(float multiplier)
        {
            udpTransport?.SendFloat(13, Mathf.Clamp(multiplier, 0.5f, 2.0f));
        }

        // =====================================
        // Telemetry (ECU → Server)
        // =====================================

        /// <summary>
        /// Get dual-winch state from ECU (Channel 310)
        /// </summary>
        public bool GetDualWinchState(out SuperheroFlightDualWinchState outWinchState)
        {
            outWinchState = lastWinchState;
            return (Time.time - lastWinchStateTime) < connectionTimeout;
        }

        /// <summary>
        /// Get system telemetry from ECU (Channel 311)
        /// </summary>
        public bool GetSystemTelemetry(out SuperheroFlightTelemetry outTelemetry)
        {
            outTelemetry = lastTelemetry;
            return (Time.time - lastTelemetryTime) < connectionTimeout;
        }

        // =====================================
        // Private Methods
        // =====================================

        private void OnBytesReceived(int channel, byte[] data)
        {
            // Process Channel 310 (dual-winch state) and Channel 311 (telemetry)
            if (channel == 310)
            {
                // Parse SuperheroFlightDualWinchState struct
                if (data.Length >= Marshal.SizeOf<SuperheroFlightDualWinchState>())
                {
                    GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                    try
                    {
                        lastWinchState = (SuperheroFlightDualWinchState)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SuperheroFlightDualWinchState));
                        lastWinchStateTime = Time.time;
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                else
                {
                    Debug.LogWarning($"[HoloCade] SuperheroFlightECU: Invalid winch state packet size ({data.Length} bytes, expected {Marshal.SizeOf<SuperheroFlightDualWinchState>()})");
                }
            }
            else if (channel == 311)
            {
                // Parse SuperheroFlightTelemetry struct
                if (data.Length >= Marshal.SizeOf<SuperheroFlightTelemetry>())
                {
                    GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                    try
                    {
                        lastTelemetry = (SuperheroFlightTelemetry)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SuperheroFlightTelemetry));
                        lastTelemetryTime = Time.time;
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                else
                {
                    Debug.LogWarning($"[HoloCade] SuperheroFlightECU: Invalid telemetry packet size ({data.Length} bytes, expected {Marshal.SizeOf<SuperheroFlightTelemetry>()})");
                }
            }
        }
    }
}

