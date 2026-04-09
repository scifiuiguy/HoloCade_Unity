// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.ProLighting
{
    struct FadeState
    {
        public float CurrentIntensity;
        public float TargetIntensity;
        public float FadeSpeed;
        public bool Fading;
    }

    /// <summary>Time-based intensity fades per virtual fixture</summary>
    public class FadeEngine
    {
        private readonly Dictionary<int, FadeState> states = new Dictionary<int, FadeState>();

        public void StartFade(int virtualId, float current, float target, float durationSec)
        {
            var s = states.TryGetValue(virtualId, out var existing) ? existing : new FadeState { CurrentIntensity = current };
            s.CurrentIntensity = current;
            s.TargetIntensity = Mathf.Clamp01(target);
            s.FadeSpeed = durationSec > 0f ? (Mathf.Abs(s.TargetIntensity - s.CurrentIntensity) / durationSec) : 0f;
            s.Fading = durationSec > 0f && s.FadeSpeed > 0f;
            states[virtualId] = s;
        }

        public void Cancel(int virtualId) => states.Remove(virtualId);

        public void Tick(float deltaTime, Action<int, float> onIntensity)
        {
            var toRemove = new List<int>();
            foreach (var kvp in states)
            {
                var id = kvp.Key;
                var s = kvp.Value;
                if (!s.Fading) continue;
                var delta = s.FadeSpeed * deltaTime;
                if (Mathf.Abs(s.TargetIntensity - s.CurrentIntensity) <= delta)
                {
                    s.CurrentIntensity = s.TargetIntensity;
                    s.Fading = false;
                }
                else
                    s.CurrentIntensity += (s.TargetIntensity > s.CurrentIntensity) ? delta : -delta;
                states[id] = s;
                onIntensity(id, s.CurrentIntensity);
                if (!s.Fading) toRemove.Add(id);
            }
            foreach (var id in toRemove) states.Remove(id);
        }
    }
}

