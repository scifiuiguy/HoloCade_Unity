// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;

namespace HoloCade.ProLighting
{
    /// <summary>Simple registry for fixtures and RDM mappings</summary>
    public class FixtureRegistry
    {
        private readonly Dictionary<int, HoloCadeDMXFixture> fixtures = new Dictionary<int, HoloCadeDMXFixture>();
        private readonly Dictionary<int, string> virtualToRDM = new Dictionary<int, string>();
        private readonly Dictionary<string, int> rdmToVirtual = new Dictionary<string, int>();

        public bool Register(HoloCadeDMXFixture fixture)
        {
            if (fixtures.ContainsKey(fixture.VirtualFixtureID)) return false;
            fixtures[fixture.VirtualFixtureID] = fixture;
            return true;
        }

        public void Unregister(int virtualFixtureID)
        {
            fixtures.Remove(virtualFixtureID);
            if (virtualToRDM.TryGetValue(virtualFixtureID, out var uid))
            {
                virtualToRDM.Remove(virtualFixtureID);
                rdmToVirtual.Remove(uid);
            }
        }

        public HoloCadeDMXFixture Find(int virtualFixtureID) => fixtures.TryGetValue(virtualFixtureID, out var fixture) ? fixture : null;

        public HoloCadeDMXFixture FindMutable(int virtualFixtureID) => Find(virtualFixtureID); // In C#, classes are reference types, so Find works for mutable access

        public int[] GetIDs()
        {
            var keys = new int[fixtures.Count];
            fixtures.Keys.CopyTo(keys, 0);
            return keys;
        }

        public void MapRDM(int virtualFixtureID, string uid)
        {
            virtualToRDM[virtualFixtureID] = uid;
            rdmToVirtual[uid] = virtualFixtureID;
        }

        public bool TryGetRDMUID(int virtualFixtureID, out string uid) => virtualToRDM.TryGetValue(virtualFixtureID, out uid);

        public void Reset()
        {
            fixtures.Clear();
            virtualToRDM.Clear();
            rdmToVirtual.Clear();
        }
    }
}

