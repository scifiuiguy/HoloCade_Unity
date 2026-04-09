// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using HoloCade.Core;
using HoloCade.EmbeddedSystems;

namespace HoloCade.ExperienceTemplates
{
    /// <summary>
    /// Escape Room Experience Template
    /// 
    /// Pre-configured experience for escape room installations with embedded systems integration.
    /// Perfect for interactive escape rooms, puzzle experiences, and narrative-driven LBE.
    /// 
    /// Features:
    /// - Narrative state machine for story progression (Intro -> Puzzle1 -> Puzzle2 -> Finale)
    /// - Embedded systems integration for door locks, latches, and prop controls
    /// - Wireless communication examples for all microcontroller platforms
    /// - Example firmware sketches for unlocking mechanisms
    /// - Multi-device support for complex room setups
    /// 
    /// Embedded Systems Integration:
    /// This template demonstrates how to integrate microcontroller firmware for:
    /// - Door/latch unlocking via wireless commands
    /// - Sensor reading (pressure plates, motion sensors, etc.)
    /// - LED feedback and status indicators
    /// - Haptic feedback for props
    /// 
    /// See FirmwareExamples/ for example firmware sketches.
    /// </summary>
    [AddComponentMenu("HoloCade/Experience Templates/Escape Room Experience")]
    public class EscapeRoomExperience : HoloCadeExperienceBase
    {
        [Header("Escape Room Configuration")]
        [Tooltip("Number of doors/locks in this escape room")]
        [Range(1, 16)]
        public int numberOfDoors = 4;

        [Tooltip("Number of props with embedded systems")]
        [Range(0, 16)]
        public int numberOfProps = 2;

        [Header("Embedded Device Controllers")]
        [Tooltip("Embedded device controller for door locks and props")]
        public SerialDeviceController doorController;

        [Tooltip("Optional second embedded device for additional props")]
        public SerialDeviceController propController;

        [Header("Narrative State Mapping")]
        [Tooltip("Mapping of narrative states to door indices for automatic unlocking. " +
                 "When a state is reached, the corresponding door will be automatically unlocked. " +
                 "Leave empty to disable automatic unlocking, or override OnNarrativeStateChanged for custom logic.")]
        public Dictionary<string, int> stateToDoorMapping = new Dictionary<string, int>();

        [Header("Events")]
        [Tooltip("Event fired when a door unlock is confirmed by the embedded device")]
        public UnityEvent<int> onDoorUnlockConfirmed = new UnityEvent<int>();

        // Track door unlock states (cached from embedded devices)
        private bool[] doorUnlockStates;
        private float[] propSensorValues;

        protected override void Awake()
        {
            base.Awake();

            // Enable narrative state machine by default for escape rooms
            useNarrativeStateMachine = true;

            // Initialize arrays
            doorUnlockStates = new bool[numberOfDoors];
            propSensorValues = new float[numberOfProps];

            // Initialize all doors as locked
            for (int i = 0; i < doorUnlockStates.Length; i++)
            {
                doorUnlockStates[i] = false;
            }

            // Initialize all prop sensors to 0
            for (int i = 0; i < propSensorValues.Length; i++)
            {
                propSensorValues[i] = 0.0f;
            }

            // Initialize default state-to-door mapping (example mappings)
            // Users can modify these in Inspector or override in code
            if (stateToDoorMapping.Count == 0)
            {
                stateToDoorMapping.Add("Puzzle1", 0);
                stateToDoorMapping.Add("Puzzle2", 1);
                stateToDoorMapping.Add("Puzzle3", 2);
                stateToDoorMapping.Add("Finale", 3);
            }
        }

        protected override bool InitializeExperienceImpl()
        {
            // Note: InitializeExperienceImpl is abstract in base class, no base call needed

            Debug.Log("[HoloCade] EscapeRoomExperience: Initializing escape room experience...");

            // Initialize embedded devices
            InitializeEmbeddedDevices();

            // Initialize narrative state machine with default escape room states
            if (narrativeStateMachine != null && useNarrativeStateMachine)
            {
                List<ExperienceState> defaultStates = new List<ExperienceState>
                {
                    new ExperienceState("Intro", "Introduction and briefing"),
                    new ExperienceState("Puzzle1", "First puzzle challenge"),
                    new ExperienceState("Puzzle2", "Second puzzle challenge"),
                    new ExperienceState("Puzzle3", "Third puzzle challenge"),
                    new ExperienceState("Finale", "Final challenge and escape"),
                    new ExperienceState("Credits", "Completion and credits")
                };

                narrativeStateMachine.Initialize(defaultStates);
                Debug.Log($"[HoloCade] EscapeRoomExperience: Narrative state machine initialized with {defaultStates.Count} states");
            }

            Debug.Log("[HoloCade] EscapeRoomExperience: Initialization complete");
            return true;
        }

        protected override void ShutdownExperienceImpl()
        {
            // Disconnect embedded devices
            if (doorController != null)
            {
                doorController.DisconnectDevice();
            }

            if (propController != null)
            {
                propController.DisconnectDevice();
            }

            // Note: ShutdownExperienceImpl is abstract in base class, no base call needed
        }

        /// <summary>
        /// Initialize embedded device controllers for doors and props
        /// </summary>
        private void InitializeEmbeddedDevices()
        {
            // Create door controller if not already assigned
            if (doorController == null)
            {
                doorController = GetComponent<SerialDeviceController>();
                if (doorController == null)
                {
                    doorController = gameObject.AddComponent<SerialDeviceController>();
                }
            }

            if (doorController != null)
            {
                // Configure for door locks (example: ESP32 over WiFi)
                EmbeddedDeviceConfig doorConfig = new EmbeddedDeviceConfig
                {
                    deviceType = MicrocontrollerType.ESP32,
                    protocol = CommProtocol.WiFi,
                    deviceAddress = "192.168.1.50",  // Change to your door controller IP
                    port = 8888,
                    inputChannelCount = numberOfDoors,  // One input per door (lock state)
                    outputChannelCount = numberOfDoors, // One output per door (unlock command)
                    debugMode = false  // Use binary mode in production
                };

                // Bind to door state change events
                doorController.onBoolReceived.AddListener(OnDoorStateChanged);

                if (doorController.InitializeDevice(doorConfig))
                {
                    Debug.Log($"[HoloCade] EscapeRoomExperience: Door controller initialized at {doorConfig.deviceAddress}:{doorConfig.port}");
                }
                else
                {
                    Debug.LogWarning("[HoloCade] EscapeRoomExperience: Failed to initialize door controller");
                }
            }

            // Create prop controller if needed
            if (numberOfProps > 0 && propController == null)
            {
                propController = gameObject.AddComponent<SerialDeviceController>();
            }

            if (propController != null && numberOfProps > 0)
            {
                // Configure for props (example: ESP32 over WiFi)
                EmbeddedDeviceConfig propConfig = new EmbeddedDeviceConfig
                {
                    deviceType = MicrocontrollerType.ESP32,
                    protocol = CommProtocol.WiFi,
                    deviceAddress = "192.168.1.51",  // Change to your prop controller IP
                    port = 8888,
                    inputChannelCount = numberOfProps,  // One input per prop (sensor)
                    outputChannelCount = numberOfProps, // One output per prop (actuator)
                    debugMode = false
                };

                // Bind to prop sensor events
                propController.onFloatReceived.AddListener(OnPropSensorValue);

                if (propController.InitializeDevice(propConfig))
                {
                    Debug.Log($"[HoloCade] EscapeRoomExperience: Prop controller initialized at {propConfig.deviceAddress}:{propConfig.port}");
                }
                else
                {
                    Debug.LogWarning("[HoloCade] EscapeRoomExperience: Failed to initialize prop controller");
                }
            }
        }

        /// <summary>
        /// Unlock a door by index
        /// Sends unlock command to embedded device via wireless communication
        /// </summary>
        /// <param name="doorIndex">Index of door to unlock (0-based)</param>
        /// <returns>true if command was sent successfully</returns>
        public bool UnlockDoor(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= numberOfDoors)
            {
                Debug.LogWarning($"[HoloCade] EscapeRoomExperience: Invalid door index {doorIndex}");
                return false;
            }

            if (doorController == null || !doorController.IsDeviceConnected())
            {
                Debug.LogWarning("[HoloCade] EscapeRoomExperience: Door controller not connected");
                return false;
            }

            // Send unlock command (bool true = unlock) to DoorLock_Example.ino firmware
            // Note: This sends the command to the firmware. For unlock confirmation callback,
            // subscribe to onDoorUnlockConfirmed event. The firmware will send back confirmation
            // when the door actually unlocks, which triggers OnDoorStateChanged and fires onDoorUnlockConfirmed.
            doorController.SendBool(doorIndex, true);

            Debug.Log($"[HoloCade] EscapeRoomExperience: Unlock command sent to door {doorIndex}");
            return true;
        }

        /// <summary>
        /// Lock a door by index
        /// Sends lock command to embedded device via wireless communication
        /// </summary>
        /// <param name="doorIndex">Index of door to lock (0-based)</param>
        /// <returns>true if command was sent successfully</returns>
        public bool LockDoor(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= numberOfDoors)
            {
                Debug.LogWarning($"[HoloCade] EscapeRoomExperience: Invalid door index {doorIndex}");
                return false;
            }

            if (doorController == null || !doorController.IsDeviceConnected())
            {
                Debug.LogWarning("[HoloCade] EscapeRoomExperience: Door controller not connected");
                return false;
            }

            // Send lock command (bool false = lock)
            doorController.SendBool(doorIndex, false);

            Debug.Log($"[HoloCade] EscapeRoomExperience: Lock command sent to door {doorIndex}");
            return true;
        }

        /// <summary>
        /// Check if a door is unlocked
        /// Reads state from embedded device
        /// </summary>
        /// <param name="doorIndex">Index of door to check (0-based)</param>
        /// <returns>true if door is unlocked</returns>
        public bool IsDoorUnlocked(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= doorUnlockStates.Length)
            {
                return false;
            }

            return doorUnlockStates[doorIndex];
        }

        /// <summary>
        /// Trigger a prop action (e.g., open drawer, activate mechanism)
        /// Sends command to prop controller via wireless communication
        /// </summary>
        /// <param name="propIndex">Index of prop to activate (0-based)</param>
        /// <param name="actionValue">Action value (0.0-1.0 for intensity, or specific command)</param>
        /// <returns>true if command was sent successfully</returns>
        public bool TriggerPropAction(int propIndex, float actionValue = 1.0f)
        {
            if (propIndex < 0 || propIndex >= numberOfProps)
            {
                Debug.LogWarning($"[HoloCade] EscapeRoomExperience: Invalid prop index {propIndex}");
                return false;
            }

            if (propController == null || !propController.IsDeviceConnected())
            {
                Debug.LogWarning("[HoloCade] EscapeRoomExperience: Prop controller not connected");
                return false;
            }

            // Send action command (float 0.0-1.0 for intensity/position)
            propController.SendFloat(propIndex, Mathf.Clamp01(actionValue));

            Debug.Log($"[HoloCade] EscapeRoomExperience: Prop action triggered on prop {propIndex} (value: {actionValue:F2})");
            return true;
        }

        /// <summary>
        /// Read sensor value from a prop
        /// Reads analog/digital sensor state from embedded device
        /// </summary>
        /// <param name="propIndex">Index of prop sensor to read (0-based)</param>
        /// <returns>Sensor value (0.0-1.0 normalized, or raw value depending on sensor type)</returns>
        public float ReadPropSensor(int propIndex)
        {
            if (propIndex < 0 || propIndex >= propSensorValues.Length)
            {
                return 0.0f;
            }

            return propSensorValues[propIndex];
        }

        /// <summary>
        /// Get the current narrative state (from state machine)
        /// Useful for triggering door unlocks based on puzzle completion
        /// </summary>
        public string GetCurrentPuzzleState()
        {
            return GetCurrentNarrativeState();
        }

        /// <summary>
        /// Override to handle narrative state changes and unlock doors based on puzzle completion
        /// </summary>
        protected override void OnNarrativeStateChanged(string oldState, string newState, int newStateIndex)
        {
            base.OnNarrativeStateChanged(oldState, newState, newStateIndex);

            Debug.Log($"[HoloCade] EscapeRoomExperience: Narrative state changed from {oldState} to {newState}");

            // Check if this state maps to a door
            int doorIndex = GetDoorIndexForState(newState);
            if (doorIndex >= 0 && doorIndex < numberOfDoors)
            {
                UnlockDoor(doorIndex);
                Debug.Log($"[HoloCade] EscapeRoomExperience: Automatically unlocked door {doorIndex} for state {newState}");
            }
        }

        /// <summary>
        /// Get the door index mapped to a narrative state
        /// </summary>
        /// <param name="stateName">Name of the narrative state</param>
        /// <returns>Door index if mapped, or -1 if no mapping exists</returns>
        private int GetDoorIndexForState(string stateName)
        {
            if (stateToDoorMapping.TryGetValue(stateName, out int doorIndex))
            {
                return doorIndex;
            }
            return -1; // No mapping found
        }

        /// <summary>
        /// Handle door unlock events from embedded devices
        /// </summary>
        private void OnDoorStateChanged(int channel, bool isUnlocked)
        {
            if (channel >= 0 && channel < doorUnlockStates.Length)
            {
                bool wasUnlocked = doorUnlockStates[channel];
                doorUnlockStates[channel] = isUnlocked;

                Debug.Log($"[HoloCade] EscapeRoomExperience: Door {channel} state changed to {(isUnlocked ? "UNLOCKED" : "LOCKED")}");

                // Fire callback when door transitions to unlocked state
                // This provides confirmation that the unlock command was received and executed by the firmware
                if (isUnlocked && !wasUnlocked)
                {
                    onDoorUnlockConfirmed?.Invoke(channel);
                    Debug.Log($"[HoloCade] EscapeRoomExperience: Door {channel} unlock confirmed by firmware");
                }
            }
        }

        /// <summary>
        /// Handle prop sensor readings from embedded devices
        /// </summary>
        private void OnPropSensorValue(int channel, float value)
        {
            if (channel >= 0 && channel < propSensorValues.Length)
            {
                propSensorValues[channel] = value;
                Debug.Log($"[HoloCade] EscapeRoomExperience: Prop {channel} sensor value: {value:F3}");
            }
        }
    }
}

