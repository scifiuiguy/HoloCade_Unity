// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using HoloCade.HoloCadeAI;
using System.Linq;

namespace HoloCade.ExperienceTemplates
{
    /// <summary>
    /// AIFacemask Script Manager Component
    /// 
    /// Facemask-specific script manager that extends AIScriptManager.
    /// Adds narrative state machine integration and facemask-specific script structures.
    /// 
    /// Inherits from AIScriptManager for generic script management.
    /// Adds:
    /// - Narrative state machine integration
    /// - Facemask-specific script structures
    /// - Face controller integration
    /// - Experience-specific delegates
    /// </summary>
    public class AIFacemaskScriptManager : AIScriptManager
    {
        [Header("AIFacemask Script Configuration")]
        [Tooltip("NVIDIA ACE server base URL (e.g., \"http://192.168.1.100:8000\")")]
        public string aceServerBaseURL = "http://localhost:8000";

        [Tooltip("Whether to auto-trigger scripts on narrative state changes")]
        public bool autoTriggerOnStateChange = true;

        [Header("Status")]
        [Tooltip("Currently playing script (if any)")]
        public string currentScriptStateName = "";

        [Tooltip("Current script line index being played")]
        public int currentScriptLineIndex = -1;

        [Header("Events")]
        public UnityEvent<string, string> OnScriptStarted = new UnityEvent<string, string>();
        public UnityEvent<string, int, string> OnScriptLineStarted = new UnityEvent<string, int, string>();
        public UnityEvent<string, string> OnScriptFinished = new UnityEvent<string, string>();
        public UnityEvent<string> OnScriptPreBakeComplete = new UnityEvent<string>();

        // Override generic base class methods
        public override bool InitializeScriptManager(string inAIServerBaseURL)
        {
            aceServerBaseURL = inAIServerBaseURL;
            return base.InitializeScriptManager(inAIServerBaseURL);
        }

        /// <summary>
        /// Trigger a script for a specific narrative state
        /// </summary>
        public bool TriggerScriptForState(string stateName)
        {
            if (!HasScript(stateName))
                return false;

            currentScriptStateName = stateName;
            return PlayScript(stateName);
        }

        /// <summary>
        /// Handle narrative state change (called by experience base)
        /// </summary>
        public void HandleNarrativeStateChanged(string oldState, string newState, int newStateIndex)
        {
            if (autoTriggerOnStateChange)
                TriggerScriptForState(newState);
        }

        protected override void RequestScriptPlayback(string scriptID)
        {
            // Facemask-specific implementation
            // Request script playback from NVIDIA ACE server
            if (httpClient == null)
                httpClient = gameObject.AddComponent<AIHTTPClient>();

            if (!scripts.ContainsKey(scriptID))
            {
                Debug.LogError($"AIFacemaskScriptManager: Script {scriptID} not found");
                return;
            }

            string playbackURL = $"{aceServerBaseURL}/script/play";
            var requestBody = new ScriptPlaybackRequest
            {
                script_id = scriptID,
                start_line_index = 0
            };

            string jsonString = JsonUtility.ToJson(requestBody);
            var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };

            httpClient.PostJSON(playbackURL, jsonString, headers, (result) =>
            {
                if (result.success)
                {
                    OnScriptStarted?.Invoke(scriptID, currentScriptStateName);
                }
                else
                {
                    Debug.LogError($"AIFacemaskScriptManager: Script playback failed: {result.errorMessage}");
                    isPlayingScript = false;
                }
            });
        }

        protected override void RequestScriptPreBake(string scriptID)
        {
            // Facemask-specific implementation
            // Request script pre-baking from NVIDIA ACE server
            if (httpClient == null)
                httpClient = gameObject.AddComponent<AIHTTPClient>();

            if (!scripts.ContainsKey(scriptID))
            {
                Debug.LogError($"AIFacemaskScriptManager: Script {scriptID} not found");
                return;
            }

            var script = scripts[scriptID];
            string preBakeURL = $"{aceServerBaseURL}/script/prebake";
            var requestBody = new ScriptPreBakeRequest
            {
                script_id = scriptID,
                text_content = script.textContent
            };

            string jsonString = JsonUtility.ToJson(requestBody);
            var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };

            httpClient.PostJSON(preBakeURL, jsonString, headers, (result) =>
            {
                if (result.success)
                {
                    script.isPreBaked = true;
                    scripts[scriptID] = script;
                    OnScriptPreBakeComplete?.Invoke(scriptID);
                }
                else
                {
                    Debug.LogError($"AIFacemaskScriptManager: Script pre-baking failed: {result.errorMessage}");
                }
            });
        }

        [System.Serializable]
        private class ScriptPlaybackRequest
        {
            public string script_id;
            public int start_line_index;
        }

        [System.Serializable]
        private class ScriptPreBakeRequest
        {
            public string script_id;
            public string text_content;
        }
    }
}

