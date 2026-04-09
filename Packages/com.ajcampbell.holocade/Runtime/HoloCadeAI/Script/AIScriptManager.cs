// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// Generic script data structure (for use by generic AIScriptManager)
    /// Subclasses can extend this with experience-specific fields
    /// </summary>
    [Serializable]
    public class AIScript
    {
        public string scriptID;
        public string description;
        public string textContent;
        public bool isPreBaked = false;
        public string preBakedDataPath;
    }

    /// <summary>
    /// Generic Script Manager Component
    /// 
    /// Base class for managing AI scripts (text-to-speech, audio-to-face, etc.).
    /// Provides generic script management without experience-specific logic.
    /// 
    /// Subclasses should extend this for experience-specific needs:
    /// - Narrative state machine integration
    /// - Experience-specific script structures
    /// - Experience-specific playback triggers
    /// 
    /// WORKFLOW:
    /// 1. Define scripts (text content + settings)
    /// 2. Pre-bake scripts on AI server (TTS → Audio, Audio → Facial data)
    /// 3. Play scripts by ID/key
    /// 4. AI server streams pre-baked data
    /// </summary>
    public class AIScriptManager : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("AI server base URL (e.g., \"http://192.168.1.100:8000\")")]
        public string aiServerBaseURL = "http://localhost:8000";

        [Header("Status")]
        [Tooltip("Currently playing script ID (if any)")]
        public string currentScriptID = "";

        [Tooltip("Whether a script is currently playing")]
        public bool isPlayingScript = false;

        protected bool isInitialized = false;
        protected AIHTTPClient httpClient;
        protected Dictionary<string, AIScript> scripts = new Dictionary<string, AIScript>();

        protected virtual void Start()
        {
            if (httpClient == null)
                httpClient = gameObject.AddComponent<AIHTTPClient>();
        }

        /// <summary>
        /// Initialize the script manager
        /// </summary>
        public virtual bool InitializeScriptManager(string inAIServerBaseURL)
        {
            aiServerBaseURL = inAIServerBaseURL;
            isInitialized = true;
            return true;
        }

        /// <summary>
        /// Play a script by ID
        /// </summary>
        public virtual bool PlayScript(string scriptID)
        {
            if (!HasScript(scriptID))
                return false;

            currentScriptID = scriptID;
            isPlayingScript = true;
            RequestScriptPlayback(scriptID);
            return true;
        }

        /// <summary>
        /// Stop the currently playing script
        /// </summary>
        public virtual void StopCurrentScript()
        {
            currentScriptID = "";
            isPlayingScript = false;
        }

        /// <summary>
        /// Pre-bake a script (convert text to audio/facial data)
        /// </summary>
        public virtual void PreBakeScript(string scriptID, bool async = true)
        {
            if (!HasScript(scriptID))
                return;

            RequestScriptPreBake(scriptID);
        }

        /// <summary>
        /// Check if a script exists
        /// </summary>
        public virtual bool HasScript(string scriptID)
        {
            return scripts.ContainsKey(scriptID);
        }

        /// <summary>
        /// Request script playback from AI server
        /// Subclasses can override for custom playback logic
        /// </summary>
        protected virtual void RequestScriptPlayback(string scriptID)
        {
            // Base implementation - subclasses should override
            Debug.LogWarning($"AIScriptManager: RequestScriptPlayback not implemented for script {scriptID}");
        }

        /// <summary>
        /// Request script pre-baking from AI server
        /// Subclasses can override for custom pre-baking logic
        /// </summary>
        protected virtual void RequestScriptPreBake(string scriptID)
        {
            // Base implementation - subclasses should override
            Debug.LogWarning($"AIScriptManager: RequestScriptPreBake not implemented for script {scriptID}");
        }
    }
}

