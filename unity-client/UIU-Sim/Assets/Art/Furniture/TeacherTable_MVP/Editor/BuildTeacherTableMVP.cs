#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildTeacherTableMVP
{
    private const string RootFolder = "Assets/Art/Furniture/TeacherTable_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";

    private const string PrefabPath = RootFolder + "/PF_TeacherTable_MVP.prefab";
    private const string WoodMaterialPath = MaterialsFolder + "/M_TeacherTable_WoodLaminate.mat";
    private const string WoodMeshPath = MeshesFolder + "/TeacherTable_WoodCombined.asset";

    [MenuItem("Tools/UIU Simulator/Assets/Build Teacher Table MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);

        Material woodMaterial = GetOrCreateMaterial(
            WoodMaterialPath,
            new Color(0.78f, 0.60f, 0.36f, 1f),
            metallic: 0.0f,
            smoothness: 0.24f);

        GameObject root = new GameObject("PF_TeacherTable_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        GameObject tempWood = new GameObject("__TEMP_WoodParts");
        tempWood.transform.SetParent(root.transform, false);

        try
        {
            BuildWoodParts(tempWood.transform, woodMaterial);

            Mesh woodMesh = CombineAndSave(tempWood, root.transform, WoodMeshPath, "TeacherTable_WoodCombined");

            UnityEngine.Object.DestroyImmediate(tempWood);

            CreateFinalRenderer(root.transform, "Visual_Wood", woodMesh, woodMaterial);

            BuildSimpleColliders(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the teacher table prefab.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                $"Teacher table MVP built successfully.\n" +
                $"Prefab: {PrefabPath}\n" +
                $"Menu: Tools/UIU Simulator/Assets/Build Teacher Table MVP\n" +
                $"Designed as a reusable classroom teacher/front-desk table.");
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
        // Teacher/front desk based on the classroom reference:
        // broad laminate top, boxed side panels, front modesty panel, open back.
        // Pivot stays at floor center so it is easy to place.

        // Main top
        CreateCubePart(
            parent, "Top",
            new Vector3(1.40f, 0.045f, 0.72f),
            new Vector3(0f, 0.7425f, 0f),
            Quaternion.identity,
            material);

        // Slight raised edging strips to capture the look from the reference
        CreateCubePart(
            parent, "Edge_Left",
            new Vector3(0.025f, 0.030f, 0.66f),
            new Vector3(-0.6875f, 0.772f, 0f),
            Quaternion.identity,
            material);

        CreateCubePart(
            parent, "Edge_Right",
            new Vector3(0.025f, 0.030f, 0.66f),
            new Vector3(0.6875f, 0.772f, 0f),
            Quaternion.identity,
            material);

        CreateCubePart(
            parent, "Edge_Back",
            new Vector3(1.35f, 0.030f, 0.025f),
            new Vector3(0f, 0.772f, -0.3475f),
            Quaternion.identity,
            material);

        // Side panels
        CreateCubePart(
            parent, "SidePanel_Left",
            new Vector3(0.05f, 0.70f, 0.68f),
            new Vector3(-0.675f, 0.35f, 0f),
            Quaternion.identity,
            material);

        CreateCubePart(
            parent, "SidePanel_Right",
            new Vector3(0.05f, 0.70f, 0.68f),
            new Vector3(0.675f, 0.35f, 0f),
            Quaternion.identity,
            material);

        // Front modesty panel
        CreateCubePart(
            parent, "FrontPanel",
            new Vector3(1.30f, 0.50f, 0.03f),
            new Vector3(0f, 0.285f, 0.325f),
            Quaternion.identity,
            material);

        // Small underside rails for structure
        CreateCubePart(
            parent, "TopRail_Left",
            new Vector3(0.035f, 0.045f, 0.58f),
            new Vector3(-0.61f, 0.69f, -0.03f),
            Quaternion.identity,
            material);

        CreateCubePart(
            parent, "TopRail_Right",
            new Vector3(0.035f, 0.045f, 0.58f),
            new Vector3(0.61f, 0.69f, -0.03f),
            Quaternion.identity,
            material);

        CreateCubePart(
            parent, "TopRail_Front",
            new Vector3(1.18f, 0.045f, 0.035f),
            new Vector3(0f, 0.69f, 0.29f),
            Quaternion.identity,
            material);

        // Back lower stretcher so the desk still feels solid while remaining open.
        CreateCubePart(
            parent, "BackStretcher",
            new Vector3(1.20f, 0.18f, 0.03f),
            new Vector3(0f, 0.18f, -0.315f),
            Quaternion.identity,
            material);
    }

    private static void BuildSimpleColliders(Transform root)
    {
        Transform collisionRoot = new GameObject("Collision").transform;
        collisionRoot.SetParent(root, false);

        CreateBoxCollider(
            collisionRoot, "TopCollider",
            new Vector3(0f, 0.7425f, 0f),
            new Vector3(1.40f, 0.05f, 0.72f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "LeftSideCollider",
            new Vector3(-0.675f, 0.35f, 0f),
            new Vector3(0.05f, 0.70f, 0.68f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "RightSideCollider",
            new Vector3(0.675f, 0.35f, 0f),
            new Vector3(0.05f, 0.70f, 0.68f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "FrontPanelCollider",
            new Vector3(0f, 0.285f, 0.325f),
            new Vector3(1.30f, 0.50f, 0.03f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "BackStretcherCollider",
            new Vector3(0f, 0.18f, -0.315f),
            new Vector3(1.20f, 0.18f, 0.03f),
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
