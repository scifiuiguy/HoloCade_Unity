// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Renders one logical text element to all four station cameras of a HoloCade Cube by
    /// spawning one TextMeshPro instance per station. Each instance is placed on its station's
    /// OmniText layer (per <see cref="CubeRuntimeConfig"/>) and rotated to a fixed per-side yaw
    /// at spawn time so its TMP face points outward toward that side's station camera.
    /// <see cref="CubeRigController"/> handles the matching per-camera culling so each station
    /// camera only renders its own instance.
    ///
    /// This is the TextMeshPro-backed implementation of <see cref="IOmniTextElement"/>; a future
    /// 3D-extruded backend (<c>OmniTextMeshElement</c>) is planned for jumbotron-grade titles
    /// where mesh depth and lighting matter.
    ///
    /// Typical use (HoloSnake lobby): place an element as a child under <c>[CubeBase]</c>,
    /// position it where the text should appear in cube-local space (e.g. centered under a
    /// jumbotron), assign a font asset, and update <see cref="Text"/> at runtime to drive
    /// countdowns, ready badges, etc.
    /// </summary>
    [AddComponentMenu("HoloCade/Cube/OmniText Element (TextMeshPro)")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class OmniTextElement : MonoBehaviour, IOmniTextElement
    {
        // Rig + runtime-config are runtime-resolved, not serialized: there's typically exactly
        // one CubeRigController per scene, so the element auto-discovers it (parent walk first,
        // scene-wide fallback second) and pulls the runtime config off the rig. Tests and any
        // unusual multi-rig setups can override via SetStationCameraSource / SetRuntimeConfig
        // before calling Rebuild().
        CubeRigController _cubeRig;
        CubeRuntimeConfig _runtimeConfig;

        [Header("Content")]
        [TextArea(1, 4)]
        [SerializeField] string text = "OMNI TEXT";
        [SerializeField] Color color = Color.black;
        [SerializeField] TMP_FontAsset font;
        [Tooltip("TMP font size on each station instance. The default of 12 is paired with stationLocalScale = 0.1 (see Layout) so glyph rasterization runs at typographic scales TMP is tuned for, instead of the floating-point edge cases you see at fontSize ≈ 1 with parent scale 1.")]
        [Min(0.001f)] [SerializeField] float fontSize = 12f;
        [SerializeField] TextAlignmentOptions alignment = TextAlignmentOptions.Center;
        [Tooltip("Optional template for typographic settings. If assigned, the element copies font/material/style from this TMP instance into each per-station instance and disables the template at runtime. Leave null to drive everything from the inspector fields above.")]
        [SerializeField] TextMeshPro template;

        [Header("Layout")]
        [Tooltip("Uniform local scale applied to each station child. Default 0.1 keeps TMP rasterizing at large internal glyph sizes (paired with fontSize = 12) while the resulting world-space text fits typical HoloCade cube dimensions (~0.6 m wide). Increase for larger text in scene units; decrease for finer text. Affects spawn-time scale; live edits propagate via OnValidate.")]
        [Min(0.0001f)] [SerializeField] float stationLocalScale = 0.1f;

        [Header("Stations")]
        [Tooltip("Which station instances to spawn. Disable a side to hide the text from that station entirely.")]
        [SerializeField] bool showOnNorth = true;
        [SerializeField] bool showOnSouth = true;
        [SerializeField] bool showOnEast = true;
        [SerializeField] bool showOnWest = true;

        [Header("Orientation")]
        [Tooltip("If your TextMeshPro mesh appears mirrored (back-facing) at runtime, enable this to add 180° to every station child's yaw. Toggle and re-run if text reads backwards from every side.")]
        [SerializeField] bool flipFacing = false;

        struct StationInstance
        {
            public CubeSide Side;
            public GameObject Go;
            public TextMeshPro Tmp;
        }

        readonly List<StationInstance> _instances = new List<StationInstance>(4);
        bool _built;
        ICubeStationCameraSource _cameraSourceOverride;

        public string Text
        {
            get => text;
            set
            {
                if (string.Equals(text, value, StringComparison.Ordinal))
                    return;
                text = value ?? string.Empty;
                ApplyText();
            }
        }

        public Color Color
        {
            get => color;
            set
            {
                if (color == value)
                    return;
                color = value;
                ApplyColor();
            }
        }

        public Transform Transform => transform;

        /// <summary>
        /// Override the camera source used to gate <see cref="Rebuild"/>. Most titles can leave
        /// this alone and the element will auto-discover the scene's <see cref="CubeRigController"/>;
        /// tests with stub rigs can inject an <see cref="ICubeStationCameraSource"/> here so
        /// Rebuild proceeds even without a real rig. Pass null to revert to auto-discovery.
        /// </summary>
        public void SetStationCameraSource(ICubeStationCameraSource source)
        {
            _cameraSourceOverride = source;
        }

        /// <summary>
        /// Override the runtime config used to look up OmniText layer indices. Useful for tests
        /// or for titles that drive layer maps separately from the rig's own config. Pass null
        /// to revert to auto-discovery from the resolved cube rig.
        /// </summary>
        public void SetRuntimeConfig(CubeRuntimeConfig config)
        {
            _runtimeConfig = config;
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            if (!_built)
                Rebuild();
        }

        void OnDisable()
        {
            DestroyInstances();
            _built = false;
        }

        public void Rebuild()
        {
            DestroyInstances();
            ResolveReferences();
            if (ResolveCameraSource() == null || _runtimeConfig == null)
            {
                _built = false;
                return;
            }

            if (template != null)
                template.gameObject.SetActive(false);

            BuildSide(CubeSide.North, showOnNorth);
            BuildSide(CubeSide.South, showOnSouth);
            BuildSide(CubeSide.East, showOnEast);
            BuildSide(CubeSide.West, showOnWest);

            _built = true;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneView.RepaintAll();
#endif
        }

        ICubeStationCameraSource ResolveCameraSource()
        {
            if (_cameraSourceOverride != null)
                return _cameraSourceOverride;
            return _cubeRig;
        }

        void ResolveReferences()
        {
            // Two-step discovery: prefer the rig that owns this element's hierarchy (typical
            // case: dropped under [CubeBase]), fall back to a scene-wide search for unusual
            // placements. Skip if a test injected an override via SetStationCameraSource.
            if (_cubeRig == null && _cameraSourceOverride == null)
            {
                _cubeRig = GetComponentInParent<CubeRigController>();
                if (_cubeRig == null)
#if UNITY_2022_2_OR_NEWER
                    _cubeRig = FindFirstObjectByType<CubeRigController>(FindObjectsInactive.Include);
#else
                    _cubeRig = FindObjectOfType<CubeRigController>(includeInactive: true);
#endif
            }
            if (_runtimeConfig == null && _cubeRig != null)
                _runtimeConfig = _cubeRig.RuntimeConfig;
        }

        /// <summary>
        /// Fixed per-side yaw (degrees) so each station's TMP face points outward toward its
        /// camera. Cube convention: N faces +Z, E faces +X, S faces -Z, W faces -X.
        /// </summary>
        static float YawDegreesForSide(CubeSide side)
        {
            switch (side)
            {
                case CubeSide.North: return 0f;
                case CubeSide.East:  return 90f;
                case CubeSide.South: return 180f;
                case CubeSide.West:  return 270f;
                default: return 0f;
            }
        }

        void BuildSide(CubeSide side, bool enabled)
        {
            if (!enabled)
                return;

            var go = new GameObject($"OmniText_{CubeSideUtility.SideName(side)}");
            // Children are runtime-generated under the element's transform; never serialize
            // them into the scene or prefab.
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, YawDegreesForSide(side) + (flipFacing ? 180f : 0f), 0f);
            go.transform.localScale = Vector3.one * stationLocalScale;
            go.layer = _runtimeConfig.GetOmniTextLayerForSide(side);

            var tmp = go.AddComponent<TextMeshPro>();
            ApplyTypographyTo(tmp);
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontSize = fontSize;
            if (font != null)
                tmp.font = font;

            _instances.Add(new StationInstance { Side = side, Go = go, Tmp = tmp });
        }

        void ApplyTypographyTo(TextMeshPro tmp)
        {
            if (template == null)
                return;

            tmp.fontSharedMaterial = template.fontSharedMaterial;
            tmp.fontStyle = template.fontStyle;
            tmp.characterSpacing = template.characterSpacing;
            tmp.wordSpacing = template.wordSpacing;
            tmp.lineSpacing = template.lineSpacing;
            tmp.outlineColor = template.outlineColor;
            tmp.outlineWidth = template.outlineWidth;
            tmp.enableWordWrapping = template.enableWordWrapping;
            tmp.overflowMode = template.overflowMode;
            tmp.rectTransform.sizeDelta = template.rectTransform.sizeDelta;
            if (template.font != null)
                tmp.font = template.font;
        }

        void ApplyText()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst.Tmp != null)
                    inst.Tmp.text = text;
            }
        }

        void ApplyColor()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst.Tmp != null)
                    inst.Tmp.color = color;
            }
        }

        void DestroyInstances()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst.Go == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(inst.Go);
                else
                    DestroyImmediate(inst.Go);
            }
            _instances.Clear();
        }

#if UNITY_EDITOR
        bool _editorRebuildQueued;

        void OnValidate()
        {
            // Cheap property updates: propagate font/text/color/alignment/scale to live
            // instances. Safe to run during serialization because we only mutate existing
            // components — we don't create or destroy GameObjects here.
            ApplyText();
            ApplyColor();
            var scaleVec = Vector3.one * stationLocalScale;
            for (int i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst.Go != null)
                {
                    inst.Go.transform.localScale = scaleVec;
                    inst.Go.transform.localRotation = Quaternion.Euler(
                        0f,
                        YawDegreesForSide(inst.Side) + (flipFacing ? 180f : 0f),
                        0f);
                }
                var tmp = inst.Tmp;
                if (tmp == null) continue;
                tmp.fontSize = fontSize;
                tmp.alignment = alignment;
                if (font != null) tmp.font = font;
            }

            // Structural changes (showOnX toggles, layer rebinds) require Rebuild, which
            // destroys/creates GameObjects — illegal during OnValidate's serialization callback,
            // so defer until the next editor tick. Skip in play mode.
            if (!Application.isPlaying)
                QueueEditorSafeRebuild();
        }

        void QueueEditorSafeRebuild()
        {
            if (_editorRebuildQueued)
                return;
            _editorRebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _editorRebuildQueued = false;
                if (this == null)
                    return;
                Rebuild();
            };
        }
#endif
    }
}
