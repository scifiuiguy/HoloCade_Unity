// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.ProLighting
{
    /// <summary>Interface for fixture drivers that apply DMX values to universe buffer</summary>
    public interface IFixtureDriver
    {
        void ApplyIntensity(HoloCadeDMXFixture fixture, float intensity, UniverseBuffer buffer);
        void ApplyColor(HoloCadeDMXFixture fixture, float red, float green, float blue, float white, UniverseBuffer buffer);
    }

    /// <summary>Dimmable fixture driver (1 channel - intensity only)</summary>
    public class FixtureDriverDimmable : IFixtureDriver
    {
        public void ApplyIntensity(HoloCadeDMXFixture fixture, float intensity, UniverseBuffer buffer)
        {
            var v = (byte)(Mathf.Clamp01(intensity) * 255f);
            buffer.SetChannel(fixture.Universe, fixture.DMXChannel, v);
        }

        public void ApplyColor(HoloCadeDMXFixture fixture, float red, float green, float blue, float white, UniverseBuffer buffer) { }
    }

    /// <summary>RGB fixture driver (3 channels - R, G, B)</summary>
    public class FixtureDriverRGB : IFixtureDriver
    {
        public void ApplyIntensity(HoloCadeDMXFixture fixture, float intensity, UniverseBuffer buffer)
        {
            var v = (byte)(Mathf.Clamp01(intensity) * 255f);
            buffer.SetChannel(fixture.Universe, fixture.DMXChannel, v);
        }

        public void ApplyColor(HoloCadeDMXFixture fixture, float red, float green, float blue, float white, UniverseBuffer buffer)
        {
            var baseCh = fixture.DMXChannel;
            buffer.SetChannel(fixture.Universe, baseCh + 0, (byte)(Mathf.Clamp01(red) * 255f));
            buffer.SetChannel(fixture.Universe, baseCh + 1, (byte)(Mathf.Clamp01(green) * 255f));
            buffer.SetChannel(fixture.Universe, baseCh + 2, (byte)(Mathf.Clamp01(blue) * 255f));
        }
    }

    /// <summary>RGBW fixture driver (4 channels - R, G, B, W)</summary>
    public class FixtureDriverRGBW : IFixtureDriver
    {
        public void ApplyIntensity(HoloCadeDMXFixture fixture, float intensity, UniverseBuffer buffer)
        {
            var v = (byte)(Mathf.Clamp01(intensity) * 255f);
            buffer.SetChannel(fixture.Universe, fixture.DMXChannel, v);
        }

        public void ApplyColor(HoloCadeDMXFixture fixture, float red, float green, float blue, float white, UniverseBuffer buffer)
        {
            var baseCh = fixture.DMXChannel;
            buffer.SetChannel(fixture.Universe, baseCh + 0, (byte)(Mathf.Clamp01(red) * 255f));
            buffer.SetChannel(fixture.Universe, baseCh + 1, (byte)(Mathf.Clamp01(green) * 255f));
            buffer.SetChannel(fixture.Universe, baseCh + 2, (byte)(Mathf.Clamp01(blue) * 255f));
            buffer.SetChannel(fixture.Universe, baseCh + 3, (byte)(Mathf.Clamp01(white) * 255f));
        }
    }

    /// <summary>Moving head fixture driver (variable channels, intensity at offset 2, RGB at 3-5)</summary>
    public class FixtureDriverMovingHead : IFixtureDriver
    {
        public void ApplyIntensity(HoloCadeDMXFixture fixture, float intensity, UniverseBuffer buffer)
        {
            var v = (byte)(Mathf.Clamp01(intensity) * 255f);
            var baseCh = fixture.DMXChannel;
            var offset = fixture.ChannelCount >= 3 ? 2 : 0;
            buffer.SetChannel(fixture.Universe, baseCh + offset, v);
        }

        public void ApplyColor(HoloCadeDMXFixture fixture, float red, float green, float blue, float white, UniverseBuffer buffer)
        {
            var baseCh = fixture.DMXChannel;
            buffer.SetChannel(fixture.Universe, baseCh + 3, (byte)(Mathf.Clamp01(red) * 255f));
            buffer.SetChannel(fixture.Universe, baseCh + 4, (byte)(Mathf.Clamp01(green) * 255f));
            buffer.SetChannel(fixture.Universe, baseCh + 5, (byte)(Mathf.Clamp01(blue) * 255f));
        }
    }

    /// <summary>Custom fixture driver (uses custom channel mapping)</summary>
    public class FixtureDriverCustom : IFixtureDriver
    {
        public void ApplyIntensity(HoloCadeDMXFixture fixture, float intensity, UniverseBuffer buffer)
        {
            if (fixture.CustomChannelMapping == null || fixture.CustomChannelMapping.Count == 0) return;
            var v = (byte)(Mathf.Clamp01(intensity) * 255f);
            buffer.SetChannel(fixture.Universe, fixture.DMXChannel, v);
        }

        public void ApplyColor(HoloCadeDMXFixture fixture, float red, float green, float blue, float white, UniverseBuffer buffer)
        {
            if (fixture.CustomChannelMapping == null || fixture.CustomChannelMapping.Count < 3) return;
            var baseCh = fixture.DMXChannel;
            var ro = fixture.CustomChannelMapping[0] - 1;
            var go = fixture.CustomChannelMapping[1] - 1;
            var bo = fixture.CustomChannelMapping[2] - 1;
            if (ro >= 0) buffer.SetChannel(fixture.Universe, baseCh + ro, (byte)(Mathf.Clamp01(red) * 255f));
            if (go >= 0) buffer.SetChannel(fixture.Universe, baseCh + go, (byte)(Mathf.Clamp01(green) * 255f));
            if (bo >= 0) buffer.SetChannel(fixture.Universe, baseCh + bo, (byte)(Mathf.Clamp01(blue) * 255f));
        }
    }
}

