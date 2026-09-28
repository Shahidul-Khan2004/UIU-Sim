#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildCampusHedgeBushMVP
{
    private const string RootFolder = "Assets/Art/Environment/CampusHedgeBush_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_CampusHedgeBush_MVP.prefab";

    [MenuItem("Tools/UIU Simulator/Assets/Build Campus Hedge Bush MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        Material green = GetOrCreateMaterial(MaterialsFolder + "/M_Hedge_Green.mat",
            new Color(0.18f, 0.34f, 0.065f), 0f, 0.3f);
        Material flower = GetOrCreateMaterial(MaterialsFolder + "/M_Hedge_Flower.mat",
            new Color(0.95f, 0.95f, 0.85f), 0f, 0.35f);
        Material stem = GetOrCreateMaterial(MaterialsFolder + "/M_Hedge_Stem.mat",
            new Color(0.19f, 0.12f, 0.055f), 0f, 0.25f);
        Material[] materials = { green, flower, stem };
        GameObject root = new GameObject("PF_CampusHedgeBush_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        var ownedMeshes = new List<Mesh>();
        try
        {
            Transform temp = new GameObject("__TEMP_MaterialGroups").transform;
            temp.SetParent(root.transform, false);
            BuildGeometry(temp, materials, ownedMeshes);
            Mesh mesh = CombineAndSave(temp.gameObject, root.transform,
                MeshesFolder + "/CampusHedgeBush_Combined.asset", "CampusHedgeBush_Combined", materials);
            UnityEngine.Object.DestroyImmediate(temp.gameObject);
            CreateFinalRenderer(root.transform, "Visual_AllCombined", mesh, materials);
            Transform collision = new GameObject("Collision").transform;
            collision.SetParent(root.transform, false);
            BoxCollider collider = collision.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.375f, 0f);
            collider.size = new Vector3(2f, 0.75f, 0.45f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success) throw new InvalidOperationException("Unity failed to save the campus hedge prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Campus hedge MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "2 x 0.75 x 0.45 m; floor-center pivot; length along X.\n" +
                "32 foliage clusters, 32 flowers, 8 stems; 1 renderer, 3 opaque submeshes, 1 BoxCollider.\n" +
                $"Triangles: {mesh.triangles.Length / 3}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (Mesh mesh in ownedMeshes) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    private static void BuildGeometry(Transform parent, Material[] materials, List<Mesh> owned)
    {
        // Shared procedural primitives are transformed directly into combine entries.
        // No leaf, flower, stem or cluster GameObjects are instantiated.
        Mesh roundedBox = CreateRoundedCluster();
        Mesh flower = CreateFlower();
        Mesh cylinder = CreateStemCylinder();
        owned.Add(roundedBox); owned.Add(flower); owned.Add(cylinder);
        var foliage = new List<CombineInstance>();
        var flowers = new List<CombineInstance>();
        var stems = new List<CombineInstance>();
        var random = new System.Random(713);
        Add(foliage, roundedBox, new Vector3(0f, 0.425f, 0f),
            new Vector3(1.96f, 0.53f, 0.39f), Quaternion.identity);
        for (int x = 0; x < 8; x++)
        {
            float px = -0.84f + x * 0.24f;
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 2; row++)
                {
                    float h = 0.285f + (float)random.NextDouble() * 0.035f;
                    Vector3 center = new Vector3(px, row == 0 ? 0.32f : 0.585f, side * 0.12f);
                    Vector3 size = new Vector3(0.31f, h, 0.21f);
                    Add(foliage, roundedBox, center, size, Quaternion.identity);
                    // Sixteen front flowers and sixteen flowers anchored to upper cluster peaks.
                    if (side > 0)
                        AddFlower(flowers, flower, center + new Vector3(0f, 0f, size.z * 0.5f + 0.001f),
                            Vector3.forward, random);
                    if (row == 1)
                        AddFlower(flowers, flower, center + new Vector3(0f, size.y * 0.5f + 0.001f, 0f),
                            Vector3.up, random);
                }
            // Eight simple stem hints, partially concealed by the dense body.
            Add(stems, cylinder, new Vector3(px, 0.145f, 0.015f),
                new Vector3(0.021f, 0.29f, 0.021f), Quaternion.identity);
        }
        List<CombineInstance>[] groups = { foliage, flowers, stems };
        for (int i = 0; i < groups.Length; i++)
        {
            Mesh mesh = new Mesh { name = "__TEMP_HedgeGroup_" + i, indexFormat = IndexFormat.UInt16 };
            owned.Add(mesh);
            mesh.CombineMeshes(groups[i].ToArray(), true, true, false);
            mesh.RecalculateBounds();
            GameObject go = new GameObject("__TEMP_Material_" + i);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = materials[i];
        }
    }

    private static void AddFlower(List<CombineInstance> flowers, Mesh flower, Vector3 position,
        Vector3 normal, System.Random random)
    {
        float size = 0.022f + (float)random.NextDouble() * 0.008f;
        Add(flowers, flower, position, Vector3.one * size,
            Quaternion.FromToRotation(Vector3.forward, normal) *
            Quaternion.AngleAxis((float)random.NextDouble() * 360f, Vector3.forward));
    }

    private static void Add(List<CombineInstance> group, Mesh mesh, Vector3 position, Vector3 size, Quaternion rotation)
    {
        group.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, size) });
    }

    private static float Rounded(float value)
    {
        return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), 0.45f) * 0.5f;
    }

    private static Mesh CreateRoundedCluster()
    {
        // Low-poly superellipsoid: 26 vertices / 48 triangles, boxier than a sphere.
        var vertices = new List<Vector3> { new Vector3(0f, -0.5f, 0f) };
        var triangles = new List<int>();
        for (int ring = 1; ring <= 3; ring++)
        {
            float latitude = -Mathf.PI * 0.5f + ring * Mathf.PI / 4f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                vertices.Add(new Vector3(Rounded(Mathf.Cos(latitude) * Mathf.Cos(a)),
                    Rounded(Mathf.Sin(latitude)), Rounded(Mathf.Cos(latitude) * Mathf.Sin(a))));
            }
        }
        vertices.Add(new Vector3(0f, 0.5f, 0f));
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8;
            triangles.Add(0); triangles.Add(1 + i); triangles.Add(1 + next);
            for (int ring = 0; ring < 2; ring++)
            {
                int a = 1 + ring * 8 + i, b = 1 + ring * 8 + next;
                triangles.Add(a); triangles.Add(a + 8); triangles.Add(b + 8);
                triangles.Add(a); triangles.Add(b + 8); triangles.Add(b);
            }
            triangles.Add(25); triangles.Add(17 + next); triangles.Add(17 + i);
        }
        return FinishMesh("__TEMP_RoundedCluster", vertices, triangles);
    }

    private static Mesh CreateFlower()
    {
        // One ten-triangle five-petal silhouette; no separate petals or flower objects.
        var vertices = new List<Vector3> { Vector3.zero };
        var triangles = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.PI / 5f;
            float radius = i % 2 == 0 ? 1f : 0.35f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
        for (int i = 0; i < 10; i++)
        {
            triangles.Add(0); triangles.Add(i + 1); triangles.Add((i + 1) % 10 + 1);
        }
        return FinishMesh("__TEMP_Flower", vertices, triangles);
    }

    private static Mesh CreateStemCylinder()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f, -0.5f, Mathf.Sin(a) * 0.5f));
            vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0.5f, Mathf.Sin(a) * 0.5f));
        }
        vertices.Add(new Vector3(0f, -0.5f, 0f));
        vertices.Add(new Vector3(0f, 0.5f, 0f));
        for (int i = 0; i < 6; i++)
        {
            int a = i * 2, b = (i + 1) % 6 * 2;
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(b + 1);
            triangles.Add(a); triangles.Add(b + 1); triangles.Add(b);
            triangles.Add(12); triangles.Add(a); triangles.Add(b);
            triangles.Add(13); triangles.Add(b + 1); triangles.Add(a + 1);
        }
        return FinishMesh("__TEMP_Stem", vertices, triangles);
    }

    private static Mesh FinishMesh(string name, List<Vector3> vertices, List<int> triangles)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void FitFinalBounds(Mesh mesh)
    {
        // Include the flowers in the exact modular envelope and anchor stems at ground level.
        Bounds bounds = mesh.bounds;
        Vector3 scale = new Vector3(2f / bounds.size.x, 0.75f / bounds.size.y, 0.45f / bounds.size.z);
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i] - origin, scale);
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
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
            FitFinalBounds(combined);
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
