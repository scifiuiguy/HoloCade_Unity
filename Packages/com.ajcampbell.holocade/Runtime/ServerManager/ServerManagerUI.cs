// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using UnityEngine.UIElements;

namespace HoloCade.ServerManager
{
    /// <summary>
    /// Server Manager UI Controller
    /// 
    /// Manages the UI Toolkit interface for the server manager.
    /// Binds to ServerManagerController for backend logic.
    /// </summary>
    [RequireComponent(typeof(ServerManagerController))]
    public class ServerManagerUI : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        private ServerManagerController controller;
        private VisualElement root;

        // UI Elements
        private TextField serverNameField;
        private DropdownField experienceTypeDropdown;
        private IntegerField maxPlayersField;
        private IntegerField portField;
        private TextField sceneNameField;
        private Button startServerButton;
        private Button stopServerButton;
        private Button configureOmniverseButton;
        private Label statusLabel;
        private Label playersLabel;
        private Label stateLabel;
        private Label uptimeLabel;
        private Label pidLabel;
        private Label omniverseStatusLabel;
        private Label omniverseStreamsLabel;
        private ScrollView logScrollView;
        private Label logTextLabel;

        private void Awake()
        {
            controller = GetComponent<ServerManagerController>();

            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument != null)
            {
                root = uiDocument.rootVisualElement;
                SetupUI();
            }
            else
            {
                Debug.LogError("[ServerManagerUI] UIDocument not found!");
            }
        }

        private void OnEnable()
        {
            // Subscribe to controller events
            if (controller != null)
            {
                controller.OnLogMessageAdded += OnLogMessageAdded;
                controller.OnServerStatusChanged += OnServerStatusChanged;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from controller events
            if (controller != null)
            {
                controller.OnLogMessageAdded -= OnLogMessageAdded;
                controller.OnServerStatusChanged -= OnServerStatusChanged;
            }
        }

        private void SetupUI()
        {
            // Find all UI elements
            serverNameField = root.Q<TextField>("ServerName");
            experienceTypeDropdown = root.Q<DropdownField>("ExperienceType");
            maxPlayersField = root.Q<IntegerField>("MaxPlayers");
            portField = root.Q<IntegerField>("Port");
            sceneNameField = root.Q<TextField>("SceneName");
            startServerButton = root.Q<Button>("StartServerButton");
            stopServerButton = root.Q<Button>("StopServerButton");
            configureOmniverseButton = root.Q<Button>("ConfigureOmniverseButton");
            statusLabel = root.Q<Label>("StatusValue");
            playersLabel = root.Q<Label>("PlayersValue");
            stateLabel = root.Q<Label>("StateValue");
            uptimeLabel = root.Q<Label>("UptimeValue");
            pidLabel = root.Q<Label>("PIDValue");
            omniverseStatusLabel = root.Q<Label>("OmniverseStatusValue");
            omniverseStreamsLabel = root.Q<Label>("StreamsValue");
            logScrollView = root.Q<ScrollView>("LogScrollView");
            logTextLabel = root.Q<Label>("LogText");

            // Bind configuration fields
            if (serverNameField != null)
            {
                serverNameField.value = controller.ServerConfig.ServerName;
                serverNameField.RegisterValueChangedCallback(evt => controller.ServerConfig.ServerName = evt.newValue);
            }

            if (experienceTypeDropdown != null)
            {
                experienceTypeDropdown.choices = new System.Collections.Generic.List<string>(controller.GetAvailableExperienceTypes());
                experienceTypeDropdown.value = controller.ServerConfig.ExperienceType;
                experienceTypeDropdown.RegisterValueChangedCallback(evt => controller.ServerConfig.ExperienceType = evt.newValue);
            }

            if (maxPlayersField != null)
            {
                maxPlayersField.value = controller.ServerConfig.MaxPlayers;
                maxPlayersField.RegisterValueChangedCallback(evt => controller.ServerConfig.MaxPlayers = evt.newValue);
            }

            if (portField != null)
            {
                portField.value = controller.ServerConfig.Port;
                portField.RegisterValueChangedCallback(evt => controller.ServerConfig.Port = evt.newValue);
            }

            if (sceneNameField != null)
            {
                sceneNameField.value = controller.ServerConfig.SceneName;
                sceneNameField.RegisterValueChangedCallback(evt => controller.ServerConfig.SceneName = evt.newValue);
            }

            // Bind buttons
            if (startServerButton != null)
            {
                startServerButton.clicked += () => controller.StartServer();
            }

            if (stopServerButton != null)
            {
                stopServerButton.clicked += () => controller.StopServer();
            }

            if (configureOmniverseButton != null)
            {
                configureOmniverseButton.clicked += () => controller.OpenOmniverseConfig();
            }

            // Initial UI update
            UpdateUI();
        }

        private void Update()
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (controller == null) return;

            // Update status display
            if (statusLabel != null)
            {
                statusLabel.text = controller.ServerStatus.IsRunning ? "● Running" : "○ Stopped";
                statusLabel.style.color = controller.ServerStatus.IsRunning ? Color.green : Color.gray;
            }

            if (playersLabel != null)
            {
                playersLabel.text = $"{controller.ServerStatus.CurrentPlayers}/{controller.ServerConfig.MaxPlayers}";
            }

            if (stateLabel != null)
            {
                stateLabel.text = controller.ServerStatus.ExperienceState;
            }

            if (uptimeLabel != null)
            {
                uptimeLabel.text = FormatUptime(controller.ServerStatus.Uptime);
            }

            if (pidLabel != null)
            {
                pidLabel.text = controller.ServerStatus.ProcessID > 0 ? $"PID: {controller.ServerStatus.ProcessID}" : "N/A";
            }

            // Update Omniverse status
            if (omniverseStatusLabel != null)
            {
                omniverseStatusLabel.text = controller.OmniverseStatus.IsConnected ? "● Connected" : "○ Not Connected";
                omniverseStatusLabel.style.color = controller.OmniverseStatus.IsConnected ? Color.green : Color.gray;
            }

            if (omniverseStreamsLabel != null)
            {
                omniverseStreamsLabel.text = $"{controller.OmniverseStatus.ActiveFaceStreams} active";
            }

            // Update button states
            if (startServerButton != null)
            {
                startServerButton.SetEnabled(!controller.ServerStatus.IsRunning);
            }

            if (stopServerButton != null)
            {
                stopServerButton.SetEnabled(controller.ServerStatus.IsRunning);
            }
        }

        private void OnLogMessageAdded(string message)
        {
            if (logTextLabel != null)
            {
                logTextLabel.text += message + "\n";
                
                // Auto-scroll to bottom
                if (logScrollView != null)
                {
                    logScrollView.ScrollTo(logTextLabel);
                }
            }
        }

        private void OnServerStatusChanged(ServerStatus status)
        {
            // UI updates happen in Update() loop
            // This is just a notification that status changed
        }

        private string FormatUptime(float seconds)
        {
            int hours = Mathf.FloorToInt(seconds / 3600f);
            int minutes = Mathf.FloorToInt((seconds % 3600f) / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{hours:00}:{minutes:00}:{secs:00}";
        }
    }
}



