// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.ProLighting
{
    /// <summary>DMX Fixture Types</summary>
    public enum HoloCadeDMXFixtureType : byte
    {
        Dimmable = 0,
        RGB = 1,
        RGBW = 2,
        MovingHead = 3,
        Custom = 4
    }

    /// <summary>DMX Communication Mode</summary>
    public enum HoloCadeDMXMode : byte
    {
        USBDMX = 0,
        ArtNet = 1,
        SACN = 2
    }

    /// <summary>DMX Fixture Definition</summary>
    [Serializable]
    public class HoloCadeDMXFixture
    {
        [Tooltip("Virtual fixture ID (unique identifier for this fixture)")]
        public int VirtualFixtureID = 1;

        [Tooltip("Type of fixture (determines channel count and behavior)")]
        public HoloCadeDMXFixtureType FixtureType = HoloCadeDMXFixtureType.Dimmable;

        [Tooltip("DMX channel (1-512)")]
        [Range(1, 512)]
        public int DMXChannel = 1;

        [Tooltip("DMX universe (0-15)")]
        [Range(0, 15)]
        public int Universe = 0;

        [Tooltip("Number of DMX channels this fixture uses (1-512)")]
        [Range(1, 512)]
        public int ChannelCount = 1;

        [Tooltip("Custom channel mapping (only used for Custom/MovingHead types)")]
        public List<int> CustomChannelMapping = new List<int>();

        [Tooltip("RDM Unique ID (if RDM-capable)")]
        public string RDMUID = string.Empty;

        [Tooltip("Whether this fixture supports RDM")]
        public bool RDMCapable = false;
    }

    /// <summary>Discovered Art-Net Node</summary>
    [Serializable]
    public class HoloCadeArtNetNode
    {
        [Tooltip("Node IP address")]
        public string IPAddress = string.Empty;

        [Tooltip("Node name (from ArtPollReply)")]
        public string NodeName = string.Empty;

        [Tooltip("Node type description")]
        public string NodeType = string.Empty;

        [Tooltip("Number of DMX outputs (ports)")]
        public int OutputCount = 1;

        [Tooltip("Universes per output (typical: 1-4)")]
        public int UniversesPerOutput = 1;

        [Tooltip("Last time this node was seen")]
        public DateTime LastSeenTimestamp = DateTime.MinValue;
    }

    /// <summary>ProLighting Controller Configuration</summary>
    [Serializable]
    public class HoloCadeProLightingConfig
    {
        [Tooltip("Communication mode (USB DMX or Art-Net)")]
        public HoloCadeDMXMode DMXMode = HoloCadeDMXMode.USBDMX;

        // USB DMX Settings
        [Tooltip("COM port for USB DMX interface (e.g., \"COM3\" on Windows)")]
        public string COMPort = "COM3";

        [Tooltip("Baud rate for USB DMX (typically 57600 for ENTTEC)")]
        public int BaudRate = 57600;

        // Art-Net Settings
        [Tooltip("Art-Net target IP address (broadcast address for all nodes, or specific node IP)")]
        public string ArtNetIPAddress = "255.255.255.255";

        [Tooltip("Art-Net port (default: 6454)")]
        public int ArtNetPort = 6454;

        [Tooltip("Art-Net Net (0-127, default: 0)")]
        [Range(0, 127)]
        public int ArtNetNet = 0;

        [Tooltip("Art-Net SubNet (0-15, default: 0)")]
        [Range(0, 15)]
        public int ArtNetSubNet = 0;

        [Tooltip("Maximum universe number to support (0-15 per subnet, default: 0 = single universe)")]
        [Range(0, 15)]
        public int MaxUniverse = 0;

        // RDM Settings
        [Tooltip("Enable RDM (Remote Device Management) for fixture discovery and bidirectional sync")]
        public bool EnableRDM = false;

        [Tooltip("RDM polling interval in seconds (how often to query fixture status)")]
        [Range(0.1f, 10.0f)]
        public float RDMPollInterval = 0.5f;

        [Tooltip("RDM discovery timeout in seconds (how long to wait for discovery responses)")]
        [Range(1.0f, 30.0f)]
        public float RDMDiscoveryTimeout = 5.0f;

        [Tooltip("If true, only use RDM-capable fixtures (ignore non-RDM fixtures)")]
        public bool RDMOnlyMode = false;
    }

    /// <summary>Discovered RDM Fixture (from RDM discovery)</summary>
    [Serializable]
    public class HoloCadeDiscoveredFixture
    {
        [Tooltip("RDM Unique ID (64-bit, formatted as hex string like \"0x12345678ABCDEF01\")")]
        public string RDMUID = string.Empty;

        [Tooltip("Fixture manufacturer ID (from RDM)")]
        public int ManufacturerID = 0;

        [Tooltip("Fixture model ID (from RDM)")]
        public int ModelID = 0;

        [Tooltip("Manufacturer name (from RDM)")]
        public string ManufacturerName = string.Empty;

        [Tooltip("Fixture model name (from RDM)")]
        public string ModelName = string.Empty;

        [Tooltip("Current DMX address (from RDM)")]
        public int DMXAddress = 0;

        [Tooltip("Universe (0-based, from RDM)")]
        public int Universe = 0;

        [Tooltip("Number of DMX channels this fixture uses (from RDM)")]
        public int ChannelCount = 1;

        [Tooltip("Fixture type (inferred from model or user-specified)")]
        public HoloCadeDMXFixtureType FixtureType = HoloCadeDMXFixtureType.Dimmable;

        [Tooltip("Whether fixture is currently online (last RDM query succeeded)")]
        public bool IsOnline = true;

        [Tooltip("Last time this fixture was seen (for offline detection)")]
        public DateTime LastSeenTimestamp = DateTime.MinValue;

        [Tooltip("Virtual fixture ID (if mapped to UI widget, -1 if not mapped)")]
        public int VirtualFixtureID = -1;
    }
}

