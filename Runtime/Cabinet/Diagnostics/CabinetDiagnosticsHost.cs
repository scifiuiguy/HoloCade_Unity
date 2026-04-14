// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using HoloCade.Core.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoloCade.Cabinet.Diagnostics
{
    /// <summary>
    /// In-game operator / service diagnostic shell (UI Toolkit). Same menu tree is intended across HoloCade cabinets; games add pages via <see cref="CabinetDiagnosticsRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CabinetDiagnosticsHost : MonoBehaviour
    {
        [SerializeField] private ArcadeCabinetBridge bridge;

        [Tooltip("If null, bridge is found on the same GameObject.")]
        [SerializeField] private HoloCadeUDPTransport transport;

        [SerializeField] private KeyCode toggleKey = KeyCode.F10;

        [SerializeField] private bool startHidden = true;

        GameObject _uiHostGo;
        UIDocument _uiDocument;
        VisualElement _root;
        VisualElement _pageHost;
        readonly List<(string title, System.Action<VisualElement, CabinetDiagnosticsContext> build)> _builtIns = new List<(string, System.Action<VisualElement, CabinetDiagnosticsContext>)>();

        bool _visible;

        void Awake()
        {
            if (bridge == null)
                bridge = GetComponent<ArcadeCabinetBridge>();
            if (transport == null && bridge != null)
                transport = bridge.Transport;
            BuildBuiltinPages();
            SetupUiDocument();
        }

        void Start()
        {
            if (startHidden)
                SetShellVisible(false);
        }

        void OnDestroy()
        {
            if (_uiHostGo != null)
                Destroy(_uiHostGo);
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                SetShellVisible(!_visible);
        }

        void BuildBuiltinPages()
        {
            _builtIns.Clear();
            _builtIns.Add(("Input test (switch test)", BuildSwitchTestPage));
            _builtIns.Add(("Output test (LED / solenoid)", BuildOutputTestPlaceholder));
            _builtIns.Add(("Monitor test", BuildMonitorPlaceholder));
            _builtIns.Add(("System & versions", BuildSystemInfoPage));
        }

        void SetupUiDocument()
        {
            _uiHostGo = new GameObject("CabinetDiagnostics_UI");
            _uiHostGo.transform.SetParent(transform, false);
            _uiHostGo.hideFlags = HideFlags.HideAndDontSave;
            _uiDocument = _uiHostGo.AddComponent<UIDocument>();
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            _uiDocument.panelSettings = ps;
            _uiDocument.sortingOrder = 30000;

            _root = new VisualElement { name = "cabinet-diagnostics-root" };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.96f);
            _root.style.paddingLeft = 24;
            _root.style.paddingRight = 24;
            _root.style.paddingTop = 16;
            _root.style.paddingBottom = 16;

            var title = new Label("HoloCade cabinet diagnostics")
            {
                style =
                {
                    fontSize = 22,
                    marginBottom = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white
                }
            };
            _root.Add(title);

            var hint = new Label($"Press {toggleKey} to toggle. Game-specific pages register via {nameof(CabinetDiagnosticsRegistry)}.")
            {
                style = { marginBottom = 12, color = new Color(0.85f, 0.85f, 0.85f) }
            };
            _root.Add(hint);

            var split = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            var menuCol = new VisualElement
            {
                name = "menu-column",
                style =
                {
                    width = 260,
                    marginRight = 16,
                    flexShrink = 0
                }
            };
            _pageHost = new VisualElement { name = "page-column", style = { flexGrow = 1 } };

            foreach (var (ttl, _) in _builtIns)
                menuCol.Add(MakeMenuButton(ttl));

            foreach (var page in CabinetDiagnosticsRegistry.RegisteredPages)
                menuCol.Add(MakeMenuButton(page.Title, page));

            split.Add(menuCol);
            split.Add(_pageHost);
            _root.Add(split);

            _uiDocument.rootVisualElement.Add(_root);
        }

        Button MakeMenuButton(string title, ICabinetDiagnosticPage custom = null)
        {
            var b = new Button(() => ShowPage(title, custom))
            {
                text = title,
                style = { marginBottom = 6, height = 32 }
            };
            return b;
        }

        void ShowPage(string title, ICabinetDiagnosticPage custom)
        {
            _pageHost.Clear();
            var ctx = new CabinetDiagnosticsContext(bridge, transport);
            if (custom != null)
            {
                custom.Build(_pageHost, ctx);
                return;
            }

            foreach (var (ttl, build) in _builtIns)
            {
                if (ttl != title)
                    continue;
                build(_pageHost, ctx);
                return;
            }
        }

        void BuildSwitchTestPage(VisualElement ve, CabinetDiagnosticsContext ctx)
        {
            ve.Add(new Label("Live digital inputs (bools) — assign channels in ArcadeCabinetIOConfig.") { style = { whiteSpace = WhiteSpace.Normal } });
            var lines = new Label("Waiting…") { name = "switch-lines" };
            ve.Add(lines);
            ve.schedule.Execute(() => RefreshSwitchLines(lines, ctx)).Every(100);
        }

        static void RefreshSwitchLines(Label lines, CabinetDiagnosticsContext ctx)
        {
            if (ctx.Transport == null || !ctx.Transport.IsUDPConnected())
            {
                lines.text = "Transport not connected.";
                return;
            }
            lines.text = "UDP connected — detailed channel inspection can be extended (HoloCade firmware channel map).";
        }

        static void BuildOutputTestPlaceholder(VisualElement ve, CabinetDiagnosticsContext _)
        {
            ve.Add(new Label("LED / solenoid / macro tests: drive outputs via ArcadeCabinetBridge and firmware channel map.")
                { style = { whiteSpace = WhiteSpace.Normal } });
        }

        static void BuildMonitorPlaceholder(VisualElement ve, CabinetDiagnosticsContext _)
        {
            ve.Add(new Label("Per-display color bars / crosshatch — hook venue display pipeline here.")
                { style = { whiteSpace = WhiteSpace.Normal } });
        }

        static void BuildSystemInfoPage(VisualElement ve, CabinetDiagnosticsContext ctx)
        {
            var b = ctx.Bridge != null && ctx.Bridge.CabinetConfiguration != null ? ctx.Bridge.CabinetConfiguration.name : "(none)";
            ve.Add(new Label($"Unity: {Application.unityVersion}"));
            ve.Add(new Label($"Config asset: {b}"));
            ve.Add(new Label($"Transport: {(ctx.Transport != null && ctx.Transport.IsUDPConnected() ? "connected" : "not connected")}"));
        }

        public void SetShellVisible(bool visible)
        {
            _visible = visible;
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
