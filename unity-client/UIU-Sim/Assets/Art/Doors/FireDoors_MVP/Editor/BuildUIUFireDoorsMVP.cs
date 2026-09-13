#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildUIUFireDoorsMVP
{
    private const string ScriptSearchName = "BuildUIUFireDoorsMVP";

    [MenuItem("Tools/UIU Simulator/Build Fire Doors MVP")]
    public static void BuildAll()
    {
        string root = ResolveRoot();
        if (string.IsNullOrEmpty(root))
            return;

        string textureDir = root + "/Textures";
        string materialDir = root + "/Materials";
        string prefabDir = root + "/Prefabs";

        EnsureFolder(root, "Materials");
        EnsureFolder(root, "Prefabs");

        AssetDatabase.Refresh();

        string basePath = textureDir + "/FireDoorPaint_BaseColor.png";
        string normalPath = textureDir + "/FireDoorPaint_Normal.png";
        string maskPath = textureDir + "/FireDoorPaint_Mask.png";

        if (!ValidateAssetPath(basePath, "Base Color") ||
            !ValidateAssetPath(normalPath, "Normal") ||
            !ValidateAssetPath(maskPath, "Mask"))
        {
            Debug.LogError("Fire Doors MVP: Build cancelled because one or more required textures are missing.");
            return;
        }

        Debug.Log("Fire Doors MVP resolved paths:\n" +
                  "Root: " + root + "\n" +
                  "Base Color: " + basePath + "\n" +
                  "Normal: " + normalPath + "\n" +
                  "Mask: " + maskPath);

        ConfigureTexture(basePath, false, true);
        ConfigureTexture(normalPath, true, false);
        ConfigureTexture(maskPath, false, false);

        Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
        Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            lit = Shader.Find("Standard");

        if (lit == null)
        {
            Debug.LogError("Fire Doors MVP: Could not find URP Lit or Standard shader.");
            return;
        }

        Material redPaint = GetOrCreateMaterial(materialDir + "/M_FireDoor_RedPaint.mat", lit);
        SetupPaintMaterial(redPaint, baseTex, normalTex, maskTex);

        Material framePaint = GetOrCreateMaterial(materialDir + "/M_FireDoor_FramePaint.mat", lit);
        SetupSolidMaterial(framePaint, new Color(0.12f, 0.025f, 0.020f, 1f), 0f, 0.24f);

        Material stainless = GetOrCreateMaterial(materialDir + "/M_FireDoor_Stainless.mat", lit);
        SetupSolidMaterial(stainless, new Color(0.62f, 0.64f, 0.64f, 1f), 0.90f, 0.50f);

        Material darkMetal = GetOrCreateMaterial(materialDir + "/M_FireDoor_DarkMetal.mat", lit);
        SetupSolidMaterial(darkMetal, new Color(0.055f, 0.060f, 0.062f, 1f), 0.80f, 0.28f);

        AssetDatabase.SaveAssets();

        GameObject panicPrefab = BuildPanicBarDoor(redPaint, framePaint, stainless, darkMetal);
        string panicPath = prefabDir + "/PF_FireDoor_PanicBar_MVP.prefab";
        GameObject panicAsset = PrefabUtility.SaveAsPrefabAsset(panicPrefab, panicPath);
        UnityEngine.Object.DestroyImmediate(panicPrefab);

        GameObject leverPrefab = BuildLeverDoor(redPaint, framePaint, stainless, darkMetal);
        string leverPath = prefabDir + "/PF_FireDoor_Lever_MVP.prefab";
        GameObject leverAsset = PrefabUtility.SaveAsPrefabAsset(leverPrefab, leverPath);
        UnityEngine.Object.DestroyImmediate(leverPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = panicAsset;
        EditorGUIUtility.PingObject(panicAsset);

        Debug.Log(
            "Fire Doors MVP created successfully.\n" +
            "Panic-bar door: " + panicPath + "\n" +
            "Lever-handle door: " + leverPath + "\n" +
            "Both prefabs share the same materials/textures for efficient reuse."
        );
    }

    private static GameObject BuildPanicBarDoor(Material redPaint, Material framePaint, Material stainless, Material darkMetal)
    {
        GameObject root = CreateDoorCore("FireDoor_PanicBar_MVP", redPaint, framePaint, out Transform pivot);

        // Horizontal crash / panic bar inspired by the reference.
        CreateCube(pivot, "PanicBar_Rail",
            new Vector3(0.53f, 1.00f, -0.075f),
            new Vector3(0.76f, 0.045f, 0.055f), stainless, false);

        CreateCube(pivot, "PanicBar_CenterGrip",
            new Vector3(0.53f, 1.00f, -0.095f),
            new Vector3(0.36f, 0.065f, 0.050f), darkMetal, false);

        CreateCube(pivot, "PanicBar_LeftMount",
            new Vector3(0.18f, 1.00f, -0.070f),
            new Vector3(0.08f, 0.12f, 0.075f), stainless, false);

        CreateCube(pivot, "PanicBar_RightMount",
            new Vector3(0.88f, 1.00f, -0.070f),
            new Vector3(0.08f, 0.12f, 0.075f), stainless, false);

        // Right-side latch housing.
        CreateCube(pivot, "PanicBar_LatchHousing",
            new Vector3(0.96f, 1.00f, -0.066f),
            new Vector3(0.07f, 0.18f, 0.085f), darkMetal, false);

        AddDoorCloser(pivot, stainless, darkMetal);
        AddHinges(pivot, darkMetal);
        AddAnchors(root.transform, pivot);

        return root;
    }

    private static GameObject BuildLeverDoor(Material redPaint, Material framePaint, Material stainless, Material darkMetal)
    {
        GameObject root = CreateDoorCore("FireDoor_Lever_MVP", redPaint, framePaint, out Transform pivot);

        // Long rectangular handle plate.
        CreateRoundedApproxPlate(pivot, "Lever_Backplate",
            new Vector3(0.19f, 1.00f, -0.073f),
            new Vector3(0.115f, 0.36f, 0.030f), stainless);

        // Lever spindle / neck
        CreateCylinder(pivot, "Lever_Neck",
            new Vector3(0.19f, 1.00f, -0.115f),
            new Vector3(0.032f, 0.055f, 0.032f),
            new Vector3(90f, 0f, 0f), stainless, false);

        // Horizontal lever.
        CreateCylinder(pivot, "Lever_Handle",
            new Vector3(0.26f, 1.00f, -0.145f),
            new Vector3(0.022f, 0.10f, 0.022f),
            new Vector3(0f, 0f, 90f), stainless, false);

        // Small circular key / lock detail.
        CreateCylinder(pivot, "Lock_Detail",
            new Vector3(0.19f, 1.09f, -0.103f),
            new Vector3(0.018f, 0.010f, 0.018f),
            new Vector3(90f, 0f, 0f), darkMetal, false);

        AddDoorCloser(pivot, stainless, darkMetal);
        AddHinges(pivot, darkMetal);
        AddAnchors(root.transform, pivot);

        return root;
    }

    private static GameObject CreateDoorCore(string rootName, Material redPaint, Material framePaint, out Transform pivot)
    {
        GameObject root = new GameObject(rootName);

        // Generic single fire-door dimensions for MVP:
        // Leaf = 1.00 m wide x 2.10 m high x 0.05 m thick.
        CreateCube(root.transform, "Frame_Left",
            new Vector3(-0.565f, 1.08f, 0f),
            new Vector3(0.13f, 2.16f, 0.11f), framePaint, false);

        CreateCube(root.transform, "Frame_Right",
            new Vector3(0.565f, 1.08f, 0f),
            new Vector3(0.13f, 2.16f, 0.11f), framePaint, false);

        CreateCube(root.transform, "Frame_Top",
            new Vector3(0f, 2.095f, 0f),
            new Vector3(1.26f, 0.13f, 0.11f), framePaint, false);

        GameObject hingePivot = new GameObject("DoorHingePivot");
        hingePivot.transform.SetParent(root.transform, false);
        hingePivot.transform.localPosition = new Vector3(-0.50f, 0f, 0f);
        pivot = hingePivot.transform;

        GameObject leaf = CreateCube(pivot, "DoorLeaf",
            new Vector3(0.50f, 1.025f, 0f),
            new Vector3(1.00f, 2.05f, 0.05f), redPaint, true);

        // A slightly inset central face helps resemble a pressed steel fire door.
        CreateCube(pivot, "DoorFaceInset",
            new Vector3(0.50f, 1.025f, -0.028f),
            new Vector3(0.89f, 1.91f, 0.008f), redPaint, false);

        return root;
    }

    private static void AddDoorCloser(Transform pivot, Material stainless, Material darkMetal)
    {
        // Surface-mounted closer body near the top, matching the second reference.
        CreateCube(pivot, "DoorCloser_Body",
            new Vector3(0.78f, 1.87f, -0.078f),
            new Vector3(0.28f, 0.10f, 0.07f), stainless, false);

        CreateCylinder(pivot, "DoorCloser_Arm_A",
            new Vector3(0.70f, 1.96f, -0.105f),
            new Vector3(0.012f, 0.14f, 0.012f),
            new Vector3(0f, 0f, -32f), darkMetal, false);

        CreateCylinder(pivot, "DoorCloser_Arm_B",
            new Vector3(0.63f, 2.04f, -0.105f),
            new Vector3(0.010f, 0.12f, 0.010f),
            new Vector3(0f, 0f, 10f), darkMetal, false);
    }

    private static void AddHinges(Transform pivot, Material darkMetal)
    {
        float[] y = { 0.32f, 1.03f, 1.72f };
        foreach (float yy in y)
        {
            CreateCube(pivot, "Hinge",
                new Vector3(0.015f, yy, 0.032f),
                new Vector3(0.030f, 0.14f, 0.018f), darkMetal, false);
        }
    }

    private static void AddAnchors(Transform root, Transform pivot)
    {
        GameObject exitSign = new GameObject("ExitSignAnchor");
        exitSign.transform.SetParent(root, false);
        exitSign.transform.localPosition = new Vector3(0f, 2.37f, -0.08f);

        GameObject interaction = new GameObject("InteractionAnchor");
        interaction.transform.SetParent(root, false);
        interaction.transform.localPosition = new Vector3(0f, 1.0f, -0.65f);

        GameObject label = new GameObject("DoorLabelAnchor");
        label.transform.SetParent(pivot, false);
        label.transform.localPosition = new Vector3(0.18f, 1.83f, -0.08f);
    }

    private static void CreateRoundedApproxPlate(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        // Kept as a cuboid for MVP performance; can be replaced by a rounded mesh later.
        CreateCube(parent, name, pos, scale, mat, false);
    }

    private static GameObject CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        AssignMaterial(go, material);

        if (!keepCollider)
            RemoveCollider(go);

        return go;
    }

    private static GameObject CreateCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.transform.localEulerAngles = localEuler;
        AssignMaterial(go, material);

        if (!keepCollider)
            RemoveCollider(go);

        return go;
    }

    private static void AssignMaterial(GameObject go, Material material)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private static void RemoveCollider(GameObject go)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null)
            UnityEngine.Object.DestroyImmediate(c);
    }

    private static void SetupPaintMaterial(Material mat, Texture2D baseTex, Texture2D normalTex, Texture2D maskTex)
    {
        SetBaseColor(mat, Color.white);
        SetBaseTexture(mat, baseTex);
        SetMetallic(mat, 0f);
        SetSmoothness(mat, 0.30f);

        if (normalTex != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.SetFloat("_BumpScale", 0.35f);
            mat.EnableKeyword("_NORMALMAP");
        }

        if (maskTex != null)
        {
            if (mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", maskTex);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (mat.HasProperty("_OcclusionMap"))
            {
                mat.SetTexture("_OcclusionMap", maskTex);
                mat.SetFloat("_OcclusionStrength", 0.25f);
            }
        }
    }

    private static void SetupSolidMaterial(Material mat, Color color, float metallic, float smoothness)
    {
        SetBaseColor(mat, color);
        SetMetallic(mat, metallic);
        SetSmoothness(mat, smoothness);
    }

    private static Material GetOrCreateMaterial(string path, Shader shader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    private static void SetBaseTexture(Material mat, Texture tex)
    {
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
    }

    private static void SetBaseColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }

    private static void SetMetallic(Material mat, float value)
    {
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", value);
    }

    private static void SetSmoothness(Material mat, float value)
    {
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", value);
        else if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", value);
    }

    private static void ConfigureTexture(string path, bool normalMap, bool sRGB)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("Fire Doors MVP: Could not get TextureImporter for: " + path);
            return;
        }

        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;

        if (normalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
        }
        else
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = sRGB;
        }

        importer.SaveAndReimport();
    }

    private static string ResolveRoot()
    {
        string[] guids = AssetDatabase.FindAssets(ScriptSearchName + " t:MonoScript");

        if (guids == null || guids.Length == 0)
        {
            Debug.LogError("Fire Doors MVP: Could not locate " + ScriptSearchName + ".cs anywhere under Assets/.");
            return null;
        }

        if (guids.Length > 1)
        {
            Debug.LogWarning("Fire Doors MVP: Multiple copies of the builder script were found. Using the first one.");
        }

        string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]).Replace("\\", "/");
        string editorDir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");

        if (string.IsNullOrEmpty(editorDir))
        {
            Debug.LogError("Fire Doors MVP: Failed to resolve the builder script directory from: " + scriptPath);
            return null;
        }

        string root = Path.GetDirectoryName(editorDir)?.Replace("\\", "/");

        if (string.IsNullOrEmpty(root) || !root.StartsWith("Assets/", StringComparison.Ordinal))
        {
            Debug.LogError("Fire Doors MVP: Resolved root is invalid: " + root);
            return null;
        }

        return root;
    }

    private static bool ValidateAssetPath(string path, string label)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            return true;

        Debug.LogError("Fire Doors MVP: Missing " + label + " texture. Attempted path: " + path);
        return false;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
