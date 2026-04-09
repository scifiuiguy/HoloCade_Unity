// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using System.Runtime.InteropServices;

namespace HoloCade.LargeHaptics.Models
{
    /// <summary>
    /// Scissor lift state (Y and Z translations only)
    /// 
    /// Data model for efficient struct-based UDP transmission of scissor lift position.
    /// Used by 4DOF motion platforms: Gunship, MovingPlatform, CarSim.
    /// 
    /// This is a Model (M) in MVC architecture - pure data structure with built-in mapping functions.
    /// Designed for UDP transport via HoloCade binary protocol (channel-agnostic struct packets).
    /// 
    /// Storage: Centimeters (clamped to hardware limits)
    /// Input: Normalized values (-1.0 to +1.0)
    /// Output: Centimeters for hardware control
    /// 
    /// IMPORTANT: Field order must match Unreal exactly for binary compatibility!
    /// </summary>
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ScissorLiftState
    {
        /// <summary>Y translation in cm (forward/reverse, positive = forward, stored value)</summary>
        public float TranslationY;

        /// <summary>Z translation in cm (up/down, positive = up, stored value)</summary>
        public float TranslationZ;

        public ScissorLiftState(float translationY, float translationZ)
        {
            TranslationY = translationY;
            TranslationZ = translationZ;
        }

        /// <summary>
        /// Create from normalized input (-1.0 to +1.0)
        /// </summary>
        /// <param name="normalizedY">Forward/reverse (-1.0 = full reverse, +1.0 = full forward)</param>
        /// <param name="normalizedZ">Up/down (-1.0 = full down, +1.0 = full up)</param>
        /// <param name="maxTranslationY">Maximum Y translation in cm (hardware limit)</param>
        /// <param name="maxTranslationZ">Maximum Z translation in cm (hardware limit)</param>
        /// <returns>ScissorLiftState struct with cm mapped from normalized input</returns>
        public static ScissorLiftState FromNormalized(float normalizedY, float normalizedZ, float maxTranslationY, float maxTranslationZ)
        {
            return new ScissorLiftState(
                Mathf.Clamp(normalizedY, -1f, 1f) * maxTranslationY,
                Mathf.Clamp(normalizedZ, -1f, 1f) * maxTranslationZ
            );
        }

        /// <summary>
        /// Convert to normalized values (-1.0 to +1.0)
        /// </summary>
        /// <param name="maxTranslationY">Maximum Y translation in cm (hardware limit)</param>
        /// <param name="maxTranslationZ">Maximum Z translation in cm (hardware limit)</param>
        /// <returns>Vector2 with normalized values (X = TranslationY, Y = TranslationZ)</returns>
        public Vector2 ToNormalized(float maxTranslationY, float maxTranslationZ)
        {
            return new Vector2(
                maxTranslationY > 0f ? Mathf.Clamp(TranslationY / maxTranslationY, -1f, 1f) : 0f,
                maxTranslationZ > 0f ? Mathf.Clamp(TranslationZ / maxTranslationZ, -1f, 1f) : 0f
            );
        }
    }
}



