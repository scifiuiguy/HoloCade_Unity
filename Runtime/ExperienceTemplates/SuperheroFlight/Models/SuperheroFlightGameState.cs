// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.ExperienceTemplates.SuperheroFlight.Models
{
    /// <summary>
    /// Superhero Flight Experience Game States
    /// 
    /// Five distinct flight modes for the dual-winch suspended harness system.
    /// </summary>
    public enum SuperheroFlightGameState
    {
        /// <summary>Standing Mode - Player upright, feet on ground</summary>
        Standing,

        /// <summary>Hovering Mode - Player lifted to airHeight, upright position</summary>
        Hovering,

        /// <summary>Flight-Up Mode - Player lifted to airHeight, upright, arms pointing up</summary>
        FlightUp,

        /// <summary>Flight-Forward Mode - Player lifted to proneHeight, prone position, arms pointing forward</summary>
        FlightForward,

        /// <summary>Flight-Down Mode - Player lifted to airHeight, upright, arms pointing down</summary>
        FlightDown
    }
}

