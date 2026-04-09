// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// ASR Request Parameters
    /// </summary>
    [Serializable]
    public class ASRRequest
    {
        public byte[] audioData;
        public int sampleRate = 48000;
        public string languageCode = "en-US";
        public bool useStreaming = true;
        public string endpointURL;
    }

    /// <summary>
    /// ASR Response
    /// </summary>
    [Serializable]
    public class ASRResponse
    {
        public string transcribedText;
        public bool success = false;
        public string errorMessage;
        public float confidence = 0.0f;
    }

    /// <summary>
    /// Interface for ASR providers.
    /// This interface allows for hot-swapping different ASR backends (Riva, Parakeet, Canary, etc.)
    /// without modifying the core AIASRManager.
    /// 
    /// **NVIDIA NIM Containerized Approach:**
    /// NIM runs as Docker containers, making it perfect for hot-swapping:
    /// - Each ASR model runs in its own container
    /// - Containers can be started/stopped independently
    /// - Multiple models can run simultaneously on different ports
    /// - Easy to swap models by changing endpoint URL
    /// 
    /// **Supported Providers:**
    /// - NVIDIA Riva ASR (containerized, gRPC streaming)
    /// - Parakeet via NIM (containerized, gRPC streaming)
    /// - Canary via NIM (containerized, gRPC streaming, includes translation)
    /// - Whisper via NIM (containerized, gRPC offline only - not recommended for real-time)
    /// - Any custom provider implementing this interface
    /// </summary>
    public interface IASRProvider
    {
        /// <summary>
        /// Initializes the ASR provider.
        /// </summary>
        bool Initialize(string endpointURL);

        /// <summary>
        /// Requests ASR transcription asynchronously.
        /// </summary>
        void RequestASRTranscription(ASRRequest request, Action<ASRResponse> callback);

        /// <summary>
        /// Checks if the ASR provider is available and ready to process requests.
        /// </summary>
        bool IsAvailable();

        /// <summary>
        /// Gets the name of the ASR provider.
        /// </summary>
        string GetProviderName();

        /// <summary>
        /// Gets a list of models supported by this provider.
        /// </summary>
        List<string> GetSupportedModels();

        /// <summary>
        /// Gets whether this provider supports streaming recognition.
        /// </summary>
        bool SupportsStreaming();
    }
}

