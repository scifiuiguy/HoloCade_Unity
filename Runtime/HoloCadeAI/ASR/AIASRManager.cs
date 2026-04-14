// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using HoloCade.VOIP;

namespace HoloCade.HoloCadeAI
{
    /// <summary>
    /// Generic configuration for ASR (Automatic Speech Recognition)
    /// </summary>
    [Serializable]
    public class AIASRConfig
    {
        public bool enableASR = true;
        public string localASREndpointURL = "localhost:50051";
        public bool useLocalASR = true;
        public string languageCode = "en-US";
        public float minAudioDuration = 0.5f;
        public float maxAudioDuration = 10.0f;
        public bool autoStartContainer = false;
        public ContainerConfig containerConfig = new ContainerConfig();
    }

    /// <summary>
    /// Generic ASR Manager Component
    /// 
    /// Base class for managing Automatic Speech Recognition (ASR).
    /// Provides generic ASR functionality without experience-specific logic.
    /// 
    /// Subclasses should extend this for experience-specific needs:
    /// - Auto-triggering improv responses after transcription
    /// - Experience-specific transcription handling
    /// - Experience-specific source identification
    /// 
    /// ARCHITECTURE:
    /// - Runs on dedicated server (receives audio from VOIP)
    /// - Receives audio streams via IVOIPAudioVisitor pattern
    /// - Converts speech to text using local ASR (NVIDIA Riva, Parakeet, Canary via gRPC)
    /// - Broadcasts transcription events for experience-specific handling
    /// </summary>
    public class AIASRManager : MonoBehaviour, IVOIPAudioVisitor
    {
        [Header("Configuration")]
        public AIASRConfig asrConfig = new AIASRConfig();

        [Header("Events")]
        public UnityEvent<int, string> OnTranscriptionComplete = new UnityEvent<int, string>();
        public UnityEvent<int> OnTranscriptionStarted = new UnityEvent<int>();

        protected bool isInitialized = false;
        protected Dictionary<int, List<float>> sourceAudioBuffers = new Dictionary<int, List<float>>();
        protected Dictionary<int, float> sourceAudioStartTimes = new Dictionary<int, float>();
        protected Dictionary<int, bool> sourceSpeakingStates = new Dictionary<int, bool>();
        protected Dictionary<int, bool> sourceTranscribingStates = new Dictionary<int, bool>();
        protected float voiceActivityTimer = 0.0f;
        public float silenceThreshold = 1.0f;
        protected ASRProviderManager asrProviderManager;

        protected virtual void Start()
        {
            // ASRProviderManager will be created in InitializeASRManager
        }

        /// <summary>
        /// Initialize the ASR manager
        /// </summary>
        public virtual bool InitializeASRManager()
        {
            if (asrProviderManager == null)
                asrProviderManager = gameObject.AddComponent<ASRProviderManager>();

            isInitialized = true;
            return true;
        }

        /// <summary>
        /// Process audio data from a source (called by VOIPManager when audio is received)
        /// </summary>
        public virtual void ProcessAudio(int sourceId, float[] audioData, int sampleRate)
        {
            if (!isInitialized || !asrConfig.enableASR)
                return;

            // Buffer audio
            if (!sourceAudioBuffers.ContainsKey(sourceId))
            {
                sourceAudioBuffers[sourceId] = new List<float>();
                sourceAudioStartTimes[sourceId] = Time.time;
                sourceSpeakingStates[sourceId] = false;
            }

            sourceAudioBuffers[sourceId].AddRange(audioData);

            // Detect voice activity
            if (DetectVoiceActivity(audioData))
            {
                sourceSpeakingStates[sourceId] = true;
                voiceActivityTimer = 0.0f;
            }
            else
            {
                voiceActivityTimer += Time.deltaTime;
                if (voiceActivityTimer >= silenceThreshold && sourceSpeakingStates[sourceId])
                {
                    // Trigger transcription
                    TriggerTranscriptionForSource(sourceId);
                }
            }
        }

        // IVOIPAudioVisitor implementation
        public virtual void OnPlayerAudioReceived(int playerId, float[] audioData, int sampleRate, Vector3 position)
        {
            ProcessAudio(playerId, audioData, sampleRate);
        }

        /// <summary>
        /// Manually trigger transcription for a source (if audio buffering is enabled)
        /// </summary>
        public virtual void TriggerTranscriptionForSource(int sourceId)
        {
            if (!sourceAudioBuffers.ContainsKey(sourceId) || sourceAudioBuffers[sourceId].Count == 0)
                return;

            if (sourceTranscribingStates.ContainsKey(sourceId) && sourceTranscribingStates[sourceId])
                return; // Already transcribing

            sourceTranscribingStates[sourceId] = true;
            OnTranscriptionStarted?.Invoke(sourceId);

            // Convert float audio to bytes for ASR
            var audioBytes = ConvertPCMFloatToBytes(sourceAudioBuffers[sourceId].ToArray(), 48000);

            RequestASRTranscription(sourceId, audioBytes, 48000);
        }

        /// <summary>
        /// Check if a source is currently being transcribed
        /// </summary>
        public bool IsSourceBeingTranscribed(int sourceId)
        {
            return sourceTranscribingStates.ContainsKey(sourceId) && sourceTranscribingStates[sourceId];
        }

        /// <summary>
        /// Request ASR transcription from local ASR service
        /// Subclasses can override for custom transcription handling
        /// </summary>
        protected virtual void RequestASRTranscription(int sourceId, byte[] audioData, int sampleRate)
        {
            if (asrProviderManager == null)
            {
                Debug.LogWarning("AIASRManager: ASRProviderManager not initialized");
                return;
            }

            var request = new ASRRequest
            {
                audioData = audioData,
                sampleRate = sampleRate,
                languageCode = asrConfig.languageCode,
                useStreaming = true,
                endpointURL = asrConfig.localASREndpointURL
            };

            asrProviderManager.RequestTranscription(request, (response) =>
            {
                HandleTranscriptionResult(sourceId, response.transcribedText);
            });
        }

        /// <summary>
        /// Handle transcription result
        /// Subclasses can override for experience-specific handling (e.g., trigger improv)
        /// </summary>
        protected virtual void HandleTranscriptionResult(int sourceId, string transcribedText)
        {
            sourceTranscribingStates[sourceId] = false;
            ClearSourceAudioBuffer(sourceId);
            OnTranscriptionComplete?.Invoke(sourceId, transcribedText);
        }

        /// <summary>
        /// Detect voice activity in audio buffer (simple energy-based VAD)
        /// </summary>
        protected virtual bool DetectVoiceActivity(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                return false;

            float energy = 0.0f;
            foreach (var sample in audioData)
                energy += sample * sample;

            energy /= audioData.Length;
            return energy > 0.01f; // Threshold for voice activity
        }

        /// <summary>
        /// Clear audio buffer for a source
        /// </summary>
        protected virtual void ClearSourceAudioBuffer(int sourceId)
        {
            if (sourceAudioBuffers.ContainsKey(sourceId))
                sourceAudioBuffers[sourceId].Clear();
            sourceSpeakingStates[sourceId] = false;
            voiceActivityTimer = 0.0f;
        }

        /// <summary>
        /// Convert PCM float audio data to uint8 bytes for gRPC
        /// </summary>
        protected byte[] ConvertPCMFloatToBytes(float[] floatAudio, int sampleRate)
        {
            var bytes = new byte[floatAudio.Length * 2]; // 16-bit PCM
            for (int i = 0; i < floatAudio.Length; i++)
            {
                short sample = (short)(floatAudio[i] * 32767.0f);
                bytes[i * 2] = (byte)(sample & 0xFF);
                bytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return bytes;
        }
    }
}

