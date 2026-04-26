// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using UnityEngine;

namespace HoloCade.Cube
{
    [Serializable]
    public struct CubeStereoIntrinsics
    {
        public float fx;
        public float fy;
        public float cx;
        public float cy;

        public Matrix4x4 BuildK()
        {
            var k = Matrix4x4.identity;
            k.m00 = fx;
            k.m11 = fy;
            k.m02 = cx;
            k.m12 = cy;
            return k;
        }
    }

    [Serializable]
    public struct CubeStereoViewFrame
    {
        public Texture2D color;
        public Texture2D depthMeters;
        public Texture2D confidence;
        public CubeStereoIntrinsics intrinsics;
        public Matrix4x4 cameraToWorld;
    }

    [Serializable]
    public struct CubeStereoFrame
    {
        public CubeStereoViewFrame left;
        public CubeStereoViewFrame right;
    }
}
