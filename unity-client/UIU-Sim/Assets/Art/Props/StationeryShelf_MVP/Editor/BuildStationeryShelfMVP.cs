#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildStationeryShelfMVP
{
    private const string RootFolder = "Assets/Art/Props/StationeryShelf_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_StationeryShelf_MVP.prefab";

    [MenuItem("Tools/UIU Simulator/Assets/Build Stationery Shelf MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        Material metal = GetOrCreateMaterial(MaterialsFolder + "/M_Shelf_WhiteMetal.mat",
            new Color(0.77f, 0.78f, 0.75f), 0.3f, 0.45f);
        Material glass = GetOrCreateMaterial(MaterialsFolder + "/M_Shelf_Glass.mat",
            new Color(0.78f, 0.92f, 0.91f, 0.25f), 0f, 0.8f);
        ConfigureGlass(glass);
        Color[] colors = { new Color(0.065f, 0.30f, 0.57f), new Color(0.73f, 0.13f, 0.10f),
            new Color(0.91f, 0.69f, 0.08f), new Color(0.76f, 0.84f, 0.73f) };
        string[] suffixes = { "", "_Red", "_Yellow", "_Light" };
        var products = new Material[colors.Length];
        var opaque = new Material[colors.Length + 1];
        opaque[0] = metal;
        for (int i = 0; i < colors.Length; i++)
        {
            products[i] = GetOrCreateMaterial(MaterialsFolder + "/M_Display_Products" + suffixes[i] + ".mat",
                colors[i], 0f, 0.3f);
            opaque[i + 1] = products[i];
        }
        GameObject root = new GameObject("PF_StationeryShelf_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        var temporaryMeshes = new List<Mesh>();
        try
        {
            Transform structure = new GameObject("__TEMP_ShelfStructure").transform;
            Transform glazing = new GameObject("__TEMP_GlassDisplay").transform;
            structure.SetParent(root.transform, false);
            glazing.SetParent(root.transform, false);
            BuildStructure(structure, metal);
            BuildDisplayItems(structure, products, temporaryMeshes);
            for (int side = -1; side <= 1; side += 2)
                Part(glazing, "GlassPane_" + side, new Vector3(1.425f, 0.80f, 0.006f),
                    new Vector3(side * 0.7375f, 0.70f, 0.258f), glass);
            Mesh combined = CombineAndSave(structure.gameObject, root.transform,
                MeshesFolder + "/StationeryShelf_Combined.asset", "StationeryShelf_Combined", opaque);
            Mesh glassMesh = CombineAndSave(glazing.gameObject, root.transform,
                MeshesFolder + "/StationeryShelf_Glass.asset", "GlassDisplayMesh", new[] { glass });
            UnityEngine.Object.DestroyImmediate(structure.gameObject);
            UnityEngine.Object.DestroyImmediate(glazing.gameObject);
            CreateFinalRenderer(root.transform, "Visual_AllCombined", combined, opaque);
            CreateFinalRenderer(root.transform, "Visual_Glass", glassMesh, new[] { glass }, glass: true);
            Transform collision = new GameObject("Collision").transform;
            collision.SetParent(root.transform, false);
            BoxCollider collider = collision.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.1f, 0f);
            collider.size = new Vector3(3f, 2.2f, 0.55f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the stationery shelf prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Stationery shelf MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "3 x 2.2 x 0.55 m; floor-center pivot; front +Z.\n" +
                "64 product shapes, 2 renderers, 6 material submeshes, 1 BoxCollider.\n" +
                $"Triangles: {(combined.triangles.Length + glassMesh.triangles.Length) / 3}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (Mesh mesh in temporaryMeshes)
                UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    private static void BuildStructure(Transform parent, Material material)
    {
        Part(parent, "Back", new Vector3(2.90f, 2.12f, 0.025f), new Vector3(0f, 1.12f, -0.2625f), material);
        for (int side = -1; side <= 1; side += 2)
        {
            Part(parent, "Side_" + side, new Vector3(0.05f, 2.2f, 0.55f),
                new Vector3(side * 1.475f, 1.1f, 0f), material);
            Part(parent, "LowerFrontPost_" + side, new Vector3(0.045f, 0.84f, 0.025f),
                new Vector3(side * 1.4275f, 0.70f, 0.2625f), material);
        }
        Part(parent, "CenterDivider", new Vector3(0.045f, 2.2f, 0.55f), new Vector3(0f, 1.1f, 0f), material);
        Part(parent, "Top", new Vector3(2.90f, 0.04f, 0.55f), new Vector3(0f, 2.18f, 0f), material);
        Part(parent, "LowerCabinet", new Vector3(2.90f, 0.25f, 0.53f), new Vector3(0f, 0.155f, -0.01f), material);
        float[] levels = { 0.29f, 0.68f, 1.12f, 1.65f };
        for (int i = 0; i < levels.Length; i++)
            Part(parent, "Shelf_" + i, new Vector3(2.90f, 0.025f, i < 2 ? 0.47f : 0.55f),
                new Vector3(0f, levels[i], i < 2 ? -0.015f : 0f), material);
        Part(parent, "CounterEdge", new Vector3(2.90f, 0.035f, 0.025f), new Vector3(0f, 1.11f, 0.2625f), material);
        Part(parent, "DisplaySill", new Vector3(2.90f, 0.045f, 0.025f), new Vector3(0f, 0.28f, 0.2625f), material);
    }

    private static void BuildDisplayItems(Transform parent, Material[] palette, List<Mesh> ownedMeshes)
    {
        // Only two primitive templates are created. Products are matrix entries,
        // never individual GameObjects, renderers, colliders, or saved assets.
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject cylinder = null;
        cube.SetActive(false);
        try
        {
            cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.SetActive(false);
            Mesh cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Mesh cylinderMesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
            var groups = new List<CombineInstance>[palette.Length];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<CombineInstance>();
            var random = new System.Random(513);
            int count = 0;
            // Twenty upright books on the upper shelf; differing width, height and palette.
            for (int side = -1; side <= 1; side += 2)
                for (int book = 0; book < 10; book++)
                {
                    float h = 0.28f + (float)random.NextDouble() * 0.15f;
                    float w = 0.065f + (float)random.NextDouble() * 0.035f;
                    AddProduct(groups, count++ % palette.Length, cubeMesh,
                        new Vector3(side * 0.7375f - 0.56f + book * 0.12f, 1.6625f + h * 0.5f, -0.04f),
                        new Vector3(w, h, 0.25f));
                }
            // Twelve stock boxes, in pairs, behind the countertop display.
            for (int side = -1; side <= 1; side += 2)
                for (int stack = 0; stack < 3; stack++)
                    for (int layer = 0; layer < 2; layer++)
                        AddProduct(groups, count++ % palette.Length, cubeMesh,
                            new Vector3(side * 0.7375f - 0.43f + stack * 0.43f, 1.1325f + 0.06f + layer * 0.122f, -0.13f),
                            new Vector3(0.35f, 0.12f, 0.20f));
            // Four pen cups, with two coarse cylinder silhouettes each (12 shapes).
            for (int cup = 0; cup < 4; cup++)
            {
                float x = -1.22f + cup * 0.30f;
                AddProduct(groups, count++ % palette.Length, cubeMesh,
                    new Vector3(x, 1.1875f, 0.16f), new Vector3(0.22f, 0.11f, 0.16f));
                for (int pen = 0; pen < 2; pen++)
                    AddProduct(groups, count++ % palette.Length, cylinderMesh,
                        new Vector3(x - 0.05f + pen * 0.10f, 1.29f, 0.16f),
                        new Vector3(0.018f, 0.085f, 0.018f)); // Unity cylinder height = 2.
            }
            // Four upright folders occupy the right counter without tiny spine details.
            for (int folder = 0; folder < 4; folder++)
                AddProduct(groups, count++ % palette.Length, cubeMesh,
                    new Vector3(0.45f + folder * 0.23f, 1.2975f, 0.14f), new Vector3(0.075f, 0.33f, 0.18f));
            // Sixteen flat packs on two lower display levels, visible through the glazing.
            for (int level = 0; level < 2; level++)
                for (int side = -1; side <= 1; side += 2)
                    for (int stack = 0; stack < 2; stack++)
                        for (int layer = 0; layer < 2; layer++)
                            AddProduct(groups, count++ % palette.Length, cubeMesh,
                                new Vector3(side * 0.7375f - 0.33f + stack * 0.66f,
                                    (level == 0 ? 0.3025f : 0.6925f) + 0.033f + layer * 0.068f, -0.005f),
                                new Vector3(0.54f, 0.066f, 0.36f));
            if (count != 64) throw new InvalidOperationException("Unexpected product count.");
            // Temporary material groups are subsequently baked into the opaque mesh.
            for (int i = 0; i < groups.Length; i++)
            {
                Mesh mesh = new Mesh { name = "DisplayItemsMesh_" + i, indexFormat = IndexFormat.UInt16 };
                ownedMeshes.Add(mesh);
                mesh.CombineMeshes(groups[i].ToArray(), true, true, false);
                mesh.RecalculateBounds();
                GameObject group = new GameObject("__TEMP_DisplayItems_" + i);
                group.transform.SetParent(parent, false);
                group.AddComponent<MeshFilter>().sharedMesh = mesh;
                group.AddComponent<MeshRenderer>().sharedMaterial = palette[i];
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cube);
            if (cylinder != null) UnityEngine.Object.DestroyImmediate(cylinder);
        }
    }

    private static void AddProduct(List<CombineInstance>[] groups, int color, Mesh primitive,
        Vector3 position, Vector3 scale)
    {
        groups[color].Add(new CombineInstance { mesh = primitive,
            transform = Matrix4x4.TRS(position, Quaternion.identity, scale) });
    }

    private static void Part(Transform parent, string name, Vector3 size, Vector3 position, Material material)
    {
        CreateCubePart(parent, name, size, position, Quaternion.identity, material);
    }

    private static void ConfigureGlass(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_BlendModePreserveSpecular", 1f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_AlphaToMask", 0f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_ZWriteControl", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ReceiveShadows", 1f);
        material.SetFloat("_QueueOffset", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetShaderPassEnabled("DepthOnly", false);
        material.SetShaderPassEnabled("DepthNormals", false);
        material.SetShaderPassEnabled("MotionVectors", false);
        EditorUtility.SetDirty(material);
    }

    private static void CreateFinalRenderer(Transform root, string name, Mesh mesh, Material[] materials, bool glass = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = glass ? ShadowCastingMode.Off : ShadowCastingMode.On;
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
