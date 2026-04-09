// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// Generic configuration for improvised responses
    /// </summary>
    [Serializable]
    public class AIImprovConfig
    {
        public bool enableImprov = true;
        public string localLLMEndpointURL = "http://localhost:8000";
        public string llmModelName = "llama-3.2-3b-instruct";
        public LLMProviderType llmProviderType = LLMProviderType.AutoDetect;
        public bool autoStartContainer = false;
        public ContainerConfig containerConfig = new ContainerConfig();
        public string systemPrompt = "You are a helpful AI assistant.";
        public int maxResponseTokens = 150;
        public float llmTemperature = 0.7f;
        public bool useLocalTTS = true;
        public string localTTSEndpointURL = "http://localhost:50051";
        public bool useLocalAudio2Face = true;
        public string localAudio2FaceEndpointURL = "http://localhost:8000";
    }

    /// <summary>
    /// LLM Provider Type
    /// </summary>
    public enum LLMProviderType
    {
        AutoDetect,
        Ollama,
        OpenAICompatible,
        Custom
    }

    /// <summary>
    /// Generic Improv Manager Component
    /// 
    /// Base class for managing real-time improvised AI responses.
    /// Provides generic LLM + TTS + Audio2Face pipeline without experience-specific logic.
    /// 
    /// Subclasses should extend this for experience-specific needs:
    /// - Face controller integration (for facial animation)
    /// - Experience-specific voice/emotion settings
    /// - Experience-specific response formatting
    /// 
    /// WORKFLOW:
    /// 1. Receive text input
    /// 2. Local LLM generates improvised response
    /// 3. Local TTS converts text → audio
    /// 4. Local Audio2Face converts audio → facial animation (or other output)
    /// 5. Output streamed to experience-specific handler
    /// </summary>
    public class AIImprovManager : MonoBehaviour
    {
        [Header("Configuration")]
        public AIImprovConfig improvConfig = new AIImprovConfig();

        [Header("Status")]
        public List<string> conversationHistory = new List<string>();
        public int maxConversationHistory = 10;
        public bool isGeneratingResponse = false;

        [Header("Events")]
        public UnityEvent<string, string> OnImprovResponseGenerated = new UnityEvent<string, string>();
        public UnityEvent<string> OnImprovResponseStarted = new UnityEvent<string>();
        public UnityEvent<string> OnImprovResponseFinished = new UnityEvent<string>();

        protected bool isInitialized = false;
        protected string currentInput;
        protected string currentAIResponse;
        protected AIHTTPClient httpClient;
        protected LLMProviderManager llmProviderManager; // Will be created in InitializeImprovManager

        protected virtual void Start()
        {
            if (httpClient == null)
                httpClient = gameObject.AddComponent<AIHTTPClient>();
        }

        /// <summary>
        /// Initialize the improv manager
        /// </summary>
        public virtual bool InitializeImprovManager()
        {
            // Initialize LLM provider manager
            if (llmProviderManager == null)
                llmProviderManager = gameObject.AddComponent<LLMProviderManager>();

            isInitialized = true;
            return true;
        }

        /// <summary>
        /// Generate an improvised response to input
        /// </summary>
        public virtual string GenerateImprovResponse(string input)
        {
            if (!isInitialized)
                return "";

            currentInput = input;
            RequestLLMResponseAsync(input, improvConfig.systemPrompt, conversationHistory);
            return ""; // Async - response comes via callback
        }

        /// <summary>
        /// Generate and play an improvised response (text → LLM → TTS → Audio2Face)
        /// </summary>
        public virtual void GenerateAndPlayImprovResponse(string input, bool async = true)
        {
            if (!isInitialized)
                return;

            currentInput = input;
            isGeneratingResponse = true;
            RequestLLMResponseAsync(input, improvConfig.systemPrompt, conversationHistory);
        }

        /// <summary>
        /// Clear conversation history
        /// </summary>
        public virtual void ClearConversationHistory()
        {
            conversationHistory.Clear();
        }

        /// <summary>
        /// Stop current improv response generation/playback
        /// </summary>
        public virtual void StopCurrentResponse()
        {
            isGeneratingResponse = false;
            currentInput = "";
            currentAIResponse = "";
        }

        /// <summary>
        /// Request LLM response asynchronously
        /// </summary>
        protected virtual void RequestLLMResponseAsync(string input, string systemPrompt, List<string> inConversationHistory)
        {
            if (llmProviderManager == null)
            {
                Debug.LogWarning("AIImprovManager: LLMProviderManager not initialized - creating default");
                llmProviderManager = gameObject.AddComponent<LLMProviderManager>();
            }

            var request = new LLMRequest
            {
                playerInput = input,
                systemPrompt = systemPrompt,
                conversationHistory = inConversationHistory,
                modelName = improvConfig.llmModelName,
                temperature = improvConfig.llmTemperature,
                maxTokens = improvConfig.maxResponseTokens
            };

            llmProviderManager.RequestResponse(request, OnLLMResponseReceived);
        }

        /// <summary>
        /// Handle LLM response callback
        /// </summary>
        protected virtual void OnLLMResponseReceived(LLMResponse response)
        {
            if (string.IsNullOrEmpty(response.responseText) && !string.IsNullOrEmpty(response.errorMessage))
            {
                Debug.LogError($"AIImprovManager: LLM request failed: {response.errorMessage}");
                isGeneratingResponse = false;
                return;
            }

            currentAIResponse = response.responseText;
            OnImprovResponseGenerated?.Invoke(currentInput, currentAIResponse);
            RequestTTSConversion(currentAIResponse);
        }

        /// <summary>
        /// Request TTS conversion
        /// Subclasses can override for custom TTS handling
        /// </summary>
        protected virtual void RequestTTSConversion(string text)
        {
            // Base implementation - subclasses should override
            Debug.LogWarning($"AIImprovManager: RequestTTSConversion not implemented");
        }

        /// <summary>
        /// Request Audio2Face conversion
        /// Subclasses can override for custom Audio2Face handling
        /// </summary>
        protected virtual void RequestAudio2FaceConversion(string audioFilePath)
        {
            // Base implementation - subclasses should override
            Debug.LogWarning($"AIImprovManager: RequestAudio2FaceConversion not implemented");
        }

        /// <summary>
        /// Called when TTS conversion is complete
        /// Subclasses can override for custom handling
        /// </summary>
        protected virtual void OnTTSConversionComplete(string audioFilePath, byte[] audioData)
        {
            Debug.Log($"AIImprovManager: TTS conversion complete. Audio file: {audioFilePath}");
            // Default: Trigger Audio2Face conversion
            RequestAudio2FaceConversion(audioFilePath);
        }

        /// <summary>
        /// Called when Audio2Face conversion is complete
        /// Subclasses can override for custom handling
        /// </summary>
        protected virtual void OnAudio2FaceConversionComplete(bool success)
        {
            if (success)
            {
                Debug.Log("AIImprovManager: Audio2Face conversion complete.");
                OnImprovResponseFinished?.Invoke(currentAIResponse);
            }
            else
            {
                Debug.LogError("AIImprovManager: Audio2Face conversion failed.");
            }
            isGeneratingResponse = false;
        }

        /// <summary>
        /// Build conversation context for LLM
        /// </summary>
        protected virtual string BuildConversationContext(string input)
        {
            var context = improvConfig.systemPrompt + "\n\n";
            foreach (var history in conversationHistory)
                context += history + "\n";
            context += $"Player: {input}\n";
            return context;
        }
    }
}

