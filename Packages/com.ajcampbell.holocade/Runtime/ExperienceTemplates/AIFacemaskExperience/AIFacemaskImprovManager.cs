// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;
using HoloCade.HoloCadeAI;

namespace HoloCade.ExperienceTemplates
{
    /// <summary>
    /// Usage state for improv responses (queued → spoken when face starts speaking)
    /// </summary>
    public enum ImprovResponseState
    {
        Queued,
        Spoken
    }

    /// <summary>
    /// Facemask-specific configuration for improvised responses
    /// </summary>
    [System.Serializable]
    public class AIFacemaskImprovConfig
    {
        public AIImprovConfig baseConfig = new AIImprovConfig();
        // Facemask-specific config can be added here
    }

    /// <summary>
    /// AIFacemask Improv Manager Component
    /// 
    /// Facemask-specific improv manager that extends AIImprovManager.
    /// Adds face controller integration for streaming facial animation.
    /// 
    /// Inherits from AIImprovManager for generic LLM + TTS + Audio2Face pipeline.
    /// Adds:
    /// - Face controller integration
    /// - Facemask-specific voice/emotion settings
    /// - Experience-specific response formatting
    /// </summary>
    public class AIFacemaskImprovManager : AIImprovManager
    {
        [Header("AIFacemask Improv Configuration")]
        public AIFacemaskImprovConfig facemaskImprovConfig = new AIFacemaskImprovConfig();

        [Header("Status")]
        public ImprovResponseState currentAIResponseState = ImprovResponseState.Queued;

        // Override generic base class methods
        public override bool InitializeImprovManager()
        {
            // Copy facemask config to base config
            improvConfig = facemaskImprovConfig.baseConfig;
            return base.InitializeImprovManager();
        }

        /// <summary>
        /// Get current AI response (for HUD display)
        /// </summary>
        public string GetCurrentAIResponse()
        {
            return currentAIResponse;
        }

        /// <summary>
        /// Get current AI response usage state (queued or spoken)
        /// </summary>
        public ImprovResponseState GetCurrentAIResponseState()
        {
            return currentAIResponseState;
        }

        /// <summary>
        /// Mark current AI response as spoken (called when face animation starts)
        /// </summary>
        public void MarkCurrentResponseAsSpoken()
        {
            currentAIResponseState = ImprovResponseState.Spoken;
        }

        /// <summary>
        /// Notify of narrative state change (for transition buffering)
        /// </summary>
        public void NotifyNarrativeStateChanged(string oldState, string newState, int newStateIndex)
        {
            // TODO: Implement transition buffering
        }

        protected override void RequestTTSConversion(string text)
        {
            // Facemask-specific TTS implementation
            // Request TTS from NVIDIA ACE server
            if (httpClient == null)
                httpClient = gameObject.AddComponent<AIHTTPClient>();

            string ttsURL = $"{improvConfig.localTTSEndpointURL}/tts";
            var requestBody = new TTSRequest
            {
                text = text,
                voice = GetVoiceNameString(),
                sample_rate = 48000
            };

            string jsonString = JsonUtility.ToJson(requestBody);
            var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };

            httpClient.PostJSON(ttsURL, jsonString, headers, (result) =>
            {
                if (result.success)
                {
                    // Save audio file and trigger Audio2Face
                    string audioFilePath = System.IO.Path.Combine(Application.temporaryCachePath, $"tts_{System.Guid.NewGuid()}.wav");
                    System.IO.File.WriteAllBytes(audioFilePath, System.Text.Encoding.UTF8.GetBytes(result.responseBody));
                    OnTTSConversionComplete(audioFilePath, System.Text.Encoding.UTF8.GetBytes(result.responseBody));
                }
                else
                {
                    Debug.LogError($"AIFacemaskImprovManager: TTS conversion failed: {result.errorMessage}");
                    isGeneratingResponse = false;
                }
            });
        }

        protected override void RequestAudio2FaceConversion(string audioFilePath)
        {
            // Facemask-specific Audio2Face implementation
            // Request Audio2Face from NVIDIA ACE server
            if (httpClient == null)
                httpClient = gameObject.AddComponent<AIHTTPClient>();

            string audio2FaceURL = $"{improvConfig.localAudio2FaceEndpointURL}/audio2face";
            
            // Read audio file and send as base64 or multipart
            byte[] audioData = System.IO.File.ReadAllBytes(audioFilePath);
            string base64Audio = System.Convert.ToBase64String(audioData);

            var requestBody = new Audio2FaceRequest
            {
                audio_data = base64Audio,
                audio_format = "wav"
            };

            string jsonString = JsonUtility.ToJson(requestBody);
            var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };

            httpClient.PostJSON(audio2FaceURL, jsonString, headers, (result) =>
            {
                if (result.success)
                {
                    // Parse facial animation data and stream to face controller
                    OnAudio2FaceConversionComplete(true);
                }
                else
                {
                    Debug.LogError($"AIFacemaskImprovManager: Audio2Face conversion failed: {result.errorMessage}");
                    isGeneratingResponse = false;
                }
            });
        }

        private string GetVoiceNameString()
        {
            // Convert voice type to voice name string for TTS
            // TODO: Map to actual NVIDIA ACE voice names
            return "default";
        }

        [System.Serializable]
        private class TTSRequest
        {
            public string text;
            public string voice;
            public int sample_rate;
        }

        [System.Serializable]
        private class Audio2FaceRequest
        {
            public string audio_data;
            public string audio_format;
        }
    }
}

