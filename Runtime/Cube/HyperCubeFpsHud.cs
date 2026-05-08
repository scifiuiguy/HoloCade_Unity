using UnityEngine;

namespace HoloCade.Runtime.Cube
{
    /// <summary>
    /// Lightweight FPS overlay for quick perf validation in Play Mode.
    /// Attach to any active GameObject in scene.
    /// </summary>
    public sealed class HyperCubeFpsHud : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private bool showHud = true;
        [SerializeField] private bool showMs = true;
        [SerializeField] private int fontSize = 20;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private Vector2 padding = new Vector2(10f, 8f);
        [SerializeField] private Vector2 offset = new Vector2(12f, 12f);

        [Header("Sampling")]
        [SerializeField] private float smoothingSeconds = 0.5f;

        private float _smoothedDeltaTime;
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;
        private Texture2D _bgTexture;
        private bool _stylesDirty = true;

        private void Awake()
        {
            _smoothedDeltaTime = Time.unscaledDeltaTime;
            _stylesDirty = true;
        }

        private void OnValidate()
        {
            if (fontSize < 8)
            {
                fontSize = 8;
            }

            if (smoothingSeconds < 0.05f)
            {
                smoothingSeconds = 0.05f;
            }

            _stylesDirty = true;
        }

        private void Update()
        {
            float lerpFactor = 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothingSeconds);
            _smoothedDeltaTime = Mathf.Lerp(_smoothedDeltaTime, Time.unscaledDeltaTime, lerpFactor);
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            if (_stylesDirty || _labelStyle == null || _boxStyle == null)
            {
                BuildStyles();
            }

            float fps = 1f / Mathf.Max(0.00001f, _smoothedDeltaTime);
            float frameMs = _smoothedDeltaTime * 1000f;
            string text = showMs ? $"FPS: {fps:0.0} ({frameMs:0.0} ms)" : $"FPS: {fps:0.0}";

            Vector2 size = _labelStyle.CalcSize(new GUIContent(text));
            Rect boxRect = new Rect(
                offset.x,
                offset.y,
                size.x + padding.x * 2f,
                size.y + padding.y * 2f
            );
            Rect textRect = new Rect(
                boxRect.x + padding.x,
                boxRect.y + padding.y,
                size.x,
                size.y
            );

            GUI.Box(boxRect, GUIContent.none, _boxStyle);
            GUI.Label(textRect, text, _labelStyle);
        }

        private void OnDestroy()
        {
            if (_bgTexture != null)
            {
                Destroy(_bgTexture);
                _bgTexture = null;
            }
        }

        private void BuildStyles()
        {
            if (_bgTexture == null)
            {
                _bgTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            }

            _bgTexture.SetPixel(0, 0, backgroundColor);
            _bgTexture.Apply();

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = textColor }
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _bgTexture }
            };

            _stylesDirty = false;
        }
    }
}
