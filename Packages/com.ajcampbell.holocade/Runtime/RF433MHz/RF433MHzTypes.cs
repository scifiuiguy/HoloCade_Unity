// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using UnityEngine;

namespace HoloCade.RF433MHz
{
    /// <summary>
    /// 433MHz USB Receiver Module Types
    /// </summary>
    public enum RF433MHzReceiverType
    {
        RTL_SDR,    // RTL-SDR USB Dongle
        CC1101,     // CC1101 USB Module
        RFM69,      // RFM69 USB Module
        RFM95,      // RFM95 USB Module
        Generic     // Generic 433MHz USB Receiver
    }

    /// <summary>
    /// RF433MHz Receiver Configuration
    /// </summary>
    [Serializable]
    public class RF433MHzReceiverConfig
    {
        [Tooltip("USB receiver module type")]
        public RF433MHzReceiverType ReceiverType = RF433MHzReceiverType.Generic;

        [Tooltip("USB device path (COM port on Windows, /dev/ttyUSB0 on Linux, varies by module)")]
        public string USBDevicePath = "COM3";

        [Tooltip("Enable rolling code validation (prevents replay attacks)")]
        public bool EnableRollingCodeValidation = true;

        [Tooltip("Rolling code seed (must match remote firmware)")]
        public uint RollingCodeSeed = 0x12345678;

        [Tooltip("Enable replay attack prevention (reject duplicate codes)")]
        public bool EnableReplayAttackPrevention = true;

        [Tooltip("Replay attack window (ms) - reject codes within this window of last code")]
        public int ReplayAttackWindow = 100;

        [Tooltip("Enable AES encryption (for custom solutions with encrypted remotes)")]
        public bool EnableAESEncryption = false;

        [Tooltip("AES encryption key (128-bit = 16 bytes, 256-bit = 32 bytes) - stored as hex string")]
        public string AESEncryptionKey = "";  // Hex string: "0123456789ABCDEF0123456789ABCDEF" for AES-128

        [Tooltip("AES key size (128 or 256 bits)")]
        public int AESKeySize = 128;  // 128 or 256

        [Tooltip("Update rate for button event polling (Hz)")]
        [Range(1.0f, 100.0f)]
        public float UpdateRate = 20.0f;  // 20 Hz default
    }

    /// <summary>
    /// Button Event from 433MHz Remote
    /// </summary>
    [Serializable]
    public struct RF433MHzButtonEvent
    {
        /// <summary>Button code (0-255, mapped from remote)</summary>
        public int ButtonCode;

        /// <summary>Button state (true = pressed, false = released)</summary>
        public bool Pressed;

        /// <summary>Rolling code (if rolling code validation enabled)</summary>
        public uint RollingCode;

        /// <summary>Timestamp when event occurred (seconds since receiver initialization)</summary>
        public float Timestamp;

        public RF433MHzButtonEvent(int buttonCode, bool pressed, uint rollingCode = 0, float timestamp = 0.0f)
        {
            ButtonCode = buttonCode;
            Pressed = pressed;
            RollingCode = rollingCode;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Learned Button Information
    /// Tracks a button that has been learned during learning mode
    /// </summary>
    [Serializable]
    public class RF433MHzLearnedButton
    {
        /// <summary>Button code (0-255, unique identifier)</summary>
        public int ButtonCode = 0;

        /// <summary>Rolling code seed for this button (for validation)</summary>
        public uint RollingCodeSeed = 0;

        /// <summary>Timestamp when button was learned</summary>
        public float LearnedTimestamp = 0.0f;

        /// <summary>Assigned function name (empty if not assigned)</summary>
        public string AssignedFunctionName = "";

        /// <summary>Whether this button is currently mapped/active</summary>
        public bool IsMapped = false;
    }

    /// <summary>
    /// Button Function Mapping
    /// Maps a button code to a specific function/action
    /// </summary>
    [Serializable]
    public class RF433MHzButtonMapping
    {
        /// <summary>Button code</summary>
        public int ButtonCode = 0;

        /// <summary>Function name (e.g., "HeightUp", "HeightDown", "Calibrate")</summary>
        public string FunctionName = "";

        /// <summary>Whether this mapping is active</summary>
        public bool IsActive = true;
    }
}

