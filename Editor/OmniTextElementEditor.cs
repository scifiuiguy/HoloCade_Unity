// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using HoloCade.Cube;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HoloCade.Editor
{
    /// <summary>
    /// Inspector + menu utilities for <see cref="OmniTextElement"/>, including removal of
    /// duplicate <c>OmniText_*</c> direct children (legacy spawns, Play-mode overlap, etc.).
    /// </summary>
    [CustomEditor(typeof(OmniTextElement))]
    public class OmniTextElementEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "\"Drive Station Content From Parent In Edit Mode\" is off by default so Merge/Prefab Apply " +
                "can persist station TMP overrides without ExecuteAlways immediately overwriting them. " +
                "Turn it on only when you want live mirroring from this component’s Content fields in Edit Mode.",
                MessageType.Info);
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Duplicate station children (same name under this transform) usually come from older " +
                "OmniText builds that respawned TMP during Play / Edit or from overlapping Rebuild calls. " +
                "Only the first sibling per name is kept; extras are deleted.",
                MessageType.Info);
            if (GUILayout.Button("Remove duplicate OmniText_* station children"))
            {
                var el = (OmniTextElement)target;
                Undo.RecordObject(el.transform, "Remove duplicate OmniText station children");
                var removed = RemoveDuplicateOmniTextStationChildren(el.transform);
                EditorUtility.SetDirty(el.gameObject);
                if (removed > 0)
                    Debug.Log($"[OmniTextElement] Removed {removed} duplicate station child(ren) under '{el.name}'.", el);
                else
                    Debug.Log($"[OmniTextElement] No duplicate OmniText_* names under '{el.name}'.", el);
            }
        }

        /// <summary>
        /// Keeps the first direct child per <c>OmniText_*</c> name (lowest sibling index); destroys the rest.
        /// </summary>
        public static int RemoveDuplicateOmniTextStationChildren(Transform root)
        {
            if (root == null)
                return 0;

            var firstIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (!c.name.StartsWith("OmniText_", StringComparison.Ordinal))
                    continue;
                if (!firstIndexByName.ContainsKey(c.name))
                    firstIndexByName[c.name] = i;
            }

            var removed = 0;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i);
                if (!c.name.StartsWith("OmniText_", StringComparison.Ordinal))
                    continue;
                if (!firstIndexByName.TryGetValue(c.name, out var keepIdx) || keepIdx != i)
                {
                    Undo.DestroyObjectImmediate(c.gameObject);
                    removed++;
                }
            }

            return removed;
        }

        [MenuItem("GameObject/HoloCade/Cube/Remove Duplicate OmniText Station Children", false, 10)]
        static void MenuRemoveDuplicates()
        {
            var total = 0;
            foreach (var go in Selection.gameObjects)
            {
                var el = go.GetComponent<OmniTextElement>();
                if (el == null)
                    continue;
                Undo.RecordObject(el.transform, "Remove duplicate OmniText station children");
                total += RemoveDuplicateOmniTextStationChildren(el.transform);
                EditorUtility.SetDirty(el.gameObject);
            }

            if (total > 0)
                Debug.Log($"[OmniTextElement] Removed {total} duplicate station child(ren) on selection.");
        }

        [MenuItem("GameObject/HoloCade/Cube/Remove Duplicate OmniText Station Children", true)]
        static bool MenuRemoveDuplicatesValidate()
        {
            foreach (var go in Selection.gameObjects)
                if (go.GetComponent<OmniTextElement>() != null)
                    return true;
            return false;
        }

        /// <summary>
        /// Canonical station shell names produced by <see cref="OmniTextElement"/> — avoids deleting unrelated GameObjects named OmniText_*.
        /// </summary>
        static bool IsCanonicalOmniTextStationShellName(string name)
        {
            switch (name)
            {
                case "OmniText_North":
                case "OmniText_South":
                case "OmniText_East":
                case "OmniText_West":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Same as <see cref="IsCanonicalOmniTextStationShellName"/> but strips Unity duplicate / clone suffixes
        /// (<c>OmniText_North (1)</c>, <c>OmniText_North (Clone)</c>) so orphaned duplicates still match as strays.
        /// </summary>
        static bool IsCanonicalOmniTextStationShellNameIgnoringUnitySuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            var baseName = name;
            var spaceParen = baseName.LastIndexOf(" (", StringComparison.Ordinal);
            if (spaceParen >= 0 && baseName.EndsWith(")", StringComparison.Ordinal))
                baseName = baseName.Substring(0, spaceParen);
            return IsCanonicalOmniTextStationShellName(baseName);
        }

        /// <summary>
        /// Edit mode: include scene objects in loaded scenes, plus transient editor objects with no valid
        /// <see cref="UnityEngine.SceneManagement.Scene"/> (e.g. HideAndDontSave leftovers that still render/pick).
        /// Play mode: only objects in loaded scenes (same as before).
        /// </summary>
        static bool ShouldIncludeStrayOmniTextStationShell(GameObject go)
        {
            if (Application.isPlaying)
                return go.scene.IsValid() && go.scene.isLoaded;
            if (go.scene.IsValid())
                return go.scene.isLoaded;
            return true;
        }

        /// <summary>
        /// Station TMP shells with no <see cref="OmniTextElement"/> parent.
        /// Uses <see cref="Object.FindObjectsOfType{T}(bool)"/> so roots with
        /// <see cref="HideFlags.HideInHierarchy"/> (and similar) are found — they never appear in the
        /// Hierarchy window but still render and pick in Scene view. Recursive traversal from
        /// <see cref="Scene.GetRootGameObjects"/> skips those roots entirely.
        /// </summary>
        static List<GameObject> CollectStrayOmniTextStationObjectsInLoadedScenes()
        {
            var result = new List<GameObject>();
            var seen = new HashSet<int>();
            var transforms = UnityEngine.Object.FindObjectsOfType<Transform>(true);
            foreach (var t in transforms)
            {
                var go = t.gameObject;
                if (EditorUtility.IsPersistent(go))
                    continue;
                if (!ShouldIncludeStrayOmniTextStationShell(go))
                    continue;
                if (!IsCanonicalOmniTextStationShellNameIgnoringUnitySuffix(go.name))
                    continue;
                if (go.GetComponentInParent<OmniTextElement>(true) != null)
                    continue;

                var id = go.GetInstanceID();
                if (!seen.Add(id))
                    continue;
                result.Add(go);
            }

            return result;
        }

        static void ClearHideFlagsRecursiveUndo(GameObject go)
        {
            if (go == null)
                return;
            Undo.RecordObject(go, "Unhide OmniText hierarchy");
            go.hideFlags = HideFlags.None;
            var tr = go.transform;
            for (var i = 0; i < tr.childCount; i++)
                ClearHideFlagsRecursiveUndo(tr.GetChild(i).gameObject);
        }

        /// <summary>
        /// Objects with <see cref="HideFlags.HideInHierarchy"/> still render and pick in Scene view but
        /// do not show in Hierarchy — this clears flags on OmniText roots and station shells so you can
        /// select them normally (does not delete anything).
        /// </summary>
        [MenuItem("HoloCade/Cube/Unhide OmniText Hierarchies (Clear HideFlags)", false, 19)]
        static void MenuUnhideOmniTextHierarchies()
        {
            Undo.SetCurrentGroupName("Unhide OmniText hierarchies");
            var undoGroup = Undo.GetCurrentGroup();
            var touched = 0;

            var elements = Object.FindObjectsOfType<OmniTextElement>(true);
            foreach (var el in elements)
            {
                if (el == null || EditorUtility.IsPersistent(el.gameObject))
                    continue;
                if (!ShouldIncludeStrayOmniTextStationShell(el.gameObject))
                    continue;
                ClearHideFlagsRecursiveUndo(el.gameObject);
                touched++;
            }

            var transforms = Object.FindObjectsOfType<Transform>(true);
            foreach (var t in transforms)
            {
                var go = t.gameObject;
                if (EditorUtility.IsPersistent(go))
                    continue;
                if (!ShouldIncludeStrayOmniTextStationShell(go))
                    continue;
                if (!IsCanonicalOmniTextStationShellNameIgnoringUnitySuffix(go.name))
                    continue;
                // Shells under OmniTextElement were fully cleared in the loop above.
                if (go.GetComponentInParent<OmniTextElement>(true) != null)
                    continue;
                ClearHideFlagsRecursiveUndo(go);
                touched++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[OmniTextElement] Cleared HideFlags on {touched} OmniText hierarchy / shell root(s). Save scenes if you want the change persisted.");
        }

        [MenuItem("HoloCade/Cube/Delete Stray OmniText Station Objects (Loaded Scenes)", false, 20)]
        static void MenuDeleteStrayOmniTextStationObjects()
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete stray OmniText station objects",
                    "Delete GameObjects whose names resolve to OmniText_North / South / East / West (including " +
                    "\"OmniText_North (1)\" style duplicates) and have no OmniTextElement on any parent — usually " +
                    "leftovers after removing the OmniText root.\n\n" +
                    "Includes objects hidden from the Hierarchy (e.g. HideInHierarchy), which still pick in Scene view.\n\n" +
                    "Edit mode: also targets transient objects with no saved scene (HideAndDontSave-style orphans).\n\n" +
                    "Does not delete Project assets (prefabs on disk stay untouched unless you have the prefab open).\n\nProceed?",
                    "Delete",
                    "Cancel"))
                return;

            var strays = CollectStrayOmniTextStationObjectsInLoadedScenes();
            if (strays.Count == 0)
            {
                EditorUtility.DisplayDialog("OmniText cleanup", "No stray OmniText station objects found.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Delete stray OmniText station objects");
            var undoGroup = Undo.GetCurrentGroup();

            var scenesTouched = new HashSet<Scene>();
            foreach (var go in strays)
            {
                if (go != null && go.scene.IsValid())
                    scenesTouched.Add(go.scene);
                Undo.DestroyObjectImmediate(go);
            }

            Undo.CollapseUndoOperations(undoGroup);

            foreach (var sc in scenesTouched)
                EditorSceneManager.MarkSceneDirty(sc);

            Debug.Log($"[OmniTextElement] Deleted {strays.Count} stray OmniText station object(s) in loaded scene(s).");
        }

        [MenuItem("HoloCade/Cube/Clean Up All OmniText Issues (Duplicates + Strays, Loaded Scenes)", false, 21)]
        static void MenuCleanupAllOmniTextIssuesInLoadedScenes()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clean up OmniText",
                    "1) Remove duplicate OmniText_* siblings under every OmniTextElement in loaded scenes.\n" +
                    "2) Delete stray OmniText_North/South/East/West objects with no OmniTextElement parent.\n\nProceed?",
                    "Clean up",
                    "Cancel"))
                return;

            Undo.SetCurrentGroupName("OmniText full cleanup (loaded scenes)");
            var undoGroup = Undo.GetCurrentGroup();

            var dupTotal = 0;
            var els = UnityEngine.Object.FindObjectsOfType<OmniTextElement>(true);
            foreach (var el in els)
            {
                if (!el.gameObject.scene.IsValid())
                    continue;
                if (EditorUtility.IsPersistent(el))
                    continue;

                Undo.RecordObject(el.transform, "OmniText duplicate cleanup");
                dupTotal += RemoveDuplicateOmniTextStationChildren(el.transform);
                EditorUtility.SetDirty(el.gameObject);
                EditorSceneManager.MarkSceneDirty(el.gameObject.scene);
            }

            var strays = CollectStrayOmniTextStationObjectsInLoadedScenes();
            foreach (var go in strays)
            {
                if (go != null && go.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(go.scene);
                Undo.DestroyObjectImmediate(go);
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[OmniTextElement] Full cleanup: removed {dupTotal} duplicate station child(ren); deleted {strays.Count} stray station object(s).");
        }
    }
}
