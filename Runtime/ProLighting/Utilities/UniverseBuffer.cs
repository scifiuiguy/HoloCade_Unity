// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;

namespace HoloCade.ProLighting
{
    /// <summary>
    /// UniverseBuffer - Manages per-universe DMX channel data
    /// 
    /// DMX Universes Explained:
    /// 
    /// A DMX "universe" is a collection of 512 channels (0-511). Each channel can hold a value from 0-255.
    /// 
    /// Why Multiple Universes?
    /// - Traditional USB DMX interfaces typically support only ONE universe (512 channels total)
    ///   - These are usually mapped to Universe 0 in this system
    /// - Art-Net and sACN (E1.31) network protocols support MULTIPLE universes
    ///   - Art-Net can address up to 32,767 universes (organized as Net:SubNet:Universe)
    ///   - This allows large lighting systems with thousands of fixtures
    /// 
    /// Usage:
    /// - Fixtures specify which universe they're on via HoloCadeDMXFixture.Universe
    /// - FixtureService writes fixture data to this buffer (universe-agnostic)
    /// - Controller reads from this buffer and flushes to the active transport (USB DMX or Art-Net)
    /// - USB DMX transport typically only uses Universe 0, but the abstraction supports multiple
    /// - Art-Net transport can send any universe number to network nodes
    /// 
    /// Example:
    ///   - USB DMX: All fixtures on Universe 0, buffer stores 512 channels
    ///   - Art-Net: Fixtures on Universe 0, 1, 2, etc. Buffer stores 512 channels per universe
    /// 
    /// This buffer is transport-agnostic - it's the core DMX data store used by all transports.
    /// </summary>
    public class UniverseBuffer
    {
        private readonly Dictionary<int, byte[]> universeToData = new Dictionary<int, byte[]>();

        public void EnsureUniverse(int universe)
        {
            if (!universeToData.ContainsKey(universe))
            {
                var data = new byte[512];
                universeToData[universe] = data;
            }
        }

        public void SetChannel(int universe, int channel1Based, byte value)
        {
            if (channel1Based < 1 || channel1Based > 512) return;
            EnsureUniverse(universe);
            universeToData[universe][channel1Based - 1] = value;
        }

        public byte GetChannel(int universe, int channel1Based)
        {
            if (!universeToData.TryGetValue(universe, out var data) || channel1Based < 1 || channel1Based > 512) return 0;
            return data[channel1Based - 1];
        }

        public byte[] GetUniverse(int universe) => universeToData.TryGetValue(universe, out var data) ? data : null;

        public int[] GetUniverses()
        {
            var keys = new int[universeToData.Count];
            universeToData.Keys.CopyTo(keys, 0);
            return keys;
        }

        public void Reset() => universeToData.Clear();
    }
}

