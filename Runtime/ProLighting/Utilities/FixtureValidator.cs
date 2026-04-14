// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.ProLighting
{
    /// <summary>Validates fixture registration to prevent overlaps and invalid configurations</summary>
    public static class FixtureValidator
    {
        public static bool ValidateRegister(HoloCadeDMXFixture candidate, FixtureRegistry registry, out string error)
        {
            error = string.Empty;
            if (candidate.VirtualFixtureID <= 0) { error = "Invalid VirtualFixtureID"; return false; }
            if (candidate.DMXChannel < 1 || candidate.DMXChannel > 512) { error = "Invalid DMX channel"; return false; }
            var channels = candidate.ChannelCount > 0 ? candidate.ChannelCount : RequiredChannels(candidate);
            var end = candidate.DMXChannel + channels - 1;
            if (end > 512) { error = "Fixture exceeds universe size"; return false; }
            foreach (var id in registry.GetIDs())
            {
                var existing = registry.Find(id);
                if (existing == null) continue;
                if (existing.Universe != candidate.Universe) continue;
                var existingEnd = existing.DMXChannel + existing.ChannelCount - 1;
                var overlap = !(end < existing.DMXChannel || candidate.DMXChannel > existingEnd);
                if (overlap)
                {
                    error = $"Overlaps with fixture {existing.VirtualFixtureID}";
                    return false;
                }
            }
            return true;
        }

        static int RequiredChannels(HoloCadeDMXFixture f)
        {
            return f.FixtureType switch
            {
                HoloCadeDMXFixtureType.Dimmable => 1,
                HoloCadeDMXFixtureType.RGB => 3,
                HoloCadeDMXFixtureType.RGBW => 4,
                HoloCadeDMXFixtureType.MovingHead => 8,
                HoloCadeDMXFixtureType.Custom => f.CustomChannelMapping == null ? 1 : Mathf.Max(1, f.CustomChannelMapping.Count),
                _ => 1
            };
        }
    }
}

