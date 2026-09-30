#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildTieredCampusTreePlanterMVP
{
    private const string RootFolder = "Assets/Art/Environment/TieredCampusTreePlanter_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_TieredCampusTreePlanter_MVP.prefab";

    [MenuItem("Tools/UIU Simulator/Assets/Build Tiered Campus Tree Planter MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        Material trunk = GetOrCreateMaterial(MaterialsFolder + "/M_CampusTree_Trunk.mat",
            new Color(0.24f, 0.16f, 0.09f), 0f, 0.25f);
        Material leaves = GetOrCreateMaterial(MaterialsFolder + "/M_CampusTree_Leaves.mat",
            new Color(0.21f, 0.37f, 0.07f), 0f, 0.35f);
        Material brick = GetOrCreateMaterial(MaterialsFolder + "/M_CampusTree_Brick.mat",
            new Color(0.59f, 0.24f, 0.14f), 0f, 0.3f);
        Material soil = GetOrCreateMaterial(MaterialsFolder + "/M_CampusTree_Soil.mat",
            new Color(0.16f, 0.105f, 0.06f), 0f, 0.2f);
        Material[] materials = { trunk, leaves, brick, soil };
        GameObject root = new GameObject("PF_TieredCampusTreePlanter_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        var owned = new List<Mesh>();
        try
        {
            Transform temp = new GameObject("__TEMP_MaterialGroups").transform;
            temp.SetParent(root.transform, false);
            BuildGeometry(temp, materials, owned);
            Mesh mesh = CombineAndSave(temp.gameObject, root.transform,
                MeshesFolder + "/TieredCampusTreePlanter_Combined.asset", "TieredCampusTreePlanter_Combined", materials);
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
            if (prefab == null || prefab.GetComponentsInChildren<MeshRenderer>(true).Length != 1 ||
                prefab.GetComponentsInChildren<Transform>(true).Length != 3 ||
                prefab.GetComponentsInChildren<BoxCollider>(true).Length != 1 ||
                prefab.GetComponentsInChildren<MeshCollider>(true).Length != 0 ||
                mesh.triangles.Length / 3 > 5000 || !AssetDatabase.Contains(mesh))
                throw new InvalidOperationException("Tiered tree prefab validation failed.");
            foreach (Material material in materials)
                if (!AssetDatabase.Contains(material))
                    throw new InvalidOperationException("Tree material was not saved.");
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Campus tree planter MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "Approximately 4 m tall, 3 m canopy; 2 x 2 x 0.25 m planter; floor-center pivot.\n" +
                "18 canopy clusters, 12 branches, 5 ground clusters, 1 renderer, 4 opaque material submeshes, 1 planter BoxCollider.\n" +
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
        Mesh foliage = CreateCanopySphere(); owned.Add(foliage);
        GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        template.SetActive(false);
        try
        {
            Mesh cube = template.GetComponent<MeshFilter>().sharedMesh;
            var groups = new List<CombineInstance>[4];
            for (int i = 0; i < 4; i++) groups[i] = new List<CombineInstance>();
            Mesh trunk = TaperedTube(new Vector3(0f, 0.18f, 0f), new Vector3(0.035f, 3.92f, 0.015f), 0.075f, 0.013f, 10);
            owned.Add(trunk); Add(groups[0], trunk, Vector3.zero, Vector3.one);
            // Six open tiers, two primary branches per tier. Opposing directions
            // vary in depth and length, leaving the central trunk visible.
            for (int tier = 0; tier < 6; tier++)
            {
                float height = 0.98f + tier * 0.49f;
                float reach = 1.25f - tier * 0.145f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float angle = (side < 0 ? 180f : 0f) + (tier % 3 - 1) * 19f + (side < 0 ? 8f : -6f);
                    Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                    Vector3 start = new Vector3(0.01f, height, 0f);
                    Vector3 end = start + direction * reach * (side < 0 ? 0.94f : 1f) + Vector3.up * 0.26f;
                    Mesh branch = TaperedTube(start, end, 0.039f - tier * 0.003f, 0.008f, 8);
                    owned.Add(branch); Add(groups[0], branch, Vector3.zero, Vector3.one);
                    Vector3 center = Vector3.Lerp(start, end, 0.76f) + Vector3.up * 0.075f;
                    Quaternion rotation = Quaternion.Euler(0f, -angle, side * 4f);
                    groups[1].Add(new CombineInstance { mesh = foliage,
                        transform = Matrix4x4.TRS(center, rotation,
                            new Vector3(0.98f - tier * 0.085f, 0.22f + (tier % 2) * 0.035f, 0.52f - tier * 0.025f)) });
                }
                // One smaller rear cluster per tier suggests depth without filling the gaps.
                Add(groups[1], foliage, new Vector3(tier % 2 == 0 ? -0.18f : 0.17f,
                    height + 0.31f, -0.22f), new Vector3(0.60f - tier * 0.035f, 0.20f, 0.48f));
            }
            // Four continuous dark mortar cores support three visible brick courses.
            for (int side = -1; side <= 1; side += 2)
            {
                Add(groups[3], cube, new Vector3(0f, 0.125f, side * 0.905f), new Vector3(1.98f, 0.248f, 0.17f));
                Add(groups[3], cube, new Vector3(side * 0.905f, 0.125f, 0f), new Vector3(0.17f, 0.248f, 1.64f));
                for (int course = 0; course < 3; course++)
                {
                    float y = (course + 0.5f) * 0.25f / 3f;
                    for (int axis = 0; axis < 2; axis++)
                    {
                        float length = axis == 0 ? 2f : 1.6f;
                        int count = axis == 0 ? 8 : 6;
                        // Alternating courses use clipped end bricks for staggered seams.
                        float step = length / count;
                        float offset = course % 2 == 0 ? 0f : step * 0.5f;
                        for (int brick = -1; brick < count; brick++)
                        {
                            float left = Mathf.Max(-length * 0.5f, -length * 0.5f + brick * step + offset);
                            float right = Mathf.Min(length * 0.5f, -length * 0.5f + (brick + 1) * step + offset);
                            if (right - left < 0.01f) continue;
                            float center = (left + right) * 0.5f;
                            Vector3 position = axis == 0 ? new Vector3(center, y, side * 0.90f) : new Vector3(side * 0.90f, y, center);
                            Vector3 size = axis == 0 ? new Vector3(right - left - 0.004f, 0.079f, 0.20f) : new Vector3(0.20f, 0.079f, right - left - 0.004f);
                            Add(groups[2], cube, position, size);
                        }
                    }
                }
            }
            Add(groups[3], cube, new Vector3(0f, 0.09f, 0f), new Vector3(1.60f, 0.18f, 1.60f));
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f;
                Add(groups[1], foliage, new Vector3(Mathf.Cos(angle) * 0.50f, 0.23f, Mathf.Sin(angle) * 0.48f),
                    new Vector3(0.42f, 0.13f, 0.35f));
            }
            for (int i = 0; i < groups.Length; i++)
            {
                Mesh mesh = new Mesh { name = "__TEMP_MaterialMesh_" + i };
                owned.Add(mesh); mesh.CombineMeshes(groups[i].ToArray(), true, true, false);
                mesh.RecalculateBounds();
                GameObject go = new GameObject("__TEMP_Material_" + i);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = materials[i];
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(template); }
    }

    private static Mesh TaperedTube(Vector3 start, Vector3 end, float bottomRadius, float topRadius, int sides)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            Vector3 radial = rotation * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices.Add(start + radial * bottomRadius); vertices.Add(end + radial * topRadius);
        }
        vertices.Add(start); vertices.Add(end);
        for (int i = 0; i < sides; i++)
        {
            int a = i * 2, b = (i + 1) % sides * 2;
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(b + 1);
            triangles.Add(a); triangles.Add(b + 1); triangles.Add(b);
            triangles.Add(sides * 2); triangles.Add(a); triangles.Add(b);
            triangles.Add(sides * 2 + 1); triangles.Add(b + 1); triangles.Add(a + 1);
        }
        return FinishMesh("__TEMP_TaperedTube", vertices, triangles);
    }

    private static void Add(List<CombineInstance> group, Mesh mesh, Vector3 position, Vector3 size)
    {
        group.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.identity, size) });
    }

    private static Mesh CreateCanopySphere()
    {
        const int sides = 12;
        var vertices = new List<Vector3> { new Vector3(0f, -0.5f, 0f) };
        var triangles = new List<int>();
        for (int ring = 1; ring <= 3; ring++)
        {
            float latitude = -Mathf.PI * 0.5f + ring * Mathf.PI / 4f;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float variation = 1f + 0.09f * Mathf.Sin(i * 2.3f + ring * 1.7f);
                vertices.Add(new Vector3(0.5f * Mathf.Cos(latitude) * Mathf.Cos(angle) * variation,
                    0.5f * Mathf.Sin(latitude), 0.5f * Mathf.Cos(latitude) * Mathf.Sin(angle) * variation));
            }
        }
        vertices.Add(new Vector3(0f, 0.5f, 0f));
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles.Add(0); triangles.Add(1 + i); triangles.Add(1 + next);
            for (int ring = 0; ring < 2; ring++)
            {
                int a = 1 + ring * sides + i, b = 1 + ring * sides + next;
                triangles.Add(a); triangles.Add(a + sides); triangles.Add(b + sides);
                triangles.Add(a); triangles.Add(b + sides); triangles.Add(b);
            }
            triangles.Add(37); triangles.Add(25 + next); triangles.Add(25 + i);
        }
        return FinishMesh("__TEMP_FlattenedFoliage", vertices, triangles);
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
