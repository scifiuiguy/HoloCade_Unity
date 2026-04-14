/*
 * HoloCade Example: Actor Costume Controller
 * 
 * Demonstrates secure bidirectional communication with ESP32-based costume
 * - Receives button presses from actor's vest
 * - Sends haptic feedback to vibration motors
 * - Uses AES-128 encryption for production security
 * 
 * Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.
 */

using UnityEngine;
using HoloCade.EmbeddedSystems;

namespace HoloCade.Examples
{
    /// <summary>
    /// Example controller for an interactive actor costume with:
    /// - 4 buttons (chest/shoulder mounted)
    /// - 6 vibration motors (vest + gloves)
    /// - WiFi communication via ESP32
    /// </summary>
    public class ExampleCostumeController : MonoBehaviour
    {
        [Header("Device Configuration")]
        [Tooltip("ESP32 IP address on venue LAN")]
        public string esp32IPAddress = "192.168.1.50";
        
        [Tooltip("UDP port (must match ESP32 firmware)")]
        public int udpPort = 8888;
        
        [Tooltip("Shared encryption key (must match ESP32 firmware)")]
        public string sharedSecret = "CHANGE_ME_IN_PRODUCTION_2025";

        [Header("Security")]
        [Tooltip("Enable for production, disable for development")]
        public bool enableEncryption = true;
        
        [Tooltip("Enable to see packets in Wireshark (disables encryption!)")]
        public bool debugMode = false;

        [Header("Motor Mapping")]
        [Tooltip("Which motor channels to use for haptic feedback")]
        public int vestMotorChannel = 0;
        public int leftGloveChannel = 1;
        public int rightGloveChannel = 2;

        private SerialDeviceController device;

        // =====================================
        // Initialization
        // =====================================

        void Start()
        {
            // Create device controller
            device = gameObject.AddComponent<SerialDeviceController>();

            // Configure device
            device.config.protocol = CommProtocol.WiFi;
            device.config.deviceAddress = esp32IPAddress;
            device.config.port = udpPort;
            device.config.inputChannelCount = 4;   // 4 buttons
            device.config.outputChannelCount = 6;  // 6 motors

            // Security settings
            device.config.debugMode = debugMode;
            device.config.securityLevel = enableEncryption ? SecurityLevel.Encrypted : SecurityLevel.None;
            device.config.sharedSecret = sharedSecret;

            // Subscribe to button events
            device.onBoolReceived.AddListener(OnCostumeButtonChanged);

            // Initialize connection
            if (device.InitializeDevice(device.config))
            {
                Debug.Log("✅ Costume controller initialized successfully!");
            }
            else
            {
                Debug.LogError("❌ Failed to initialize costume controller");
            }
        }

        // =====================================
        // Button Handling
        // =====================================

        void OnCostumeButtonChanged(int channel, bool pressed)
        {
            if (!pressed) return;  // Only handle button presses, not releases

            switch (channel)
            {
                case 0:
                    OnButton1Pressed();
                    break;
                case 1:
                    OnButton2Pressed();
                    break;
                case 2:
                    OnButton3Pressed();
                    break;
                case 3:
                    OnButton4Pressed();
                    break;
            }

            // Send haptic confirmation
            TriggerHapticPulse(vestMotorChannel, 0.5f);
        }

        // =====================================
        // Game Logic (Customize for Your Game)
        // =====================================

        void OnButton1Pressed()
        {
            Debug.Log("🎭 Actor: Dialogue Option 1");
            // NOOP: TODO - Trigger dialogue tree branch 1
        }

        void OnButton2Pressed()
        {
            Debug.Log("🎭 Actor: Dialogue Option 2");
            // NOOP: TODO - Trigger dialogue tree branch 2
        }

        void OnButton3Pressed()
        {
            Debug.Log("⚠️ Actor: Emergency Stop Requested!");
            // NOOP: TODO - Pause game, call game master
        }

        void OnButton4Pressed()
        {
            Debug.Log("❓ Actor: Hint Requested");
            // NOOP: TODO - Provide hint to actor via HMD
        }

        // =====================================
        // Haptic Feedback
        // =====================================

        /// <summary>
        /// Send a brief haptic pulse to actor's costume
        /// </summary>
        public void TriggerHapticPulse(int motorChannel, float intensity)
        {
            if (device == null || !device.IsDeviceConnected())
                return;

            // Send intensity (0.0 to 1.0)
            device.SendFloat(motorChannel, Mathf.Clamp01(intensity));

            // Auto-stop after 200ms (ESP32 firmware handles timing)
            Debug.Log($"💥 Haptic pulse: Motor {motorChannel} @ {intensity * 100}%");
        }

        /// <summary>
        /// Send continuous vibration to motor
        /// </summary>
        public void SetContinuousVibration(int motorChannel, float intensity)
        {
            if (device == null || !device.IsDeviceConnected())
                return;

            device.SendFloat(motorChannel, Mathf.Clamp01(intensity));
        }

        /// <summary>
        /// Stop all motors
        /// </summary>
        public void StopAllMotors()
        {
            if (device == null || !device.IsDeviceConnected())
                return;

            for (int i = 0; i < device.config.outputChannelCount; i++)
            {
                device.SendFloat(i, 0f);
            }

            Debug.Log("🛑 All motors stopped");
        }

        // =====================================
        // Example Usage (Keyboard Testing)
        // =====================================

        void Update()
        {
            // Test haptic feedback with keyboard (for development)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Debug.Log("Test: Vest motor pulse");
                TriggerHapticPulse(vestMotorChannel, 0.8f);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Debug.Log("Test: Left glove pulse");
                TriggerHapticPulse(leftGloveChannel, 0.6f);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                Debug.Log("Test: Right glove pulse");
                TriggerHapticPulse(rightGloveChannel, 0.6f);
            }

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                Debug.Log("Test: Stop all motors");
                StopAllMotors();
            }
        }

        // =====================================
        // Cleanup
        // =====================================

        void OnDestroy()
        {
            if (device != null)
            {
                StopAllMotors();
                device.DisconnectDevice();
            }
        }
    }
}



