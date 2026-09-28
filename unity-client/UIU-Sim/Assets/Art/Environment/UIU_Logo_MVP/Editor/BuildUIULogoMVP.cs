#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildUIULogoMVP
{
    private const string RootFolder = "Assets/Art/Environment/UIU_Logo_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_UIU_Logo_MVP.prefab";
    private const float Bevel = 0.004f;
    private const float Depth = 0.08f;
    private const int CurveSegments = 16;

    [MenuItem("Tools/UIU Simulator/Assets/Build UIU Logo MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        Material silver = GetOrCreateMaterial(MaterialsFolder + "/M_UIU_SilverMetal.mat",
            new Color(0.72f, 0.75f, 0.77f), 0.9f, 0.82f);
        silver.DisableKeyword("_EMISSION");
        silver.SetColor("_EmissionColor", Color.black);
        silver.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(silver);
        GameObject root = new GameObject("PF_UIU_Logo_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        Mesh temporary = null;
        try
        {
            temporary = CreateLogoGeometry();
            GameObject temp = new GameObject("__TEMP_LogoGeometry");
            temp.transform.SetParent(root.transform, false);
            temp.AddComponent<MeshFilter>().sharedMesh = temporary;
            temp.AddComponent<MeshRenderer>().sharedMaterial = silver;
            Mesh mesh = CombineAndSave(temp, root.transform, MeshesFolder + "/UIU_Logo_Combined.asset",
                "UIU_Logo_Combined", new[] { silver });
            UnityEngine.Object.DestroyImmediate(temp);
            CreateFinalRenderer(root.transform, "Visual_Logo", mesh, new[] { silver });
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success) throw new InvalidOperationException("Unity failed to save the UIU logo prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"UIU logo MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "1.6 x 0.5 x 0.08 m; pivot at center back; front +Z.\n" +
                $"1 renderer, 1 non-emissive material, {mesh.triangles.Length / 3} triangles; no collider or backplate.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
        }
    }

    private static Mesh CreateLogoGeometry()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        // Both U outlines and the I are authored directly into one temporary mesh.
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2[] outer = UOutline(side * 0.49f, 0f);
            Vector2[] inset = UOutline(side * 0.49f, Bevel);
            int stations = CurveSegments + 3;
            var cap = new List<int>();
            for (int i = 0; i < stations - 1; i++)
            {
                int inner = 2 * stations - 1 - i;
                cap.Add(i); cap.Add(i + 1); cap.Add(inner - 1);
                cap.Add(i); cap.Add(inner - 1); cap.Add(inner);
            }
            Extrude(outer, inset, cap, vertices, triangles);
        }
        Vector2[] bar = {
            new Vector2(-0.06f, -0.25f), new Vector2(0.06f, -0.25f),
            new Vector2(0.06f, 0.25f), new Vector2(-0.06f, 0.25f)
        };
        Vector2[] barInset = {
            new Vector2(-0.06f + Bevel, -0.25f + Bevel), new Vector2(0.06f - Bevel, -0.25f + Bevel),
            new Vector2(0.06f - Bevel, 0.25f - Bevel), new Vector2(-0.06f + Bevel, 0.25f - Bevel)
        };
        Extrude(bar, barInset, new List<int> { 0, 1, 2, 0, 2, 3 }, vertices, triangles);
        Mesh mesh = new Mesh { name = "__TEMP_UIULogo", indexFormat = IndexFormat.UInt16 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector2[] UOutline(float centerX, float inset)
    {
        // A semicircular lower stroke joins the straight uprights without internal seams.
        float outerRadius = 0.31f - inset;
        float innerRadius = 0.20f + inset;
        const float centerY = 0.06f;
        float top = 0.25f - inset;
        var outer = new List<Vector2> { new Vector2(centerX - outerRadius, top) };
        var inner = new List<Vector2> { new Vector2(centerX - innerRadius, top) };
        for (int i = 0; i <= CurveSegments; i++)
        {
            float angle = Mathf.PI + i * Mathf.PI / CurveSegments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            outer.Add(new Vector2(centerX, centerY) + direction * outerRadius);
            inner.Add(new Vector2(centerX, centerY) + direction * innerRadius);
        }
        outer.Add(new Vector2(centerX + outerRadius, top));
        inner.Add(new Vector2(centerX + innerRadius, top));
        inner.Reverse();
        outer.AddRange(inner);
        return outer.ToArray();
    }

    private static void Extrude(Vector2[] outline, Vector2[] inset, List<int> cap,
        List<Vector3> vertices, List<int> triangles)
    {
        // Separate cap vertices keep the large polished faces flat; the side bands
        // share vertices around each contour for smooth shading along the U curves.
        AddCap(inset, 0f, cap, true, vertices, triangles);
        AddCap(inset, Depth, cap, false, vertices, triangles);
        AddBand(inset, 0f, outline, Bevel, vertices, triangles);
        AddBand(outline, Bevel, outline, Depth - Bevel, vertices, triangles);
        AddBand(outline, Depth - Bevel, inset, Depth, vertices, triangles);
    }

    private static void AddCap(Vector2[] outline, float z, List<int> indices, bool reverse,
        List<Vector3> vertices, List<int> triangles)
    {
        int start = vertices.Count;
        foreach (Vector2 point in outline) vertices.Add(new Vector3(point.x, point.y, z));
        for (int i = 0; i < indices.Count; i += 3)
        {
            triangles.Add(start + indices[i]);
            triangles.Add(start + indices[i + (reverse ? 2 : 1)]);
            triangles.Add(start + indices[i + (reverse ? 1 : 2)]);
        }
    }

    private static void AddBand(Vector2[] back, float backZ, Vector2[] front, float frontZ,
        List<Vector3> vertices, List<int> triangles)
    {
        int start = vertices.Count, count = back.Length;
        foreach (Vector2 point in back) vertices.Add(new Vector3(point.x, point.y, backZ));
        foreach (Vector2 point in front) vertices.Add(new Vector3(point.x, point.y, frontZ));
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            triangles.Add(start + i); triangles.Add(start + next); triangles.Add(start + count + next);
            triangles.Add(start + i); triangles.Add(start + count + next); triangles.Add(start + count + i);
        }
    }

    private static void CreateFinalRenderer(Transform root, string name, Mesh mesh, Material[] materials)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
    }

    private static Mesh CombineAndSave(GameObject tempRoot, Transform finalRoot,
        string assetPath, string meshName, Material[] materials)
    {
        MeshFilter[] filters = tempRoot.GetComponentsInChildren<MeshFilter>(true);
        var groupedMeshes = new List<Mesh>();
        Mesh combined = new Mesh { name = meshName, indexFormat = IndexFormat.UInt16 };
        try
        {
            var groups = new List<CombineInstance>();
            foreach (Material material in materials)
            {
                var parts = new List<CombineInstance>();
                foreach (MeshFilter filter in filters)
                    if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>().sharedMaterial == material)
                        parts.Add(new CombineInstance {
                            mesh = filter.sharedMesh,
                            transform = finalRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix
                        });
                Mesh group = new Mesh();
                groupedMeshes.Add(group);
                group.CombineMeshes(parts.ToArray(), true, true, false);
                groups.Add(new CombineInstance { mesh = group, transform = Matrix4x4.identity });
            }
            // One submesh per palette color, all in one saved mesh and renderer.
            combined.CombineMeshes(groups.ToArray(), false, true, false);
            combined.RecalculateBounds();
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                // Keep GUIDs stable so existing prefab instances retain their mesh references.
                EditorUtility.CopySerialized(combined, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(combined, assetPath);
            return combined;
        }
        finally
        {
            foreach (Mesh group in groupedMeshes)
                UnityEngine.Object.DestroyImmediate(group);
            if (!AssetDatabase.Contains(combined))
                UnityEngine.Object.DestroyImmediate(combined);
        }
    }

    private static Material GetOrCreateMaterial(
        string path,
        Color baseColor,
        float metallic,
        float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("Could not find Universal Render Pipeline/Lit shader.");

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", baseColor);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string normalized = path.Replace("\\", "/");
        int slash = normalized.LastIndexOf('/');
        if (slash <= 0)
            return;

        string parent = normalized.Substring(0, slash);
        string folderName = normalized.Substring(slash + 1);

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(normalized))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
