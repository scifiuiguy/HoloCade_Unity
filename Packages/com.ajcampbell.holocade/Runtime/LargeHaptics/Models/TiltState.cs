// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using System.Runtime.InteropServices;

namespace HoloCade.LargeHaptics.Models
{
    /// <summary>
    /// Tilt state (pitch and roll only)
    /// 
    /// Data model for efficient struct-based UDP transmission of platform tilt.
    /// Used by 4DOF motion platforms: Gunship, MovingPlatform, CarSim.
    /// 
    /// This is a Model (M) in MVC architecture - pure data structure with built-in mapping functions.
    /// Designed for UDP transport via HoloCade binary protocol (channel-agnostic struct packets).
    /// 
    /// Storage: Degrees (clamped to hardware limits)
    /// Input: Normalized joystick values (-1.0 to +1.0)
    /// Output: Degrees for hardware control
    /// 
    /// IMPORTANT: Field order must match Unreal exactly for binary compatibility!
    /// </summary>
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TiltState
    {
        /// <summary>Pitch angle in degrees (stored value, clamped to hardware limits)</summary>
        public float Pitch;

        /// <summary>Roll angle in degrees (stored value, clamped to hardware limits)</summary>
        public float Roll;

        public TiltState(float pitch, float roll)
        {
            Pitch = pitch;
            Roll = roll;
        }

        /// <summary>
        /// Create from normalized joystick input (-1.0 to +1.0)
        /// </summary>
        /// <param name="normalizedPitch">Joystick Y axis (-1.0 = full backward, +1.0 = full forward)</param>
        /// <param name="normalizedRoll">Joystick X axis (-1.0 = full left, +1.0 = full right)</param>
        /// <param name="maxPitchDegrees">Maximum pitch angle in degrees (hardware limit)</param>
        /// <param name="maxRollDegrees">Maximum roll angle in degrees (hardware limit)</param>
        /// <returns>Tilt struct with degrees mapped from normalized input</returns>
        public static TiltState FromNormalized(float normalizedPitch, float normalizedRoll, float maxPitchDegrees, float maxRollDegrees)
        {
            return new TiltState(
                Mathf.Clamp(normalizedPitch, -1f, 1f) * maxPitchDegrees,
                Mathf.Clamp(normalizedRoll, -1f, 1f) * maxRollDegrees
            );
        }

        /// <summary>
        /// Convert to normalized joystick values (-1.0 to +1.0)
        /// </summary>
        /// <param name="maxPitchDegrees">Maximum pitch angle in degrees (hardware limit)</param>
        /// <param name="maxRollDegrees">Maximum roll angle in degrees (hardware limit)</param>
        /// <returns>Vector2 with normalized values (X = Roll, Y = Pitch)</returns>
        public Vector2 ToNormalized(float maxPitchDegrees, float maxRollDegrees)
        {
            return new Vector2(
                maxRollDegrees > 0f ? Mathf.Clamp(Roll / maxRollDegrees, -1f, 1f) : 0f,
                maxPitchDegrees > 0f ? Mathf.Clamp(Pitch / maxPitchDegrees, -1f, 1f) : 0f
            );
        }

        /// <summary>Convert pitch to radians</summary>
        public float GetPitchRadians() => Pitch * Mathf.Deg2Rad;

        /// <summary>Convert roll to radians</summary>
        public float GetRollRadians() => Roll * Mathf.Deg2Rad;
    }
}



