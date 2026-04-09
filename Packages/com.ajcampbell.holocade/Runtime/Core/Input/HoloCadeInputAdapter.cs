// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using Unity.Netcode;
using UnityEngine;

namespace HoloCade.Core.Input
{
    /// <summary>
    /// Universal input adapter for HoloCade experiences.
    /// Handles all input sources (embedded systems, VR, keyboard, gamepad, AI) with automatic
    /// authority checks, replication, and RPC routing.
    /// 
    /// Works with:
    /// - Dedicated servers and listen servers
    /// - All input sources (ESP32, VR, keyboard, gamepad, AI)
    /// - All experience templates
    /// </summary>
    public class HoloCadeInputAdapter : NetworkBehaviour
    {
        // ========================================
        // CONFIGURATION
        // ========================================

        [Header("Input Sources")]
        [Tooltip("Embedded device controller to read input from (e.g., ESP32 wrist buttons)")]
        public EmbeddedSystems.SerialDeviceController embeddedDeviceController;

        [Tooltip("Enable processing input from the assigned embedded device controller")]
        public bool enableEmbeddedSystemInput = true;

        [Tooltip("Enable processing input from VR controllers (requires override in subclass)")]
        public bool enableVRControllerInput = false;

        [Header("Input Channels")]
        [Tooltip("Number of digital input channels (buttons) to monitor")]
        [Range(1, 32)]
        public int buttonCount = 4;

        [Tooltip("Number of analog input channels (axes) to monitor")]
        [Range(0, 32)]
        public int axisCount = 0;

        // ========================================
        // EVENTS (C# event Action)
        // ========================================

        /// <summary>
        /// Called when a digital button is pressed (fires on all machines after replication).
        /// Subscribe: inputAdapter.OnButtonPressed += YourMethod;
        /// </summary>
        public event Action<int> OnButtonPressed;

        /// <summary>
        /// Called when a digital button is released (fires on all machines after replication).
        /// Subscribe: inputAdapter.OnButtonReleased += YourMethod;
        /// </summary>
        public event Action<int> OnButtonReleased;

        /// <summary>
        /// Called when an analog axis value changes (fires on all machines after replication).
        /// Subscribe: inputAdapter.OnAxisChanged += YourMethod;
        /// </summary>
        public event Action<int, float> OnAxisChanged;

        // ========================================
        // REPLICATED STATE
        // ========================================

        private NetworkVariable<int> replicatedButtonStates = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<NetworkListWrapper> replicatedAxisValues = new NetworkVariable<NetworkListWrapper>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // ========================================
        // INTERNAL STATE
        // ========================================

        private int previousButtonStates = 0;
        private float[] previousAxisValues;
        private float[] currentAxisValues;

        // ========================================
        // LIFECYCLE
        // ========================================

        private void Awake()
        {
            previousAxisValues = new float[32];
            currentAxisValues = new float[32];
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Subscribe to network variable changes
            replicatedButtonStates.OnValueChanged += OnButtonStatesChanged;
            replicatedAxisValues.OnValueChanged += OnAxisValuesChanged;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // Unsubscribe from network variable changes
            replicatedButtonStates.OnValueChanged -= OnButtonStatesChanged;
            replicatedAxisValues.OnValueChanged -= OnAxisValuesChanged;
        }

        private void Update()
        {
            // Only process input on server/host
            if (!IsServer) return;

            if (enableEmbeddedSystemInput && embeddedDeviceController != null)
            {
                ProcessEmbeddedSystemInput();
            }

            if (enableVRControllerInput)
            {
                ProcessVRControllerInput();
            }
        }

        // ========================================
        // PUBLIC API (Input Injection)
        // ========================================

        /// <summary>
        /// Injects a digital button press event into the system.
        /// Automatically handles authority checks and RPCs if called on a client.
        /// </summary>
        /// <param name="buttonIndex">The index of the button (0 to buttonCount-1)</param>
        public void InjectButtonPress(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= buttonCount)
            {
                Debug.LogWarning($"[HoloCadeInputAdapter] Invalid button index {buttonIndex} (must be 0-{buttonCount - 1})");
                return;
            }

            if (IsServer)
            {
                UpdateButtonState(buttonIndex, true);
            }
            else
            {
                InjectButtonPressServerRpc(buttonIndex);
            }
        }

        /// <summary>
        /// Injects a digital button release event into the system.
        /// Automatically handles authority checks and RPCs if called on a client.
        /// </summary>
        /// <param name="buttonIndex">The index of the button (0 to buttonCount-1)</param>
        public void InjectButtonRelease(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= buttonCount)
            {
                Debug.LogWarning($"[HoloCadeInputAdapter] Invalid button index {buttonIndex} (must be 0-{buttonCount - 1})");
                return;
            }

            if (IsServer)
            {
                UpdateButtonState(buttonIndex, false);
            }
            else
            {
                InjectButtonReleaseServerRpc(buttonIndex);
            }
        }

        /// <summary>
        /// Injects an analog axis value into the system.
        /// Automatically handles authority checks and RPCs if called on a client.
        /// </summary>
        /// <param name="axisIndex">The index of the axis (0 to axisCount-1)</param>
        /// <param name="value">The value of the axis (typically -1.0 to 1.0)</param>
        public void InjectAxisValue(int axisIndex, float value)
        {
            if (axisIndex < 0 || axisIndex >= axisCount)
            {
                Debug.LogWarning($"[HoloCadeInputAdapter] Invalid axis index {axisIndex} (must be 0-{axisCount - 1})");
                return;
            }

            if (IsServer)
            {
                UpdateAxisValue(axisIndex, value);
            }
            else
            {
                InjectAxisValueServerRpc(axisIndex, value);
            }
        }

        /// <summary>
        /// Queries the current state of a button.
        /// </summary>
        /// <param name="buttonIndex">The index of the button</param>
        /// <returns>True if the button is currently pressed</returns>
        public bool IsButtonPressed(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= buttonCount) return false;
            return (replicatedButtonStates.Value & (1 << buttonIndex)) != 0;
        }

        /// <summary>
        /// Queries the current value of an axis.
        /// </summary>
        /// <param name="axisIndex">The index of the axis</param>
        /// <returns>The current axis value</returns>
        public float GetAxisValue(int axisIndex)
        {
            if (axisIndex < 0 || axisIndex >= axisCount) return 0f;
            return currentAxisValues[axisIndex];
        }

        // ========================================
        // RPC FUNCTIONS (Client → Server)
        // ========================================

        [ServerRpc(RequireOwnership = false)]
        private void InjectButtonPressServerRpc(int buttonIndex)
        {
            UpdateButtonState(buttonIndex, true);
        }

        [ServerRpc(RequireOwnership = false)]
        private void InjectButtonReleaseServerRpc(int buttonIndex)
        {
            UpdateButtonState(buttonIndex, false);
        }

        [ServerRpc(RequireOwnership = false)]
        private void InjectAxisValueServerRpc(int axisIndex, float value)
        {
            UpdateAxisValue(axisIndex, value);
        }

        // ========================================
        // NETWORK VARIABLE CALLBACKS (Server → Client)
        // ========================================

        private void OnButtonStatesChanged(int previousValue, int newValue)
        {
            // Fire events for changed buttons (edge detection)
            for (int i = 0; i < buttonCount; i++)
            {
                bool wasPrevPressed = (previousValue & (1 << i)) != 0;
                bool isNowPressed = (newValue & (1 << i)) != 0;

                if (isNowPressed && !wasPrevPressed)
                {
                    OnButtonPressed?.Invoke(i);
                }
                else if (!isNowPressed && wasPrevPressed)
                {
                    OnButtonReleased?.Invoke(i);
                }
            }
        }

        private void OnAxisValuesChanged(NetworkListWrapper previousValue, NetworkListWrapper newValue)
        {
            // Fire events for changed axes
            for (int i = 0; i < axisCount; i++)
            {
                float oldValue = i < previousValue.values.Length ? previousValue.values[i] : 0f;
                float newVal = i < newValue.values.Length ? newValue.values[i] : 0f;

                if (Mathf.Abs(newVal - oldValue) > 0.001f)
                {
                    currentAxisValues[i] = newVal;
                    OnAxisChanged?.Invoke(i, newVal);
                }
            }
        }

        // ========================================
        // INTERNAL INPUT PROCESSING (Authority Only)
        // ========================================

        private void ProcessEmbeddedSystemInput()
        {
            if (!embeddedDeviceController.IsDeviceConnected()) return;

            // Read digital inputs (buttons)
            for (int i = 0; i < buttonCount; i++)
            {
                bool currentState = embeddedDeviceController.GetDigitalInput(i);
                bool previousState = (previousButtonStates & (1 << i)) != 0;

                if (currentState != previousState)
                {
                    UpdateButtonState(i, currentState);
                }
            }

            // Read analog inputs (axes)
            for (int i = 0; i < axisCount; i++)
            {
                float currentValue = embeddedDeviceController.GetAnalogInput(i);
                if (Mathf.Abs(currentValue - previousAxisValues[i]) > 0.001f)
                {
                    UpdateAxisValue(i, currentValue);
                }
            }
        }

        /// <summary>
        /// Override this method in a subclass or via composition to add VR controller input support.
        /// Only called on server/host authority.
        /// </summary>
        protected virtual void ProcessVRControllerInput()
        {
            // Default: no VR controller input
            // Override in subclass or use composition to add VR support
        }

        // ========================================
        // INTERNAL STATE UPDATES (Authority Only)
        // ========================================

        private void UpdateButtonState(int buttonIndex, bool pressed)
        {
            if (!IsServer) return;

            int currentStates = replicatedButtonStates.Value;
            int newStates;

            if (pressed)
            {
                newStates = currentStates | (1 << buttonIndex);
            }
            else
            {
                newStates = currentStates & ~(1 << buttonIndex);
            }

            if (newStates != currentStates)
            {
                replicatedButtonStates.Value = newStates;
                previousButtonStates = newStates;

                // Fire event on server
                if (pressed)
                {
                    OnButtonPressed?.Invoke(buttonIndex);
                }
                else
                {
                    OnButtonReleased?.Invoke(buttonIndex);
                }
            }
        }

        private void UpdateAxisValue(int axisIndex, float value)
        {
            if (!IsServer) return;

            // Update local cache
            currentAxisValues[axisIndex] = value;
            previousAxisValues[axisIndex] = value;

            // Update replicated state
            NetworkListWrapper wrapper = replicatedAxisValues.Value;
            if (wrapper.values == null || wrapper.values.Length != axisCount)
            {
                wrapper.values = new float[axisCount];
            }
            wrapper.values[axisIndex] = value;
            replicatedAxisValues.Value = wrapper;

            // Fire event on server
            OnAxisChanged?.Invoke(axisIndex, value);
        }
    }

    // ========================================
    // NETWORK LIST WRAPPER (for NetworkVariable)
    // ========================================

    /// <summary>
    /// Wrapper struct for float array to be used with NetworkVariable.
    /// Unity's NetworkVariable doesn't support arrays directly, so we wrap them.
    /// </summary>
    [System.Serializable]
    public struct NetworkListWrapper : INetworkSerializable
    {
        public float[] values;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                var reader = serializer.GetFastBufferReader();
                int length;
                reader.ReadValueSafe(out length);
                values = new float[length];
                for (int i = 0; i < length; i++)
                {
                    reader.ReadValueSafe(out values[i]);
                }
            }
            else
            {
                var writer = serializer.GetFastBufferWriter();
                int length = values?.Length ?? 0;
                writer.WriteValueSafe(length);
                for (int i = 0; i < length; i++)
                {
                    writer.WriteValueSafe(values[i]);
                }
            }
        }
    }
}



