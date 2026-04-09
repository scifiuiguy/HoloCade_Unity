// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// ASR Provider Type
    /// </summary>
    public enum ASRProviderType
    {
        AutoDetect,
        Riva,
        NIM,
        Custom
    }

    /// <summary>
    /// Manages ASR providers, enabling hot-swapping and extensibility.
    /// This class acts as a factory and registry for different ASR backends.
    /// 
    /// **NVIDIA NIM Containerized Hot-Swapping:**
    /// 
    /// NIM runs as Docker containers, making hot-swapping seamless:
    /// 
    /// ```bash
    /// # Start Riva ASR container
    /// docker run -d -p 50051:50051 --gpus all nvcr.io/nim/riva-asr:latest
    /// 
    /// # Later, swap to Parakeet (different port)
    /// docker run -d -p 50052:50051 --gpus all nvcr.io/nim/parakeet-rnnt-1.1b:latest
    /// 
    /// # In Unity: Update config endpoint URL from localhost:50051 to localhost:50052
    /// # System automatically uses new model - no code changes!
    /// ```
    /// </summary>
    public class ASRProviderManager : MonoBehaviour
    {
        private IASRProvider activeProvider;

        /// <summary>
        /// Initializes the ASR provider manager and its default providers.
        /// </summary>
        public bool Initialize(string defaultEndpointURL, ASRProviderType defaultProviderType = ASRProviderType.AutoDetect, ContainerConfig containerConfig = null, bool autoStartContainer = false)
        {
            activeProvider = CreateProvider(defaultProviderType, defaultEndpointURL);
            return activeProvider != null && activeProvider.Initialize(defaultEndpointURL);
        }

        /// <summary>
        /// Hot-swap to different provider/endpoint at runtime
        /// </summary>
        public bool SetProviderEndpoint(string endpointURL, ASRProviderType providerType = ASRProviderType.AutoDetect)
        {
            activeProvider = CreateProvider(providerType, endpointURL);
            return activeProvider != null && activeProvider.Initialize(endpointURL);
        }

        /// <summary>
        /// Request ASR transcription (uses current provider)
        /// </summary>
        public void RequestTranscription(ASRRequest request, Action<ASRResponse> callback)
        {
            if (activeProvider == null)
            {
                Debug.LogError("ASRProviderManager: No provider initialized");
                callback?.Invoke(new ASRResponse { errorMessage = "No provider initialized" });
                return;
            }

            if (!activeProvider.IsAvailable())
            {
                Debug.LogWarning("ASRProviderManager: Provider not available");
                callback?.Invoke(new ASRResponse { errorMessage = "Provider not available" });
                return;
            }

            activeProvider.RequestASRTranscription(request, callback);
        }

        /// <summary>
        /// Gets the currently active ASR provider.
        /// </summary>
        public IASRProvider GetActiveProvider()
        {
            return activeProvider;
        }

        /// <summary>
        /// Gets a list of models supported by the active provider.
        /// </summary>
        public List<string> GetActiveProviderSupportedModels()
        {
            return activeProvider?.GetSupportedModels() ?? new List<string>();
        }

        /// <summary>
        /// Checks if the active provider supports streaming recognition.
        /// </summary>
        public bool ActiveProviderSupportsStreaming()
        {
            return activeProvider?.SupportsStreaming() ?? false;
        }

        private ASRProviderType AutoDetectProviderType(string endpointURL)
        {
            // Simple auto-detection based on URL
            if (endpointURL.Contains("50051"))
                return ASRProviderType.Riva;
            return ASRProviderType.NIM;
        }

        private IASRProvider CreateProvider(ASRProviderType providerType, string endpointURL)
        {
            if (providerType == ASRProviderType.AutoDetect)
                providerType = AutoDetectProviderType(endpointURL);

            GameObject providerObj = new GameObject($"ASRProvider_{providerType}");
            providerObj.transform.SetParent(transform);

            IASRProvider provider = null;
            switch (providerType)
            {
                case ASRProviderType.Riva:
                    var rivaProvider = providerObj.AddComponent<ASRProviderRiva>();
                    rivaProvider.Initialize(endpointURL);
                    provider = rivaProvider;
                    break;
                case ASRProviderType.NIM:
                    var nimProvider = providerObj.AddComponent<ASRProviderNIM>();
                    nimProvider.Initialize(endpointURL);
                    provider = nimProvider;
                    break;
                default:
                    Debug.LogWarning($"ASRProviderManager: Unknown provider type {providerType}, using NOOP");
                    provider = new NOOPASRProvider();
                    Destroy(providerObj);
                    break;
            }

            return provider;
        }
    }

    /// <summary>
    /// NOOP ASR Provider (placeholder until full implementation)
    /// </summary>
    internal class NOOPASRProvider : IASRProvider
    {
        public bool Initialize(string endpointURL)
        {
            Debug.LogWarning("NOOPASRProvider: ASR provider not yet implemented");
            return false;
        }

        public void RequestASRTranscription(ASRRequest request, Action<ASRResponse> callback)
        {
            Debug.LogWarning("NOOPASRProvider: ASR transcription not yet implemented");
            callback?.Invoke(new ASRResponse { errorMessage = "ASR provider not yet implemented" });
        }

        public bool IsAvailable() => false;
        public string GetProviderName() => "NOOP";
        public List<string> GetSupportedModels() => new List<string>();
        public bool SupportsStreaming() => false;
    }
}

