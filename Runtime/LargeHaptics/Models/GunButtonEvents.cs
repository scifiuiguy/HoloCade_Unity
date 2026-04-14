// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace HoloCade.LargeHaptics.Models
{
    /// <summary>
    /// Gun button events (fast updates, sent on state change)
    /// 
    /// Data model for efficient struct-based UDP transmission of button states from all 4 gun stations.
    /// Used by GunshipExperience for low-latency button event handling.
    /// 
    /// This is a Model (M) in MVC architecture - pure data structure.
    /// Designed for UDP transport via HoloCade binary protocol (Channel 310).
    /// 
    /// Binary compatibility: Must match firmware struct exactly:
    /// - bool Button0State[4] (4 bytes)
    /// - bool Button1State[4] (4 bytes)
    /// - unsigned long Timestamp (4 bytes, uint32)
    /// Total: 12 bytes
    /// 
    /// Update rate: Configurable (default 20 Hz / 50ms)
    /// 
    /// IMPORTANT: Field order must match Unreal and firmware exactly for binary compatibility!
    /// </summary>
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GunButtonEvents
    {
        /// <summary>Left thumb button state per station (0-3)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public bool[] Button0State;

        /// <summary>Right thumb button state per station (0-3)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public bool[] Button1State;

        /// <summary>Timestamp when events occurred (milliseconds since boot)</summary>
        public uint Timestamp;

        public GunButtonEvents(bool[] button0State, bool[] button1State, uint timestamp)
        {
            Button0State = button0State ?? new bool[4];
            Button1State = button1State ?? new bool[4];
            Timestamp = timestamp;
        }
    }
}










