// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloCade.AIFacemask
{
    /// <summary>
    /// Facial animation data structure - receives output from NVIDIA ACE
    /// 
    /// This structure receives facial textures and blend shapes from NVIDIA ACE pipeline.
    /// The AI facial animation is fully automated - no manual control or keyframe animation.
    /// NVIDIA ACE determines facial expressions based on audio track and state machine context.
    /// </summary>
    [Serializable]
    public class FacialAnimationData
    {
        [Tooltip("Blend shape weights from NVIDIA ACE (normalized 0-1)")]
        public Dictionary<string, float> blendShapeWeights = new Dictionary<string, float>();

        [Tooltip("Facial texture data from NVIDIA ACE (if applicable)")]
        public Texture2D facialTexture = null;

        [Tooltip("Timestamp of this animation frame")]
        public float timestamp = 0.0f;

        public FacialAnimationData()
        {
            blendShapeWeights = new Dictionary<string, float>();
            facialTexture = null;
            timestamp = 0.0f;
        }
    }

    /// <summary>
    /// Configuration for AI Face system
    /// </summary>
    [Serializable]
    public class AIFaceConfig
    {
        [Tooltip("Target skeletal mesh component attached to live actor's HMD/head")]
        public SkinnedMeshRenderer targetMesh = null;

        [Tooltip("NVIDIA ACE endpoint URL for receiving facial animation data")]
        public string nvidiaACEEndpointURL = "";

        [Tooltip("Update rate for receiving facial animation data from NVIDIA ACE (Hz)")]
        [Range(1f, 120f)]
        public float updateRate = 30.0f;

        public AIFaceConfig()
        {
            targetMesh = null;
            nvidiaACEEndpointURL = "";
            updateRate = 30.0f;
        }
    }
}



