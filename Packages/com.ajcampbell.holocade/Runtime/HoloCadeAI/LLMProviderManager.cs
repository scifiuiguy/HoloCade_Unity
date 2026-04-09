// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// LLM Provider Manager
    /// 
    /// Manages LLM provider instances and enables hot-swapping at runtime.
    /// Similar to MCP (Model Context Protocol) - provides extensible LLM backend system.
    /// 
    /// **NVIDIA NIM Containerized Hot-Swapping:**
    /// 
    /// NIM runs as Docker containers, making hot-swapping seamless:
    /// 
    /// ```bash
    /// # Start Llama 3.2 container
    /// docker run -d -p 8000:8000 --gpus all nvcr.io/nim/llama-3.2-3b-instruct:latest
    /// 
    /// # Later, swap to Mistral (different port)
    /// docker run -d -p 8001:8000 --gpus all nvcr.io/nim/mistral-7b-instruct:latest
    /// 
    /// # In Unity: Update config endpoint URL from localhost:8000 to localhost:8001
    /// # System automatically uses new model - no code changes!
    /// ```
    /// </summary>
    public class LLMProviderManager : MonoBehaviour
    {
        private ILLMProvider currentProvider;

        /// <summary>
        /// Initialize provider manager
        /// </summary>
        public bool InitializeProvider(string endpointURL, LLMProviderType providerType = LLMProviderType.AutoDetect, string modelName = "", ContainerConfig containerConfig = null, bool autoStartContainer = false)
        {
            // For now, create a default OpenAI-compatible provider
            // TODO: Implement full provider system with hot-swapping
            currentProvider = CreateProvider(providerType, endpointURL, modelName);
            return currentProvider != null;
        }

        /// <summary>
        /// Hot-swap to different provider/endpoint at runtime
        /// </summary>
        public bool SetProviderEndpoint(string endpointURL, LLMProviderType providerType = LLMProviderType.AutoDetect, string modelName = "")
        {
            currentProvider = CreateProvider(providerType, endpointURL, modelName);
            return currentProvider != null;
        }

        /// <summary>
        /// Request LLM response (uses current provider)
        /// </summary>
        public void RequestResponse(LLMRequest request, Action<LLMResponse> callback)
        {
            if (currentProvider == null)
            {
                Debug.LogError("LLMProviderManager: No provider initialized");
                callback?.Invoke(new LLMResponse { errorMessage = "No provider initialized" });
                return;
            }

            if (!currentProvider.IsAvailable())
            {
                Debug.LogWarning("LLMProviderManager: Provider not available");
                callback?.Invoke(new LLMResponse { errorMessage = "Provider not available" });
                return;
            }

            currentProvider.RequestResponse(request, callback);
        }

        /// <summary>
        /// Get current provider name
        /// </summary>
        public string GetCurrentProviderName()
        {
            return currentProvider?.GetProviderName() ?? "None";
        }

        /// <summary>
        /// Get supported models from current provider
        /// </summary>
        public List<string> GetSupportedModels()
        {
            return currentProvider?.GetSupportedModels() ?? new List<string>();
        }

        /// <summary>
        /// Check if provider is available
        /// </summary>
        public bool IsProviderAvailable()
        {
            return currentProvider != null && currentProvider.IsAvailable();
        }

        private LLMProviderType DetectProviderType(string endpointURL)
        {
            // Simple auto-detection based on URL
            if (endpointURL.Contains("11434"))
                return LLMProviderType.Ollama;
            return LLMProviderType.OpenAICompatible;
        }

        private ILLMProvider CreateProvider(LLMProviderType providerType, string endpointURL, string modelName)
        {
            if (providerType == LLMProviderType.AutoDetect)
                providerType = DetectProviderType(endpointURL);

            GameObject providerObj = new GameObject($"LLMProvider_{providerType}");
            providerObj.transform.SetParent(transform);

            ILLMProvider provider = null;
            switch (providerType)
            {
                case LLMProviderType.Ollama:
                    var ollamaProvider = providerObj.AddComponent<LLMProviderOllama>();
                    ollamaProvider.Initialize(endpointURL);
                    provider = ollamaProvider;
                    break;
                case LLMProviderType.OpenAICompatible:
                    var openAIProvider = providerObj.AddComponent<LLMProviderOpenAICompatible>();
                    openAIProvider.Initialize(endpointURL);
                    provider = openAIProvider;
                    break;
                default:
                    Debug.LogWarning($"LLMProviderManager: Unknown provider type {providerType}, using NOOP");
                    provider = new NOOPLLMProvider();
                    Destroy(providerObj);
                    break;
            }

            return provider;
        }
    }

    /// <summary>
    /// NOOP LLM Provider (placeholder until full implementation)
    /// </summary>
    internal class NOOPLLMProvider : ILLMProvider
    {
        public void RequestResponse(LLMRequest request, Action<LLMResponse> callback)
        {
            Debug.LogWarning("NOOPLLMProvider: LLM requests not yet implemented");
            callback?.Invoke(new LLMResponse { errorMessage = "LLM provider not yet implemented" });
        }

        public bool IsAvailable() => false;
        public string GetProviderName() => "NOOP";
        public List<string> GetSupportedModels() => new List<string>();
    }
}

