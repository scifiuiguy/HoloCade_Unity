// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using HoloCade.EmbeddedSystems;

namespace HoloCade.RF433MHz
{
    /// <summary>
    /// UnityEvent for button pressed
    /// </summary>
    [Serializable]
    public class RF433MHzButtonPressedEvent : UnityEvent<int> { }

    /// <summary>
    /// UnityEvent for button released
    /// </summary>
    [Serializable]
    public class RF433MHzButtonReleasedEvent : UnityEvent<int> { }

    /// <summary>
    /// UnityEvent for button event (pressed or released)
    /// </summary>
    [Serializable]
    public class RF433MHzButtonEventEvent : UnityEvent<int, bool> { }

    /// <summary>
    /// UnityEvent for code learned
    /// </summary>
    [Serializable]
    public class RF433MHzCodeLearnedEvent : UnityEvent<int, uint> { }

    /// <summary>
    /// UnityEvent for button function triggered
    /// </summary>
    [Serializable]
    public class RF433MHzButtonFunctionTriggeredEvent : UnityEvent<int, string, bool> { }

    /// <summary>
    /// HoloCade RF433MHz Receiver Component
    /// 
    /// Hardware-agnostic 433MHz wireless remote/receiver integration.
    /// Provides abstraction layer for different USB receiver modules (RTL-SDR, CC1101, RFM69, Generic)
    /// with rolling code validation and replay attack prevention.
    /// 
    /// Usage:
    /// 1. Add component to GameObject
    /// 2. Configure receiver type and USB device path
    /// 3. Subscribe to button event delegates
    /// 4. Handle button events in your experience logic
    /// </summary>
    public class RF433MHzReceiver : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private RF433MHzReceiverConfig config = new RF433MHzReceiverConfig();

        [Header("Events")]
        [Tooltip("Called when a button is pressed")]
        public RF433MHzButtonPressedEvent OnButtonPressed = new RF433MHzButtonPressedEvent();

        [Tooltip("Called when a button is released")]
        public RF433MHzButtonReleasedEvent OnButtonReleased = new RF433MHzButtonReleasedEvent();

        [Tooltip("Called when any button event occurs (pressed or released)")]
        public RF433MHzButtonEventEvent OnButtonEvent = new RF433MHzButtonEventEvent();

        [Tooltip("Called when a new remote code is learned during learning mode")]
        public RF433MHzCodeLearnedEvent OnCodeLearned = new RF433MHzCodeLearnedEvent();

        [Tooltip("Called when a mapped button function is triggered (only fires if button has assigned function)")]
        public RF433MHzButtonFunctionTriggeredEvent OnButtonFunctionTriggered = new RF433MHzButtonFunctionTriggeredEvent();

        private I433MHzReceiver receiverImpl;
        private Dictionary<int, bool> lastButtonStates = new Dictionary<int, bool>();
        private Dictionary<int, RF433MHzLearnedButton> learnedButtons = new Dictionary<int, RF433MHzLearnedButton>();
        private Dictionary<int, string> buttonFunctionMappings = new Dictionary<int, string>();
        private bool autoSaveEnabled = true;
        private string customSaveFilePath = "";

        void Start()
        {
            // Load saved button mappings on startup
            LoadButtonMappings();

            if (!string.IsNullOrEmpty(config.USBDevicePath))
            {
                InitializeReceiver(config);
            }
        }

        void OnDestroy()
        {
            ShutdownReceiver();
        }

        void Update()
        {
            if (receiverImpl == null || !receiverImpl.IsConnected())
            {
                return;
            }

            // Poll for button events
            List<RF433MHzButtonEvent> events = new List<RF433MHzButtonEvent>();
            if (receiverImpl.GetButtonEvents(events))
            {
                ProcessButtonEvents(events);
            }
        }

        /// <summary>
        /// Initialize receiver with configuration
        /// </summary>
        public bool InitializeReceiver(RF433MHzReceiverConfig inConfig)
        {
            config = inConfig;

            if (receiverImpl != null && receiverImpl.IsConnected())
            {
                Debug.LogWarning("[HoloCade] RF433MHzReceiver: Already initialized");
                return false;
            }

            // Create receiver using factory
            receiverImpl = CreateReceiver(config);

            if (receiverImpl == null || !receiverImpl.Initialize(config))
            {
                Debug.LogError($"[HoloCade] RF433MHzReceiver: Failed to initialize receiver (Type: {config.ReceiverType}, Device: {config.USBDevicePath})");
                receiverImpl = null;
                return false;
            }

            Debug.Log($"[HoloCade] RF433MHzReceiver: Initialized (Type: {config.ReceiverType}, Device: {config.USBDevicePath})");
            return true;
        }

        /// <summary>
        /// Shutdown receiver and close USB connection
        /// </summary>
        public void ShutdownReceiver()
        {
            if (receiverImpl != null)
            {
                receiverImpl.Shutdown();
                receiverImpl = null;
                Debug.Log("[HoloCade] RF433MHzReceiver: Shutdown");
            }
        }

        /// <summary>
        /// Check if receiver is connected
        /// </summary>
        public bool IsConnected() => receiverImpl != null && receiverImpl.IsConnected();

        /// <summary>
        /// Get button events (polling method - delegates are preferred)
        /// </summary>
        public bool GetButtonEvents(List<RF433MHzButtonEvent> outEvents)
        {
            if (receiverImpl == null || !receiverImpl.IsConnected())
            {
                return false;
            }

            return receiverImpl.GetButtonEvents(outEvents);
        }

        /// <summary>
        /// Check if rolling code validation is enabled and valid
        /// </summary>
        public bool IsRollingCodeValid() => receiverImpl != null && receiverImpl.IsRollingCodeValid();

        /// <summary>
        /// Get rolling code drift
        /// </summary>
        public int GetRollingCodeDrift() => receiverImpl != null ? receiverImpl.GetRollingCodeDrift() : 0;

        /// <summary>
        /// Enable code learning mode (for pairing new remotes)
        /// </summary>
        public void EnableLearningMode(float timeoutSeconds = 30.0f)
        {
            receiverImpl?.EnableLearningMode(timeoutSeconds);
        }

        /// <summary>
        /// Disable code learning mode
        /// </summary>
        public void DisableLearningMode()
        {
            receiverImpl?.DisableLearningMode();
        }

        /// <summary>
        /// Check if learning mode is active
        /// </summary>
        public bool IsLearningModeActive() => receiverImpl != null && receiverImpl.IsLearningModeActive();

        // ========================================
        // BUTTON MAPPING & LEARNING API
        // ========================================

        /// <summary>
        /// Get all learned buttons
        /// </summary>
        public int GetLearnedButtons(List<RF433MHzLearnedButton> outLearnedButtons)
        {
            outLearnedButtons.Clear();
            outLearnedButtons.AddRange(learnedButtons.Values);
            return outLearnedButtons.Count;
        }

        /// <summary>
        /// Get number of learned buttons
        /// </summary>
        public int GetLearnedButtonCount() => learnedButtons.Count;

        /// <summary>
        /// Check if a button code has been learned
        /// </summary>
        public bool IsButtonLearned(int buttonCode) => learnedButtons.ContainsKey(buttonCode);

        /// <summary>
        /// Assign a function name to a button code
        /// </summary>
        public bool AssignButtonFunction(int buttonCode, string functionName)
        {
            if (!IsButtonLearned(buttonCode))
            {
                Debug.LogWarning($"[HoloCade] RF433MHzReceiver: Cannot assign function to unlearned button (Code: {buttonCode})");
                return false;
            }

            if (string.IsNullOrEmpty(functionName))
            {
                Debug.LogWarning("[HoloCade] RF433MHzReceiver: Function name cannot be empty");
                return false;
            }

            buttonFunctionMappings[buttonCode] = functionName;

            // Update learned button info
            if (learnedButtons.TryGetValue(buttonCode, out RF433MHzLearnedButton learnedButton))
            {
                learnedButton.AssignedFunctionName = functionName;
                learnedButton.IsMapped = true;
            }

            Debug.Log($"[HoloCade] RF433MHzReceiver: Assigned function '{functionName}' to button {buttonCode}");

            // Auto-save if enabled
            AutoSaveIfEnabled();

            return true;
        }

        /// <summary>
        /// Unassign a function from a button code
        /// </summary>
        public bool UnassignButtonFunction(int buttonCode)
        {
            if (!buttonFunctionMappings.ContainsKey(buttonCode))
            {
                return false;
            }

            buttonFunctionMappings.Remove(buttonCode);

            // Update learned button info
            if (learnedButtons.TryGetValue(buttonCode, out RF433MHzLearnedButton learnedButton))
            {
                learnedButton.AssignedFunctionName = "";
                learnedButton.IsMapped = false;
            }

            Debug.Log($"[HoloCade] RF433MHzReceiver: Unassigned function from button {buttonCode}");

            // Auto-save if enabled
            AutoSaveIfEnabled();

            return true;
        }

        /// <summary>
        /// Get function name assigned to a button code
        /// </summary>
        public bool GetButtonFunction(int buttonCode, out string outFunctionName)
        {
            if (buttonFunctionMappings.TryGetValue(buttonCode, out outFunctionName) && !string.IsNullOrEmpty(outFunctionName))
            {
                return true;
            }

            outFunctionName = "";
            return false;
        }

        /// <summary>
        /// Get all button mappings
        /// </summary>
        public int GetButtonMappings(List<RF433MHzButtonMapping> outMappings)
        {
            outMappings.Clear();

            foreach (var pair in buttonFunctionMappings)
            {
                RF433MHzButtonMapping mapping = new RF433MHzButtonMapping
                {
                    ButtonCode = pair.Key,
                    FunctionName = pair.Value,
                    IsActive = true
                };
                outMappings.Add(mapping);
            }

            return outMappings.Count;
        }

        /// <summary>
        /// Clear all learned buttons and mappings
        /// </summary>
        public void ClearAllButtons()
        {
            learnedButtons.Clear();
            buttonFunctionMappings.Clear();
            lastButtonStates.Clear();
            Debug.Log("[HoloCade] RF433MHzReceiver: Cleared all learned buttons and mappings");

            // Auto-save if enabled (saves empty state)
            AutoSaveIfEnabled();
        }

        /// <summary>
        /// Remove a specific learned button
        /// </summary>
        public bool RemoveLearnedButton(int buttonCode)
        {
            if (!IsButtonLearned(buttonCode))
            {
                return false;
            }

            learnedButtons.Remove(buttonCode);
            buttonFunctionMappings.Remove(buttonCode);
            lastButtonStates.Remove(buttonCode);

            Debug.Log($"[HoloCade] RF433MHzReceiver: Removed learned button {buttonCode}");

            // Auto-save if enabled
            AutoSaveIfEnabled();

            return true;
        }

        // ========================================
        // PERSISTENCE (Save/Load to JSON)
        // ========================================

        /// <summary>
        /// Save learned buttons and mappings to JSON file
        /// </summary>
        public bool SaveButtonMappings(string customFilePath = "")
        {
            string filePath = string.IsNullOrEmpty(customFilePath) ? GetDefaultButtonMappingsFilePath() : customFilePath;

            try
            {
                // Create JSON object
                var jsonObject = new Dictionary<string, object>
                {
                    ["LastSaved"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["Version"] = 1
                };

                // Store learned buttons array
                var learnedButtonsArray = new List<Dictionary<string, object>>();
                foreach (var pair in learnedButtons)
                {
                    var learnedButton = pair.Value;
                    learnedButtonsArray.Add(new Dictionary<string, object>
                    {
                        ["ButtonCode"] = learnedButton.ButtonCode,
                        ["RollingCodeSeed"] = learnedButton.RollingCodeSeed,
                        ["LearnedTimestamp"] = learnedButton.LearnedTimestamp,
                        ["AssignedFunctionName"] = learnedButton.AssignedFunctionName,
                        ["bIsMapped"] = learnedButton.IsMapped
                    });
                }
                jsonObject["LearnedButtons"] = learnedButtonsArray;

                // Store button mappings array
                var mappingsArray = new List<Dictionary<string, object>>();
                foreach (var pair in buttonFunctionMappings)
                {
                    mappingsArray.Add(new Dictionary<string, object>
                    {
                        ["ButtonCode"] = pair.Key,
                        ["FunctionName"] = pair.Value
                    });
                }
                jsonObject["ButtonMappings"] = mappingsArray;

                // Serialize to JSON string using MiniJSON
                string jsonString = MiniJSON.Json.Serialize(jsonObject);

                // Write to file
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, jsonString);

                Debug.Log($"[HoloCade] RF433MHzReceiver: Saved {learnedButtons.Count} learned buttons and {buttonFunctionMappings.Count} mappings to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCade] RF433MHzReceiver: Failed to save button mappings to {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load learned buttons and mappings from JSON file
        /// </summary>
        public bool LoadButtonMappings(string customFilePath = "")
        {
            string filePath = string.IsNullOrEmpty(customFilePath) ? GetDefaultButtonMappingsFilePath() : customFilePath;

            // Check if file exists
            if (!File.Exists(filePath))
            {
                Debug.Log($"[HoloCade] RF433MHzReceiver: Button mappings file not found at {filePath} (will create on first save)");
                return false;
            }

            try
            {
                // Read file
                string fileContents = File.ReadAllText(filePath);

                // Parse JSON using MiniJSON
                var jsonObject = MiniJSON.Json.Deserialize(fileContents) as Dictionary<string, object>;
                if (jsonObject == null)
                {
                    Debug.LogError($"[HoloCade] RF433MHzReceiver: Failed to parse button mappings JSON from {filePath}");
                    return false;
                }

                // Clear existing data
                learnedButtons.Clear();
                buttonFunctionMappings.Clear();

                // Load learned buttons
                if (jsonObject.TryGetValue("LearnedButtons", out object learnedButtonsObj) && learnedButtonsObj is List<object> learnedButtonsList)
                {
                    foreach (var buttonObj in learnedButtonsList)
                    {
                        if (buttonObj is Dictionary<string, object> buttonDict)
                        {
                            RF433MHzLearnedButton learnedButton = new RF433MHzLearnedButton
                            {
                                ButtonCode = Convert.ToInt32(buttonDict["ButtonCode"]),
                                RollingCodeSeed = Convert.ToUInt32(buttonDict["RollingCodeSeed"]),
                                LearnedTimestamp = Convert.ToSingle(buttonDict["LearnedTimestamp"]),
                                AssignedFunctionName = buttonDict["AssignedFunctionName"]?.ToString() ?? "",
                                IsMapped = Convert.ToBoolean(buttonDict["bIsMapped"])
                            };
                            learnedButtons[learnedButton.ButtonCode] = learnedButton;
                        }
                    }
                }

                // Load button mappings
                if (jsonObject.TryGetValue("ButtonMappings", out object mappingsObj) && mappingsObj is List<object> mappingsList)
                {
                    foreach (var mappingObj in mappingsList)
                    {
                        if (mappingObj is Dictionary<string, object> mappingDict)
                        {
                            int buttonCode = Convert.ToInt32(mappingDict["ButtonCode"]);
                            string functionName = mappingDict["FunctionName"]?.ToString() ?? "";
                            buttonFunctionMappings[buttonCode] = functionName;

                            // Update learned button mapping status
                            if (learnedButtons.TryGetValue(buttonCode, out RF433MHzLearnedButton learnedButton))
                            {
                                learnedButton.AssignedFunctionName = functionName;
                                learnedButton.IsMapped = true;
                            }
                        }
                    }
                }

                string lastSaved = jsonObject.TryGetValue("LastSaved", out object lastSavedObj) ? lastSavedObj?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(lastSaved))
                {
                    Debug.Log($"[HoloCade] RF433MHzReceiver: Loaded {learnedButtons.Count} learned buttons and {buttonFunctionMappings.Count} mappings from {filePath} (saved: {lastSaved})");
                }
                else
                {
                    Debug.Log($"[HoloCade] RF433MHzReceiver: Loaded {learnedButtons.Count} learned buttons and {buttonFunctionMappings.Count} mappings from {filePath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HoloCade] RF433MHzReceiver: Failed to load button mappings from {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get default file path for button mappings
        /// </summary>
        public string GetDefaultButtonMappingsFilePath()
        {
            // Use default path: Application.persistentDataPath/Config/HoloCade/RF433MHz_Buttons.json
            string configDir = Path.Combine(Application.persistentDataPath, "Config", "HoloCade");

            // Create directory if it doesn't exist
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            return Path.Combine(configDir, "RF433MHz_Buttons.json");
        }

        /// <summary>
        /// Enable/disable auto-save (saves automatically when buttons are learned or mappings change)
        /// </summary>
        public void SetAutoSave(bool enable)
        {
            autoSaveEnabled = enable;
            Debug.Log($"[HoloCade] RF433MHzReceiver: Auto-save {(enable ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Check if auto-save is enabled
        /// </summary>
        public bool IsAutoSaveEnabled() => autoSaveEnabled;

        // ========================================
        // Private Methods
        // ========================================

        private void ProcessButtonEvents(List<RF433MHzButtonEvent> events)
        {
            foreach (var evt in events)
            {
                // Check if learning mode is active - register new button
                if (IsLearningModeActive())
                {
                    // Check if this is a new button (not yet learned)
                    if (!IsButtonLearned(evt.ButtonCode))
                    {
                        RegisterLearnedButton(evt.ButtonCode, evt.RollingCode);
                        OnCodeLearned?.Invoke(evt.ButtonCode, evt.RollingCode);
                        Debug.Log($"[HoloCade] RF433MHzReceiver: Learned new button (Code: {evt.ButtonCode}, RollingCode: {evt.RollingCode})");
                    }
                }

                // Check if this is a new state change
                bool wasPressed = lastButtonStates.TryGetValue(evt.ButtonCode, out bool lastState) && lastState;

                if (evt.Pressed != wasPressed)
                {
                    // State changed - trigger delegates
                    if (evt.Pressed)
                    {
                        OnButtonPressed?.Invoke(evt.ButtonCode);
                    }
                    else
                    {
                        OnButtonReleased?.Invoke(evt.ButtonCode);
                    }

                    OnButtonEvent?.Invoke(evt.ButtonCode, evt.Pressed);

                    // Check if button has assigned function - trigger function delegate
                    if (buttonFunctionMappings.TryGetValue(evt.ButtonCode, out string functionName) && !string.IsNullOrEmpty(functionName))
                    {
                        OnButtonFunctionTriggered?.Invoke(evt.ButtonCode, functionName, evt.Pressed);
                    }

                    // Update last state
                    lastButtonStates[evt.ButtonCode] = evt.Pressed;
                }
            }
        }

        private void RegisterLearnedButton(int buttonCode, uint rollingCode)
        {
            RF433MHzLearnedButton learnedButton = new RF433MHzLearnedButton
            {
                ButtonCode = buttonCode,
                RollingCodeSeed = rollingCode,
                LearnedTimestamp = Time.time,
                AssignedFunctionName = "",
                IsMapped = false
            };

            learnedButtons[buttonCode] = learnedButton;

            // Auto-save if enabled
            AutoSaveIfEnabled();
        }

        private void AutoSaveIfEnabled()
        {
            if (autoSaveEnabled)
            {
                SaveButtonMappings();
            }
        }

        /// <summary>
        /// Factory method to create the appropriate receiver based on configuration
        /// </summary>
        private static I433MHzReceiver CreateReceiver(RF433MHzReceiverConfig config)
        {
            // NOOP: Will create receiver implementations based on type
            // For now, return null (will be implemented with actual USB receiver drivers)
            // This is a placeholder that will be filled in when USB receiver drivers are implemented
            return null;
        }
    }
}

