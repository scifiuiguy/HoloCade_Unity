// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using HoloCade.Cube;
using NUnit.Framework;
using UnityEngine;

namespace HoloCade.Tests.Editor
{
    /// <summary>
    /// Behavioral tests for <see cref="OmniTextElement"/>: per-station spawn, layer assignment,
    /// content propagation, and orientation. These tests stand up bare GameObjects + a stub
    /// <see cref="ICubeStationCameraSource"/> so they can run in EditMode without a full cube
    /// rig (no <see cref="CubeRigController"/> rebuild, no monitor catalog, no displays).
    /// </summary>
    public class OmniTextElementTests
    {
        sealed class FakeStationCameraSource : ICubeStationCameraSource
        {
            readonly Dictionary<CubeSide, Camera> _cams = new Dictionary<CubeSide, Camera>();

            public void Set(CubeSide side, Camera cam)
            {
                _cams[side] = cam;
            }

            public bool TryGetSideCamera(CubeSide side, out Camera sideCamera)
            {
                if (_cams.TryGetValue(side, out var cam) && cam != null)
                {
                    sideCamera = cam;
                    return true;
                }
                sideCamera = null;
                return false;
            }
        }

        readonly List<GameObject> _spawned = new List<GameObject>();
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
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null)
                    Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();

            if (_config != null)
                ScriptableObject.DestroyImmediate(_config);
            _config = null;
        }

        GameObject SpawnGo(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        (OmniTextElement element, FakeStationCameraSource source) MakeElement(bool n = true, bool s = true, bool e = true, bool w = true)
        {
            var elementGo = SpawnGo("OmniTextElement_Test");
            var element = elementGo.AddComponent<OmniTextElement>();
            var source = new FakeStationCameraSource();

            // Stub cameras at the four cardinal positions outside the cube center so the
            // element can reorient instances toward each of them.
            source.Set(CubeSide.North, MakeStubCamera("N", new Vector3(0f, 0f, +1f)));
            source.Set(CubeSide.South, MakeStubCamera("S", new Vector3(0f, 0f, -1f)));
            source.Set(CubeSide.East, MakeStubCamera("E", new Vector3(+1f, 0f, 0f)));
            source.Set(CubeSide.West, MakeStubCamera("W", new Vector3(-1f, 0f, 0f)));

            element.SetStationCameraSource(source);
            element.SetRuntimeConfig(_config);

            // Mutate station toggles via SerializedObject so we can test partial spawns.
            var so = new UnityEditor.SerializedObject(element);
            so.FindProperty("showOnNorth").boolValue = n;
            so.FindProperty("showOnSouth").boolValue = s;
            so.FindProperty("showOnEast").boolValue = e;
            so.FindProperty("showOnWest").boolValue = w;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (element, source);
        }

        Camera MakeStubCamera(string label, Vector3 worldPos)
        {
            var go = SpawnGo($"StubCamera_{label}");
            go.transform.position = worldPos;
            return go.AddComponent<Camera>();
        }

        [Test]
        public void Rebuild_SpawnsFourChildrenWhenAllSidesEnabled()
        {
            var (element, _) = MakeElement();
            element.Rebuild();
            Assert.AreEqual(4, element.transform.childCount);
        }

        [Test]
        public void Rebuild_SkipsDisabledSides()
        {
            var (element, _) = MakeElement(n: true, s: false, e: true, w: false);
            element.Rebuild();
            Assert.AreEqual(2, element.transform.childCount);

            bool foundNorth = false;
            bool foundEast = false;
            for (int i = 0; i < element.transform.childCount; i++)
            {
                var name = element.transform.GetChild(i).name;
                if (name.Contains("North")) foundNorth = true;
                if (name.Contains("East")) foundEast = true;
            }
            Assert.IsTrue(foundNorth, "North child must be present.");
            Assert.IsTrue(foundEast, "East child must be present.");
        }

        [Test]
        public void Rebuild_AssignsCorrectLayerPerStationChild()
        {
            var (element, _) = MakeElement();
            element.Rebuild();

            var byLayer = new Dictionary<int, string>();
            for (int i = 0; i < element.transform.childCount; i++)
            {
                var child = element.transform.GetChild(i);
                byLayer[child.gameObject.layer] = child.name;
            }

            Assert.IsTrue(byLayer.ContainsKey(20), "North child should be on layer 20.");
            Assert.IsTrue(byLayer.ContainsKey(21), "South child should be on layer 21.");
            Assert.IsTrue(byLayer.ContainsKey(22), "East child should be on layer 22.");
            Assert.IsTrue(byLayer.ContainsKey(23), "West child should be on layer 23.");
        }

        [Test]
        public void Rebuild_DoesNothingWithoutRuntimeConfig()
        {
            var elementGo = SpawnGo("OmniTextElement_NoConfig");
            var element = elementGo.AddComponent<OmniTextElement>();
            element.SetStationCameraSource(new FakeStationCameraSource());
            element.SetRuntimeConfig(null);
            element.Rebuild();
            Assert.AreEqual(0, element.transform.childCount, "No config should mean no children.");
        }

        [Test]
        public void Rebuild_DoesNothingWithoutCameraSource()
        {
            var elementGo = SpawnGo("OmniTextElement_NoSource");
            var element = elementGo.AddComponent<OmniTextElement>();
            element.SetStationCameraSource(null);
            element.SetRuntimeConfig(_config);
            element.Rebuild();
            Assert.AreEqual(0, element.transform.childCount, "No camera source should mean no children.");
        }

        [Test]
        public void Rebuild_IsIdempotentAndDoesNotLeakChildren()
        {
            var (element, _) = MakeElement();
            element.Rebuild();
            Assert.AreEqual(4, element.transform.childCount);
            element.Rebuild();
            Assert.AreEqual(4, element.transform.childCount, "Rebuild must replace existing children, not append.");
            element.Rebuild();
            Assert.AreEqual(4, element.transform.childCount);
        }

        [Test]
        public void TextSetter_PropagatesToAllChildrenAfterBuild()
        {
            var (element, _) = MakeElement();
            element.Rebuild();

            const string expected = "READY";
            element.Text = expected;

            for (int i = 0; i < element.transform.childCount; i++)
            {
                var tmp = element.transform.GetChild(i).GetComponent<TMPro.TextMeshPro>();
                Assert.IsNotNull(tmp, "Each station child should have a TextMeshPro.");
                Assert.AreEqual(expected, tmp.text);
            }
        }

        [Test]
        public void ColorSetter_PropagatesToAllChildrenAfterBuild()
        {
            var (element, _) = MakeElement();
            element.Rebuild();

            var expected = new Color(0.1f, 0.2f, 0.3f, 1f);
            element.Color = expected;

            for (int i = 0; i < element.transform.childCount; i++)
            {
                var tmp = element.transform.GetChild(i).GetComponent<TMPro.TextMeshPro>();
                Assert.IsNotNull(tmp);
                Assert.AreEqual(expected, tmp.color);
            }
        }

        [Test]
        public void LocalDepth_OffsetsNorthShellAlongLocalForward()
        {
            var (element, _) = MakeElement(n: true, s: false, e: false, w: false);
            const float depth = 0.25f;
            element.LocalDepth = depth;

            var north = element.transform.Find("OmniText_North");
            Assert.IsNotNull(north);
            var stationRot = Quaternion.Euler(0f, 180f, 0f);
            var expected = stationRot * Vector3.forward * depth;
            Assert.AreEqual(expected.x, north.localPosition.x, 0.001);
            Assert.AreEqual(expected.y, north.localPosition.y, 0.001);
            Assert.AreEqual(expected.z, north.localPosition.z, 0.001);
        }

        [Test]
        public void LocalDepth_AllowsNegativeOffsetAlongLocalForward()
        {
            var (element, _) = MakeElement(n: true, s: false, e: false, w: false);
            const float depth = -0.26f;
            element.LocalDepth = depth;

            var north = element.transform.Find("OmniText_North");
            Assert.IsNotNull(north);
            var stationRot = Quaternion.Euler(0f, 180f, 0f);
            var expected = stationRot * Vector3.forward * depth;
            Assert.AreEqual(expected.x, north.localPosition.x, 0.001);
            Assert.AreEqual(expected.y, north.localPosition.y, 0.001);
            Assert.AreEqual(expected.z, north.localPosition.z, 0.001);
        }
    }
}
