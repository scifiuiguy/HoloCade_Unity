// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using HoloCade.LargeHaptics.Models;
using HoloCade.EmbeddedSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HoloCade.LargeHaptics
{
	/// <summary>
	/// Unity 2DOF gyroscope controller: handles HOTAS input and UDP transmission to ECU.
	/// Mirrors Unreal's U2DOFGyroPlatformController feature set.
	/// </summary>
	[DisallowMultipleComponent]
	public class GyroscopeController2DOF : MonoBehaviour
	{
		[Header("Embedded Device (UDP)")]
		[SerializeField] private EmbeddedDeviceConfig deviceConfig = new EmbeddedDeviceConfig
		{
			deviceAddress = "192.168.1.120",
			port = 8888,
			securityLevel = SecurityLevel.None
		};
		[SerializeField] private SerialDeviceController device;

		[Header("Gyro Settings")]
		[Range(10f, 180f)]
		[SerializeField] private float maxRotationSpeedDegPerSec = 90f;
		[SerializeField] private bool enableContinuousPitch = true;
		[SerializeField] private bool enableContinuousRoll = true;

		[Header("HOTAS")]
		[SerializeField] private bool enableHOTAS = true;
		[Range(0.1f, 5f)]
		[SerializeField] private float joystickSensitivity = 1.0f;
		[SerializeField] private bool invertPitch = false;
		[SerializeField] private bool invertRoll = false;

		[Header("Gravity Reset (ECU Parameters)")]
		[SerializeField] private bool gravityReset = false;   // Ch9
		[Range(1f, 180f)]
		[SerializeField] private float resetSpeed = 30f;      // Ch10
		[Range(0f, 60f)]
		[SerializeField] private float resetIdleTimeout = 5f; // Ch11

		// Runtime state
		private float currentPitch;
		private float currentRoll;
		private Vector2 joystickInput;
		private float throttleInput;
		private float pedalInput;
		private bool hotasConnected;

		private void Awake()
		{
			if (device == null)
			{
				device = GetComponent<SerialDeviceController>();
				if (device == null)
				{
					device = gameObject.AddComponent<SerialDeviceController>();
				}
			}
		}

		private void Start()
		{
			// Initialize device connection
			if (device != null && !device.IsDeviceConnected())
			{
				device.InitializeDevice(deviceConfig);
			}

			// Send gravity reset parameters on start (match Unreal behavior)
			SendGravityResetParameters();
		}

		private void Update()
		{
			if (!enableHOTAS)
			{
				return;
			}

			ReadHOTASInput();

			// Apply sensitivity and inversions
			float pitchNorm = joystickInput.y * joystickSensitivity;
			float rollNorm  = joystickInput.x * joystickSensitivity;
			if (invertPitch) pitchNorm = -pitchNorm;
			if (invertRoll)  rollNorm  = -rollNorm;

			var next = GyroState.FromNormalized(
				normalizedPitch: enableContinuousPitch ? pitchNorm : 0f,
				normalizedRoll:  enableContinuousRoll  ? rollNorm  : 0f,
				deltaTime: Time.deltaTime,
				currentPitch: currentPitch,
				currentRoll: currentRoll,
				maxRotationSpeedDegPerSec: maxRotationSpeedDegPerSec
			);

			currentPitch = next.pitch;
			currentRoll  = next.roll;

			// Transmit as struct packet (Channel 102)
			SendGyroStruct(next, 102);
		}

		/// <summary>
		/// Initialize the controller and connect to ECU
		/// </summary>
		public bool Initialize()
		{
			if (device == null)
			{
				device = GetComponent<SerialDeviceController>();
				if (device == null)
				{
					device = gameObject.AddComponent<SerialDeviceController>();
				}
			}

			if (!device.IsDeviceConnected())
			{
				return device.InitializeDevice(deviceConfig);
			}
			return true;
		}

		/// <summary>
		/// Set maximum rotation speed (degrees per second)
		/// </summary>
		public void SetMaxRotationSpeed(float speed)
		{
			maxRotationSpeedDegPerSec = Mathf.Clamp(speed, 10f, 180f);
		}

		/// <summary>
		/// Set joystick sensitivity multiplier
		/// </summary>
		public void SetJoystickSensitivity(float sensitivity)
		{
			joystickSensitivity = Mathf.Clamp(sensitivity, 0.1f, 5f);
		}

		/// <summary>
		/// Enable/disable HOTAS input processing
		/// </summary>
		public void SetEnableHOTAS(bool enable)
		{
			enableHOTAS = enable;
		}

		/// <summary>
		/// Send gravity reset parameters (bReset, speed, idle timeout) to ECU.
		/// Channels: 9 (bool), 10 (float), 11 (float)
		/// </summary>
		public void SendGravityResetParameters()
		{
			if (device == null || !device.IsDeviceConnected()) return;
			device.SendBool(9, gravityReset);
			device.SendFloat(10, resetSpeed);
			device.SendFloat(11, resetIdleTimeout);
		}

		/// <summary>
		/// Send gravity reset parameters with custom values (for FlightSimExperience)
		/// </summary>
		public void SendGravityResetParams(bool enable, float speed, float timeout)
		{
			if (device == null || !device.IsDeviceConnected()) return;
			device.SendBool(9, enable);
			device.SendFloat(10, speed);
			device.SendFloat(11, timeout);
		}

		/// <summary>
		/// Send float value on specified channel
		/// </summary>
		public void SendFloat(int channel, float value)
		{
			if (device == null || !device.IsDeviceConnected()) return;
			device.SendFloat(channel, value);
		}

		/// <summary>
		/// Send bool value on specified channel
		/// </summary>
		public void SendBool(int channel, bool value)
		{
			if (device == null || !device.IsDeviceConnected()) return;
			device.SendBool(channel, value);
		}

		/// <summary>
		/// Send gyro state as binary struct on specified channel (default 102).
		/// Packet payload: [float pitch][float roll] little-endian
		/// </summary>
		public void SendGyroStruct(GyroState state, int channel = 102)
		{
			if (device == null || !device.IsDeviceConnected()) return;

			byte[] buffer = new byte[8];
			WriteFloatLE(buffer, 0, state.pitch);
			WriteFloatLE(buffer, 4, state.roll);
			device.SendBytes(channel, buffer);
		}

		/// <summary>
		/// Return to neutral (sends Ch8 true)
		/// </summary>
		public void ReturnToNeutral()
		{
			if (device == null || !device.IsDeviceConnected()) return;
			device.SendBool(8, true);
			currentPitch = 0f;
			currentRoll = 0f;
		}

		/// <summary>
		/// Emergency stop (sends Ch7 true)
		/// </summary>
		public void EmergencyStop()
		{
			if (device == null || !device.IsDeviceConnected()) return;
			device.SendBool(7, true);
		}

		/// <summary>
		/// Public getters for experience/UI
		/// </summary>
		public Vector2 GetHOTASJoystickInput() => joystickInput;
		public float GetHOTASThrottleInput() => throttleInput;
		public float GetHOTASPedalInput() => pedalInput;
		public bool IsHOTASConnected() => hotasConnected;

		private void ReadHOTASInput()
		{
			hotasConnected = false;
			joystickInput = Vector2.zero;
			throttleInput = 0f;
			pedalInput = 0f;

#if ENABLE_INPUT_SYSTEM
			// Prefer Joystick (generic HID such as HOTAS)
			if (Joystick.current != null)
			{
				var stick = Joystick.current.stick;
				if (stick != null)
				{
					joystickInput = stick.ReadValue();
					hotasConnected = true;
				}

				// Try common additional axes if available
				// Many HOTAS expose throttle/pedals as separate devices; default to 0 if absent
				// Unity's generic Joystick doesn't provide standard throttle/pedals properties
			}

			// Fallback to gamepad for dev convenience
			if (!hotasConnected && Gamepad.current != null)
			{
				joystickInput = Gamepad.current.leftStick.ReadValue();
				hotasConnected = true;
			}
#else
			// Legacy fallback (optional): map to Horizontal/Vertical for quick testing
			float x = Input.GetAxis("Horizontal");
			float y = Input.GetAxis("Vertical");
			if (Mathf.Abs(x) > 0.01f || Mathf.Abs(y) > 0.01f)
			{
				joystickInput = new Vector2(x, y);
				hotasConnected = true;
			}
#endif
		}

		private static void WriteFloatLE(byte[] buffer, int offset, float value)
		{
			byte[] tmp = BitConverter.GetBytes(value);
			if (!BitConverter.IsLittleEndian)
			{
				Array.Reverse(tmp);
			}
			Buffer.BlockCopy(tmp, 0, buffer, offset, 4);
		}
	}
}

