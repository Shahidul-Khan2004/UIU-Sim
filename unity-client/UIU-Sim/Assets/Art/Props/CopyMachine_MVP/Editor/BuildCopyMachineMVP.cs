#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildCopyMachineMVP
{
    private const string RootFolder = "Assets/Art/Props/CopyMachine_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_CopyMachine_MVP.prefab";

    [MenuItem("Tools/UIU Simulator/Assets/Build Copy Machine MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        Material white = GetOrCreateMaterial(MaterialsFolder + "/M_CopyMachine_WhitePlastic.mat",
            new Color(0.84f, 0.82f, 0.76f), 0f, 0.45f);
        Material dark = GetOrCreateMaterial(MaterialsFolder + "/M_CopyMachine_DarkPlastic.mat",
            new Color(0.065f, 0.075f, 0.085f), 0f, 0.35f);
        Material screen = GetOrCreateMaterial(MaterialsFolder + "/M_CopyMachine_Screen.mat",
            new Color(0.16f, 0.24f, 0.29f), 0f, 0.6f);
        Material button = GetOrCreateMaterial(MaterialsFolder + "/M_CopyMachine_Button.mat",
            new Color(0.43f, 0.45f, 0.46f), 0f, 0.4f);
        GameObject root = new GameObject("PF_CopyMachine_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        try
        {
            Transform body = new GameObject("__TEMP_BodyParts").transform;
            Transform top = new GameObject("__TEMP_TopParts").transform;
            Transform control = new GameObject("__TEMP_ControlParts").transform;
            body.SetParent(root.transform, false);
            top.SetParent(root.transform, false);
            control.SetParent(root.transform, false);
            BuildBodyParts(body, white, dark);
            BuildTopParts(top, white, dark, screen);
            BuildControlParts(control, dark, screen, button);
            Material[] bodyMaterials = { white, dark };
            Material[] topMaterials = { white, dark, screen };
            Material[] controlMaterials = { dark, screen, button };
            Mesh bodyMesh = CombineAndSave(body.gameObject, root.transform,
                MeshesFolder + "/CopyMachine_Body.asset", "CopyMachine_Body", bodyMaterials);
            Mesh topMesh = CombineAndSave(top.gameObject, root.transform,
                MeshesFolder + "/CopyMachine_Top.asset", "CopyMachine_Top", topMaterials);
            Mesh controlMesh = CombineAndSave(control.gameObject, root.transform,
                MeshesFolder + "/CopyMachine_Control.asset", "CopyMachine_Control", controlMaterials);
            UnityEngine.Object.DestroyImmediate(body.gameObject);
            UnityEngine.Object.DestroyImmediate(top.gameObject);
            UnityEngine.Object.DestroyImmediate(control.gameObject);
            CreateFinalRenderer(root.transform, "Visual_Body", bodyMesh, bodyMaterials);
            CreateFinalRenderer(root.transform, "Visual_Top", topMesh, topMaterials);
            CreateFinalRenderer(root.transform, "Visual_ControlPanel", controlMesh, controlMaterials);
            Transform collision = new GameObject("Collision").transform;
            collision.SetParent(root.transform, false);
            BoxCollider collider = collision.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.675f, 0f);
            collider.size = new Vector3(0.9f, 1.35f, 0.75f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the copy machine prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Copy machine MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "Floor-center pivot; front +Z; 0.9 x 1.35 x 0.75 m.\n" +
                "3 renderers, 4 shared materials, 8 submeshes, 1 BoxCollider; no runtime scripts.");
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

    private static void BuildBodyParts(Transform parent, Material white, Material dark)
    {
        // Dark core remains visible at the sides and between the cream drawer fronts.
        Part(parent, "LowerCabinet", new Vector3(0.86f, 0.78f, 0.65f), new Vector3(0f, 0.44f, -0.025f), dark);
        Part(parent, "Plinth", new Vector3(0.80f, 0.05f, 0.61f), new Vector3(0f, 0.025f, -0.025f), dark);
        Part(parent, "LeftCasing", new Vector3(0.04f, 0.79f, 0.68f), new Vector3(-0.43f, 0.445f, -0.035f), white);
        Part(parent, "RightCasing", new Vector3(0.04f, 0.79f, 0.68f), new Vector3(0.43f, 0.445f, -0.035f), white);
        Part(parent, "FrontServiceDoor", new Vector3(0.81f, 0.275f, 0.035f), new Vector3(0f, 0.6875f, 0.3175f), white);
        for (int drawer = 0; drawer < 3; drawer++)
        {
            float y = 0.06f + drawer * 0.16f;
            // A real notch between two shoulders makes a recognizable recessed pull.
            Part(parent, "DrawerFace_" + drawer, new Vector3(0.81f, 0.11f, 0.035f),
                new Vector3(0f, y + 0.055f, 0.3175f), white);
            for (int side = -1; side <= 1; side += 2)
                Part(parent, $"DrawerShoulder_{drawer}_{side}", new Vector3(0.285f, 0.043f, 0.035f),
                    new Vector3(side * 0.2625f, y + 0.1315f, 0.3175f), white);
        }
        Part(parent, "OutputFloor", new Vector3(0.84f, 0.025f, 0.65f), new Vector3(0f, 0.8425f, -0.025f), dark);
        Part(parent, "OutputBack", new Vector3(0.81f, 0.19f, 0.055f), new Vector3(0f, 0.95f, -0.315f), dark);
        for (int side = -1; side <= 1; side += 2)
            Part(parent, "ScannerSupport_" + side, new Vector3(0.08f, 0.19f, 0.66f),
                new Vector3(side * 0.405f, 0.95f, -0.035f), white);
        Part(parent, "ScannerHousing", new Vector3(0.9f, 0.085f, 0.75f), new Vector3(0f, 1.0875f, 0f), white);
        // Paper geometry is baked into Visual_Body; no fourth renderer or paper objects.
        Transform paper = new GameObject("__TEMP_Visual_Paper").transform;
        paper.SetParent(parent, false);
        Part(paper, "OutputLip", new Vector3(0.68f, 0.014f, 0.13f), new Vector3(-0.015f, 0.862f, 0.25f), dark);
        for (int drawer = 0; drawer < 3; drawer++)
            Part(paper, "RecessedPull_" + drawer, new Vector3(0.24f, 0.041f, 0.006f),
                new Vector3(0f, 0.1915f + drawer * 0.16f, 0.304f), dark);
    }

    private static void BuildTopParts(Transform parent, Material white, Material dark, Material screen)
    {
        // Opaque dark scanner-bed insert: a simple visual patch, no glass shell or internals.
        Part(parent, "ScannerBed", new Vector3(0.79f, 0.008f, 0.62f), new Vector3(0f, 1.134f, 0f), screen);
        Part(parent, "ScannerLid", new Vector3(0.85f, 0.065f, 0.60f), new Vector3(0f, 1.1705f, -0.045f), white);
        Part(parent, "FeederHousing", new Vector3(0.38f, 0.147f, 0.48f), new Vector3(-0.225f, 1.2765f, -0.08f), white);
        Part(parent, "FeederMouth", new Vector3(0.012f, 0.042f, 0.36f), new Vector3(-0.03f, 1.265f, -0.06f), dark);
        CreateCubePart(parent, "DocumentTray", new Vector3(0.43f, 0.025f, 0.43f),
            new Vector3(0.18f, 1.285f, -0.08f), Quaternion.Euler(0f, 0f, 7f), dark);
        for (int side = -1; side <= 1; side += 2)
            Part(parent, "TrayGuide_" + side, new Vector3(0.24f, 0.035f, 0.018f),
                new Vector3(0.12f, 1.313f, -0.08f + side * 0.19f), dark);
    }

    private static void BuildControlParts(Transform parent, Material dark, Material screen, Material button)
    {
        // Transform every temporary panel primitive together before combining into root space.
        parent.localPosition = new Vector3(0.19f, 1.042f, 0.265f);
        parent.localRotation = Quaternion.Euler(25f, 0f, 0f);
        Part(parent, "Console", new Vector3(0.43f, 0.04f, 0.20f), Vector3.zero, dark);
        Part(parent, "Display", new Vector3(0.235f, 0.005f, 0.14f), new Vector3(-0.072f, 0.0225f, 0f), screen);
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 2; column++)
                Part(parent, $"Button_{row}_{column}", new Vector3(0.035f, 0.008f, 0.025f),
                    new Vector3(0.095f + column * 0.065f, 0.024f, -0.06f + row * 0.04f), button);
    }

    private static void Part(Transform parent, string name, Vector3 size, Vector3 position, Material material)
    {
        CreateCubePart(parent, name, size, position, Quaternion.identity, material);
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
            // One submesh per shared material, all in one saved mesh and renderer.
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
