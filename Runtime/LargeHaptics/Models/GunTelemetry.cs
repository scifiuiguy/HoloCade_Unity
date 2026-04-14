// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace HoloCade.LargeHaptics.Models
{
    /// <summary>
    /// Gun telemetry (slow updates, sent periodically)
    /// 
    /// Data model for efficient struct-based UDP transmission of telemetry from all 4 gun stations.
    /// Used by GunshipExperience for monitoring gun system health, temperatures, and firing state.
    /// 
    /// This is a Model (M) in MVC architecture - pure data structure.
    /// Designed for UDP transport via HoloCade binary protocol (Channel 311).
    /// 
    /// Binary compatibility: Must match firmware struct exactly:
    /// - float ActiveSolenoidTemp[4] (16 bytes)
    /// - float DriverModuleTemp[4] (16 bytes)
    /// - uint8 ActiveSolenoidID[4] (4 bytes)
    /// - uint8 NumSolenoids[4] (4 bytes)
    /// - bool ThermalShutdown[4] (4 bytes)
    /// - float PWMThrottle[4] (16 bytes)
    /// - bool FireCommandActive[4] (4 bytes)
    /// - float FireIntensity[4] (16 bytes)
    /// - uint32 FireDuration[4] (16 bytes)
    /// - bool PlaySessionActive (1 byte, may be padded to 4)
    /// - bool CanFire[4] (4 bytes)
    /// - bool StationConnected[4] (4 bytes)
    /// - uint32 Timestamp (4 bytes)
    /// Total: ~113 bytes (with padding)
    /// 
    /// Update rate: Configurable (default 1 Hz / 1000ms)
    /// 
    /// IMPORTANT: Field order must match Unreal and firmware exactly for binary compatibility!
    /// </summary>
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GunTelemetry
    {
        /// <summary>Temperature of active solenoid per station (°C)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] ActiveSolenoidTemp;

        /// <summary>PWM driver module temperature per station (°C)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] DriverModuleTemp;

        /// <summary>Currently active solenoid ID per station (0 to N-1)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] ActiveSolenoidID;

        /// <summary>Total number of solenoids per station (N)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] NumSolenoids;

        /// <summary>Thermal shutdown active per station</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public bool[] ThermalShutdown;

        /// <summary>Current PWM throttle factor per station (0.5-1.0)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] PWMThrottle;

        /// <summary>Currently firing per station</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public bool[] FireCommandActive;

        /// <summary>Current fire intensity per station (0.0-1.0)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] FireIntensity;

        /// <summary>Fire pulse duration per station (milliseconds)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] FireDuration;

        /// <summary>Play session authorization (same for all stations)</summary>
        public bool PlaySessionActive;

        /// <summary>Computed: Can fire per station (PlaySessionActive && !ThermalShutdown)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public bool[] CanFire;

        /// <summary>Station is sending telemetry (not timed out)</summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public bool[] StationConnected;

        /// <summary>Timestamp when telemetry was collected (milliseconds since boot)</summary>
        public uint Timestamp;

        public GunTelemetry(
            float[] activeSolenoidTemp,
            float[] driverModuleTemp,
            byte[] activeSolenoidID,
            byte[] numSolenoids,
            bool[] thermalShutdown,
            float[] pwmThrottle,
            bool[] fireCommandActive,
            float[] fireIntensity,
            uint[] fireDuration,
            bool playSessionActive,
            bool[] canFire,
            bool[] stationConnected,
            uint timestamp)
        {
            ActiveSolenoidTemp = activeSolenoidTemp ?? new float[4];
            DriverModuleTemp = driverModuleTemp ?? new float[4];
            ActiveSolenoidID = activeSolenoidID ?? new byte[4];
            NumSolenoids = numSolenoids ?? new byte[4];
            ThermalShutdown = thermalShutdown ?? new bool[4];
            PWMThrottle = pwmThrottle ?? new float[4];
            FireCommandActive = fireCommandActive ?? new bool[4];
            FireIntensity = fireIntensity ?? new float[4];
            FireDuration = fireDuration ?? new uint[4];
            PlaySessionActive = playSessionActive;
            CanFire = canFire ?? new bool[4];
            StationConnected = stationConnected ?? new bool[4];
            Timestamp = timestamp;
        }
    }
}










