#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildLibraryBookshelfMVP
{
    private const string RootFolder = "Assets/Art/Furniture/LibraryBookshelf_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_LibraryBookshelf_MVP.prefab";
    private const float Width = 1.8f;
    private const float Height = 2.2f;
    private const float Depth = 0.45f;
    private const float SideThickness = 0.04f;
    private const float InnerWidth = Width - 2f * SideThickness;
    private const int ShelfCount = 5;
    private const int BooksPerShelf = 13;
    private const float ShelfThickness = 0.025f;
    private const float FirstShelfY = 0.10f;
    private const float ShelfSpacing = 0.405f;

    [MenuItem("Tools/UIU Simulator/Assets/Build Library Bookshelf MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);

        Material wood = GetOrCreateMaterial(MaterialsFolder + "/M_Bookshelf_Wood.mat",
            new Color(0.76f, 0.51f, 0.27f), 0f, 0.35f);
        Material metal = GetOrCreateMaterial(MaterialsFolder + "/M_Bookshelf_Metal.mat",
            new Color(0.10f, 0.115f, 0.13f), 0.7f, 0.45f);
        // URP/Lit does not read vertex colors. Shared flat-color materials preserve
        // the requested shader without textures or a separate renderer per book.
        Color[] colors = {
            new Color(0.85f, 0.83f, 0.72f), new Color(0.035f, 0.25f, 0.55f),
            new Color(0.65f, 0.065f, 0.045f), new Color(0.06f, 0.32f, 0.20f),
            new Color(0.18f, 0.52f, 0.58f), new Color(0.065f, 0.085f, 0.12f)
        };
        string[] suffixes = { "", "_Blue", "_Red", "_Green", "_Teal", "_Charcoal" };
        var books = new Material[colors.Length];
        for (int i = 0; i < books.Length; i++)
            books[i] = GetOrCreateMaterial(MaterialsFolder + "/M_Bookshelf_Books" + suffixes[i] + ".mat",
                colors[i], 0f, 0.2f);

        GameObject root = new GameObject("PF_LibraryBookshelf_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        try
        {
            Transform tempWood = new GameObject("__TEMP_WoodParts").transform;
            Transform tempMetal = new GameObject("__TEMP_MetalParts").transform;
            Transform tempBooks = new GameObject("__TEMP_BookParts").transform;
            tempWood.SetParent(root.transform, false);
            tempMetal.SetParent(root.transform, false);
            tempBooks.SetParent(root.transform, false);
            BuildWoodParts(tempWood, wood);
            BuildMetalParts(tempMetal, metal);
            BuildBooks(tempBooks, books);

            Mesh woodMesh = CombineAndSave(tempWood.gameObject, root.transform,
                MeshesFolder + "/Bookshelf_WoodCombined.asset", "Bookshelf_WoodCombined", new[] { wood });
            Mesh metalMesh = CombineAndSave(tempMetal.gameObject, root.transform,
                MeshesFolder + "/Bookshelf_MetalCombined.asset", "Bookshelf_MetalCombined", new[] { metal });
            Mesh bookMesh = CombineAndSave(tempBooks.gameObject, root.transform,
                MeshesFolder + "/Bookshelf_BooksCombined.asset", "Bookshelf_BooksCombined", books);
            UnityEngine.Object.DestroyImmediate(tempWood.gameObject);
            UnityEngine.Object.DestroyImmediate(tempMetal.gameObject);
            UnityEngine.Object.DestroyImmediate(tempBooks.gameObject);

            CreateFinalRenderer(root.transform, "Visual_Wood", woodMesh, new[] { wood });
            CreateFinalRenderer(root.transform, "Visual_Metal", metalMesh, new[] { metal });
            CreateFinalRenderer(root.transform, "Visual_Books", bookMesh, books);
            BuildSimpleColliders(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the library bookshelf prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Library bookshelf MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "Dimensions: 1.8 x 2.2 x 0.45 m. Pivot: floor center. Front: +Z.\n" +
                "65 books; 3 renderers; 8 material submeshes; 3 BoxColliders; shared instanced materials.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildWoodParts(Transform parent, Material material)
    {
        for (int side = -1; side <= 1; side += 2)
            CreateCubePart(parent, side < 0 ? "SideLeft" : "SideRight",
                new Vector3(SideThickness, Height, Depth),
                new Vector3(side * (Width - SideThickness) * 0.5f, Height * 0.5f, 0f),
                Quaternion.identity, material);
    }

    private static void BuildMetalParts(Transform parent, Material material)
    {
        CreateCubePart(parent, "TopFrame", new Vector3(InnerWidth, 0.055f, Depth),
            new Vector3(0f, Height - 0.0275f, 0f), Quaternion.identity, material);
        CreateCubePart(parent, "BottomFrame", new Vector3(InnerWidth, 0.075f, Depth),
            new Vector3(0f, 0.0375f, 0f), Quaternion.identity, material);
        CreateCubePart(parent, "BackPanel", new Vector3(InnerWidth, Height - 0.13f, 0.018f),
            new Vector3(0f, 1.11f, -Depth * 0.5f + 0.009f), Quaternion.identity, material);
        for (int side = -1; side <= 1; side += 2)
            CreateCubePart(parent, side < 0 ? "UprightLeft" : "UprightRight",
                new Vector3(0.025f, Height - 0.13f, 0.025f),
                new Vector3(side * (InnerWidth * 0.5f - 0.0125f), 1.11f, Depth * 0.5f - 0.0125f),
                Quaternion.identity, material);
        for (int shelf = 0; shelf < ShelfCount; shelf++)
            CreateCubePart(parent, "Shelf_" + shelf,
                new Vector3(InnerWidth, ShelfThickness, Depth - 0.025f),
                new Vector3(0f, FirstShelfY + shelf * ShelfSpacing, 0f), Quaternion.identity, material);
    }

    private static void BuildBooks(Transform parent, Material[] palette)
    {
        // Local seed makes rebuilds repeatable without changing Unity's global random state.
        var random = new System.Random(4271);
        for (int shelf = 0; shelf < ShelfCount; shelf++)
        {
            float cursor = -0.80f;
            float shelfTop = FirstShelfY + shelf * ShelfSpacing + ShelfThickness * 0.5f;
            for (int book = 0; book < BooksPerShelf; book++)
            {
                float width = Mathf.Lerp(0.065f, 0.105f, (float)random.NextDouble());
                float height = Mathf.Lerp(0.255f, 0.35f, (float)random.NextDouble());
                float depth = Mathf.Lerp(0.25f, 0.34f, (float)random.NextDouble());
                float angle = book == 9 ? (shelf % 2 == 0 ? -8f : 8f) : 0f;
                float radians = angle * Mathf.Deg2Rad;
                // Reserve rotated bounds and rest the lowest corner on the shelf.
                float span = width * Mathf.Cos(radians) + height * Mathf.Abs(Mathf.Sin(radians));
                float rise = height * Mathf.Cos(radians) + width * Mathf.Abs(Mathf.Sin(radians));
                float front = 0.17f - 0.02f * (float)random.NextDouble();
                Material material = palette[(book + shelf + random.Next(3)) % palette.Length];
                CreateCubePart(parent, $"Book_{shelf}_{book}", new Vector3(width, height, depth),
                    new Vector3(cursor + span * 0.5f, shelfTop + rise * 0.5f, front - depth * 0.5f),
                    Quaternion.Euler(0f, 0f, angle), material);
                cursor += span + 0.008f;
                if (book == 3 || book == 8)
                    cursor += 0.06f;
            }
        }
    }

    private static void BuildSimpleColliders(Transform root)
    {
        Transform collision = new GameObject("Collision").transform;
        collision.SetParent(root, false);
        CreateBoxCollider(collision, "LeftSide", new Vector3(-(Width - SideThickness) * 0.5f, Height * 0.5f, 0f),
            new Vector3(SideThickness, Height, Depth), Quaternion.identity);
        CreateBoxCollider(collision, "RightSide", new Vector3((Width - SideThickness) * 0.5f, Height * 0.5f, 0f),
            new Vector3(SideThickness, Height, Depth), Quaternion.identity);
        CreateBoxCollider(collision, "ShelfBody", new Vector3(0f, Height * 0.5f, 0f),
            new Vector3(InnerWidth, Height, Depth), Quaternion.identity);
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

    private static void CreateBoxCollider(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 size,
        Quaternion localRotation)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;

        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.center = Vector3.zero;
        collider.size = size;
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
