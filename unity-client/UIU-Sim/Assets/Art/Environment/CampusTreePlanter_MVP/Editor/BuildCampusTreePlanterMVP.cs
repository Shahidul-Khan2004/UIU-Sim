#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildCampusTreePlanterMVP
{
    private const string RootFolder = "Assets/Art/Environment/CampusTreePlanter_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_CampusTreePlanter_MVP.prefab";

    [MenuItem("Tools/UIU Simulator/Assets/Build Campus Tree Planter MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        Material trunk = GetOrCreateMaterial(MaterialsFolder + "/M_Tree_Trunk.mat",
            new Color(0.24f, 0.16f, 0.09f), 0f, 0.25f);
        Material leaves = GetOrCreateMaterial(MaterialsFolder + "/M_Tree_Leaves.mat",
            new Color(0.21f, 0.37f, 0.07f), 0f, 0.35f);
        Material brick = GetOrCreateMaterial(MaterialsFolder + "/M_Planter_Brick.mat",
            new Color(0.59f, 0.24f, 0.14f), 0f, 0.3f);
        Material soil = GetOrCreateMaterial(MaterialsFolder + "/M_Soil.mat",
            new Color(0.16f, 0.105f, 0.06f), 0f, 0.2f);
        Material[] materials = { trunk, leaves, brick, soil };
        GameObject root = new GameObject("PF_CampusTreePlanter_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        var owned = new List<Mesh>();
        try
        {
            Transform temp = new GameObject("__TEMP_MaterialGroups").transform;
            temp.SetParent(root.transform, false);
            BuildGeometry(temp, materials, owned);
            Mesh mesh = CombineAndSave(temp.gameObject, root.transform,
                MeshesFolder + "/CampusTreePlanter_Combined.asset", "CampusTreePlanter_Combined", materials);
            UnityEngine.Object.DestroyImmediate(temp.gameObject);
            CreateFinalRenderer(root.transform, "Visual_AllCombined", mesh, materials);
            Transform collision = new GameObject("Collision").transform;
            collision.SetParent(root.transform, false);
            BoxCollider collider = collision.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.125f, 0f);
            collider.size = new Vector3(2f, 0.25f, 2f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success) throw new InvalidOperationException("Unity failed to save the campus tree planter prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Campus tree planter MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "3 m tall, 2.2 m canopy; 2 x 2 x 0.25 m planter; floor-center pivot.\n" +
                "13 canopy clusters, 1 renderer, 4 opaque material submeshes, 1 planter BoxCollider.\n" +
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
            foreach (Mesh mesh in owned) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    private static void BuildGeometry(Transform parent, Material[] materials, List<Mesh> owned)
    {
        Mesh sphere = CreateCanopySphere();
        owned.Add(sphere);
        Mesh trunk = CreateTaperedTrunk();
        owned.Add(trunk);
        GameObject cubeTemplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeTemplate.SetActive(false);
        try
        {
            Mesh cube = cubeTemplate.GetComponent<MeshFilter>().sharedMesh;
            var groups = new List<CombineInstance>[4];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<CombineInstance>();
            Add(groups[0], trunk, Vector3.zero, Vector3.one);
            // Central mass plus eight lower lobes and four crown lobes. No leaf objects.
            Add(groups[1], sphere, new Vector3(0f, 2.22f, 0f), new Vector3(1.85f, 1.35f, 1.85f));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                Add(groups[1], sphere, new Vector3(0.65f * Mathf.Cos(angle), 2.18f, 0.65f * Mathf.Sin(angle)),
                    new Vector3(0.90f, 0.95f, 0.90f));
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = Mathf.PI / 4f + i * Mathf.PI / 2f;
                Add(groups[1], sphere, new Vector3(0.35f * Mathf.Cos(angle), 2.55f, 0.35f * Mathf.Sin(angle)),
                    new Vector3(1.10f, 0.90f, 1.10f));
            }
            // Twenty-eight broad brick blocks, added as matrices, not individual objects.
            // Small gaps provide the brick rhythm without textures or mortar geometry.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 8; i++)
                {
                    float left = -1f + i * 0.25f + (i == 0 ? 0f : 0.003f);
                    float right = -1f + (i + 1) * 0.25f - (i == 7 ? 0f : 0.003f);
                    Add(groups[2], cube, new Vector3((left + right) * 0.5f, 0.125f, side * 0.90f),
                        new Vector3(right - left, 0.25f, 0.20f));
                }
                for (int i = 0; i < 6; i++)
                    Add(groups[2], cube, new Vector3(side * 0.90f, 0.125f, -0.80f + (i + 0.5f) * 1.60f / 6f),
                        new Vector3(0.20f, 0.25f, 1.60f / 6f - 0.006f));
            }
            Add(groups[3], cube, new Vector3(0f, 0.09f, 0f), new Vector3(1.60f, 0.18f, 1.60f));
            for (int i = 0; i < groups.Length; i++)
            {
                Mesh mesh = new Mesh { name = i == 1 ? "TreeCanopyCombinedMesh" : "__TEMP_MaterialMesh_" + i };
                owned.Add(mesh);
                mesh.CombineMeshes(groups[i].ToArray(), true, true, false);
                mesh.RecalculateBounds();
                GameObject go = new GameObject("__TEMP_Material_" + i);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = materials[i];
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cubeTemplate);
        }
    }

    private static void Add(List<CombineInstance> group, Mesh mesh, Vector3 position, Vector3 size)
    {
        group.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.identity, size) });
    }

    private static Mesh CreateTaperedTrunk()
    {
        // Eight-sided tapered cylinder; no separate branches, roots or bark details.
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * 0.115f, 0.17f, Mathf.Sin(angle) * 0.115f));
            vertices.Add(new Vector3(Mathf.Cos(angle) * 0.06f + 0.025f, 2.03f, Mathf.Sin(angle) * 0.06f));
        }
        vertices.Add(new Vector3(0f, 0.17f, 0f));
        vertices.Add(new Vector3(0.025f, 2.03f, 0f));
        for (int i = 0; i < 8; i++)
        {
            int a = i * 2, b = (i + 1) % 8 * 2;
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(b + 1);
            triangles.Add(a); triangles.Add(b + 1); triangles.Add(b);
            triangles.Add(16); triangles.Add(a); triangles.Add(b);
            triangles.Add(17); triangles.Add(b + 1); triangles.Add(a + 1);
        }
        return FinishMesh("__TEMP_TaperedTrunk", vertices, triangles);
    }

    private static Mesh CreateCanopySphere()
    {
        // Low-poly sphere: 26 vertices and 48 triangles per foliage mass.
        var vertices = new List<Vector3> { new Vector3(0f, -0.5f, 0f) };
        var triangles = new List<int>();
        for (int ring = 1; ring <= 3; ring++)
        {
            float latitude = -Mathf.PI * 0.5f + ring * Mathf.PI / 4f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                vertices.Add(new Vector3(0.5f * Mathf.Cos(latitude) * Mathf.Cos(a),
                    0.5f * Mathf.Sin(latitude), 0.5f * Mathf.Cos(latitude) * Mathf.Sin(a)));
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
        return FinishMesh("__TEMP_CanopySphere", vertices, triangles);
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
