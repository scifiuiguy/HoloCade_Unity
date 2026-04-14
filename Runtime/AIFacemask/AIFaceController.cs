// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.AIFacemask
{
    /// <summary>
    /// AI Face Controller Component
    /// 
    /// Receives and applies NVIDIA ACE facial animation output to a live actor's HMD-mounted mesh.
    /// 
    /// ARCHITECTURE:
    /// - Live actor wears HMD with AIFace mesh tracked on top of their face (like a mask)
    /// - NVIDIA ACE pipeline (Audio → NLU → Emotion → Facial Animation) generates facial textures
    ///   and blend shapes automatically based on audio track and state machine context
    /// - This component receives NVIDIA ACE output and applies it to the mesh in real-time
    /// - NO manual control, keyframe animation, rigging, or blend shape tools required
    /// 
    /// USAGE:
    /// - Attach to live actor's HMD/head GameObject
    /// - Configure TargetMesh to point to the AIFace SkinnedMeshRenderer component
    /// - NVIDIA ACE streams facial animation data to this component
    /// - Component applies received data to mesh automatically
    /// 
    /// IMPORTANT:
    /// - This is a RECEIVER/DISPLAY system, not a control system
    /// - Facial expressions are determined by NVIDIA ACE, not manually configured
    /// - Live actor controls experience flow via wrist buttons, not facial animation
    /// </summary>
    [AddComponentMenu("HoloCade/AI Face Controller")]
    public class AIFaceController : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Configuration for this AI face controller")]
        public AIFaceConfig config = new AIFaceConfig();

        [Header("Status (Read-Only)")]
        [Tooltip("Current facial animation data from NVIDIA ACE")]
        public FacialAnimationData currentAnimationData = new FacialAnimationData();

        [Tooltip("Whether the system is initialized")]
        public bool isInitialized = false;

        private Dictionary<string, int> blendShapeIndices = new Dictionary<string, int>();
        private float updateTimer = 0.0f;

        #region Unity Lifecycle

        private void Awake()
        {
            if (config.targetMesh == null)
            {
                config.targetMesh = GetComponentInChildren<SkinnedMeshRenderer>();
            }
        }

        private void Start()
        {
            if (config.targetMesh != null)
            {
                InitializeAIFace(config);
            }
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            updateTimer += Time.deltaTime;

            // Update at configured rate (receiving from NVIDIA ACE)
            if (updateTimer >= (1.0f / config.updateRate))
            {
                updateTimer = 0.0f;

                // NOOP: TODO - Receive facial animation data from NVIDIA ACE endpoint
                // NVIDIA ACE will stream facial textures and blend shapes based on:
                // - Audio track (speech recognition)
                // - NLU (natural language understanding)
                // - Emotion detection
                // - State machine context
                // This component receives and applies the output - no manual control needed
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the AI face system with given configuration
        /// </summary>
        /// <param name="inConfig">Configuration settings</param>
        /// <returns>true if initialization was successful</returns>
        public bool InitializeAIFace(AIFaceConfig inConfig)
        {
            if (isInitialized)
            {
                Debug.LogWarning("[HoloCade] AIFaceController: Already initialized");
                return true;
            }

            config = inConfig;

            if (config.targetMesh == null)
            {
                Debug.LogError("[HoloCade] AIFaceController: No target mesh assigned");
                return false;
            }

            // Cache blend shape indices for performance
            CacheBlendShapeIndices();

            isInitialized = true;
            Debug.Log($"[HoloCade] AIFaceController: Initialized with {blendShapeIndices.Count} blend shapes (NVIDIA ACE receiver mode)");
            return true;
        }

        private void CacheBlendShapeIndices()
        {
            blendShapeIndices.Clear();

            if (config.targetMesh == null || config.targetMesh.sharedMesh == null)
            {
                Debug.LogWarning("[HoloCade] AIFaceController: No mesh found on SkinnedMeshRenderer");
                return;
            }

            Mesh mesh = config.targetMesh.sharedMesh;
            int blendShapeCount = mesh.blendShapeCount;

            for (int i = 0; i < blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                blendShapeIndices[name] = i;
            }
        }

        #endregion

        #region Receive Animation Data

        /// <summary>
        /// Receive and apply facial animation data from NVIDIA ACE
        /// Called automatically when NVIDIA ACE sends new facial animation data
        /// </summary>
        /// <param name="animationData">Facial animation data from NVIDIA ACE (blend shapes + textures)</param>
        public void ReceiveFacialAnimationData(FacialAnimationData animationData)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[HoloCade] AIFaceController: Cannot receive animation data - not initialized");
                return;
            }

            currentAnimationData = animationData;

            // Apply blend shapes from NVIDIA ACE to target mesh
            if (config.targetMesh != null && animationData.blendShapeWeights != null && animationData.blendShapeWeights.Count > 0)
            {
                ApplyBlendShapesToMesh(animationData.blendShapeWeights);
            }

            // Apply facial texture from NVIDIA ACE to target mesh
            if (config.targetMesh != null && animationData.facialTexture != null)
            {
                ApplyFacialTextureToMesh(animationData.facialTexture);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Apply received blend shapes to target mesh
        /// </summary>
        private void ApplyBlendShapesToMesh(Dictionary<string, float> blendShapeWeights)
        {
            if (config.targetMesh == null)
            {
                return;
            }

            // NOOP: TODO - Apply blend shape weights to SkinnedMeshRenderer
            // Map NVIDIA ACE blend shape names to Unity blend shape names
            // Apply weights using SkinnedMeshRenderer.SetBlendShapeWeight()
            foreach (var kvp in blendShapeWeights)
            {
                string blendShapeName = kvp.Key;
                float weight = kvp.Value;  // Normalized 0-1 from NVIDIA ACE

                if (blendShapeIndices.TryGetValue(blendShapeName, out int index))
                {
                    // Convert normalized weight (0-1) to Unity blend shape weight (0-100)
                    float unityWeight = weight * 100f;
                    config.targetMesh.SetBlendShapeWeight(index, unityWeight);
                }
                else
                {
                    Debug.LogWarning($"[HoloCade] AIFaceController: Blend shape '{blendShapeName}' not found on mesh");
                }
            }
        }

        /// <summary>
        /// Apply received facial texture to target mesh
        /// </summary>
        private void ApplyFacialTextureToMesh(Texture2D facialTexture)
        {
            if (config.targetMesh == null || facialTexture == null)
            {
                return;
            }

            // NOOP: TODO - Apply facial texture to mesh material
            // Update material parameter for facial texture
            // Example:
            // Material material = config.targetMesh.material;
            // if (material != null)
            // {
            //     material.SetTexture("_MainTex", facialTexture);
            //     // Or use a specific texture property name for facial texture
            // }
        }

        #endregion
    }
}



