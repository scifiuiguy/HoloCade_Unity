// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;

namespace HoloCade.ProLighting
{
    /// <summary>
    /// IDMXTransport - Interface for DMX transport implementations
    /// Provides a polymorphic interface for different DMX transport methods:
    /// - USB DMX: Direct serial connection to USB-to-DMX interface
    /// - Art-Net: Network-based DMX over UDP
    /// - sACN (future): Alternative network protocol
    /// </summary>
    public interface IDMXTransport
    {
        bool Initialize();
        void Shutdown();
        bool IsConnected { get; }
        void SendDMX(int universe, byte[] dmxData);
    }

    /// <summary>
    /// Transport setup result - contains transport and any additional setup needed
    /// Uses object references to avoid incomplete type issues - ownership transferred to controller
    /// </summary>
    public class TransportSetupResult
    {
        public ArtNetManager ArtNetManager;
        public USBDMXTransport USBDMXTransport;
        public IDMXTransport Transport;
        public Func<ProLightingController, bool> SetupCallback;

        public IDMXTransport GetTransport() => Transport;
        public bool IsValid => Transport != null;
    }

    /// <summary>
    /// Factory for creating DMX transports based on configuration
    /// </summary>
    public static class DMXTransportFactory
    {
        public static TransportSetupResult CreateTransport(HoloCadeProLightingConfig config)
        {
            var result = new TransportSetupResult();
            switch (config.DMXMode)
            {
                case HoloCadeDMXMode.USBDMX:
                    var usbTransport = new USBDMXTransport(config.COMPort, config.BaudRate);
                    if (!usbTransport.Initialize())
                    {
                        UnityEngine.Debug.LogError("DMXTransportFactory: USB DMX transport initialization failed");
                        return result;
                    }
                    result.USBDMXTransport = usbTransport;
                    result.Transport = usbTransport;
                    result.SetupCallback = controller => { UnityEngine.Debug.LogWarning("ProLightingController: USB DMX transport initialized (stub - not yet fully implemented)"); return true; };
                    break;

                case HoloCadeDMXMode.ArtNet:
                    var artNetManager = new ArtNetManager();
                    if (!artNetManager.Initialize(config.ArtNetIPAddress, config.ArtNetPort, config.ArtNetNet, config.ArtNetSubNet))
                    {
                        UnityEngine.Debug.LogError("DMXTransportFactory: Art-Net manager configuration failed");
                        return result;
                    }
                    // Initialize via interface (explicit interface implementation)
                    IDMXTransport transport = artNetManager;
                    if (!transport.Initialize())
                    {
                        UnityEngine.Debug.LogError("DMXTransportFactory: Art-Net transport initialization failed");
                        return result;
                    }
                    result.ArtNetManager = artNetManager;
                    result.Transport = artNetManager;
                    var configCopy = config;
                    result.SetupCallback = controller =>
                    {
                        if (controller == null || controller.ArtNetManager == null) return false;
                        UnityEngine.Debug.Log($"ProLightingController: Art-Net initialized (IP: {configCopy.ArtNetIPAddress}:{configCopy.ArtNetPort}, Net: {configCopy.ArtNetNet}, SubNet: {configCopy.ArtNetSubNet})");
                        controller.RDMService = new RDMService();
                        controller.RDMService.Initialize(configCopy.RDMPollInterval);
                        return true;
                    };
                    break;

                case HoloCadeDMXMode.SACN:
                    UnityEngine.Debug.LogWarning("DMXTransportFactory: sACN not yet implemented");
                    break;
            }
            return result;
        }
    }
}

