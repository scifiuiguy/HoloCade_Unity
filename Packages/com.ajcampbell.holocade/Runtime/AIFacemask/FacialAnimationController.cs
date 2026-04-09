// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.AIFacemask
{
    /// <summary>
    /// Facial animation modes
    /// </summary>
    public enum FacialAnimationMode
    {
        Live,           // Real-time AI-driven from actor
        Prerecorded,    // Playback from recorded session
        Procedural      // Procedural/scripted animation
    }

    /// <summary>
    /// Facial animation blend shape data
    /// </summary>
    [System.Serializable]
    public class FacialBlendShapeData
    {
        public string blendShapeName;
        [Range(0f, 100f)]
        public float weight;

        public FacialBlendShapeData(string name, float value)
        {
            blendShapeName = name;
            weight = value;
        }
    }

    /// <summary>
    /// Facial animation frame containing all blend shape weights
    /// </summary>
    [System.Serializable]
    public class FacialAnimationFrame
    {
        public float timestamp;
        public List<FacialBlendShapeData> blendShapes = new List<FacialBlendShapeData>();

        public FacialAnimationFrame(float time)
        {
            timestamp = time;
        }
    }

    /// <summary>
    /// Controls facial animation for AI-driven avatars
    /// 
    /// Designed as a bridge to NVIDIA Audio2Face (Neural Face) technology for autonomous
    /// conversational avatars that can interact with players without manual control.
    /// 
    /// The live actor does NOT manually control facial expressions - the AI does this autonomously
    /// based on conversation, emotion detection, and narrative context. The live actor focuses on
    /// directing the experience through wrist-mounted state machine controls.
    /// 
    /// Future Integration: NVIDIA Omniverse Audio2Face (requires separate server for Unity)
    /// 
    /// Supports live autonomous animation, prerecorded playback, and procedural animation.
    /// </summary>
    public class FacialAnimationController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private FacialAnimationMode animationMode = FacialAnimationMode.Live;
        [SerializeField] private SkinnedMeshRenderer faceMeshRenderer;
        // NOOP: TODO - Implement lip sync enable/disable logic
#pragma warning disable CS0414 // Field is assigned but never used (intentionally unused - future feature)
        [SerializeField] private bool enableLipSync = true;
        // NOOP: TODO - Implement eye tracking enable/disable logic
        [SerializeField] private bool enableEyeTracking = false;
#pragma warning restore CS0414

        [Header("Network Settings")]
        [SerializeField] private string actorStreamIP = "192.168.1.50";
        [SerializeField] private int actorStreamPort = 9000;

        [Header("Playback")]
        [SerializeField] private List<FacialAnimationFrame> recordedFrames = new List<FacialAnimationFrame>();
        [SerializeField] private bool loop = true;

        private bool isInitialized = false;
        private bool isPlaying = false;
        private float playbackTime = 0f;
        private int currentFrameIndex = 0;

        private Dictionary<string, int> blendShapeIndices = new Dictionary<string, int>();

        #region Initialization

        private void Awake()
        {
            if (faceMeshRenderer == null)
            {
                faceMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }
        }

        /// <summary>
        /// Initialize the facial animation system
        /// </summary>
        public bool Initialize()
        {
            if (isInitialized)
            {
                return true;
            }

            if (faceMeshRenderer == null)
            {
                Debug.LogError("[HoloCade] FacialAnimationController: No SkinnedMeshRenderer assigned");
                return false;
            }

            // Cache blend shape indices for performance
            CacheBlendShapeIndices();

            isInitialized = true;
            Debug.Log($"[HoloCade] Facial animation initialized with {blendShapeIndices.Count} blend shapes");
            return true;
        }

        private void CacheBlendShapeIndices()
        {
            blendShapeIndices.Clear();
            Mesh mesh = faceMeshRenderer.sharedMesh;

            if (mesh == null)
            {
                Debug.LogWarning("[HoloCade] No mesh found on SkinnedMeshRenderer");
                return;
            }

            int blendShapeCount = mesh.blendShapeCount;
            for (int i = 0; i < blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                blendShapeIndices[name] = i;
            }
        }

        #endregion

        #region Animation Control

        /// <summary>
        /// Start facial animation
        /// </summary>
        public void StartAnimation()
        {
            if (!isInitialized)
            {
                Initialize();
            }

            switch (animationMode)
            {
                case FacialAnimationMode.Live:
                    StartLiveMode();
                    break;
                case FacialAnimationMode.Prerecorded:
                    StartPlayback();
                    break;
                case FacialAnimationMode.Procedural:
                    // Procedural mode is driven by external calls
                    break;
            }

            isPlaying = true;
            Debug.Log($"[HoloCade] Started facial animation in {animationMode} mode");
        }

        /// <summary>
        /// Stop facial animation
        /// </summary>
        public void StopAnimation()
        {
            isPlaying = false;

            if (animationMode == FacialAnimationMode.Live)
            {
                StopLiveMode();
            }

            Debug.Log("[HoloCade] Stopped facial animation");
        }

        /// <summary>
        /// Set animation mode
        /// </summary>
        public void SetAnimationMode(FacialAnimationMode mode)
        {
            if (isPlaying)
            {
                StopAnimation();
            }

            animationMode = mode;

            if (isPlaying)
            {
                StartAnimation();
            }
        }

        #endregion

        #region Live Mode (Actor-Driven)

        private void StartLiveMode()
        {
            // NOOP: TODO - Connect to actor stream via TCP/UDP
            // Receive blend shape data from actor's facial capture system
            Debug.Log($"[HoloCade] Connecting to actor stream at {actorStreamIP}:{actorStreamPort}");
        }

        private void StopLiveMode()
        {
            // NOOP: TODO - Disconnect from actor stream
            Debug.Log("[HoloCade] Disconnected from actor stream");
        }

        private void UpdateLiveMode()
        {
            // NOOP: TODO - Process incoming facial data from actor
            // Apply blend shapes in real-time
        }

        #endregion

        #region Prerecorded Mode

        /// <summary>
        /// Start playback of recorded facial animation
        /// </summary>
        public void StartPlayback()
        {
            if (recordedFrames.Count == 0)
            {
                Debug.LogWarning("[HoloCade] No recorded frames to play");
                return;
            }

            playbackTime = 0f;
            currentFrameIndex = 0;
            isPlaying = true;
        }

        /// <summary>
        /// Load recorded facial animation data
        /// </summary>
        public void LoadRecording(List<FacialAnimationFrame> frames)
        {
            recordedFrames = frames;
            Debug.Log($"[HoloCade] Loaded {frames.Count} animation frames");
        }

        /// <summary>
        /// Start recording facial animation
        /// </summary>
        public void StartRecording()
        {
            recordedFrames.Clear();
            playbackTime = 0f;
            Debug.Log("[HoloCade] Started recording facial animation");
        }

        /// <summary>
        /// Stop recording facial animation
        /// </summary>
        public List<FacialAnimationFrame> StopRecording()
        {
            Debug.Log($"[HoloCade] Stopped recording: {recordedFrames.Count} frames captured");
            return new List<FacialAnimationFrame>(recordedFrames);
        }

        #endregion

        #region Blend Shape Control

        /// <summary>
        /// Set a specific blend shape weight
        /// </summary>
        /// <param name="blendShapeName">Name of the blend shape</param>
        /// <param name="weight">Weight value (0-100)</param>
        public void SetBlendShapeWeight(string blendShapeName, float weight)
        {
            if (!isInitialized || faceMeshRenderer == null)
            {
                return;
            }

            if (blendShapeIndices.TryGetValue(blendShapeName, out int index))
            {
                faceMeshRenderer.SetBlendShapeWeight(index, Mathf.Clamp(weight, 0f, 100f));
            }
            else
            {
                Debug.LogWarning($"[HoloCade] Blend shape '{blendShapeName}' not found");
            }
        }

        /// <summary>
        /// Get current weight of a blend shape
        /// </summary>
        public float GetBlendShapeWeight(string blendShapeName)
        {
            if (!isInitialized || faceMeshRenderer == null)
            {
                return 0f;
            }

            if (blendShapeIndices.TryGetValue(blendShapeName, out int index))
            {
                return faceMeshRenderer.GetBlendShapeWeight(index);
            }

            return 0f;
        }

        /// <summary>
        /// Apply a full animation frame
        /// </summary>
        public void ApplyAnimationFrame(FacialAnimationFrame frame)
        {
            if (!isInitialized)
            {
                return;
            }

            foreach (var blendShape in frame.blendShapes)
            {
                SetBlendShapeWeight(blendShape.blendShapeName, blendShape.weight);
            }
        }

        /// <summary>
        /// Reset all blend shapes to neutral
        /// </summary>
        public void ResetAllBlendShapes()
        {
            if (!isInitialized || faceMeshRenderer == null)
            {
                return;
            }

            foreach (var kvp in blendShapeIndices)
            {
                faceMeshRenderer.SetBlendShapeWeight(kvp.Value, 0f);
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (!isInitialized || !isPlaying)
            {
                return;
            }

            switch (animationMode)
            {
                case FacialAnimationMode.Live:
                    UpdateLiveMode();
                    break;

                case FacialAnimationMode.Prerecorded:
                    UpdatePlayback();
                    break;

                case FacialAnimationMode.Procedural:
                    // Driven externally
                    break;
            }
        }

        private void UpdatePlayback()
        {
            if (recordedFrames.Count == 0)
            {
                return;
            }

            playbackTime += Time.deltaTime;

            // Find current frame
            while (currentFrameIndex < recordedFrames.Count - 1 &&
                   recordedFrames[currentFrameIndex + 1].timestamp <= playbackTime)
            {
                currentFrameIndex++;
            }

            // Apply current frame
            if (currentFrameIndex < recordedFrames.Count)
            {
                ApplyAnimationFrame(recordedFrames[currentFrameIndex]);
            }

            // Handle looping
            if (currentFrameIndex >= recordedFrames.Count - 1)
            {
                if (loop)
                {
                    playbackTime = 0f;
                    currentFrameIndex = 0;
                }
                else
                {
                    isPlaying = false;
                }
            }
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Check if animation is currently playing
        /// </summary>
        public bool IsPlaying()
        {
            return isPlaying;
        }

        /// <summary>
        /// Get current animation mode
        /// </summary>
        public FacialAnimationMode GetAnimationMode()
        {
            return animationMode;
        }

        /// <summary>
        /// Get list of available blend shape names
        /// </summary>
        public List<string> GetAvailableBlendShapes()
        {
            return new List<string>(blendShapeIndices.Keys);
        }

        #endregion
    }
}

