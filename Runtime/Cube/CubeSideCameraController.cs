// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using UnityEngine;

namespace HoloCade.Cube
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CubeSideCameraController : MonoBehaviour
    {
        CubeRuntimeConfig _runtimeConfig;
        CubeFaceTrackingProviderBase _trackingProvider;
        Transform _sideAnchor;
        Camera _sideCamera;
        Vector3 _baseLocalPosition;
        Vector3 _smoothedParallaxLocalOffset;
        float _windowWidth;
        float _windowHeight;

        public CubeSide Side { get; private set; }
        public Camera SideCamera => _sideCamera;

        public void Initialize(
            CubeSide side,
            CubeRuntimeConfig runtimeConfig,
            CubeFaceTrackingProviderBase trackingProvider,
            Transform sideAnchor,
            Vector3 baseLocalPosition,
            float windowWidth,
            float windowHeight)
        {
            Side = side;
            _runtimeConfig = runtimeConfig;
            _trackingProvider = trackingProvider;
            _sideAnchor = sideAnchor;
            _baseLocalPosition = baseLocalPosition;
            _smoothedParallaxLocalOffset = Vector3.zero;
            _windowWidth = Mathf.Max(0.001f, windowWidth);
            _windowHeight = Mathf.Max(0.001f, windowHeight);

            _sideCamera = GetComponent<Camera>();
            if (_sideCamera != null && _runtimeConfig != null)
            {
                _sideCamera.cullingMask = _runtimeConfig.sideCameraCullingMask;
                _sideCamera.farClipPlane = Mathf.Max(0.01f, _runtimeConfig.farClip);
                _sideCamera.fieldOfView = Mathf.Clamp(_runtimeConfig.fovDegrees, 1f, 179f);
                _sideCamera.nearClipPlane = Mathf.Max(0.001f, _runtimeConfig.nominalNearClip);
            }
        }

        void LateUpdate()
        {
            if (_runtimeConfig == null || !_runtimeConfig.enableParallaxTracking || _trackingProvider == null || _sideAnchor == null)
            {
                if (Application.isPlaying)
                {
                    transform.localPosition = _baseLocalPosition;
                }
                else
                {
                    // In edit mode, allow manual camera placement for debug alignment work.
                    _baseLocalPosition = transform.localPosition;
                }
                ApplyProjection();
                return;
            }

            if (!_trackingProvider.TryGetEyeCenterWorldPosition(Side, out var eyeWorld))
            {
                if (Application.isPlaying)
                {
                    transform.localPosition = _baseLocalPosition + _smoothedParallaxLocalOffset;
                }
                else
                {
                    // In edit mode with no live tracking sample, keep manual placement.
                    _baseLocalPosition = transform.localPosition;
                }
                ApplyProjection();
                return;
            }

            var sideLocalEye = _sideAnchor.InverseTransformPoint(eyeWorld);
            var bounds = new Bounds(_runtimeConfig.trackingBoundsCenter, _runtimeConfig.trackingBoundsSize);
            if (!bounds.Contains(sideLocalEye))
                return;

            var localOffset = new Vector3(
                Mathf.Clamp(sideLocalEye.x, -_runtimeConfig.maxParallaxOffsetX, _runtimeConfig.maxParallaxOffsetX),
                Mathf.Clamp(sideLocalEye.y, -_runtimeConfig.maxParallaxOffsetY, _runtimeConfig.maxParallaxOffsetY),
                Mathf.Clamp(sideLocalEye.z - _runtimeConfig.trackingBoundsCenter.z, -_runtimeConfig.maxParallaxOffsetZ, _runtimeConfig.maxParallaxOffsetZ));

            var lerpT = 1f - Mathf.Exp(-Mathf.Max(0f, _runtimeConfig.parallaxSmoothingSpeed) * Time.deltaTime);
            _smoothedParallaxLocalOffset = Vector3.Lerp(_smoothedParallaxLocalOffset, localOffset, lerpT);
            transform.localPosition = _baseLocalPosition + _smoothedParallaxLocalOffset;
            ApplyProjection();
        }

        void ApplyProjection()
        {
            if (_runtimeConfig == null || _sideAnchor == null || _sideCamera == null)
                return;

            _sideCamera.nearClipPlane = Mathf.Max(0.001f, _runtimeConfig.nominalNearClip);
            var configuredFar = Mathf.Max(_sideCamera.nearClipPlane + 0.01f, _runtimeConfig.farClip);
            var recommendedFar = Mathf.Max(_windowWidth, _windowHeight) * 6f;
            _sideCamera.farClipPlane = Mathf.Max(configuredFar, recommendedFar);

            if (!_runtimeConfig.enableOffAxisProjection)
            {
                _sideCamera.ResetProjectionMatrix();
                return;
            }

            var near = _sideCamera.nearClipPlane;
            var far = _sideCamera.farClipPlane;

            var screenCenter = _sideAnchor.position;
            var vr = _sideAnchor.right.normalized;
            var vu = _sideAnchor.up.normalized;
            // Outward normal points from boundary toward the camera side.
            var vn = (-_sideAnchor.forward).normalized;

            var halfW = _windowWidth * 0.5f;
            var halfH = _windowHeight * 0.5f;
            var pa = screenCenter - vr * halfW - vu * halfH; // lower-left
            var pb = screenCenter + vr * halfW - vu * halfH; // lower-right
            var pc = screenCenter - vr * halfW + vu * halfH; // upper-left
            var pe = transform.position; // tracked eye (camera) position

            var va = pa - pe;
            var vb = pb - pe;
            var vc = pc - pe;

            var distanceToPlane = -Vector3.Dot(va, vn);
            if (distanceToPlane <= 0.0001f)
                return;

            var left = Vector3.Dot(vr, va) * near / distanceToPlane;
            var right = Vector3.Dot(vr, vb) * near / distanceToPlane;
            var bottom = Vector3.Dot(vu, va) * near / distanceToPlane;
            var top = Vector3.Dot(vu, vc) * near / distanceToPlane;

            var projection = Matrix4x4.Frustum(left, right, bottom, top, near, far);
            var clipPlaneWorldPos = screenCenter;
            var clipPlaneWorldNormal = vn; // clip plane fixed to boundary
            var clipPlaneCameraSpace = CameraSpacePlane(_sideCamera, clipPlaneWorldPos, clipPlaneWorldNormal, 1f);
            _sideCamera.projectionMatrix = projection;
            projection = _sideCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);

            _sideCamera.projectionMatrix = projection;
        }

        static Vector4 CameraSpacePlane(Camera camera, Vector3 worldPos, Vector3 worldNormal, float sideSign)
        {
            var offsetPos = worldPos + worldNormal * 0.001f;
            var viewMatrix = camera.worldToCameraMatrix;
            var cameraPos = viewMatrix.MultiplyPoint(offsetPos);
            var cameraNormal = viewMatrix.MultiplyVector(worldNormal).normalized * sideSign;
            return new Vector4(
                cameraNormal.x,
                cameraNormal.y,
                cameraNormal.z,
                -Vector3.Dot(cameraPos, cameraNormal));
        }
    }
}
