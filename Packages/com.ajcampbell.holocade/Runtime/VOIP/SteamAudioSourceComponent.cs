// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.VOIP
{
    /// <summary>
    /// Steam Audio Source Component
    /// 
    /// Per-user audio source component that handles Steam Audio spatialization.
    /// One component is created per remote player for spatial audio rendering.
    /// 
    /// Handles:
    /// - Opus audio decoding (from Mumble)
    /// - Steam Audio HRTF processing
    /// - 3D spatialization based on player positions
    /// - Audio playback via Unity's audio system
    /// 
    /// This component will interface with the Steam Audio plugin (git submodule)
    /// to provide high-quality 3D HRTF spatialization.
    /// 
    /// Audio Output:
    /// - Routes to OS-selected audio output device (via Unity's audio system)
    /// - HMD headphones (Oculus, Vive, etc.) appear as standard audio devices
    /// - Works with any audio output device recognized by Windows/OS
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SteamAudioSourceComponent : MonoBehaviour
    {
        [Header("Audio")]
        [Tooltip("Audio output volume (0.0 to 1.0)")]
        [Range(0f, 1f)]
        public float volume = 1.0f;

        private AudioSource audioSource;
        private Vector3 currentRemotePosition = Vector3.zero;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.volume = volume;
        }

        private void Start()
        {
            // Initialize Steam Audio
            if (!InitializeSteamAudio())
            {
                Debug.LogError("SteamAudioSourceComponent: Failed to initialize Steam Audio plugin");
            }
        }

        /// <summary>
        /// Process incoming Opus audio data
        /// </summary>
        /// <param name="opusData">Compressed Opus audio data from Mumble</param>
        /// <param name="remotePosition">Position of the remote player</param>
        public void ProcessAudioData(byte[] opusData, Vector3 remotePosition)
        {
            currentRemotePosition = remotePosition;

            // Decode Opus to PCM
            float[] pcmData;
            if (!DecodeOpus(opusData, out pcmData))
            {
                Debug.LogWarning("SteamAudioSourceComponent: Failed to decode Opus data");
                return;
            }

            // Get listener position (HMD)
            Vector3 listenerPosition = Vector3.zero;
            Quaternion listenerRotation = Quaternion.identity;

            // TODO: Get from HMD interface or player controller
            // For now, use Camera.main or XR origin
            if (Camera.main != null)
            {
                listenerPosition = Camera.main.transform.position;
                listenerRotation = Camera.main.transform.rotation;
            }

            // Process through Steam Audio HRTF
            float[] binauralData;
            if (!ProcessHRTF(pcmData, remotePosition, listenerPosition, listenerRotation, out binauralData))
            {
                Debug.LogWarning("SteamAudioSourceComponent: Failed to process HRTF");
                return;
            }

            // Play binaural audio
            PlayBinauralAudio(binauralData, 48000); // Mumble uses 48kHz
        }

        /// <summary>
        /// Update audio source position for spatialization
        /// </summary>
        /// <param name="remotePosition">Position of the remote player</param>
        /// <param name="listenerPosition">Position of the local listener (HMD)</param>
        /// <param name="listenerRotation">Rotation of the local listener (HMD)</param>
        public void UpdatePosition(Vector3 remotePosition, Vector3 listenerPosition, Quaternion listenerRotation)
        {
            currentRemotePosition = remotePosition;

            // TODO: Update Steam Audio source position
            // if (steamAudioSource != null)
            // {
            //     // Update source transform in Steam Audio
            //     steamAudioSource.SetPosition(remotePosition);
            //     steamAudioSource.SetListenerTransform(listenerPosition, listenerRotation);
            // }
        }

        /// <summary>
        /// Set output volume
        /// </summary>
        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }

            // TODO: Update Steam Audio source volume
            // if (steamAudioSource != null)
            // {
            //     steamAudioSource.SetVolume(volume);
            // }
        }

        private bool DecodeOpus(byte[] opusData, out float[] pcmData)
        {
            pcmData = null;

            // TODO: Decode Opus using MumbleLink plugin or Opus library
            // This will be implemented when MumbleLink submodule is added
            // 
            // Example:
            // if (MumbleLinkInterface != null)
            // {
            //     return MumbleLinkInterface.DecodeOpus(opusData, out pcmData);
            // }

            Debug.LogWarning("SteamAudioSourceComponent: Opus decoding not yet implemented. Using placeholder.");
            return false; // Placeholder
        }

        private bool ProcessHRTF(float[] pcmData, Vector3 sourcePosition, Vector3 listenerPosition, 
            Quaternion listenerRotation, out float[] binauralData)
        {
            binauralData = null;

            // TODO: Process through Steam Audio HRTF
            // This will be implemented when Steam Audio submodule is added
            // 
            // Example:
            // if (steamAudioSource != null)
            // {
            //     return steamAudioSource.ProcessHRTF(pcmData, sourcePosition, 
            //         listenerPosition, listenerRotation, out binauralData);
            // }

            Debug.LogWarning("SteamAudioSourceComponent: Steam Audio HRTF processing not yet implemented. Using placeholder.");
            return false; // Placeholder
        }

        private void PlayBinauralAudio(float[] binauralData, int sampleRate)
        {
            // TODO: Play binaural audio via Unity's audio system
            // This will create an AudioClip from the binaural data and play it
            // 
            // Example:
            // AudioClip clip = AudioClip.Create("VOIPAudio", binauralData.Length / 2, 2, sampleRate, false);
            // clip.SetData(binauralData, 0);
            // audioSource.clip = clip;
            // audioSource.Play();

            Debug.LogWarning("SteamAudioSourceComponent: Audio playback not yet implemented. Using placeholder.");
        }

        private bool InitializeSteamAudio()
        {
            // TODO: Initialize Steam Audio plugin
            // This will be implemented when Steam Audio submodule is added
            // 
            // Example:
            // steamAudioSource = ...;
            // return steamAudioSource != null;

            Debug.LogWarning("SteamAudioSourceComponent: Steam Audio plugin not yet integrated. Using placeholder.");
            return true; // Placeholder - return true for now
        }

        private void CleanupSteamAudio()
        {
            // TODO: Cleanup Steam Audio source
            // if (steamAudioSource != null)
            // {
            //     // Destroy source
            //     steamAudioSource = null;
            // }
        }

        private void OnDestroy()
        {
            CleanupSteamAudio();
        }
    }
}



