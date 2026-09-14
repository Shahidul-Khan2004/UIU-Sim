#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildCafeStoolChairMVP
{
    private const string RootFolder = "Assets/Art/Furniture/CafeStoolChair_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";

    private const string PrefabPath = RootFolder + "/PF_CafeStoolChair_MVP.prefab";
    private const string SeatMaterialPath = MaterialsFolder + "/M_CafeChair_BlackSeat.mat";
    private const string MetalMaterialPath = MaterialsFolder + "/M_CafeChair_BlackMetal.mat";
    private const string WoodMaterialPath = MaterialsFolder + "/M_CafeChair_WoodBack.mat";

    private const string SeatMeshPath = MeshesFolder + "/CafeChair_SeatCombined.asset";
    private const string MetalMeshPath = MeshesFolder + "/CafeChair_MetalCombined.asset";
    private const string WoodMeshPath = MeshesFolder + "/CafeChair_WoodCombined.asset";

    [MenuItem("Tools/UIU Simulator/Assets/Build Cafe Stool Chair MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);

        Material seatMaterial = GetOrCreateMaterial(
            SeatMaterialPath,
            new Color(0.035f, 0.036f, 0.040f, 1f),
            metallic: 0.02f,
            smoothness: 0.42f);

        Material metalMaterial = GetOrCreateMaterial(
            MetalMaterialPath,
            new Color(0.050f, 0.052f, 0.055f, 1f),
            metallic: 0.18f,
            smoothness: 0.32f);

        Material woodMaterial = GetOrCreateMaterial(
            WoodMaterialPath,
            new Color(0.45f, 0.34f, 0.24f, 1f),
            metallic: 0.0f,
            smoothness: 0.24f);

        GameObject root = new GameObject("PF_CafeStoolChair_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        GameObject tempSeat = new GameObject("__TEMP_SeatParts");
        GameObject tempMetal = new GameObject("__TEMP_MetalParts");
        GameObject tempWood = new GameObject("__TEMP_WoodParts");
        tempSeat.transform.SetParent(root.transform, false);
        tempMetal.transform.SetParent(root.transform, false);
        tempWood.transform.SetParent(root.transform, false);

        try
        {
            BuildSeat(tempSeat.transform, seatMaterial);
            BuildMetalFrame(tempMetal.transform, metalMaterial);
            BuildWoodBack(tempWood.transform, woodMaterial);

            Mesh seatMesh = CombineAndSave(tempSeat, root.transform, SeatMeshPath, "CafeChair_SeatCombined");
            Mesh metalMesh = CombineAndSave(tempMetal, root.transform, MetalMeshPath, "CafeChair_MetalCombined");
            Mesh woodMesh = CombineAndSave(tempWood, root.transform, WoodMeshPath, "CafeChair_WoodCombined");

            UnityEngine.Object.DestroyImmediate(tempSeat);
            UnityEngine.Object.DestroyImmediate(tempMetal);
            UnityEngine.Object.DestroyImmediate(tempWood);

            CreateFinalRenderer(root.transform, "Visual_Seat", seatMesh, seatMaterial);
            CreateFinalRenderer(root.transform, "Visual_Metal", metalMesh, metalMaterial);
            CreateFinalRenderer(root.transform, "Visual_Wood", woodMesh, woodMaterial);

            BuildSimpleColliders(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the cafe stool chair prefab.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                "Cafe stool chair rebuilt with improved proportions and silhouette.\n" +
                "Prefab: " + PrefabPath);
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

    private static void BuildSeat(Transform parent, Material material)
    {
        // Main padded round seat.
        CreateCylinderPart(parent, "SeatTop",
            radius: 0.195f, height: 0.040f,
            localPosition: new Vector3(0f, 0.735f, 0f),
            localRotation: Quaternion.identity,
            material: material);

        // Slightly smaller underside lip, so the seat reads thinner than a plain cylinder.
        CreateCylinderPart(parent, "SeatUnderside",
            radius: 0.175f, height: 0.018f,
            localPosition: new Vector3(0f, 0.705f, 0f),
            localRotation: Quaternion.identity,
            material: material);
    }

    private static void BuildWoodBack(Transform parent, Material material)
    {
        // Wider rectangular backrest like the reference, lightly leaned back.
        CreateCubePart(parent, "BackPanel",
            new Vector3(0.34f, 0.14f, 0.025f),
            new Vector3(0f, 0.975f, -0.045f),
            Quaternion.Euler(-10f, 0f, 0f),
            material);
    }

    private static void BuildMetalFrame(Transform parent, Material material)
    {
        const float tube = 0.015f;

        // Four slender stool legs, splayed outward.
        CreateTube(parent, "Leg_FL",
            new Vector3(-0.125f, 0.700f, 0.125f),
            new Vector3(-0.185f, 0.040f, 0.185f),
            tube, material);

        CreateTube(parent, "Leg_FR",
            new Vector3(0.125f, 0.700f, 0.125f),
            new Vector3(0.185f, 0.040f, 0.185f),
            tube, material);

        CreateTube(parent, "Leg_RL",
            new Vector3(-0.125f, 0.700f, -0.125f),
            new Vector3(-0.185f, 0.040f, -0.185f),
            tube, material);

        CreateTube(parent, "Leg_RR",
            new Vector3(0.125f, 0.700f, -0.125f),
            new Vector3(0.185f, 0.040f, -0.185f),
            tube, material);

        // Seat support ring.
        CreateRing(parent, "SeatSupportRing", 0.165f, 0.690f, tube * 0.82f, 18, material);

        // Low footrest ring, prominent in the photo.
        CreateRing(parent, "FootRestRing", 0.205f, 0.130f, tube * 0.95f, 22, material);

        // Backrest uprights.
        CreateTube(parent, "BackUpright_Left",
            new Vector3(-0.085f, 0.740f, -0.070f),
            new Vector3(-0.120f, 0.925f, -0.045f),
            tube, material);

        CreateTube(parent, "BackUpright_Right",
            new Vector3(0.085f, 0.740f, -0.070f),
            new Vector3(0.120f, 0.925f, -0.045f),
            tube, material);
    }

    private static void BuildSimpleColliders(Transform root)
    {
        Transform collisionRoot = new GameObject("Collision").transform;
        collisionRoot.SetParent(root, false);

        CreateBoxCollider(
            collisionRoot, "SeatCollider",
            new Vector3(0f, 0.725f, 0f),
            new Vector3(0.41f, 0.08f, 0.41f),
            Quaternion.identity);

        CreateBoxCollider(
            collisionRoot, "BackCollider",
            new Vector3(0f, 0.975f, -0.045f),
            new Vector3(0.34f, 0.14f, 0.03f),
            Quaternion.Euler(-10f, 0f, 0f));
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

        RemoveCollider(go);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateCylinderPart(
        Transform parent,
        string name,
        float radius,
        float height,
        Vector3 localPosition,
        Quaternion localRotation,
        Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

        RemoveCollider(go);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void RemoveCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
    }

    private static void CreateTube(
        Transform parent,
        string name,
        Vector3 start,
        Vector3 end,
        float radius,
        Material material)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.0001f)
            return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (start + end) * 0.5f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);

        RemoveCollider(go);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateRing(
        Transform parent,
        string name,
        float radius,
        float y,
        float tubeRadius,
        int segments,
        Material material)
    {
        GameObject ringRoot = new GameObject(name);
        ringRoot.transform.SetParent(parent, false);

        for (int i = 0; i < segments; i++)
        {
            float a0 = (i / (float)segments) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;

            Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, y, Mathf.Sin(a0) * radius);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, y, Mathf.Sin(a1) * radius);

            CreateTube(ringRoot.transform, "Seg_" + i, p0, p1, tubeRadius, material);
        }
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

    private static Material GetOrCreateMaterial(string path, Color baseColor, float metallic, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);

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
