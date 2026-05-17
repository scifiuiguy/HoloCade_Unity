// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HoloCade.Cube
{
    /// <summary>
    /// Renders one logical text element to all four station cameras of a HoloCade Cube by
    /// driving one TextMeshPro instance per station. Each station object lives under this
    /// transform (typically named <c>OmniText_North</c>, etc.), is placed on its OmniText layer
    /// (per <see cref="CubeRuntimeConfig"/>), and uses a fixed per-side yaw (cardinals on local Y
    /// plus 180° by default; see <see cref="flipFacing"/>) so TMP reads toward that side's camera.
    /// Optional <see cref="localDepth"/> offsets each shell along its own local +Z (after that yaw)
    /// so quads can sit in front of embedded cabinet geometry.
    /// <see cref="CubeRigController"/> applies per-camera culling so each station camera only sees
    /// its own OmniText layer (<see cref="CubeRuntimeConfig.enableOmniTextStationCulling"/> must stay
    /// enabled for normal play). If station children remain on Default layer (0), every camera still
    /// draws all four quads — you will see one correct-facing label plus the other stations’ text
    /// appearing backward in the same frustum.
    ///
    /// <para><b>Prefab workflow:</b> look-dev station children in Edit Mode (add/rename/layout),
    /// merge into the prefab — Play Mode uses that asset. <see cref="Rebuild"/> finds children by name,
    /// creates only missing sides, applies layers/orientation, and toggles inactive per inspector flags.
    /// In Edit Mode, TMP content on <em>existing</em> shells is only overwritten from this component when
    /// <see cref="driveStationContentFromParentInEditMode"/> is enabled (default off) so Prefab Apply is not
    /// immediately undone; Play Mode always mirrors Content.</para>
    ///
    /// This is the TextMeshPro-backed implementation of <see cref="IOmniTextElement"/>.
    /// </summary>
    [AddComponentMenu("HoloCade/Cube/OmniText Element (TextMeshPro)")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class OmniTextElement : MonoBehaviour, IOmniTextElement
    {
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
        [Tooltip("Uniform local scale applied to each station child when syncing from this component. Live edits propagate via OnValidate / Rebuild.")]
        [Min(0.0001f)] [SerializeField] float stationLocalScale = 0.1f;

        [Tooltip(
            "Meters each station TMP shell moves along its own local +Z (after per-side yaw), i.e. along the quad’s forward / toward that station’s camera. " +
            "Use positive values to pop copy out from embedded geometry (e.g. jumbotron). Negative values offset the opposite way (TMP faces local −Z by default). Zero keeps shells at their authored / default local origin.")]
        [SerializeField]
        float localDepth = 0.26f;

        [Header("Stations")]
        [Tooltip("Which station instances are active. Disabled sides deactivate their child object (if present) instead of deleting it.")]
        [SerializeField] bool showOnNorth = true;
        [SerializeField] bool showOnSouth = true;
        [SerializeField] bool showOnEast = true;
        [SerializeField] bool showOnWest = true;

        [Header("Orientation")]
        [Tooltip("When true, each station uses raw cardinal yaw only (TMP reads backwards from that station). Default false adds 180° on local Y so text faces the station camera.")]
        [SerializeField] bool flipFacing = false;

        [Header("Edit-mode prefab authoring")]
        [Tooltip(
            "When false (default), Edit Mode does not push Content/TMP fields onto existing OmniText_* shells — only layout/orientation/layers. " +
            "Turn on for live mirroring from this inspector while editing. Play Mode always mirrors. New shells still receive initial Content.")]
        [SerializeField] bool driveStationContentFromParentInEditMode;

        struct StationInstance
        {
            public CubeSide Side;
            public GameObject Go;
            public TextMeshPro Tmp;
        }

        readonly List<StationInstance> _instances = new List<StationInstance>(4);
        ICubeStationCameraSource _cameraSourceOverride;

        /// <summary>Depth baked into hierarchy at the start of <see cref="Rebuild"/>; used to strip before re-applying <see cref="localDepth"/>.</summary>
        float _stripDepthForRebuild;

        /// <summary>Last <see cref="localDepth"/> committed to station transforms after a full rebuild.</summary>
        float _lastAppliedLocalDepth;

        /// <summary>Play mode: re-apply OmniText layers for a few frames so ordering after Awake cannot leave station TMPs on Default.</summary>
        int _playModeLayerWarmupFramesRemaining = 32;

        /// <summary>Once per load: force GameObject active state to match <c>showOn*</c>. After that, only apply when an inspector flag changes so manual hierarchy toggles are not undone every Rebuild.</summary>
        bool _visibilitySessionInitialized;

        bool _lastAppliedShowNorth;
        bool _lastAppliedShowSouth;
        bool _lastAppliedShowEast;
        bool _lastAppliedShowWest;

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

        /// <summary>TMP point size on every station instance (mirrors serialized <c>fontSize</c>).</summary>
        public float FontSize
        {
            get => fontSize;
            set
            {
                var v = Mathf.Max(0.001f, value);
                if (Mathf.Approximately(fontSize, v))
                    return;
                fontSize = v;
                ApplyFontSizeToInstances();
            }
        }

        /// <summary>
        /// Per-station offset along each shell’s local forward (see <see cref="localDepth"/>). Changing this triggers <see cref="Rebuild"/>.
        /// </summary>
        public float LocalDepth
        {
            get => localDepth;
            set
            {
                if (Mathf.Approximately(localDepth, value))
                    return;
                localDepth = value;
                Rebuild();
            }
        }

        public Transform Transform => transform;

        public void SetStationCameraSource(ICubeStationCameraSource source)
        {
            _cameraSourceOverride = source;
        }

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
            if (Application.isPlaying)
                _playModeLayerWarmupFramesRemaining = 32;
            Rebuild();
        }

        void Start()
        {
            // Runs after all Awakes; ensures cube rig / RuntimeConfig are resolved before the first
            // full sync when sibling/parent script order left station children on Default during OnEnable.
            ResolveReferences();
            Rebuild();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || _playModeLayerWarmupFramesRemaining <= 0)
                return;
            _playModeLayerWarmupFramesRemaining--;
            ApplyStationLayersFromConfig();
        }

        public void Rebuild()
        {
            _stripDepthForRebuild = _lastAppliedLocalDepth;
            try
            {
                if (!CanModifyHierarchyUnder(this))
                {
                    ResolveReferences();
                    RefreshInstancesFromChildren();
                    SyncStationVisibilityWithInspectorFlags();
                    ApplyStationLayersFromConfig();
                    ApplyContentAndLayoutToKnownInstances();
                    return;
                }

                ResolveReferences();
                if (template != null)
                    template.gameObject.SetActive(false);

                _instances.Clear();

                SyncSide(CubeSide.North, showOnNorth);
                SyncSide(CubeSide.South, showOnSouth);
                SyncSide(CubeSide.East, showOnEast);
                SyncSide(CubeSide.West, showOnWest);

                SyncStationVisibilityWithInspectorFlags();
                ApplyStationLayersFromConfig();

                // Push parent Content to shells after SyncSide (covers Text/Color set while _instances was empty,
                // and keeps authored shells in sync when Rebuild runs after external IOmniTextElement writes).
                ApplyContentAndLayoutToKnownInstances();

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.SceneView.RepaintAll();
#endif
            }
            finally
            {
                _lastAppliedLocalDepth = localDepth;
            }
        }

        /// <summary>
        /// Sets each <c>OmniText_*</c> child’s layer from <see cref="CubeRuntimeConfig"/> so
        /// <see cref="OmniTextStationLayers.ApplyStationCulling"/> can isolate one instance per camera.
        /// Safe to call repeatedly (idempotent).
        /// </summary>
        void ApplyStationLayersFromConfig()
        {
            ResolveReferences();
            if (_runtimeConfig == null)
                return;

            void One(CubeSide side)
            {
                var tr = FindDirectChildNamed(transform, StationChildName(side));
                if (tr == null)
                    return;
                var layer = _runtimeConfig.GetOmniTextLayerForSide(side);
                if (tr.gameObject.layer != layer)
                    tr.gameObject.layer = layer;
            }

            One(CubeSide.North);
            One(CubeSide.South);
            One(CubeSide.East);
            One(CubeSide.West);
        }

        /// <summary>
        /// Drives station GameObject active state from <c>showOn*</c> only when those flags change,
        /// or once per domain/session on first Rebuild. Avoids <c>SetActive(true)</c> every Rebuild,
        /// which was undoing manual disables and re-lighting hidden duplicate children.
        /// </summary>
        void SyncStationVisibilityWithInspectorFlags()
        {
            void ApplyDelta(CubeSide side, bool wantShow, ref bool lastApplied)
            {
                if (lastApplied == wantShow)
                    return;
                var tr = FindDirectChildNamed(transform, StationChildName(side));
                if (tr != null)
                    tr.gameObject.SetActive(wantShow);
                lastApplied = wantShow;
            }

            if (!_visibilitySessionInitialized)
            {
                void InitOne(CubeSide side, bool wantShow, ref bool lastApplied)
                {
                    var tr = FindDirectChildNamed(transform, StationChildName(side));
                    if (tr != null)
                        tr.gameObject.SetActive(wantShow);
                    lastApplied = wantShow;
                }

                InitOne(CubeSide.North, showOnNorth, ref _lastAppliedShowNorth);
                InitOne(CubeSide.South, showOnSouth, ref _lastAppliedShowSouth);
                InitOne(CubeSide.East, showOnEast, ref _lastAppliedShowEast);
                InitOne(CubeSide.West, showOnWest, ref _lastAppliedShowWest);
                _visibilitySessionInitialized = true;
                return;
            }

            ApplyDelta(CubeSide.North, showOnNorth, ref _lastAppliedShowNorth);
            ApplyDelta(CubeSide.South, showOnSouth, ref _lastAppliedShowSouth);
            ApplyDelta(CubeSide.East, showOnEast, ref _lastAppliedShowEast);
            ApplyDelta(CubeSide.West, showOnWest, ref _lastAppliedShowWest);
        }

        /// <summary>
        /// True when we may parent newly created station objects under this transform (not an on-disk prefab asset).
        /// </summary>
        static bool CanModifyHierarchyUnder(OmniTextElement self)
        {
            if (self == null)
                return false;
#if UNITY_EDITOR
            if (!Application.isPlaying && !self.gameObject.scene.IsValid())
                return false;
#endif
            return true;
        }

        ICubeStationCameraSource ResolveCameraSource()
        {
            if (_cameraSourceOverride != null)
                return _cameraSourceOverride;
            return _cubeRig;
        }

        void ResolveReferences()
        {
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

        static float StationYawDegrees(CubeSide side, bool flipFacing) =>
            YawDegreesForSide(side) + (flipFacing ? 0f : 180f);

        static string StationChildName(CubeSide side) =>
            $"OmniText_{CubeSideUtility.SideName(side)}";

        static Transform FindDirectChildNamed(Transform root, string childName)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == childName)
                    return c;
            }
            return null;
        }

        /// <summary>
        /// Legacy builds / TMP can leave <see cref="HideFlags.HideInHierarchy"/> on shells or child
        /// objects — they still raycast in Scene view but do not appear under Hierarchy. Cleared on sync.
        /// </summary>
        static void ClearHideFlagsRecursive(GameObject go)
        {
            if (go == null)
                return;
            go.hideFlags = HideFlags.None;
            var tr = go.transform;
            for (var i = 0; i < tr.childCount; i++)
                ClearHideFlagsRecursive(tr.GetChild(i).gameObject);
        }

        /// <summary>
        /// Ensures the station child exists (find authored, or create if allowed), toggles active,
        /// assigns layer and synced transform/TMP fields.
        /// </summary>
        void SyncSide(CubeSide side, bool enabled)
        {
            var childName = StationChildName(side);
            var tr = FindDirectChildNamed(transform, childName);

            if (!enabled)
                return;

            GameObject go;
            TextMeshPro tmp;
            var createdNew = false;

            Vector3 baseLocalPosition;
            if (tr != null)
            {
                go = tr.gameObject;
                tmp = go.GetComponent<TextMeshPro>();
                if (tmp == null)
                    tmp = go.AddComponent<TextMeshPro>();
                var prevRot = go.transform.localRotation;
                baseLocalPosition = go.transform.localPosition - prevRot * Vector3.forward * _stripDepthForRebuild;
            }
            else
            {
                if (ResolveCameraSource() == null || _runtimeConfig == null)
                    return;

                go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                createdNew = true;
                tmp = go.AddComponent<TextMeshPro>();
                baseLocalPosition = Vector3.zero;
            }

            if (_runtimeConfig != null)
                go.layer = _runtimeConfig.GetOmniTextLayerForSide(side);

            var stationRot = Quaternion.Euler(0f, StationYawDegrees(side, flipFacing), 0f);
            go.transform.localRotation = stationRot;
            go.transform.localScale = Vector3.one * stationLocalScale;
            go.transform.localPosition = baseLocalPosition + stationRot * Vector3.forward * localDepth;

            if (ShouldDriveStationTmpContent(createdNew))
            {
                ApplyTypographyTo(tmp);
                tmp.text = text;
                tmp.color = color;
                tmp.alignment = alignment;
                tmp.fontSize = fontSize;
                if (font != null)
                    tmp.font = font;
                tmp.ForceMeshUpdate(true);
            }

            ClearHideFlagsRecursive(go);

            _instances.Add(new StationInstance { Side = side, Go = go, Tmp = tmp });
        }

        bool ShouldDriveStationTmpContent(bool createdNewStationShell)
        {
            if (Application.isPlaying)
                return true;
#if UNITY_EDITOR
            return driveStationContentFromParentInEditMode || createdNewStationShell;
#else
            return true;
#endif
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

        void RefreshInstancesFromChildren()
        {
            _instances.Clear();
            if (showOnNorth)
                TryAddInstanceFromChild(CubeSide.North, StationChildName(CubeSide.North));
            if (showOnSouth)
                TryAddInstanceFromChild(CubeSide.South, StationChildName(CubeSide.South));
            if (showOnEast)
                TryAddInstanceFromChild(CubeSide.East, StationChildName(CubeSide.East));
            if (showOnWest)
                TryAddInstanceFromChild(CubeSide.West, StationChildName(CubeSide.West));
        }

        void TryAddInstanceFromChild(CubeSide side, string childName)
        {
            var tr = FindDirectChildNamed(transform, childName);
            if (tr == null)
                return;
            var tmp = tr.GetComponent<TextMeshPro>();
            if (tmp == null)
                return;
            _instances.Add(new StationInstance { Side = side, Go = tr.gameObject, Tmp = tmp });
        }

        void ApplyContentAndLayoutToKnownInstances()
        {
            ApplyStationLayersFromConfig();

            var scaleVec = Vector3.one * stationLocalScale;
            for (var i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst.Go == null || inst.Tmp == null)
                    continue;
                var prevRot = inst.Go.transform.localRotation;
                var baseLocalPosition = inst.Go.transform.localPosition - prevRot * Vector3.forward * _stripDepthForRebuild;
                var stationRot = Quaternion.Euler(0f, StationYawDegrees(inst.Side, flipFacing), 0f);
                inst.Go.transform.localRotation = stationRot;
                inst.Go.transform.localScale = scaleVec;
                inst.Go.transform.localPosition = baseLocalPosition + stationRot * Vector3.forward * localDepth;
                if (ShouldDriveStationTmpContent(createdNewStationShell: false))
                {
                    inst.Tmp.text = text;
                    inst.Tmp.color = color;
                    inst.Tmp.fontSize = fontSize;
                    inst.Tmp.alignment = alignment;
                    if (font != null)
                        inst.Tmp.font = font;
                    inst.Tmp.ForceMeshUpdate(true);
                }
            }
        }

        void ApplyText()
        {
            if (_instances.Count == 0)
                RefreshInstancesFromChildren();

            for (var i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst.Tmp == null)
                    continue;
                inst.Tmp.text = text;
                inst.Tmp.ForceMeshUpdate(true);
            }
        }

        void ApplyColor()
        {
            for (var i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst.Tmp != null)
                    inst.Tmp.color = color;
            }
        }

        void ApplyFontSizeToInstances()
        {
            for (var i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst.Tmp != null)
                    inst.Tmp.fontSize = fontSize;
            }
        }

#if UNITY_EDITOR
        bool _editorRebuildQueued;

        void OnValidate()
        {
            // Do not push TMP/content synchronously here — it runs before prefab Apply finishes merging
            // children and fights overrides. Defer to Rebuild(), which uses ShouldDriveStationTmpContent.
#if UNITY_EDITOR
            if (Application.isPlaying)
                Rebuild();
            else
                QueueEditorSafeRebuild();
#endif
        }

        void QueueEditorSafeRebuild()
        {
            if (_editorRebuildQueued)
                return;
            _editorRebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _editorRebuildQueued = false;
                if (this == null || Application.isPlaying)
                    return;
                Rebuild();
            };
        }
#endif
    }
}
