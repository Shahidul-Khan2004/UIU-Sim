#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildComputerWorkstationMVP
{
    private const string RootFolder = "Assets/Art/Furniture/ComputerWorkstation_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";

    private const string PrefabPath = RootFolder + "/PF_ComputerWorkstation_MVP.prefab";

    private const string WoodMaterialPath = MaterialsFolder + "/M_Workstation_WoodLaminate.mat";
    private const string DarkMaterialPath = MaterialsFolder + "/M_Workstation_DarkParts.mat";
    private const string ScreenMaterialPath = MaterialsFolder + "/M_Workstation_Screen.mat";

    private const string WoodMeshPath = MeshesFolder + "/Workstation_WoodCombined.asset";
    private const string DarkMeshPath = MeshesFolder + "/Workstation_DarkCombined.asset";
    private const string ScreenMeshPath = MeshesFolder + "/Workstation_ScreenCombined.asset";

    [MenuItem("Tools/UIU Simulator/Assets/Build Computer Workstation MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);

        Material woodMaterial = GetOrCreateMaterial(
            WoodMaterialPath,
            new Color(0.78f, 0.68f, 0.54f, 1f),
            metallic: 0.0f,
            smoothness: 0.26f);

        Material darkMaterial = GetOrCreateMaterial(
            DarkMaterialPath,
            new Color(0.05f, 0.05f, 0.055f, 1f),
            metallic: 0.08f,
            smoothness: 0.18f);

        Material screenMaterial = GetOrCreateMaterial(
            ScreenMaterialPath,
            new Color(0.10f, 0.15f, 0.22f, 1f),
            metallic: 0.0f,
            smoothness: 0.55f,
            emissionColor: new Color(0.10f, 0.16f, 0.24f, 1f),
            emissionIntensity: 0.45f);

        GameObject root = new GameObject("PF_ComputerWorkstation_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        GameObject tempWood = new GameObject("__TEMP_WoodParts");
        GameObject tempDark = new GameObject("__TEMP_DarkParts");
        GameObject tempScreen = new GameObject("__TEMP_ScreenParts");

        tempWood.transform.SetParent(root.transform, false);
        tempDark.transform.SetParent(root.transform, false);
        tempScreen.transform.SetParent(root.transform, false);

        try
        {
            BuildWoodDesk(tempWood.transform, woodMaterial);
            BuildHardwareAndAccessories(tempDark.transform, darkMaterial);
            BuildScreenParts(tempScreen.transform, screenMaterial);

            Mesh woodMesh = CombineAndSave(tempWood, root.transform, WoodMeshPath, "Workstation_WoodCombined");
            Mesh darkMesh = CombineAndSave(tempDark, root.transform, DarkMeshPath, "Workstation_DarkCombined");
            Mesh screenMesh = CombineAndSave(tempScreen, root.transform, ScreenMeshPath, "Workstation_ScreenCombined");

            UnityEngine.Object.DestroyImmediate(tempWood);
            UnityEngine.Object.DestroyImmediate(tempDark);
            UnityEngine.Object.DestroyImmediate(tempScreen);

            CreateFinalRenderer(root.transform, "Visual_Wood", woodMesh, woodMaterial);
            CreateFinalRenderer(root.transform, "Visual_Dark", darkMesh, darkMaterial);
            CreateFinalRenderer(root.transform, "Visual_Screen", screenMesh, screenMaterial);

            BuildSimpleColliders(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the computer workstation prefab.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                $"Computer workstation MVP built successfully.\n" +
                $"Prefab: {PrefabPath}\n" +
                $"Menu: Tools/UIU Simulator/Assets/Build Computer Workstation MVP\n" +
                $"Includes desk + monitor + CPU + keyboard + mouse.\n" +
                $"Designed to duplicate side-by-side for a computer lab.");
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

    private static void BuildWoodDesk(Transform parent, Material material)
    {
        CreateCubePart(parent, "Top",
            new Vector3(0.82f, 0.035f, 0.60f),
            new Vector3(0f, 0.7425f, 0f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "SidePanel_Left",
            new Vector3(0.03f, 0.69f, 0.56f),
            new Vector3(-0.395f, 0.345f, 0f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "SidePanel_Right",
            new Vector3(0.03f, 0.69f, 0.56f),
            new Vector3(0.395f, 0.345f, 0f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "BottomFrontRail",
            new Vector3(0.76f, 0.035f, 0.03f),
            new Vector3(0f, 0.0175f, 0.265f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "BottomRearRail",
            new Vector3(0.76f, 0.035f, 0.03f),
            new Vector3(0f, 0.0175f, -0.265f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "TopFrontApron",
            new Vector3(0.76f, 0.025f, 0.03f),
            new Vector3(0f, 0.695f, 0.265f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "KeyboardTrayFront",
            new Vector3(0.72f, 0.065f, 0.025f),
            new Vector3(0f, 0.585f, 0.245f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "KeyboardTrayShelf",
            new Vector3(0.72f, 0.02f, 0.33f),
            new Vector3(0f, 0.555f, -0.03f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "InnerSupport_Left",
            new Vector3(0.02f, 0.52f, 0.40f),
            new Vector3(-0.33f, 0.28f, -0.06f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "InnerSupport_Right",
            new Vector3(0.02f, 0.52f, 0.40f),
            new Vector3(0.33f, 0.28f, -0.06f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "BackPanel",
            new Vector3(0.76f, 0.44f, 0.02f),
            new Vector3(0f, 0.235f, -0.255f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "TopSideTrim_Left",
            new Vector3(0.03f, 0.055f, 0.10f),
            new Vector3(-0.395f, 0.665f, 0.215f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "TopSideTrim_Right",
            new Vector3(0.03f, 0.055f, 0.10f),
            new Vector3(0.395f, 0.665f, 0.215f),
            Quaternion.identity,
            material);
    }

    private static void BuildHardwareAndAccessories(Transform parent, Material material)
    {
        CreateCubePart(parent, "MonitorBody",
            new Vector3(0.47f, 0.295f, 0.06f),
            new Vector3(0f, 0.93f, -0.09f),
            Quaternion.Euler(-2f, 0f, 0f),
            material);

        CreateCubePart(parent, "MonitorNeck",
            new Vector3(0.07f, 0.16f, 0.05f),
            new Vector3(0f, 0.805f, -0.10f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "MonitorBaseStem",
            new Vector3(0.11f, 0.02f, 0.07f),
            new Vector3(0f, 0.73f, -0.10f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "MonitorBasePlate",
            new Vector3(0.22f, 0.018f, 0.16f),
            new Vector3(0f, 0.718f, -0.07f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "Keyboard",
            new Vector3(0.43f, 0.018f, 0.14f),
            new Vector3(-0.03f, 0.575f, 0.035f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "Mouse",
            new Vector3(0.055f, 0.025f, 0.085f),
            new Vector3(0.25f, 0.577f, 0.05f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "CPUTower",
            new Vector3(0.18f, 0.42f, 0.42f),
            new Vector3(0.20f, 0.23f, -0.02f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "CpuPlinth",
            new Vector3(0.22f, 0.02f, 0.46f),
            new Vector3(0.20f, 0.01f, -0.02f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "PowerStrip",
            new Vector3(0.18f, 0.03f, 0.06f),
            new Vector3(-0.15f, 0.03f, 0.17f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "CableRun1",
            new Vector3(0.015f, 0.30f, 0.015f),
            new Vector3(0.10f, 0.40f, -0.18f),
            Quaternion.Euler(6f, 0f, 8f),
            material);

        CreateCubePart(parent, "CableRun2",
            new Vector3(0.015f, 0.26f, 0.015f),
            new Vector3(0.22f, 0.38f, -0.20f),
            Quaternion.Euler(-8f, 0f, -6f),
            material);

        CreateCubePart(parent, "CableRun3",
            new Vector3(0.015f, 0.14f, 0.015f),
            new Vector3(-0.05f, 0.50f, -0.16f),
            Quaternion.Euler(10f, 0f, 0f),
            material);

        CreateCubePart(parent, "KeyboardShadowPanel",
            new Vector3(0.70f, 0.012f, 0.30f),
            new Vector3(0f, 0.523f, -0.035f),
            Quaternion.identity,
            material);

        CreateCubePart(parent, "RearTopStrip",
            new Vector3(0.76f, 0.012f, 0.03f),
            new Vector3(0f, 0.727f, -0.255f),
            Quaternion.identity,
            material);
    }

    private static void BuildScreenParts(Transform parent, Material material)
    {
        CreateCubePart(parent, "MonitorScreen",
            new Vector3(0.405f, 0.235f, 0.008f),
            new Vector3(0f, 0.93f, -0.057f),
            Quaternion.Euler(-2f, 0f, 0f),
            material);
    }

    private static void BuildSimpleColliders(Transform root)
    {
        Transform collisionRoot = new GameObject("Collision").transform;
        collisionRoot.SetParent(root, false);

        CreateBoxCollider(
            collisionRoot, "TopCollider",
            new Vector3(0f, 0.7425f, 0f),
            new Vector3(0.82f, 0.04f, 0.60f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "LeftSideCollider",
            new Vector3(-0.395f, 0.345f, 0f),
            new Vector3(0.03f, 0.69f, 0.56f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "RightSideCollider",
            new Vector3(0.395f, 0.345f, 0f),
            new Vector3(0.03f, 0.69f, 0.56f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "FrontApronCollider",
            new Vector3(0f, 0.585f, 0.245f),
            new Vector3(0.72f, 0.10f, 0.04f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "BackPanelCollider",
            new Vector3(0f, 0.235f, -0.255f),
            new Vector3(0.76f, 0.44f, 0.02f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "MonitorCollider",
            new Vector3(0f, 0.93f, -0.09f),
            new Vector3(0.47f, 0.295f, 0.06f),
            Quaternion.Euler(-2f, 0f, 0f));

        CreateBoxCollider(
            collisionRoot, "CpuCollider",
            new Vector3(0.20f, 0.23f, -0.02f),
            new Vector3(0.18f, 0.42f, 0.42f),
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
        float smoothness,
        Color? emissionColor = null,
        float emissionIntensity = 0f)
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

        if (emissionColor.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            Color finalEmission = emissionColor.Value * Mathf.Max(0f, emissionIntensity);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", finalEmission);
        }

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
