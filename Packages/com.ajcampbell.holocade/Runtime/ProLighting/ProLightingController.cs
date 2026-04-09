// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HoloCade.ProLighting
{
    /// <summary>
    /// HoloCade ProLighting Controller
    /// Hardware-agnostic DMX lighting control via USB DMX interfaces or Art-Net.
    /// Supports all common fixture types (dimmable, RGB, moving heads, etc.) with a unified API.
    /// 
    /// For UIToolkit integration, subscribe directly to the native events on the services:
    /// - FixtureService.OnIntensityChanged
    /// - FixtureService.OnColorChanged
    /// - RDMService.OnDiscoveredEvent
    /// - RDMService.OnWentOfflineEvent
    /// - RDMService.OnCameOnlineEvent
    /// - ArtNetManager.OnNodeDiscovered
    /// </summary>
    [AddComponentMenu("HoloCade/ProLighting Controller")]
    public class ProLightingController : MonoBehaviour
    {
        [Tooltip("ProLighting configuration")]
        public HoloCadeProLightingConfig Config = new HoloCadeProLightingConfig();

        // Services (expose native events for UIToolkit subscription)
        public FixtureService FixtureService { get; private set; }
        public RDMService RDMService { get; internal set; }
        public ArtNetManager ArtNetManager { get; internal set; }

        // DMX universe data (shared between controller for flushing and FixtureService for fixture operations)
        private readonly UniverseBuffer universeBuffer = new UniverseBuffer();
        private readonly Dictionary<int, string> virtualFixtureToRDMUIDMap = new Dictionary<int, string>();
        private readonly Dictionary<string, int> rdmUIDToVirtualFixtureMap = new Dictionary<string, int>();
        private float rdmPollTimer = 0f;
        private bool isInitialized = false;
        private bool isConnected = false;

        // Transport/Manager Instances
        private IDMXTransport activeTransport;
        private USBDMXTransport usbDMXTransport;
        private ArtNetManager artNetManager;

        void Start()
        {
            FixtureService = new FixtureService(universeBuffer);
            if (!string.IsNullOrEmpty(Config.COMPort) || !string.IsNullOrEmpty(Config.ArtNetIPAddress))
                InitializeDMX(Config);
        }

        void OnDestroy()
        {
            Shutdown();
        }

        void Update()
        {
            if (!isConnected) return;

            // Update fades via service
            if (FixtureService != null)
            {
                FixtureService.TickFades(Time.deltaTime, (id, newIntensity) =>
                {
                    if (FixtureService != null)
                    {
                        var univ = FixtureService.SetIntensityById(id, newIntensity);
                        if (univ >= 0)
                            FlushDMXUniverse(univ);
                    }
                });
            }

            // Flush universes
            foreach (var universe in universeBuffer.GetUniverses())
                FlushDMXUniverse(universe);

            // Tick discovery
            if (artNetManager != null && Config.DMXMode == HoloCadeDMXMode.ArtNet)
                artNetManager.Tick(Time.deltaTime);

            // RDM polling
            if (Config.EnableRDM && isConnected && RDMService != null)
            {
                rdmPollTimer += Time.deltaTime;
                if (rdmPollTimer >= Config.RDMPollInterval)
                {
                    rdmPollTimer = 0f;
                    PollRDMFixtures(Time.deltaTime);
                    var wentOffline = new List<int>();
                    var removed = new List<string>();
                    RDMService.Prune(Config.RDMPollInterval * 3f, Config.RDMPollInterval * 10f, wentOffline, removed, rdmUIDToVirtualFixtureMap);
                }
                RDMService.Tick(Time.deltaTime);
            }
        }

        public bool InitializeDMX(HoloCadeProLightingConfig inConfig)
        {
            Config = inConfig;
            if (isInitialized)
            {
                Debug.LogWarning("ProLightingController: Already initialized");
                return false;
            }

            var success = false;
            var setupResult = DMXTransportFactory.CreateTransport(Config);
            if (!setupResult.IsValid)
            {
                Debug.LogError("ProLightingController: Transport creation failed");
                return false;
            }

            activeTransport = setupResult.GetTransport();
            if (setupResult.USBDMXTransport != null)
                usbDMXTransport = setupResult.USBDMXTransport;
            if (setupResult.ArtNetManager != null)
                artNetManager = setupResult.ArtNetManager;

            if (setupResult.SetupCallback != null)
            {
                if (!setupResult.SetupCallback(this))
                {
                    Debug.LogError("ProLightingController: Transport setup callback failed");
                    return false;
                }
            }

            success = true;
            isInitialized = true;
            isConnected = true;
            Debug.Log($"ProLightingController: Initialized (Mode: {Config.DMXMode})");
            return success;
        }

        public bool IsDMXConnected() => isConnected;

        public void Shutdown()
        {
            if (activeTransport != null)
            {
                activeTransport.Shutdown();
                activeTransport = null;
            }
            if (usbDMXTransport != null)
            {
                usbDMXTransport.Shutdown();
                usbDMXTransport = null;
            }
            if (artNetManager != null)
            {
                artNetManager.Shutdown();
                artNetManager = null;
            }
            isInitialized = false;
            isConnected = false;
            universeBuffer.Reset();
            virtualFixtureToRDMUIDMap.Clear();
            rdmUIDToVirtualFixtureMap.Clear();
            rdmPollTimer = 0f;
        }

        void FlushDMXUniverse(int universe)
        {
            var universeData = universeBuffer.GetUniverse(universe);
            if (universeData == null || universeData.Length != 512) return;
            if (activeTransport != null && activeTransport.IsConnected)
                activeTransport.SendDMX(universe, universeData);
        }

        public void DiscoverArtNetNodes()
        {
            if (Config.DMXMode != HoloCadeDMXMode.ArtNet || artNetManager == null)
            {
                Debug.LogWarning("ProLightingController: Art-Net discovery unavailable");
                return;
            }
            artNetManager.SendArtPoll();
        }

        public void DiscoverRDMFixtures()
        {
            if (!Config.EnableRDM)
            {
                Debug.LogWarning("ProLightingController: RDM is not enabled in configuration");
                return;
            }
            if (!CheckRDMSupport())
            {
                Debug.LogWarning("ProLightingController: RDM not supported by current DMX interface");
                return;
            }
            Debug.Log("ProLightingController: Starting RDM fixture discovery...");
            foreach (var universe in universeBuffer.GetUniverses())
            {
                var discoveredFixtures = new List<HoloCadeDiscoveredFixture>();
                if (SendRDMDiscoveryPacket(universe))
                {
                    if (ReceiveRDMDiscoveryResponse(universe, discoveredFixtures))
                    {
                        foreach (var fixture in discoveredFixtures)
                        {
                            var isNew = RDMService != null && RDMService.AddOrUpdate(fixture);
                            if (isNew)
                                Debug.Log($"ProLightingController: Discovered RDM fixture: {fixture.ModelName} ({fixture.RDMUID}) at DMX {fixture.DMXAddress}");
                        }
                    }
                }
            }
            if (RDMService != null)
                Debug.Log($"ProLightingController: RDM discovery complete. Found {RDMService.GetAll().Count} fixtures");
        }

        public List<HoloCadeArtNetNode> GetDiscoveredArtNetNodes()
        {
            if (artNetManager != null)
                return artNetManager.GetNodes();
            return new List<HoloCadeArtNetNode>();
        }

        public List<HoloCadeDiscoveredFixture> GetDiscoveredRDMFixtures() => RDMService != null ? RDMService.GetAll() : new List<HoloCadeDiscoveredFixture>();

        public int AutoRegisterDiscoveredFixture(string rdmUID)
        {
            if (RDMService == null || !RDMService.TryGet(rdmUID, out var temp))
            {
                Debug.LogWarning($"ProLightingController: RDM fixture {rdmUID} not found in discovered fixtures");
                return -1;
            }
            if (rdmUIDToVirtualFixtureMap.TryGetValue(rdmUID, out var existingVirtualID))
            {
                Debug.Log($"ProLightingController: RDM fixture {rdmUID} already registered as virtual fixture {existingVirtualID}");
                return existingVirtualID;
            }

            var fixture = new HoloCadeDMXFixture
            {
                VirtualFixtureID = FixtureService != null ? FixtureService.GetNextVirtualFixtureID() : -1,
                FixtureType = temp.FixtureType,
                DMXChannel = temp.DMXAddress,
                Universe = temp.Universe,
                ChannelCount = temp.ChannelCount,
                RDMUID = rdmUID,
                RDMCapable = true
            };
            if (fixture.VirtualFixtureID < 0) return -1;

            if (FixtureService != null && FixtureService.ValidateAndRegister(fixture))
            {
                virtualFixtureToRDMUIDMap[fixture.VirtualFixtureID] = rdmUID;
                rdmUIDToVirtualFixtureMap[rdmUID] = fixture.VirtualFixtureID;
                Debug.Log($"ProLightingController: Auto-registered RDM fixture {rdmUID} as virtual fixture {fixture.VirtualFixtureID}");
                return fixture.VirtualFixtureID;
            }
            return -1;
        }

        public bool IsRDMSupported() => Config.EnableRDM && CheckRDMSupport();

        public bool IsFixtureRDMCapable(int virtualFixtureID) => FixtureService != null && FixtureService.IsFixtureRDMCapable(virtualFixtureID);

        bool CheckRDMSupport()
        {
            if (Config.DMXMode == HoloCadeDMXMode.USBDMX)
                return !string.IsNullOrEmpty(Config.COMPort);
            else if (Config.DMXMode == HoloCadeDMXMode.ArtNet)
                return true;
            return false;
        }

        bool SendRDMDiscoveryPacket(int universe)
        {
            Debug.Log($"ProLightingController: RDM discovery packet (stubbed) for universe {universe}");
            return false; // NOOP: Not yet implemented
        }

        bool ReceiveRDMDiscoveryResponse(int universe, List<HoloCadeDiscoveredFixture> outFixtures)
        {
            return false; // NOOP: Not yet implemented
        }

        bool SendRDMGetRequest(int universe, string rdmUID, int pid, out byte[] response)
        {
            Debug.Log($"ProLightingController: RDM GET request (stubbed) - Universe {universe}, UID {rdmUID}, PID {pid}");
            response = null;
            return false; // NOOP: Not yet implemented
        }

        bool ParseRDMResponse(byte[] responseData, ref HoloCadeDiscoveredFixture fixture)
        {
            return false; // NOOP: Not yet implemented
        }

        void PollRDMFixtures(float deltaTime)
        {
            if (!Config.EnableRDM) return;
            foreach (var pair in virtualFixtureToRDMUIDMap)
            {
                var virtualFixtureID = pair.Key;
                var rdmUID = pair.Value;
                var fixture = FixtureService?.FindFixture(virtualFixtureID);
                if (fixture == null) continue;
                var universe = Config.DMXMode == HoloCadeDMXMode.USBDMX ? 0 : fixture.Universe;
                if (SendRDMGetRequest(universe, rdmUID, 0x00F0, out var response))
                {
                    var discoveredFixture = new HoloCadeDiscoveredFixture();
                    if (ParseRDMResponse(response, ref discoveredFixture))
                    {
                        if (RDMService != null)
                            RDMService.AddOrUpdate(discoveredFixture);
                        if (fixture != null && discoveredFixture.DMXAddress != fixture.DMXChannel)
                        {
                            Debug.Log($"ProLightingController: RDM fixture {rdmUID} moved from DMX {fixture.DMXChannel} to {discoveredFixture.DMXAddress}");
                            var registeredFixture = FixtureService?.FindFixtureMutable(virtualFixtureID);
                            if (registeredFixture != null)
                                registeredFixture.DMXChannel = discoveredFixture.DMXAddress;
                        }
                        if (RDMService != null)
                            RDMService.MarkOnline(rdmUID, virtualFixtureID);
                    }
                }
                else
                {
                    if (RDMService != null)
                        RDMService.MarkOffline(rdmUID, virtualFixtureID);
                }
            }
        }
    }
}

