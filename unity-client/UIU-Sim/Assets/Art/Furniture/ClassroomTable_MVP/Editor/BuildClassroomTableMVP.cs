#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildClassroomTableMVP
{
    private const string RootFolder = "Assets/Art/Furniture/ClassroomTable_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";

    private const string PrefabPath = RootFolder + "/PF_ClassroomTable_MVP.prefab";
    private const string WoodMaterialPath = MaterialsFolder + "/M_Table_WoodLaminate.mat";
    private const string MetalMaterialPath = MaterialsFolder + "/M_Table_BlackMetal.mat";
    private const string WoodMeshPath = MeshesFolder + "/Table_WoodCombined.asset";
    private const string MetalMeshPath = MeshesFolder + "/Table_MetalCombined.asset";

    [MenuItem("Tools/UIU Simulator/Assets/Build Classroom Table MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);

        Material woodMaterial = GetOrCreateMaterial(
            WoodMaterialPath,
            new Color(0.78f, 0.68f, 0.54f, 1f),
            metallic: 0.0f,
            smoothness: 0.28f);

        Material metalMaterial = GetOrCreateMaterial(
            MetalMaterialPath,
            new Color(0.035f, 0.037f, 0.042f, 1f),
            metallic: 0.22f,
            smoothness: 0.34f);

        GameObject root = new GameObject("PF_ClassroomTable_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        GameObject tempWood = new GameObject("__TEMP_WoodParts");
        GameObject tempMetal = new GameObject("__TEMP_MetalParts");
        tempWood.transform.SetParent(root.transform, false);
        tempMetal.transform.SetParent(root.transform, false);

        try
        {
            BuildWoodParts(tempWood.transform, woodMaterial);
            BuildMetalParts(tempMetal.transform, metalMaterial);

            Mesh woodMesh = CombineAndSave(tempWood, root.transform, WoodMeshPath, "Table_WoodCombined");
            Mesh metalMesh = CombineAndSave(tempMetal, root.transform, MetalMeshPath, "Table_BlackMetalCombined");

            UnityEngine.Object.DestroyImmediate(tempWood);
            UnityEngine.Object.DestroyImmediate(tempMetal);

            CreateFinalRenderer(root.transform, "Visual_Wood", woodMesh, woodMaterial);
            CreateFinalRenderer(root.transform, "Visual_Metal", metalMesh, metalMaterial);

            BuildSimpleColliders(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the classroom table prefab.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                $"Classroom table MVP built successfully.\n" +
                $"Prefab: {PrefabPath}\n" +
                $"Pivot: floor center\n" +
                $"Renderers per table: 2\n" +
                $"Modular: multiple tables can be placed flush side-by-side or end-to-end.");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
        finally
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildWoodParts(Transform parent, Material material)
    {
        // Reference-inspired proportions:
        // a clean rectangular laminated wood top, suitable for grouping into longer tables.
        CreateCubePart(
            parent, "TableTop",
            size: new Vector3(1.40f, 0.03f, 0.75f),
            localPosition: new Vector3(0f, 0.725f, 0f),
            localRotation: Quaternion.identity,
            material: material);

        // Slightly thicker perimeter band under the top to add the look of a commercial desk top.
        CreateCubePart(
            parent, "TopBandFront",
            size: new Vector3(1.40f, 0.018f, 0.04f),
            localPosition: new Vector3(0f, 0.699f, 0.355f),
            localRotation: Quaternion.identity,
            material: material);

        CreateCubePart(
            parent, "TopBandBack",
            size: new Vector3(1.40f, 0.018f, 0.04f),
            localPosition: new Vector3(0f, 0.699f, -0.355f),
            localRotation: Quaternion.identity,
            material: material);

        CreateCubePart(
            parent, "TopBandLeft",
            size: new Vector3(0.04f, 0.018f, 0.67f),
            localPosition: new Vector3(-0.68f, 0.699f, 0f),
            localRotation: Quaternion.identity,
            material: material);

        CreateCubePart(
            parent, "TopBandRight",
            size: new Vector3(0.04f, 0.018f, 0.67f),
            localPosition: new Vector3(0.68f, 0.699f, 0f),
            localRotation: Quaternion.identity,
            material: material);
    }

    private static void BuildMetalParts(Transform parent, Material material)
    {
        // Black powder-coated metal frame with simple square-tube look.
        Vector3 legSize = new Vector3(0.045f, 0.69f, 0.045f);

        CreateCubePart(parent, "Leg_FL", legSize, new Vector3(-0.62f, 0.345f,  0.30f), Quaternion.identity, material);
        CreateCubePart(parent, "Leg_FR", legSize, new Vector3( 0.62f, 0.345f,  0.30f), Quaternion.identity, material);
        CreateCubePart(parent, "Leg_RL", legSize, new Vector3(-0.62f, 0.345f, -0.30f), Quaternion.identity, material);
        CreateCubePart(parent, "Leg_RR", legSize, new Vector3( 0.62f, 0.345f, -0.30f), Quaternion.identity, material);

        // Upper rails directly below the top.
        CreateCubePart(parent, "UpperRailFront",
            new Vector3(1.195f, 0.035f, 0.035f),
            new Vector3(0f, 0.665f, 0.30f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "UpperRailBack",
            new Vector3(1.195f, 0.035f, 0.035f),
            new Vector3(0f, 0.665f, -0.30f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "UpperRailLeft",
            new Vector3(0.035f, 0.035f, 0.565f),
            new Vector3(-0.62f, 0.665f, 0f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "UpperRailRight",
            new Vector3(0.035f, 0.035f, 0.565f),
            new Vector3(0.62f, 0.665f, 0f),
            Quaternion.identity,
            material);

        // Lower stretcher / support bars like the photo.
        CreateCubePart(parent, "LowerLongStretcher",
            new Vector3(0.95f, 0.03f, 0.03f),
            new Vector3(0f, 0.09f, 0f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "CenterSupport_L",
            new Vector3(0.03f, 0.54f, 0.03f),
            new Vector3(-0.12f, 0.36f, 0f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "CenterSupport_R",
            new Vector3(0.03f, 0.54f, 0.03f),
            new Vector3(0.12f, 0.36f, 0f),
            Quaternion.identity,
            material);

        // Mid horizontal brace to visually ground the center structure.
        CreateCubePart(parent, "CenterCrossbar",
            new Vector3(0.27f, 0.03f, 0.03f),
            new Vector3(0f, 0.36f, 0f),
            Quaternion.identity,
            material);
    }

    private static void BuildSimpleColliders(Transform root)
    {
        Transform collisionRoot = new GameObject("Collision").transform;
        collisionRoot.SetParent(root, false);

        CreateBoxCollider(
            collisionRoot, "TopCollider",
            new Vector3(0f, 0.725f, 0f),
            new Vector3(1.40f, 0.05f, 0.75f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "LegAreaLeft",
            new Vector3(-0.62f, 0.345f, 0f),
            new Vector3(0.08f, 0.69f, 0.64f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "LegAreaRight",
            new Vector3(0.62f, 0.345f, 0f),
            new Vector3(0.08f, 0.69f, 0.64f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "CenterSupportArea",
            new Vector3(0f, 0.25f, 0f),
            new Vector3(0.35f, 0.32f, 0.08f),
            Quaternion.identity);
    }

    private static void CreateFinalRenderer(Transform root, string name, Mesh mesh, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
    }

    private static Mesh CombineAndSave(GameObject tempRoot, Transform finalRoot, string assetPath, string meshName)
    {
        MeshFilter[] filters = tempRoot.GetComponentsInChildren<MeshFilter>(true);
        var combine = new List<CombineInstance>(filters.Length);
        Matrix4x4 worldToRoot = finalRoot.worldToLocalMatrix;

        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                continue;

            combine.Add(new CombineInstance
            {
                mesh = filter.sharedMesh,
                transform = worldToRoot * filter.transform.localToWorldMatrix
            });
        }

        Mesh combined = new Mesh
        {
            name = meshName,
            indexFormat = IndexFormat.UInt32
        };

        combined.CombineMeshes(combine.ToArray(), true, true, false);
        combined.RecalculateBounds();

        if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        AssetDatabase.CreateAsset(combined, assetPath);
        return AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
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
            shader = Shader.Find("Standard");

        if (shader == null)
            throw new InvalidOperationException("Could not find URP/Lit or Standard shader.");

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
