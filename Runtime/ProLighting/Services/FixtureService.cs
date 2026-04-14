// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.ProLighting
{
    /// <summary>
    /// FixtureService - Encapsulates fixture registration, validation, driver application, fades, and buffer updates.
    /// </summary>
    public class FixtureService
    {
        private readonly UniverseBuffer buffer;
        private readonly FixtureRegistry registry = new FixtureRegistry();
        private readonly FadeEngine fade = new FadeEngine();
        private RDMService rdmService;
        private Dictionary<int, string> virtualToUID;
        private Dictionary<string, int> uidToVirtual;
        private int nextVirtualFixtureID = 1;

        public event Action<int, float> OnIntensityChanged;
        public event Action<int, float, float, float> OnColorChanged;

        public UniverseBuffer GetUniverseBuffer() => buffer;

        public FixtureService(UniverseBuffer inBuffer) => buffer = inBuffer;

        public void SetRDMContext(RDMService inRDMService, Dictionary<int, string> inVirtualToUID, Dictionary<string, int> inUIDToVirtual)
        {
            rdmService = inRDMService;
            virtualToUID = inVirtualToUID;
            uidToVirtual = inUIDToVirtual;
        }

        public bool ValidateAndRegister(HoloCadeDMXFixture fixture)
        {
            if (!FixtureValidator.ValidateRegister(fixture, registry, out var err))
            {
                Debug.LogError($"FixtureService: Register failed - {err}");
                return false;
            }
            buffer.EnsureUniverse(fixture.Universe);
            var valid = new HoloCadeDMXFixture
            {
                VirtualFixtureID = fixture.VirtualFixtureID,
                FixtureType = fixture.FixtureType,
                DMXChannel = fixture.DMXChannel,
                Universe = fixture.Universe,
                ChannelCount = fixture.ChannelCount > 0 ? fixture.ChannelCount : (fixture.FixtureType == HoloCadeDMXFixtureType.Custom
                    ? Mathf.Max(1, fixture.CustomChannelMapping?.Count ?? 0)
                    : (fixture.FixtureType == HoloCadeDMXFixtureType.Dimmable ? 1
                        : (fixture.FixtureType == HoloCadeDMXFixtureType.RGB ? 3
                            : (fixture.FixtureType == HoloCadeDMXFixtureType.RGBW ? 4 : 8)))),
                CustomChannelMapping = fixture.CustomChannelMapping,
                RDMUID = fixture.RDMUID,
                RDMCapable = fixture.RDMCapable
            };
            return registry.Register(valid);
        }

        public void Unregister(int virtualFixtureID)
        {
            registry.Unregister(virtualFixtureID);
            fade.Cancel(virtualFixtureID);
        }

        public void ApplyIntensity(HoloCadeDMXFixture fixture, float intensity)
        {
            var driver = FixtureDriverFactory.Create(fixture.FixtureType);
            driver.ApplyIntensity(fixture, intensity, buffer);
        }

        public void ApplyColor(HoloCadeDMXFixture fixture, float red, float green, float blue, float white)
        {
            var driver = FixtureDriverFactory.Create(fixture.FixtureType);
            driver.ApplyColor(fixture, red, green, blue, white, buffer);
        }

        public void ApplyChannelRaw(HoloCadeDMXFixture fixture, int channelOffset, byte value)
        {
            buffer.SetChannel(fixture.Universe, fixture.DMXChannel + channelOffset, value);
        }

        public void StartFade(int virtualFixtureID, float current, float target, float durationSec)
        {
            fade.StartFade(virtualFixtureID, current, target, durationSec);
        }

        public void TickFades(float deltaTime, Action<int, float> onIntensity)
        {
            fade.Tick(deltaTime, onIntensity);
        }

        public void AllOff(Action<int, float> onIntensity)
        {
            foreach (var id in registry.GetIDs())
            {
                var fx = registry.Find(id);
                if (fx == null) continue;
                ApplyIntensity(fx, 0f);
                onIntensity(id, 0f);
                OnIntensityChanged?.Invoke(id, 0f);
            }
        }

        public void UpdateFixtureOnlineStatus(string rdmUID, bool isOnline)
        {
            if (rdmService == null || uidToVirtual == null) return;
            if (!uidToVirtual.TryGetValue(rdmUID, out var virtualFixtureID)) return;
            if (isOnline)
                rdmService.MarkOnline(rdmUID, virtualFixtureID);
            else
                rdmService.MarkOffline(rdmUID, virtualFixtureID);
        }

        public int SetIntensityById(int virtualFixtureID, float intensity)
        {
            var fixture = registry.Find(virtualFixtureID);
            if (fixture == null) return -1;
            var clamped = Mathf.Clamp01(intensity);
            ApplyIntensity(fixture, clamped);
            OnIntensityChanged?.Invoke(virtualFixtureID, clamped);
            return fixture.Universe;
        }

        public int SetColorRGBWById(int virtualFixtureID, float red, float green, float blue, float white)
        {
            var fixture = registry.Find(virtualFixtureID);
            if (fixture == null) return -1;
            switch (fixture.FixtureType)
            {
                case HoloCadeDMXFixtureType.RGB:
                case HoloCadeDMXFixtureType.RGBW:
                case HoloCadeDMXFixtureType.MovingHead:
                case HoloCadeDMXFixtureType.Custom:
                    break;
                default:
                    Debug.LogWarning($"FixtureService: Fixture {virtualFixtureID} does not support color");
                    return -1;
            }
            var r = Mathf.Clamp01(red);
            var g = Mathf.Clamp01(green);
            var b = Mathf.Clamp01(blue);
            var w = white >= 0f ? Mathf.Clamp01(white) : white;
            ApplyColor(fixture, r, g, b, w);
            OnColorChanged?.Invoke(virtualFixtureID, r, g, b);
            return fixture.Universe;
        }

        public int SetChannelById(int virtualFixtureID, int channelOffset, float value)
        {
            var fixture = registry.Find(virtualFixtureID);
            if (fixture == null) return -1;
            if (channelOffset < 0 || channelOffset >= fixture.ChannelCount)
            {
                Debug.LogWarning($"FixtureService: Invalid channel offset {channelOffset} for fixture {virtualFixtureID}");
                return -1;
            }
            byte dmxValue = value >= 0f && value <= 1f ? (byte)(value * 255f) : (byte)Mathf.Clamp((int)value, 0, 255);
            ApplyChannelRaw(fixture, channelOffset, dmxValue);
            return fixture.Universe;
        }

        public void StartFadeById(int virtualFixtureID, float targetIntensity, float durationSec)
        {
            var fixture = registry.Find(virtualFixtureID);
            if (fixture == null) return;
            var current = buffer.GetChannel(fixture.Universe, fixture.DMXChannel) / 255f;
            StartFade(virtualFixtureID, current, Mathf.Clamp01(targetIntensity), Mathf.Max(0.01f, durationSec));
        }

        public void AllOffAndNotify(Action<int, float> onIntensity) => AllOff(onIntensity);

        public bool IsFixtureRDMCapable(int virtualFixtureID)
        {
            var fixture = registry.Find(virtualFixtureID);
            return fixture?.RDMCapable ?? false;
        }

        public HoloCadeDMXFixture FindFixture(int virtualFixtureID) => registry.Find(virtualFixtureID);

        public HoloCadeDMXFixture FindFixtureMutable(int virtualFixtureID) => registry.FindMutable(virtualFixtureID);

        public int GetNextVirtualFixtureID() => nextVirtualFixtureID++;
    }
}

