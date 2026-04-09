// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.Core;
using HoloCade.AIFacemask;
using HoloCade.EmbeddedSystems;
using HoloCade.VOIP;

namespace HoloCade.ExperienceTemplates
{
    /// <summary>
    /// AI Facemask Experience Template
    /// 
    /// Pre-configured experience for LAN multiplayer VR with immersive theater live actors.
    /// 
    /// NETWORK ARCHITECTURE (REQUIRED):
    /// This experience REQUIRES a dedicated server setup:
    /// - Separate local PC running headless dedicated server
    /// - Same PC runs NVIDIA ACE pipeline: Audio → NLU → Emotion → Facial Animation
    /// - NVIDIA ACE streams facial textures and blend shapes to HMDs over network
    /// - Offloads AI processing from HMDs for optimal performance
    /// - Supports parallelization for multiple live actors
    /// 
    /// AI FACIAL ANIMATION:
    /// - Fully automated by NVIDIA ACE - NO manual control, keyframe animation, or rigging
    /// - Live actor wears HMD with AIFace mesh tracked on top of their face (like a mask)
    /// - NVIDIA ACE determines facial expressions based on:
    ///   - Audio track (speech recognition)
    ///   - NLU (natural language understanding)
    ///   - Emotion detection
    ///   - State machine context
    /// - AIFaceController receives NVIDIA ACE output and applies it to mesh in real-time
    /// 
    /// LIVE ACTOR CONTROLS:
    /// - Live actors wear wrist-mounted button controls (4 buttons: 2 left, 2 right)
    /// - Buttons control the narrative state machine (NOT facial animation)
    /// - Live actor directs experience flow, AI face handles expressions autonomously
    /// 
    /// Button Layout:
    /// - Left Wrist:  Button 0 (Forward), Button 1 (Backward)
    /// - Right Wrist: Button 2 (Forward), Button 3 (Backward)
    /// 
    /// Perfect for interactive theater, escape rooms, and narrative-driven LBE experiences
    /// requiring professional performers to guide players through story beats.
    /// </summary>
    public class AIFacemaskExperience : HoloCadeExperienceBase
    {
        [Header("Components")]
        [SerializeField] private AIFaceController faceController;
        [SerializeField] private SerialDeviceController costumeController;
        [SerializeField] private AIFacemaskScriptManager scriptManager;
        [SerializeField] private AIFacemaskImprovManager improvManager;
        [SerializeField] private AIFacemaskASRManager asrManager;
        [SerializeField] private VOIPManager voipManager;

        [Header("Live Actor Configuration")]
        [SerializeField] private GameObject avatarPrefab;
        [SerializeField] private SkinnedMeshRenderer liveActorMesh;
        [SerializeField] private int numberOfLiveActors = 1;
        [SerializeField] private int numberOfPlayers = 4;

        [Header("Live Actor Connection")]
        [SerializeField] private string liveActorStreamIP = "192.168.1.50";
        [SerializeField] private int liveActorStreamPort = 9000;

        private GameObject spawnedAvatar;
        private bool[] previousButtonStates = new bool[4];

        protected override void Awake()
        {
            base.Awake();

            // Enable multiplayer
            enableMultiplayer = true;
            maxPlayers = numberOfLiveActors + numberOfPlayers;

            // Enable narrative state machine (uses base class NarrativeStateMachine component)
            // This provides the narrative state progression that triggers automated AI facemask performances
            useNarrativeStateMachine = true;

            // Find or create AI face controller
            if (faceController == null)
            {
                faceController = GetComponentInChildren<AIFaceController>();
            }

            // Find or create serial device controller
            if (costumeController == null)
            {
                costumeController = GetComponent<SerialDeviceController>();
                if (costumeController == null)
                {
                    costumeController = gameObject.AddComponent<SerialDeviceController>();
                }
            }

            // Find or create Script Manager
            if (scriptManager == null)
            {
                scriptManager = GetComponent<AIFacemaskScriptManager>();
                if (scriptManager == null)
                {
                    scriptManager = gameObject.AddComponent<AIFacemaskScriptManager>();
                }
            }

            // Find or create Improv Manager
            if (improvManager == null)
            {
                improvManager = GetComponent<AIFacemaskImprovManager>();
                if (improvManager == null)
                {
                    improvManager = gameObject.AddComponent<AIFacemaskImprovManager>();
                }
            }

            // Find or create ASR Manager
            if (asrManager == null)
            {
                asrManager = GetComponent<AIFacemaskASRManager>();
                if (asrManager == null)
                {
                    asrManager = gameObject.AddComponent<AIFacemaskASRManager>();
                }
            }

            // Find or create VOIP Manager
            if (voipManager == null)
            {
                voipManager = GetComponent<VOIPManager>();
                if (voipManager == null)
                {
                    voipManager = gameObject.AddComponent<VOIPManager>();
                }
            }
        }

        protected override bool InitializeExperienceImpl()
        {
            // Spawn avatar if needed
            if (avatarPrefab != null && spawnedAvatar == null)
            {
                spawnedAvatar = Instantiate(avatarPrefab, transform);
                
                // Find AI face controller and mesh on spawned avatar
                if (faceController == null)
                {
                    faceController = spawnedAvatar.GetComponentInChildren<AIFaceController>();
                }
                
                if (liveActorMesh == null)
                {
                    liveActorMesh = spawnedAvatar.GetComponentInChildren<SkinnedMeshRenderer>();
                }
            }

            // Initialize AI Face Controller (receives NVIDIA ACE output)
            if (faceController != null && liveActorMesh != null)
            {
                AIFaceConfig faceConfig = new AIFaceConfig
                {
                    targetMesh = liveActorMesh,
                    nvidiaACEEndpointURL = "",  // NOOP: TODO - Configure NVIDIA ACE endpoint URL
                    updateRate = 30.0f
                };

                if (!faceController.InitializeAIFace(faceConfig))
                {
                    Debug.LogError("[HoloCade] AIFacemaskExperience: Failed to initialize face controller");
                    return false;
                }

                Debug.Log("[HoloCade] AIFacemaskExperience: AI Face initialized (NVIDIA ACE receiver mode)");
            }

            // Initialize costume controller (wrist-mounted buttons)
            if (costumeController != null)
            {
                EmbeddedDeviceConfig deviceConfig = new EmbeddedDeviceConfig
                {
                    deviceType = MicrocontrollerType.ESP32,
                    protocol = CommProtocol.WiFi,
                    deviceAddress = "192.168.1.50",
                    port = 8888,
                    inputChannelCount = 4,  // 4 wrist buttons (2 left, 2 right)
                    outputChannelCount = 8, // 8 haptic vibrators (general support, not used by this template)
                    debugMode = false,
                    securityLevel = SecurityLevel.Encrypted,
                    sharedSecret = "CHANGE_ME_IN_PRODUCTION_2025"
                };

                if (!costumeController.InitializeDevice(deviceConfig))
                {
                    Debug.LogWarning("[HoloCade] AIFacemaskExperience: Failed to initialize embedded device, continuing without wrist controls");
                }
                else
                {
                    Debug.Log("[HoloCade] AIFacemaskExperience: Wrist controls connected (4 buttons)");
                }
            }

            // Initialize narrative state machine with default states (uses base class NarrativeStateMachine)
            // Base class creates NarrativeStateMachine automatically when useNarrativeStateMachine is true
            if (narrativeStateMachine != null && useNarrativeStateMachine)
            {
                var defaultStates = new System.Collections.Generic.List<ExperienceState>
                {
                    new ExperienceState("Intro", "Introduction sequence"),
                    new ExperienceState("Tutorial", "Player tutorial"),
                    new ExperienceState("Act1", "First act"),
                    new ExperienceState("Act2", "Second act"),
                    new ExperienceState("Finale", "Finale sequence"),
                    new ExperienceState("Credits", "End credits")
                };

                narrativeStateMachine.Initialize(defaultStates);
                narrativeStateMachine.StartExperience();

                Debug.Log($"[HoloCade] AIFacemaskExperience: Narrative state machine initialized with {defaultStates.Count} states");
            }

            // Initialize Script Manager (pre-baked script collections for NVIDIA ACE)
            if (scriptManager != null)
            {
                // NOOP: TODO - Configure NVIDIA ACE server base URL from project settings or config
                string aceServerBaseURL = "http://localhost:8000";  // Default to localhost

                if (scriptManager.InitializeScriptManager(aceServerBaseURL))
                {
                    Debug.Log("[HoloCade] AIFacemaskExperience: Script Manager initialized");
                }
                else
                {
                    Debug.LogWarning("[HoloCade] AIFacemaskExperience: Script Manager initialization failed, continuing without script automation");
                }
            }

            // Initialize Improv Manager (real-time improvised responses using local LLM + TTS + Audio2Face)
            if (improvManager != null)
            {
                if (improvManager.InitializeImprovManager())
                {
                    Debug.Log("[HoloCade] AIFacemaskExperience: Improv Manager initialized (local LLM + TTS + Audio2Face)");
                }
                else
                {
                    Debug.LogWarning("[HoloCade] AIFacemaskExperience: Improv Manager initialization failed, continuing without improv responses");
                }
            }

            // Initialize ASR Manager (converts player voice to text for improv responses)
            if (asrManager != null)
            {
                if (asrManager.InitializeASRManager())
                {
                    Debug.Log("[HoloCade] AIFacemaskExperience: ASR Manager initialized (player voice → text for improv)");

                    // Register ASR Manager as visitor with VOIPManager
                    // This keeps AIFacemask module decoupled from VOIP module
                    if (voipManager != null)
                    {
                        voipManager.RegisterAudioVisitor(asrManager);
                        Debug.Log("[HoloCade] AIFacemaskExperience: ASR Manager registered with VOIPManager");
                    }
                }
                else
                {
                    Debug.LogWarning("[HoloCade] AIFacemaskExperience: ASR Manager initialization failed, continuing without voice input");
                }
            }

            Debug.Log($"[HoloCade] AIFacemaskExperience: Initialized with {numberOfLiveActors} live actors and {numberOfPlayers} players");
            return true;
        }

        protected override void ShutdownExperienceImpl()
        {
            // Stop narrative state machine (uses base class NarrativeStateMachine)
            if (narrativeStateMachine != null)
            {
                narrativeStateMachine.StopExperience();
            }

            // Unregister ASR Manager from VOIPManager
            if (voipManager != null && asrManager != null)
            {
                voipManager.UnregisterAudioVisitor(asrManager);
            }

            // Disconnect embedded systems
            if (costumeController != null)
            {
                costumeController.DisconnectDevice();
            }

            // Clean up spawned avatar
            if (spawnedAvatar != null)
            {
                Destroy(spawnedAvatar);
            }
        }

        private void Update()
        {
            if (!isInitialized)
                return;

            // Process button input from wrist-mounted controls
            ProcessButtonInput();
        }

        private void ProcessButtonInput()
        {
            if (costumeController == null || !costumeController.IsDeviceConnected() || narrativeStateMachine == null)
            {
                return;
            }

            // Read current button states
            bool[] currentButtonStates = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                currentButtonStates[i] = costumeController.GetDigitalInput(i);
            }

            // Button 0 (Left Wrist Forward) or Button 2 (Right Wrist Forward)
            if ((currentButtonStates[0] && !previousButtonStates[0]) ||
                (currentButtonStates[2] && !previousButtonStates[2]))
            {
                AdvanceExperience();
            }

            // Button 1 (Left Wrist Backward) or Button 3 (Right Wrist Backward)
            if ((currentButtonStates[1] && !previousButtonStates[1]) ||
                (currentButtonStates[3] && !previousButtonStates[3]))
            {
                RetreatExperience();
            }

            // Store current states for next frame
            for (int i = 0; i < 4; i++)
            {
                previousButtonStates[i] = currentButtonStates[i];
            }
        }

        #region Narrative State Control

        /// <summary>
        /// Get the current narrative state (from base class narrative state machine)
        /// This is the same as GetCurrentNarrativeState() from the base class
        /// </summary>
        public string GetCurrentExperienceState()
        {
            return GetCurrentNarrativeState();
        }

        /// <summary>
        /// Manually advance the experience to the next state (usually triggered by buttons)
        /// Uses base class AdvanceNarrativeState() method
        /// </summary>
        public bool AdvanceExperience()
        {
            return AdvanceNarrativeState();
        }

        /// <summary>
        /// Manually retreat the experience to the previous state (usually triggered by buttons)
        /// Uses base class RetreatNarrativeState() method
        /// </summary>
        public bool RetreatExperience()
        {
            return RetreatNarrativeState();
        }

        /// <summary>
        /// Handle narrative state changes (overrides base class method)
        /// Called when live actor advances/retreats narrative state via wireless trigger buttons
        /// Each state change triggers automated AI facemask performances
        /// </summary>
        protected override void OnNarrativeStateChanged(string oldState, string newState, int newStateIndex)
        {
            Debug.Log($"[HoloCade] AIFacemaskExperience: Narrative state changed from '{oldState}' to '{newState}' (Index: {newStateIndex})");

            // State changes are triggered by live actor's wireless trigger buttons
            // Each state change triggers automated AI facemask performances via NVIDIA ACE
            // Trigger script for the new state (if script manager is available and auto-trigger is enabled)
            if (scriptManager != null && scriptManager.autoTriggerOnStateChange)
            {
                scriptManager.HandleNarrativeStateChanged(oldState, newState, newStateIndex);
            }

            // Override this method in derived classes to trigger additional game events based on state changes
        }

        #endregion

        #region Public API - Live Actor Connection

        /// <summary>
        /// Set the live actor stream IP address
        /// </summary>
        public void SetLiveActorStreamIP(string ip)
        {
            liveActorStreamIP = ip;
        }

        /// <summary>
        /// Set the live actor stream port
        /// </summary>
        public void SetLiveActorStreamPort(int port)
        {
            liveActorStreamPort = port;
        }

        #endregion
    }
}
