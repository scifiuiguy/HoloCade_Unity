// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;
using UnityEngine.Serialization;

namespace HoloCade.Cube
{
    [CreateAssetMenu(fileName = "CubeMonitorSpec", menuName = "HoloCade/Cube/Monitor Spec")]
    public class CubeMonitorSpec : ScriptableObject
    {
        public const float InchesToMeters = 0.0254f;

        public string make;
        public string model;

        [FormerlySerializedAs("screenWidth")]
        [Min(0.01f)]
        [Tooltip("Active display width in inches (landscape, as typically listed on a datasheet).")]
        public float screenWidthInches;

        [FormerlySerializedAs("screenHeight")]
        [Min(0.01f)]
        [Tooltip("Active display height in inches (landscape, as typically listed on a datasheet).")]
        public float screenHeightInches;

        public float ScreenWidthMeters => screenWidthInches * InchesToMeters;

        public float ScreenHeightMeters => screenHeightInches * InchesToMeters;

        public string DisplayName
        {
            get
            {
                var safeMake = string.IsNullOrWhiteSpace(make) ? "UnknownMake" : make.Trim();
                var safeModel = string.IsNullOrWhiteSpace(model) ? "UnknownModel" : model.Trim();
                return $"{safeMake} {safeModel}";
            }
        }
    }
}
