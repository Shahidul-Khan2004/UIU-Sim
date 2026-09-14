#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildUniversityDoorMVP
{
    private const string ScriptTypeName = "BuildUniversityDoorMVP";
    private const string TextureFolderName = "Textures";
    private const string MaterialFolderName = "Materials";
    private const string PrefabFolderName = "Prefabs";

    private const string BaseColorFileName = "DoorWood_BaseColor.png";
    private const string NormalFileName = "DoorWood_Normal.png";
    private const string MaskFileName = "DoorWood_Mask.png";

    [MenuItem("Tools/UIU Simulator/Build University Door MVP")]
    public static void BuildDoor()
    {
        if (!TryResolveRoot(out string root))
            return;

        string textureDir = JoinAssetPath(root, TextureFolderName);
        string materialDir = JoinAssetPath(root, MaterialFolderName);
        string prefabDir = JoinAssetPath(root, PrefabFolderName);

        EnsureFolder(root, MaterialFolderName);
        EnsureFolder(root, PrefabFolderName);

        AssetDatabase.Refresh();

        string basePath = JoinAssetPath(textureDir, BaseColorFileName);
        string normalPath = JoinAssetPath(textureDir, NormalFileName);
        string maskPath = JoinAssetPath(textureDir, MaskFileName);

        Debug.Log("University Door MVP: Root resolved to " + root);
        Debug.Log("University Door MVP: BaseColor path -> " + basePath);
        Debug.Log("University Door MVP: Normal path -> " + normalPath);
        Debug.Log("University Door MVP: Mask path -> " + maskPath);
        Debug.Log("University Door MVP: Materials output -> " + materialDir);
        Debug.Log("University Door MVP: Prefabs output -> " + prefabDir);

        if (!ValidateTextureExists(basePath, "BaseColor")
            || !ValidateTextureExists(normalPath, "Normal")
            || !ValidateTextureExists(maskPath, "Mask"))
        {
            return;
        }

        ConfigureTexture(basePath, false, true);
        ConfigureTexture(normalPath, true, false);
        ConfigureTexture(maskPath, false, false);

        Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
        Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

        if (baseTex == null)
        {
            Debug.LogError(
                "University Door MVP: Failed to load BaseColor texture at " + basePath +
                ". The file exists but Unity could not import it as a Texture2D.");
            return;
        }

        if (normalTex == null)
        {
            Debug.LogError(
                "University Door MVP: Failed to load Normal texture at " + normalPath +
                ". The file exists but Unity could not import it as a Texture2D.");
            return;
        }

        if (maskTex == null)
        {
            Debug.LogError(
                "University Door MVP: Failed to load Mask texture at " + maskPath +
                ". The file exists but Unity could not import it as a Texture2D.");
            return;
        }

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            lit = Shader.Find("Standard");

        Material wood = GetOrCreateMaterial(JoinAssetPath(materialDir, "M_DoorWood.mat"), lit);
        SetBaseColor(wood, Color.white);
        SetBaseTexture(wood, baseTex);
        SetMetallic(wood, 0f);
        SetSmoothness(wood, 0.34f);

        if (wood.HasProperty("_BumpMap"))
        {
            wood.SetTexture("_BumpMap", normalTex);
            wood.SetFloat("_BumpScale", 0.55f);
            wood.EnableKeyword("_NORMALMAP");
        }

        if (wood.HasProperty("_MetallicGlossMap"))
        {
            wood.SetTexture("_MetallicGlossMap", maskTex);
            wood.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (wood.HasProperty("_OcclusionMap"))
        {
            wood.SetTexture("_OcclusionMap", maskTex);
            wood.SetFloat("_OcclusionStrength", 0.35f);
        }

        Material metal = GetOrCreateMaterial(JoinAssetPath(materialDir, "M_HandleMetal.mat"), lit);
        SetBaseColor(metal, new Color(0.62f, 0.58f, 0.50f, 1f));
        SetMetallic(metal, 0.82f);
        SetSmoothness(metal, 0.52f);

        Material trim = GetOrCreateMaterial(JoinAssetPath(materialDir, "M_DoorTrim.mat"), lit);
        SetBaseColor(trim, new Color(0.035f, 0.045f, 0.047f, 1f));
        SetMetallic(trim, 0.18f);
        SetSmoothness(trim, 0.26f);

        Material glass = GetOrCreateMaterial(JoinAssetPath(materialDir, "M_DoorGlass.mat"), lit);
        ConfigureTransparentGlass(glass);

        AssetDatabase.SaveAssets();

        GameObject rootGo = new GameObject("UniversityDoor_MVP");
        rootGo.transform.position = Vector3.zero;

        // Overall dimensions are intentionally generic so the prefab is reusable:
        // leaf = 1.05 m wide x 2.10 m high x 0.05 m thick.
        CreateCube(rootGo.transform, "Frame_Left",
            new Vector3(-0.585f, 1.10f, 0f),
            new Vector3(0.12f, 2.20f, 0.11f), trim, false);

        CreateCube(rootGo.transform, "Frame_Right",
            new Vector3(0.585f, 1.10f, 0f),
            new Vector3(0.12f, 2.20f, 0.11f), trim, false);

        CreateCube(rootGo.transform, "Frame_Top",
            new Vector3(0f, 2.16f, 0f),
            new Vector3(1.29f, 0.12f, 0.11f), trim, false);

        // Pivot is at the left edge of the leaf so gameplay can rotate this object later.
        GameObject pivot = new GameObject("DoorHingePivot");
        pivot.transform.SetParent(rootGo.transform, false);
        pivot.transform.localPosition = new Vector3(-0.525f, 0f, 0f);

        CreateCube(pivot.transform, "DoorLeaf",
            new Vector3(0.525f, 1.05f, 0f),
            new Vector3(1.05f, 2.10f, 0.05f), wood, true);

        // Narrow vertical vision panel, close to the supplied reference.
        Vector3 windowCenter = new Vector3(0.735f, 1.26f, -0.033f);
        CreateCube(pivot.transform, "Glass",
            windowCenter,
            new Vector3(0.15f, 0.58f, 0.014f), glass, false);

        const float border = 0.022f;
        CreateCube(pivot.transform, "GlassBorder_Left",
            windowCenter + new Vector3(-0.086f, 0f, -0.004f),
            new Vector3(border, 0.63f, 0.018f), trim, false);
        CreateCube(pivot.transform, "GlassBorder_Right",
            windowCenter + new Vector3(0.086f, 0f, -0.004f),
            new Vector3(border, 0.63f, 0.018f), trim, false);
        CreateCube(pivot.transform, "GlassBorder_Top",
            windowCenter + new Vector3(0f, 0.304f, -0.004f),
            new Vector3(0.194f, border, 0.018f), trim, false);
        CreateCube(pivot.transform, "GlassBorder_Bottom",
            windowCenter + new Vector3(0f, -0.304f, -0.004f),
            new Vector3(0.194f, border, 0.018f), trim, false);

        // Long vertical pull handle.
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Handle_Bar";
        handle.transform.SetParent(pivot.transform, false);
        handle.transform.localPosition = new Vector3(0.18f, 1.03f, -0.085f);
        handle.transform.localScale = new Vector3(0.018f, 0.47f, 0.018f);
        SetMaterial(handle, metal);
        RemoveCollider(handle);

        CreateCube(pivot.transform, "Handle_Mount_Top",
            new Vector3(0.18f, 1.47f, -0.064f),
            new Vector3(0.075f, 0.035f, 0.10f), metal, false);

        CreateCube(pivot.transform, "Handle_Mount_Bottom",
            new Vector3(0.18f, 0.59f, -0.064f),
            new Vector3(0.075f, 0.035f, 0.10f), metal, false);

        // Empty anchors keep room-specific signs out of the shared door mesh/material.
        GameObject roomSign = new GameObject("RoomSignAnchor");
        roomSign.transform.SetParent(pivot.transform, false);
        roomSign.transform.localPosition = new Vector3(0.60f, 1.52f, -0.07f);

        GameObject statusSign = new GameObject("StatusSignAnchor");
        statusSign.transform.SetParent(pivot.transform, false);
        statusSign.transform.localPosition = new Vector3(0.60f, 1.10f, -0.07f);

        GameObject interaction = new GameObject("InteractionAnchor");
        interaction.transform.SetParent(rootGo.transform, false);
        interaction.transform.localPosition = new Vector3(0f, 1.0f, -0.65f);

        string prefabPath = JoinAssetPath(prefabDir, "PF_UniversityDoor_MVP.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
        Object.DestroyImmediate(rootGo);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log(
            "University Door MVP created at " + prefabPath +
            "\nDrag PF_UniversityDoor_MVP into the scene. Duplicate the prefab instance anywhere you need another door."
        );
    }

    /// <summary>
    /// Resolves UniversityDoor_MVP by locating this editor script and taking its parent folder.
    /// Script lives at .../UniversityDoor_MVP/Editor/BuildUniversityDoorMVP.cs
    /// </summary>
    private static bool TryResolveRoot(out string root)
    {
        root = null;

        string[] guids = AssetDatabase.FindAssets(ScriptTypeName + " t:MonoScript");
        if (guids == null || guids.Length == 0)
        {
            Debug.LogError(
                "University Door MVP: Could not find editor script asset named " + ScriptTypeName +
                ". Expected it under Assets/**/UniversityDoor_MVP/Editor/.");
            return false;
        }

        string scriptPath = null;
        for (int i = 0; i < guids.Length; i++)
        {
            string candidate = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(candidate))
                continue;

            string fileName = Path.GetFileNameWithoutExtension(candidate);
            if (fileName == ScriptTypeName)
            {
                scriptPath = NormalizeAssetPath(candidate);
                break;
            }
        }

        if (string.IsNullOrEmpty(scriptPath))
        {
            Debug.LogError(
                "University Door MVP: Found MonoScript GUID(s) for " + ScriptTypeName +
                " but could not resolve a matching .cs path.");
            return false;
        }

        // .../UniversityDoor_MVP/Editor
        string editorDir = NormalizeAssetPath(Path.GetDirectoryName(scriptPath));
        if (string.IsNullOrEmpty(editorDir))
        {
            Debug.LogError("University Door MVP: Could not resolve Editor folder from " + scriptPath);
            return false;
        }

        // .../UniversityDoor_MVP  (parent of Editor/)
        root = NormalizeAssetPath(Path.GetDirectoryName(editorDir));
        if (string.IsNullOrEmpty(root) || !root.StartsWith("Assets/"))
        {
            Debug.LogError(
                "University Door MVP: Resolved root is not a valid Assets/ path.\n" +
                "Script path: " + scriptPath + "\n" +
                "Attempted root: " + root);
            root = null;
            return false;
        }

        if (!AssetDatabase.IsValidFolder(root))
        {
            Debug.LogError("University Door MVP: Resolved root folder is invalid: " + root);
            root = null;
            return false;
        }

        return true;
    }

    private static bool ValidateTextureExists(string assetPath, string label)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) != null)
            return true;

        // Also accept a present file that has not been imported yet (importer will run next).
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string absolutePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolutePath))
            return true;

        Debug.LogError(
            "University Door MVP: " + label + " texture was not found.\n" +
            "Attempted path: " + assetPath);
        return false;
    }

    private static string JoinAssetPath(string parent, string child)
    {
        return NormalizeAssetPath(parent + "/" + child);
    }

    private static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        return path.Replace('\\', '/');
    }

    private static GameObject CreateCube(
        Transform parent, string name, Vector3 localPosition, Vector3 localScale,
        Material material, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        SetMaterial(go, material);

        if (!keepCollider)
            RemoveCollider(go);

        return go;
    }

    private static void SetMaterial(GameObject go, Material material)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private static void RemoveCollider(GameObject go)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null)
            Object.DestroyImmediate(c);
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
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", tex);
        else if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", tex);
    }

    private static void SetBaseColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private static void SetMetallic(Material mat, float value)
    {
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", value);
    }

    private static void SetSmoothness(Material mat, float value)
    {
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", value);
        else if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", value);
    }

    private static void ConfigureTransparentGlass(Material mat)
    {
        SetBaseColor(mat, new Color(0.86f, 0.94f, 0.96f, 0.20f));
        SetMetallic(mat, 0f);
        SetSmoothness(mat, 0.93f);

        if (mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline"))
        {
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);

            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f);

            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");

            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            // Built-in Standard shader fallback.
            if (mat.HasProperty("_Mode"))
                mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }

    private static void ConfigureTexture(string path, bool normalMap, bool sRGB)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
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

    private static void EnsureFolder(string parent, string child)
    {
        string path = JoinAssetPath(parent, child);
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
