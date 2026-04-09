// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using HoloCade.HoloCadeAI;

namespace HoloCade.ExperienceTemplates
{
    /// <summary>
    /// Facemask-specific configuration for ASR
    /// </summary>
    [System.Serializable]
    public class AIFacemaskASRConfig
    {
        public AIASRConfig baseConfig = new AIASRConfig();
        public bool autoTriggerImprov = true;
    }

    /// <summary>
    /// AIFacemask ASR Manager Component
    /// 
    /// Facemask-specific ASR manager that extends AIASRManager.
    /// Adds auto-triggering of improv responses after transcription.
    /// 
    /// Inherits from AIASRManager for generic ASR functionality.
    /// Adds:
    /// - Auto-triggering improv responses after transcription
    /// - Facemask-specific transcription handling
    /// - Integration with AIFacemaskImprovManager
    /// </summary>
    public class AIFacemaskASRManager : AIASRManager
    {
        [Header("AIFacemask ASR Configuration")]
        public AIFacemaskASRConfig facemaskASRConfig = new AIFacemaskASRConfig();

        private AIFacemaskImprovManager improvManager;

        // Override generic base class methods
        public override bool InitializeASRManager()
        {
            // Copy facemask config to base config
            asrConfig = facemaskASRConfig.baseConfig;

            // Find improv manager
            if (improvManager == null)
                improvManager = GetComponent<AIFacemaskImprovManager>();

            return base.InitializeASRManager();
        }

        protected override void HandleTranscriptionResult(int sourceId, string transcribedText)
        {
            // Call base implementation first
            base.HandleTranscriptionResult(sourceId, transcribedText);

            // Auto-trigger improv if enabled
            if (facemaskASRConfig.autoTriggerImprov && improvManager != null)
            {
                improvManager.GenerateAndPlayImprovResponse(transcribedText);
            }
        }
    }
}

