#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildWaterFountainMVP
{
    private const string RootFolder = "Assets/Art/Props/WaterFountain_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_WaterFountain_MVP.prefab";

    [MenuItem("Tools/UIU Simulator/Assets/Build Water Fountain MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        Material cabinet = GetOrCreateMaterial(MaterialsFolder + "/M_WaterFountain_Cabinet.mat",
            new Color(0.80f, 0.79f, 0.74f), 0f, 0.35f);
        Material metal = GetOrCreateMaterial(MaterialsFolder + "/M_WaterFountain_Metal.mat",
            new Color(0.52f, 0.55f, 0.56f), 0.8f, 0.55f);
        Material fixture = GetOrCreateMaterial(MaterialsFolder + "/M_WaterFountain_Fixture.mat",
            new Color(0.31f, 0.34f, 0.35f), 0.85f, 0.65f);
        GameObject root = new GameObject("PF_WaterFountain_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        Mesh cylinder = null;
        try
        {
            Transform tempCabinet = new GameObject("__TEMP_Cabinet").transform;
            Transform tempMetal = new GameObject("__TEMP_Metal").transform;
            Transform tempFixtures = new GameObject("__TEMP_Fixtures").transform;
            tempCabinet.SetParent(root.transform, false);
            tempMetal.SetParent(root.transform, false);
            tempFixtures.SetParent(root.transform, false);
            cylinder = CreateLowPolyCylinder();
            BuildCabinet(tempCabinet, cabinet);
            BuildMetal(tempMetal, metal);
            BuildFixtures(tempFixtures, fixture, cylinder);
            Mesh cabinetMesh = CombineAndSave(tempCabinet.gameObject, root.transform,
                MeshesFolder + "/WaterFountain_CabinetCombined.asset", "WaterFountain_CabinetCombined", new[] { cabinet });
            Mesh metalMesh = CombineAndSave(tempMetal.gameObject, root.transform,
                MeshesFolder + "/WaterFountain_MetalCombined.asset", "WaterFountain_MetalCombined", new[] { metal });
            Mesh fixtureMesh = CombineAndSave(tempFixtures.gameObject, root.transform,
                MeshesFolder + "/WaterFountain_FixturesCombined.asset", "WaterFountain_FixturesCombined", new[] { fixture });
            UnityEngine.Object.DestroyImmediate(tempCabinet.gameObject);
            UnityEngine.Object.DestroyImmediate(tempMetal.gameObject);
            UnityEngine.Object.DestroyImmediate(tempFixtures.gameObject);
            CreateFinalRenderer(root.transform, "Visual_Cabinet", cabinetMesh, new[] { cabinet });
            CreateFinalRenderer(root.transform, "Visual_Metal", metalMesh, new[] { metal });
            CreateFinalRenderer(root.transform, "Visual_Fixtures", fixtureMesh, new[] { fixture });
            Transform collision = new GameObject("Collision").transform;
            collision.SetParent(root.transform, false);
            BoxCollider collider = collision.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.575f, 0f);
            collider.size = new Vector3(1.8f, 1.15f, 0.55f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the water fountain prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Water fountain MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "1.8 x 1.15 x 0.55 m cabinet; taps reach approximately 1.45 m; front +Z.\n" +
                "3 renderers, 3 shared materials, 1 BoxCollider, floor-center pivot.\n" +
                $"Triangles: {(cabinetMesh.triangles.Length + metalMesh.triangles.Length + fixtureMesh.triangles.Length) / 3}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            if (cylinder != null) UnityEngine.Object.DestroyImmediate(cylinder);
        }
    }

    private static void BuildCabinet(Transform parent, Material material)
    {
        Part(parent, "CabinetCore", new Vector3(1.76f, 0.98f, 0.49f), new Vector3(0f, 0.55f, -0.015f), material);
        Part(parent, "Plinth", new Vector3(1.69f, 0.06f, 0.46f), new Vector3(0f, 0.03f, -0.015f), material);
        for (int door = 0; door < 3; door++)
            Part(parent, "Door_" + door, new Vector3(0.574f, 0.962f, 0.028f),
                new Vector3((door - 1) * 0.586f, 0.55f, 0.251f), material);
    }

    private static void BuildMetal(Transform parent, Material material)
    {
        // A recessed floor and perimeter rails form an actual shallow trough.
        Part(parent, "TroughFloor", new Vector3(1.8f, 0.018f, 0.55f), new Vector3(0f, 1.049f, 0f), material);
        Part(parent, "FrontApron", new Vector3(1.8f, 0.065f, 0.04f), new Vector3(0f, 1.0875f, 0.255f), material);
        Part(parent, "RearDeck", new Vector3(1.8f, 0.045f, 0.11f), new Vector3(0f, 1.0975f, -0.22f), material);
        Part(parent, "RearRim", new Vector3(1.8f, 0.03f, 0.02f), new Vector3(0f, 1.135f, -0.265f), material);
        for (int side = -1; side <= 1; side += 2)
            Part(parent, "SideTrim_" + side, new Vector3(0.11f, 0.065f, 0.40f),
                new Vector3(side * 0.845f, 1.0875f, 0.035f), material);
        // Ten broad slats instead of dozens of thin grate bars; all baked into one mesh.
        for (int slat = 0; slat < 10; slat++)
            Part(parent, "DrainSlat_" + slat, new Vector3(0.113f, 0.012f, 0.37f),
                new Vector3(-0.72f + slat * 0.16f, 1.095f, 0.035f), material);
    }

    private static void BuildFixtures(Transform parent, Material material, Mesh cylinder)
    {
        for (int door = 0; door < 3; door++)
            Part(parent, "Handle_" + door, new Vector3(0.018f, 0.13f, 0.025f),
                new Vector3((door - 1) * 0.586f + 0.22f, 0.83f, 0.2625f), material);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 0.48f;
            Part(parent, "TapBase_" + side, new Vector3(0.07f, 0.055f, 0.075f),
                new Vector3(x, 1.1475f, -0.185f), material);
            Part(parent, "Valve_" + side, new Vector3(0.035f, 0.018f, 0.035f),
                new Vector3(x + 0.04f, 1.18f, -0.185f), material);
            const float radius = 0.009f;
            Tube(parent, "Stem_" + side, cylinder, new Vector3(x, 1.17f, -0.185f),
                new Vector3(x, 1.36f, -0.185f), radius, material);
            // Six straight eight-sided segments describe a compact gooseneck in Y/Z.
            Vector3 previous = new Vector3(x, 1.36f, -0.185f);
            for (int segment = 1; segment <= 6; segment++)
            {
                float angle = Mathf.PI - segment * Mathf.PI / 6f;
                Vector3 next = new Vector3(x, 1.36f + 0.08f * Mathf.Sin(angle),
                    -0.105f + 0.08f * Mathf.Cos(angle));
                Tube(parent, $"Bend_{side}_{segment}", cylinder, previous, next, radius, material);
                previous = next;
            }
            Tube(parent, "Spout_" + side, cylinder, previous, previous + Vector3.down * 0.025f, 0.011f, material);
        }
    }

    private static void Part(Transform parent, string name, Vector3 size, Vector3 position, Material material)
    {
        CreateCubePart(parent, name, size, position, Quaternion.identity, material);
    }

    private static void Tube(Transform parent, string name, Mesh cylinder, Vector3 start,
        Vector3 end, float radius, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Vector3 delta = end - start;
        go.transform.localPosition = (start + end) * 0.5f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        go.transform.localScale = new Vector3(radius, delta.magnitude, radius);
        go.AddComponent<MeshFilter>().sharedMesh = cylinder;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Mesh CreateLowPolyCylinder()
    {
        // Unit-height cylinder, radius 1, eight sides. Shared by all sixteen tap segments.
        const int sides = 8;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            float b = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 loA = new Vector3(Mathf.Cos(a), -0.5f, Mathf.Sin(a));
            Vector3 loB = new Vector3(Mathf.Cos(b), -0.5f, Mathf.Sin(b));
            Vector3 hiA = loA + Vector3.up;
            Vector3 hiB = loB + Vector3.up;
            int n = vertices.Count;
            vertices.Add(loA); vertices.Add(hiA); vertices.Add(hiB); vertices.Add(loB);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
            n = vertices.Count;
            vertices.Add(new Vector3(0f, 0.5f, 0f)); vertices.Add(hiB); vertices.Add(hiA);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            n = vertices.Count;
            vertices.Add(new Vector3(0f, -0.5f, 0f)); vertices.Add(loA); vertices.Add(loB);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
        }
        Mesh mesh = new Mesh { name = "__TEMP_EightSidedCylinder" };
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

    private static void CreateCubePart(
        Transform parent,
        string name,
        Vector3 size,
        Vector3 localPosition,
        Quaternion localRotation,
        Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = size;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        go.GetComponent<MeshRenderer>().sharedMaterial = material;
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
