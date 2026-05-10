// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using HoloCade.Cube;
using NUnit.Framework;
using UnityEngine;

namespace HoloCade.Tests.Editor
{
    public class OmniTextStationLayersTests
    {
        CubeRuntimeConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CubeRuntimeConfig>();
            _config.northOmniTextLayer = 20;
            _config.southOmniTextLayer = 21;
            _config.eastOmniTextLayer = 22;
            _config.westOmniTextLayer = 23;
            _config.enableOmniTextStationCulling = true;
            _config.sideCameraCullingMask = ~0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
                ScriptableObject.DestroyImmediate(_config);
        }

        [Test]
        public void LayerIndexForSide_ReturnsConfiguredIndexPerSide()
        {
            Assert.AreEqual(20, OmniTextStationLayers.LayerIndexForSide(CubeSide.North, _config));
            Assert.AreEqual(21, OmniTextStationLayers.LayerIndexForSide(CubeSide.South, _config));
            Assert.AreEqual(22, OmniTextStationLayers.LayerIndexForSide(CubeSide.East, _config));
            Assert.AreEqual(23, OmniTextStationLayers.LayerIndexForSide(CubeSide.West, _config));
        }

        [Test]
        public void LayerIndexForSide_ReturnsZeroForNullConfig()
        {
            Assert.AreEqual(0, OmniTextStationLayers.LayerIndexForSide(CubeSide.North, null));
        }

        [Test]
        public void UnionMask_IsTheBitwiseOrOfAllFourStationLayers()
        {
            int expected = (1 << 20) | (1 << 21) | (1 << 22) | (1 << 23);
            Assert.AreEqual(expected, OmniTextStationLayers.UnionMask(_config));
        }

        [Test]
        public void UnionMask_ReturnsZeroForNullConfig()
        {
            Assert.AreEqual(0, OmniTextStationLayers.UnionMask(null));
        }

        [Test]
        public void ApplyStationCulling_PassThroughWhenDisabled()
        {
            _config.enableOmniTextStationCulling = false;
            int baseMask = ~0;
            Assert.AreEqual(baseMask, OmniTextStationLayers.ApplyStationCulling(baseMask, CubeSide.North, _config));
        }

        [Test]
        public void ApplyStationCulling_RemovesOpponentLayersAndKeepsOwn()
        {
            int baseMask = ~0;
            int northResult = OmniTextStationLayers.ApplyStationCulling(baseMask, CubeSide.North, _config);

            // Own (north, layer 20) bit is set.
            Assert.AreNotEqual(0, northResult & (1 << 20), "North camera should keep its own OmniText layer.");

            // Opponent (south/east/west, 21/22/23) bits are cleared.
            Assert.AreEqual(0, northResult & (1 << 21), "South OmniText layer should be filtered from north camera.");
            Assert.AreEqual(0, northResult & (1 << 22), "East OmniText layer should be filtered from north camera.");
            Assert.AreEqual(0, northResult & (1 << 23), "West OmniText layer should be filtered from north camera.");
        }

        [Test]
        public void ApplyStationCulling_PreservesNonOmniTextBitsFromBaseMask()
        {
            // Base mask has the Default layer (0), a hypothetical game-content layer 8, and all
            // four OmniText layers set.
            int baseMask = (1 << 0) | (1 << 8) | (1 << 20) | (1 << 21) | (1 << 22) | (1 << 23);
            int southResult = OmniTextStationLayers.ApplyStationCulling(baseMask, CubeSide.South, _config);

            Assert.AreNotEqual(0, southResult & (1 << 0), "Default layer must survive station culling.");
            Assert.AreNotEqual(0, southResult & (1 << 8), "Title content layer 8 must survive station culling.");
            Assert.AreNotEqual(0, southResult & (1 << 21), "South camera keeps its own OmniText layer (21).");
            Assert.AreEqual(0, southResult & (1 << 20), "North OmniText layer must be filtered from south camera.");
            Assert.AreEqual(0, southResult & (1 << 22), "East OmniText layer must be filtered from south camera.");
            Assert.AreEqual(0, southResult & (1 << 23), "West OmniText layer must be filtered from south camera.");
        }

        [Test]
        public void ApplyStationCulling_HandlesAliasedLayerIndices()
        {
            // If two sides happen to share a layer index (misconfiguration or an intentional
            // narrowing), culling must NOT clear that shared layer for either camera.
            _config.northOmniTextLayer = 20;
            _config.southOmniTextLayer = 20;
            _config.eastOmniTextLayer = 22;
            _config.westOmniTextLayer = 23;

            int baseMask = ~0;
            int northResult = OmniTextStationLayers.ApplyStationCulling(baseMask, CubeSide.North, _config);
            int southResult = OmniTextStationLayers.ApplyStationCulling(baseMask, CubeSide.South, _config);

            Assert.AreNotEqual(0, northResult & (1 << 20), "North camera keeps the shared layer 20.");
            Assert.AreNotEqual(0, southResult & (1 << 20), "South camera keeps the shared layer 20 even though it 'belongs' to north too.");
            Assert.AreEqual(0, northResult & (1 << 22), "North still filters out east (22).");
            Assert.AreEqual(0, southResult & (1 << 23), "South still filters out west (23).");
        }

        [Test]
        public void ApplyStationCulling_ReturnsBaseMaskForNullConfig()
        {
            int baseMask = 0x12345678;
            Assert.AreEqual(baseMask, OmniTextStationLayers.ApplyStationCulling(baseMask, CubeSide.North, null));
        }
    }
}
