// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.ProLighting
{
    /// <summary>Factory for creating fixture drivers based on fixture type</summary>
    public static class FixtureDriverFactory
    {
        public static IFixtureDriver Create(HoloCadeDMXFixtureType type)
        {
            return type switch
            {
                HoloCadeDMXFixtureType.Dimmable => new FixtureDriverDimmable(),
                HoloCadeDMXFixtureType.RGB => new FixtureDriverRGB(),
                HoloCadeDMXFixtureType.RGBW => new FixtureDriverRGBW(),
                HoloCadeDMXFixtureType.MovingHead => new FixtureDriverMovingHead(),
                HoloCadeDMXFixtureType.Custom => new FixtureDriverCustom(),
                _ => new FixtureDriverDimmable()
            };
        }
    }
}

