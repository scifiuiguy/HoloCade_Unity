// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.AIFacemask
{
    /// <summary>
    /// Script execution mode
    /// </summary>
    public enum ACEScriptMode
    {
        /// <summary>
        /// Pre-baked script (text → TTS → Audio-to-Face, all cached on server)
        /// </summary>
        PreBaked,

        /// <summary>
        /// Real-time/improv mode (text → TTS → Audio-to-Face, generated on-the-fly)
        /// </summary>
        RealTime
    }

    /// <summary>
    /// Voice configuration for NVIDIA ACE Text-to-Speech
    /// </summary>
    public enum ACEVoiceType
    {
        /// <summary>
        /// Default voice (NVIDIA ACE default)
        /// </summary>
        Default,

        /// <summary>
        /// Male voice
        /// </summary>
        Male,

        /// <summary>
        /// Female voice
        /// </summary>
        Female,

        /// <summary>
        /// Custom voice model ID
        /// </summary>
        Custom
    }

    /// <summary>
    /// Emotion preset for NVIDIA ACE Audio-to-Face
    /// Influences facial expression generation during audio-to-face conversion
    /// </summary>
    public enum ACEEmotionPreset
    {
        /// <summary>
        /// Neutral emotion
        /// </summary>
        Neutral,

        /// <summary>
        /// Happy/excited
        /// </summary>
        Happy,

        /// <summary>
        /// Sad/melancholic
        /// </summary>
        Sad,

        /// <summary>
        /// Angry/intense
        /// </summary>
        Angry,

        /// <summary>
        /// Surprised/shocked
        /// </summary>
        Surprised,

        /// <summary>
        /// Fearful/anxious
        /// </summary>
        Fearful,

        /// <summary>
        /// Disgusted
        /// </summary>
        Disgusted,

        /// <summary>
        /// Custom emotion (specified in script)
        /// </summary>
        Custom
    }
}



