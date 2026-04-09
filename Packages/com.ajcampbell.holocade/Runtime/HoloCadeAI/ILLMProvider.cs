// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// LLM Request Parameters
    /// </summary>
    [Serializable]
    public class LLMRequest
    {
        public string playerInput;
        public string systemPrompt;
        public List<string> conversationHistory = new List<string>();
        public string modelName;
        public float temperature = 0.7f;
        public int maxTokens = 150;
    }

    /// <summary>
    /// LLM Response
    /// </summary>
    [Serializable]
    public class LLMResponse
    {
        public string responseText;
        public bool isComplete = true;
        public string errorMessage;
    }

    /// <summary>
    /// LLM Provider Interface
    /// 
    /// Extensible interface for LLM backends, similar to MCP (Model Context Protocol).
    /// Enables hot-swapping LLM providers at runtime without code changes.
    /// 
    /// **NVIDIA NIM Containerized Approach:**
    /// NIM runs as Docker containers, making it perfect for hot-swapping:
    /// - Each model runs in its own container
    /// - Containers can be started/stopped independently
    /// - Multiple models can run simultaneously on different ports
    /// - Easy to swap models by changing endpoint URL
    /// 
    /// **Supported Providers:**
    /// - NVIDIA NIM (containerized, hot-swappable)
    /// - Ollama (local, supports LoRA)
    /// - vLLM (high-performance inference)
    /// - Claude API (cloud)
    /// - OpenAI API (cloud)
    /// - Any custom provider implementing this interface
    /// </summary>
    public interface ILLMProvider
    {
        /// <summary>
        /// Request LLM response (async)
        /// </summary>
        void RequestResponse(LLMRequest request, Action<LLMResponse> callback);

        /// <summary>
        /// Check if provider is available/ready
        /// </summary>
        bool IsAvailable();

        /// <summary>
        /// Get provider name/identifier
        /// </summary>
        string GetProviderName();

        /// <summary>
        /// Get supported model names (for discovery)
        /// </summary>
        List<string> GetSupportedModels();
    }
}

