using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using EditorUtility = UnityEditor.EditorUtility;

namespace UIU.Simulator.Environment.Editor
{
    /// <summary>
    /// Bakes a selected parent's non-uniform scale into descendant ProBuilderMesh
    /// vertex positions so world-space geometry is preserved and the parent ends at scale (1,1,1).
    /// </summary>
    public static class BakeParentScaleIntoProBuilderMeshes
    {
        const string MenuPath =
            "Tools/UIU Simulator/Environment/Bake Parent Scale Into ProBuilder Meshes";

        const string UndoName = "Bake Parent Scale Into ProBuilder Meshes";
        const float ScaleEpsilon = 1e-5f;
        const float DisplacementWarningMeters = 0.001f;

        // Rebuild lighting/collision attributes only. Skip UV refresh so Auto-UV faces
        // are not regenerated from the new local positions.
        const RefreshMask BakeRefreshMask =
            RefreshMask.Normals |
            RefreshMask.Tangents |
            RefreshMask.Collisions |
            RefreshMask.Bounds;

        [MenuItem(MenuPath, priority = 200)]
        static void BakeFromSelection()
        {
            GameObject parent = Selection.activeGameObject;
            if (parent == null)
            {
                EditorUtility.DisplayDialog(
                    "Bake Parent Scale",
                    "Select a parent GameObject first.",
                    "OK");
                return;
            }

            ProBuilderMesh[] meshes = parent.GetComponentsInChildren<ProBuilderMesh>(true);
            if (meshes == null || meshes.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Bake Parent Scale",
                    "The selected GameObject has no ProBuilderMesh components in its descendants.",
                    "OK");
                return;
            }

            Vector3 parentScale = parent.transform.localScale;
            if (IsApproximatelyOne(parentScale))
            {
                EditorUtility.DisplayDialog(
                    "Bake Parent Scale",
                    "The selected GameObject already has localScale approximately (1, 1, 1). Nothing to bake.",
                    "OK");
                return;
            }

            if (!TryValidateUnsupportedDescendants(parent, out string unsupportedMessage))
            {
                EditorUtility.DisplayDialog("Bake Parent Scale", unsupportedMessage, "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Bake Parent Scale",
                "Bake the selected parent's scale into all descendant ProBuilder meshes?\n\n" +
                "This will preserve their current world-space geometry and reset the " +
                "selected parent's Scale to (1,1,1).\n\n" +
                "Make sure the scene is saved or committed before continuing.",
                "Bake",
                "Cancel");

            if (!confirmed)
                return;

            Bake(parent, meshes);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBakeFromSelection()
        {
            return Selection.activeGameObject != null;
        }

        static void Bake(GameObject parent, ProBuilderMesh[] meshes)
        {
            Transform parentTransform = parent.transform;

            // STEP 2: Cache current world-space vertex positions while the parent
            // still has its non-uniform scale. TransformPoint accounts for nested
            // rotations / diagonal walls — do not multiply local axes by parent scale.
            var worldPositionsByMesh = new Vector3[meshes.Length][];
            for (int i = 0; i < meshes.Length; i++)
                worldPositionsByMesh[i] = meshes[i].VerticesInWorldSpace();

            // STEP 3: Record Undo before any mutation.
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);

            Undo.RegisterCompleteObjectUndo(parentTransform, UndoName);

            var undoTargets = new List<Object>(meshes.Length * 3 + 1);
            for (int i = 0; i < meshes.Length; i++)
            {
                ProBuilderMesh mesh = meshes[i];
                undoTargets.Add(mesh);

                // ToMesh() mutates the Unity Mesh in place; record it so Ctrl+Z
                // restores both ProBuilder positions and the render mesh.
                // ProBuilderMesh.mesh is internal, so use MeshFilter.sharedMesh.
                MeshFilter filter = mesh.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    undoTargets.Add(filter.sharedMesh);

                // Refresh(Collisions) reassigns MeshCollider.sharedMesh — record it too.
                MeshCollider meshCollider = mesh.GetComponent<MeshCollider>();
                if (meshCollider != null)
                    undoTargets.Add(meshCollider);
            }

            Undo.RegisterCompleteObjectUndo(undoTargets.ToArray(), UndoName);

            // STEP 4: Reset only the selected parent's local scale.
            // Child pivots move in world space; vertex rebuild below compensates.
            parentTransform.localScale = Vector3.one;

            // STEP 5 / 6: Rebuild each mesh in the new local space, then refresh.
            for (int i = 0; i < meshes.Length; i++)
            {
                ProBuilderMesh mesh = meshes[i];
                Vector3[] worldPositions = worldPositionsByMesh[i];
                var newLocalPositions = new Vector3[worldPositions.Length];

                Transform meshTransform = mesh.transform;
                for (int v = 0; v < worldPositions.Length; v++)
                {
                    // InverseTransformPoint uses the mesh's post-bake world matrix
                    // (parent scale now identity), so local verts keep the old world look.
                    newLocalPositions[v] = meshTransform.InverseTransformPoint(worldPositions[v]);
                }

                mesh.positions = newLocalPositions;
                mesh.ToMesh();
                // Collisions → EnsureMeshColliderIsAssigned() (existing colliders only).
                mesh.Refresh(BakeRefreshMask);

                EditorUtility.SetDirty(mesh);
                EditorUtility.SetDirty(mesh.gameObject);
            }

            EditorUtility.SetDirty(parent);
            EditorUtility.SetDirty(parentTransform);

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
                EditorSceneManager.MarkSceneDirty(activeScene);

            Undo.CollapseUndoOperations(undoGroup);

            float maxDisplacement = MeasureMaxWorldDisplacement(meshes, worldPositionsByMesh);

            ProBuilderEditor.Refresh();
            SceneView.RepaintAll();

            string summary =
                $"Baked parent scale into {meshes.Length} ProBuilder meshes.\n" +
                "Parent scale reset to (1, 1, 1).\n" +
                $"Max vertex displacement after bake: {maxDisplacement:F6} m";

            Debug.Log($"[BakeParentScale] {summary.Replace('\n', ' ')}");

            if (maxDisplacement > DisplacementWarningMeters)
            {
                Debug.LogWarning(
                    $"[BakeParentScale] Max world-space vertex displacement ({maxDisplacement:F6} m) " +
                    $"exceeds tolerance ({DisplacementWarningMeters} m). Inspect the floor for gaps.");
            }

            EditorUtility.DisplayDialog("Bake Complete", summary, "OK");
        }

        static float MeasureMaxWorldDisplacement(
            ProBuilderMesh[] meshes,
            Vector3[][] worldPositionsBefore)
        {
            float maxDisplacement = 0f;

            for (int i = 0; i < meshes.Length; i++)
            {
                Vector3[] before = worldPositionsBefore[i];
                Vector3[] after = meshes[i].VerticesInWorldSpace();
                int count = Mathf.Min(before.Length, after.Length);

                for (int v = 0; v < count; v++)
                {
                    float distance = Vector3.Distance(before[v], after[v]);
                    if (distance > maxDisplacement)
                        maxDisplacement = distance;
                }
            }

            return maxDisplacement;
        }

        static bool IsApproximatelyOne(Vector3 scale)
        {
            return Mathf.Abs(scale.x - 1f) <= ScaleEpsilon
                && Mathf.Abs(scale.y - 1f) <= ScaleEpsilon
                && Mathf.Abs(scale.z - 1f) <= ScaleEpsilon;
        }

        /// <summary>
        /// MVP safety: abort if non-ProBuilder rendered/physical descendants would
        /// visually change when the parent scale is reset. Empty organizers are fine.
        /// </summary>
        static bool TryValidateUnsupportedDescendants(GameObject parent, out string message)
        {
            var unsupported = new List<string>();
            Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject go = transforms[i].gameObject;
                if (go.GetComponent<ProBuilderMesh>() != null)
                    continue;

                bool hasRenderer = go.GetComponent<Renderer>() != null;
                bool hasCollider = go.GetComponent<Collider>() != null;
                bool hasMeshFilter = go.GetComponent<MeshFilter>() != null;

                if (!hasRenderer && !hasCollider && !hasMeshFilter)
                    continue;

                unsupported.Add(GetHierarchyPath(go.transform, parent.transform));
            }

            if (unsupported.Count == 0)
            {
                message = null;
                return true;
            }

            var builder = new StringBuilder();
            builder.AppendLine(
                "Aborted: the selection contains non-ProBuilder rendered or physical descendants.");
            builder.AppendLine(
                "Resetting the parent scale would move/scale those objects. " +
                "This MVP only bakes ProBuilderMesh geometry.");
            builder.AppendLine();
            builder.AppendLine("Unsupported objects:");

            int listed = Mathf.Min(unsupported.Count, 20);
            for (int i = 0; i < listed; i++)
                builder.AppendLine("  • " + unsupported[i]);

            if (unsupported.Count > listed)
                builder.AppendLine($"  … and {unsupported.Count - listed} more.");

            message = builder.ToString();
            return false;
        }

        static string GetHierarchyPath(Transform target, Transform root)
        {
            if (target == root)
                return target.name;

            var parts = new List<string>();
            Transform current = target;
            while (current != null)
            {
                parts.Add(current.name);
                if (current == root)
                    break;
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
