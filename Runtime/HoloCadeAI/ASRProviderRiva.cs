// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// ASR Provider for NVIDIA Riva ASR.
    /// Implements the IASRProvider interface for communication with NVIDIA Riva ASR services.
    /// 
    /// **NVIDIA Riva ASR:**
    /// - Containerized (Docker) or local SDK installation
    /// - gRPC protocol (streaming + offline)
    /// - Production-ready, optimized for real-time
    /// - Available via NIM containers or standalone Riva containers
    /// </summary>
    public class ASRProviderRiva : MonoBehaviour, IASRProvider
    {
        private string endpointURL;
        private bool isInitialized = false;

        public bool Initialize(string inEndpointURL)
        {
            endpointURL = inEndpointURL;
            isInitialized = true;
            // TODO: Initialize gRPC client for Riva ASR
            Debug.LogWarning("ASRProviderRiva: gRPC client not yet implemented");
            return true;
        }

        public void RequestASRTranscription(ASRRequest request, Action<ASRResponse> callback)
        {
            if (!isInitialized)
            {
                callback?.Invoke(new ASRResponse { errorMessage = "Provider not initialized" });
                return;
            }

            // TODO: Implement gRPC call to Riva ASR
            Debug.LogWarning("ASRProviderRiva: gRPC transcription not yet implemented");
            callback?.Invoke(new ASRResponse { errorMessage = "gRPC transcription not yet implemented" });
        }

        public bool IsAvailable()
        {
            return isInitialized;
        }

        public string GetProviderName()
        {
            return "NVIDIA Riva ASR";
        }

        public List<string> GetSupportedModels()
        {
            return new List<string> { "riva-asr-en-us", "riva-asr-multilingual" };
        }

        public bool SupportsStreaming()
        {
            return true; // Riva supports streaming
        }
    }
}

