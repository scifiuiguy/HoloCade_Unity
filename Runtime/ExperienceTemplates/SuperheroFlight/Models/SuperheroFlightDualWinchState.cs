// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;

namespace HoloCade.ExperienceTemplates.SuperheroFlight.Models
{
    /// <summary>
    /// Dual-Winch State Telemetry
    /// 
    /// Real-time state of both front and rear winches.
    /// Sent from ECU to game engine on Channel 310 at 20 Hz (50ms).
    /// </summary>
    [Serializable]
    public struct SuperheroFlightDualWinchState
    {
        /// <summary>Front winch position (inches, relative to standingGroundHeight)</summary>
        public float FrontWinchPosition;

        /// <summary>Front winch speed (inches/second)</summary>
        public float FrontWinchSpeed;

        /// <summary>Front winch tension (load cell reading in lbs or N)</summary>
        public float FrontWinchTension;

        /// <summary>Rear winch position (inches, relative to standingGroundHeight)</summary>
        public float RearWinchPosition;

        /// <summary>Rear winch speed (inches/second)</summary>
        public float RearWinchSpeed;

        /// <summary>Rear winch tension (load cell reading in lbs or N)</summary>
        public float RearWinchTension;

        /// <summary>Current game state (0=standing, 1=hovering, 2=flight-up, 3=flight-forward, 4=flight-down)</summary>
        public int GameState;

        /// <summary>Safety state (true = safe to operate, false = safety interlock active)</summary>
        public bool SafetyState;

        /// <summary>Timestamp when state was captured (milliseconds since boot)</summary>
        public uint Timestamp;

        public SuperheroFlightDualWinchState(float frontWinchPosition = 0.0f, float frontWinchSpeed = 0.0f, float frontWinchTension = 0.0f,
            float rearWinchPosition = 0.0f, float rearWinchSpeed = 0.0f, float rearWinchTension = 0.0f,
            int gameState = 0, bool safetyState = true, uint timestamp = 0)
        {
            FrontWinchPosition = frontWinchPosition;
            FrontWinchSpeed = frontWinchSpeed;
            FrontWinchTension = frontWinchTension;
            RearWinchPosition = rearWinchPosition;
            RearWinchSpeed = rearWinchSpeed;
            RearWinchTension = rearWinchTension;
            GameState = gameState;
            SafetyState = safetyState;
            Timestamp = timestamp;
        }
    }
}

