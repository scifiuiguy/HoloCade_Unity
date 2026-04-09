// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;

namespace HoloCade.ExperienceTemplates.SuperheroFlight.Models
{
    /// <summary>
    /// Superhero Flight System Telemetry
    /// 
    /// System health, temperatures, fault states, and redundancy status.
    /// Sent from ECU to game engine on Channel 311 at 1 Hz (1000ms).
    /// </summary>
    [Serializable]
    public struct SuperheroFlightTelemetry
    {
        /// <summary>Front winch motor temperature (°C)</summary>
        public float FrontWinchMotorTemp;

        /// <summary>Rear winch motor temperature (°C)</summary>
        public float RearWinchMotorTemp;

        /// <summary>Front winch fault detected</summary>
        public bool FrontWinchFault;

        /// <summary>Rear winch fault detected</summary>
        public bool RearWinchFault;

        /// <summary>Winch redundancy status (true = both winches operational, false = degraded mode)</summary>
        public bool WinchRedundancyStatus;

        /// <summary>System voltage (V)</summary>
        public float SystemVoltage;

        /// <summary>System current (A)</summary>
        public float SystemCurrent;

        /// <summary>Timestamp when telemetry was captured (milliseconds since boot)</summary>
        public uint Timestamp;

        public SuperheroFlightTelemetry(float frontWinchMotorTemp = 0.0f, float rearWinchMotorTemp = 0.0f,
            bool frontWinchFault = false, bool rearWinchFault = false, bool winchRedundancyStatus = true,
            float systemVoltage = 0.0f, float systemCurrent = 0.0f, uint timestamp = 0)
        {
            FrontWinchMotorTemp = frontWinchMotorTemp;
            RearWinchMotorTemp = rearWinchMotorTemp;
            FrontWinchFault = frontWinchFault;
            RearWinchFault = rearWinchFault;
            WinchRedundancyStatus = winchRedundancyStatus;
            SystemVoltage = systemVoltage;
            SystemCurrent = systemCurrent;
            Timestamp = timestamp;
        }
    }
}

