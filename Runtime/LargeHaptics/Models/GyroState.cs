// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.
using System;
using UnityEngine;

namespace HoloCade.LargeHaptics.Models
{
	/// <summary>
	/// Continuous rotation gyroscope state (pitch and roll can exceed 360 degrees).
	/// Mirrors Unreal's FGyroState for struct-based UDP transmission.
	/// </summary>
	[Serializable]
	public struct GyroState
	{
		/// <summary>Pitch in degrees (continuous, unbounded)</summary>
		public float pitch;

		/// <summary>Roll in degrees (continuous, unbounded)</summary>
		public float roll;

		public GyroState(float pitch, float roll)
		{
			this.pitch = pitch;
			this.roll = roll;
		}

		/// <summary>
		/// Compute a new continuous rotation state from normalized input.
		/// Normalized inputs are joystick-style in range [-1, +1].
		/// </summary>
		/// <param name="normalizedPitch">-1 = pull back, +1 = push forward</param>
		/// <param name="normalizedRoll">-1 = left, +1 = right</param>
		/// <param name="deltaTime">Seconds since last update</param>
		/// <param name="currentPitch">Current absolute pitch (degrees)</param>
		/// <param name="currentRoll">Current absolute roll (degrees)</param>
		/// <param name="maxRotationSpeedDegPerSec">Max rotation speed to scale inputs</param>
		/// <returns>New absolute state with cumulative rotation applied</returns>
		public static GyroState FromNormalized(
			float normalizedPitch,
			float normalizedRoll,
			float deltaTime,
			float currentPitch,
			float currentRoll,
			float maxRotationSpeedDegPerSec)
		{
			// Clamp safety on inputs
			normalizedPitch = Mathf.Clamp(normalizedPitch, -1f, 1f);
			normalizedRoll  = Mathf.Clamp(normalizedRoll,  -1f, 1f);
			deltaTime       = Mathf.Max(0f, deltaTime);

			// Convert normalized input to delta degrees for this frame
			float deltaPitch = normalizedPitch * maxRotationSpeedDegPerSec * deltaTime;
			float deltaRoll  = normalizedRoll  * maxRotationSpeedDegPerSec * deltaTime;

			return new GyroState(
				currentPitch + deltaPitch,
				currentRoll + deltaRoll
			);
		}

		/// <summary>
		/// Returns a wrapped representation where each axis is in [0, 360).
		/// Useful for UI display without affecting the unbounded internal state.
		/// </summary>
		public GyroState ToWrapped360()
		{
			return new GyroState(
				WrapAngle360(pitch),
				WrapAngle360(roll)
			);
		}

		private static float WrapAngle360(float degrees)
		{
			float wrapped = degrees % 360f;
			if (wrapped < 0f) wrapped += 360f;
			return wrapped;
		}
	}
}




