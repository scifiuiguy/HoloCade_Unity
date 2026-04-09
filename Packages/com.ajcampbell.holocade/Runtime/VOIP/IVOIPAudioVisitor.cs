// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.VOIP
{
    /// <summary>
    /// Visitor interface for VOIP audio events
    /// 
    /// Allows experience templates and custom components to subscribe to VOIP audio events
    /// without creating direct dependencies between modules. This visitor pattern keeps the
    /// VOIP module decoupled from experience-specific modules.
    /// 
    /// USAGE FOR EXPERIENCE TEMPLATES:
    /// 
    /// If you're building a custom experience template that needs to process player voice
    /// (e.g., speech recognition, voice commands, audio analysis), implement this interface:
    /// 
    /// 1. Create a component in your experience template
    /// 2. Implement IVOIPAudioVisitor interface
    /// 3. Register with VOIPManager in your experience's InitializeExperienceImpl():
    ///    ```csharp
    ///    if (VOIPManager voipManager = GetComponent<VOIPManager>())
    ///    {
    ///        voipManager.RegisterAudioVisitor(this);
    ///    }
    ///    ```
    /// 4. Receive audio events via OnPlayerAudioReceived()
    /// 
    /// EXAMPLE IMPLEMENTATION:
    /// ```csharp
    /// public class MyAudioProcessor : MonoBehaviour, IVOIPAudioVisitor
    /// {
    ///     public void OnPlayerAudioReceived(int playerId, float[] audioData, int sampleRate, Vector3 position)
    ///     {
    ///         // Process audio for your custom use case
    ///         ProcessVoiceCommand(audioData, sampleRate);
    ///     }
    ///     
    ///     private void ProcessVoiceCommand(float[] audioData, int sampleRate)
    ///     {
    ///         // Your custom audio processing logic
    ///     }
    /// }
    /// ```
    /// 
    /// BENEFITS:
    /// - Decoupled architecture: VOIP module doesn't know about your experience
    /// - Multiple visitors: Multiple components can subscribe to the same audio stream
    /// - Clean separation: Your experience code stays in your experience module
    /// - Reusable: Same pattern works for any experience template
    /// 
    /// See AIFacemaskExperience for a complete example using ASRManager.
    /// </summary>
    public interface IVOIPAudioVisitor
    {
        /// <summary>
        /// Called when player audio is received via VOIP/Mumble
        /// </summary>
        /// <param name="playerId">ID of the player who spoke</param>
        /// <param name="audioData">PCM audio data (decoded from Opus)</param>
        /// <param name="sampleRate">Audio sample rate (typically 48000 for Mumble)</param>
        /// <param name="position">3D position of the player (for spatial audio)</param>
        void OnPlayerAudioReceived(int playerId, float[] audioData, int sampleRate, Vector3 position);
    }
}



