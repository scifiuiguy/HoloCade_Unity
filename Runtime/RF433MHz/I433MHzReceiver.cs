// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;

namespace HoloCade.RF433MHz
{
    /// <summary>
    /// I433MHzReceiver - Interface for 433MHz USB receiver implementations
    /// 
    /// Provides a polymorphic interface for different USB receiver modules:
    /// - RTL-SDR: Software-defined radio USB dongle
    /// - CC1101: Dedicated 433MHz transceiver module with USB interface
    /// - RFM69/RFM95: LoRa/RF modules with USB interface (433MHz capable)
    /// - Generic: Off-the-shelf USB dongles available on Amazon/eBay
    /// 
    /// Each implementation handles module-specific drivers/APIs (libusb, serial/COM ports, proprietary SDKs)
    /// and exposes a unified interface for game server code.
    /// </summary>
    public interface I433MHzReceiver
    {
        /// <summary>
        /// Initialize receiver with configuration
        /// </summary>
        bool Initialize(RF433MHzReceiverConfig config);

        /// <summary>
        /// Shutdown receiver and close USB connection
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Check if receiver is connected and operational
        /// </summary>
        bool IsConnected();

        /// <summary>
        /// Get button events from receiver
        /// </summary>
        bool GetButtonEvents(List<RF433MHzButtonEvent> outEvents);

        /// <summary>
        /// Check if rolling code validation is enabled and valid
        /// </summary>
        bool IsRollingCodeValid();

        /// <summary>
        /// Get rolling code drift (difference between expected and received code)
        /// </summary>
        int GetRollingCodeDrift();

        /// <summary>
        /// Enable code learning mode (for pairing new remotes)
        /// </summary>
        void EnableLearningMode(float timeoutSeconds = 30.0f);

        /// <summary>
        /// Disable code learning mode
        /// </summary>
        void DisableLearningMode();

        /// <summary>
        /// Check if learning mode is active
        /// </summary>
        bool IsLearningModeActive();
    }
}

