// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// NIM ASR Model Type
    /// </summary>
    public enum NIMASRModelType
    {
        Parakeet_0_6B_English,
        Parakeet_1_1B_Multilingual,
        Canary_1B_Multilingual,
        Whisper_Small,
        Whisper_Medium,
        Whisper_Large,
        AutoDetect
    }

    /// <summary>
    /// ASR Provider for NVIDIA NIM ASR models (Parakeet, Canary, Whisper).
    /// Implements the IASRProvider interface for communication with NVIDIA NIM ASR services.
    /// 
    /// **NVIDIA NIM ASR Models:**
    /// - Parakeet (0.6B English, 1.1B Multilingual) - ✅ Streaming gRPC support
    /// - Canary (1B Multilingual) - ✅ Streaming gRPC support, includes translation
    /// - Whisper (Small, Medium, Large) - ⚠️ gRPC offline only (not suitable for real-time)
    /// 
    /// **All NIM ASR models:**
    /// - Containerized (Docker)
    /// - gRPC protocol (streaming for Parakeet/Canary, offline only for Whisper)
    /// - Hot-swappable (change endpoint URL to swap models)
    /// </summary>
    public class ASRProviderNIM : MonoBehaviour, IASRProvider
    {
        public NIMASRModelType modelType = NIMASRModelType.AutoDetect;
        private string endpointURL;
        private bool isInitialized = false;

        public bool Initialize(string inEndpointURL)
        {
            endpointURL = inEndpointURL;
            if (modelType == NIMASRModelType.AutoDetect)
                modelType = DetectModelType(inEndpointURL);
            isInitialized = true;
            // TODO: Initialize gRPC client for NIM ASR
            Debug.LogWarning("ASRProviderNIM: gRPC client not yet implemented");
            return true;
        }

        public void RequestASRTranscription(ASRRequest request, Action<ASRResponse> callback)
        {
            if (!isInitialized)
            {
                callback?.Invoke(new ASRResponse { errorMessage = "Provider not initialized" });
                return;
            }

            // TODO: Implement gRPC call to NIM ASR
            Debug.LogWarning("ASRProviderNIM: gRPC transcription not yet implemented");
            callback?.Invoke(new ASRResponse { errorMessage = "gRPC transcription not yet implemented" });
        }

        public bool IsAvailable()
        {
            return isInitialized;
        }

        public string GetProviderName()
        {
            return $"NVIDIA NIM ASR ({modelType})";
        }

        public List<string> GetSupportedModels()
        {
            switch (modelType)
            {
                case NIMASRModelType.Parakeet_0_6B_English:
                    return new List<string> { "parakeet-0.6b-en" };
                case NIMASRModelType.Parakeet_1_1B_Multilingual:
                    return new List<string> { "parakeet-1.1b-multilingual" };
                case NIMASRModelType.Canary_1B_Multilingual:
                    return new List<string> { "canary-1b-multilingual" };
                case NIMASRModelType.Whisper_Small:
                    return new List<string> { "whisper-small" };
                case NIMASRModelType.Whisper_Medium:
                    return new List<string> { "whisper-medium" };
                case NIMASRModelType.Whisper_Large:
                    return new List<string> { "whisper-large" };
                default:
                    return new List<string> { "auto-detect" };
            }
        }

        public bool SupportsStreaming()
        {
            // Parakeet and Canary support streaming, Whisper does not
            return modelType == NIMASRModelType.Parakeet_0_6B_English ||
                   modelType == NIMASRModelType.Parakeet_1_1B_Multilingual ||
                   modelType == NIMASRModelType.Canary_1B_Multilingual;
        }

        private NIMASRModelType DetectModelType(string inEndpointURL)
        {
            // Simple auto-detection based on URL patterns
            string urlLower = inEndpointURL.ToLower();
            if (urlLower.Contains("parakeet"))
                return urlLower.Contains("1.1") ? NIMASRModelType.Parakeet_1_1B_Multilingual : NIMASRModelType.Parakeet_0_6B_English;
            if (urlLower.Contains("canary"))
                return NIMASRModelType.Canary_1B_Multilingual;
            if (urlLower.Contains("whisper"))
            {
                if (urlLower.Contains("large"))
                    return NIMASRModelType.Whisper_Large;
                if (urlLower.Contains("medium"))
                    return NIMASRModelType.Whisper_Medium;
                return NIMASRModelType.Whisper_Small;
            }
            return NIMASRModelType.AutoDetect;
        }
    }
}

