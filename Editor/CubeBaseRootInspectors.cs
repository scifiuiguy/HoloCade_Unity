// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using HoloCade.Cabinet;
using HoloCade.Core.Networking;
using HoloCade.Cube;
using UnityEditor;
using UnityEngine;

namespace HoloCade.Editor
{
    [CustomEditor(typeof(CubeStereoGpuReprojectionPass))]
    [CanEditMultipleObjects]
    sealed class CubeStereoGpuReprojectionPassInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InspectorPurposeDrawer.DrawIfPresent(target);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(HyperCubeQuadrantTcpHost))]
    [CanEditMultipleObjects]
    sealed class HyperCubeQuadrantTcpHostInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InspectorPurposeDrawer.DrawIfPresent(target);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(HoloCadeUDPTransport))]
    [CanEditMultipleObjects]
    sealed class HoloCadeUDPTransportInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InspectorPurposeDrawer.DrawIfPresent(target);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(HyperCubePassthroughBinder))]
    [CanEditMultipleObjects]
    sealed class HyperCubePassthroughBinderInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InspectorPurposeDrawer.DrawIfPresent(target);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(HyperCubePoseTrackingProvider))]
    [CanEditMultipleObjects]
    sealed class HyperCubePoseTrackingProviderInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InspectorPurposeDrawer.DrawIfPresent(target);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(ArcadeCabinetBridge))]
    [CanEditMultipleObjects]
    sealed class ArcadeCabinetBridgeInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InspectorPurposeDrawer.DrawIfPresent(target);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
