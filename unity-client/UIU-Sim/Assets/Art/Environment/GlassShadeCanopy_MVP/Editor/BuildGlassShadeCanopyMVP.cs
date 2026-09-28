#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildGlassShadeCanopyMVP
{
    private const string RootFolder = "Assets/Art/Environment/GlassShadeCanopy_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_GlassShadeCanopy_MVP.prefab";
    private const float Length = 8f; // Local X.
    private const float Width = 4f; // Local Z.
    private const float Height = 3f;
    private const float ColumnSize = 0.15f;
    private const float BeamWidth = 0.10f;
    private const float RoofThickness = 0.10f;
    private const float GlassThickness = 0.012f;
    private const int LengthBays = 8;
    private const int WidthBays = 2;

    [MenuItem("Tools/UIU Simulator/Assets/Build Glass Shade Canopy MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        Material steel = GetOrCreateMaterial(MaterialsFolder + "/M_GlassShade_Steel.mat",
            new Color(0.19f, 0.215f, 0.235f, 1f), 0.75f, 0.45f);
        Material glass = GetOrCreateMaterial(MaterialsFolder + "/M_GlassShade_Glass.mat",
            new Color(0.78f, 0.91f, 0.97f, 0.3f), 0f, 0.9f, transparent: true);
        GameObject root = new GameObject("PF_GlassShadeCanopy_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        try
        {
            Transform tempSteel = new GameObject("__TEMP_SteelParts").transform;
            Transform tempGlass = new GameObject("__TEMP_GlassParts").transform;
            tempSteel.SetParent(root.transform, false);
            tempGlass.SetParent(root.transform, false);
            BuildSteelParts(tempSteel, steel);
            BuildGlassParts(tempGlass, glass);
            Mesh steelMesh = CombineAndSave(tempSteel.gameObject, root.transform,
                MeshesFolder + "/GlassShade_SteelCombined.asset", "GlassShade_SteelCombined");
            Mesh glassMesh = CombineAndSave(tempGlass.gameObject, root.transform,
                MeshesFolder + "/GlassShade_GlassCombined.asset", "GlassShade_GlassCombined");
            UnityEngine.Object.DestroyImmediate(tempSteel.gameObject);
            UnityEngine.Object.DestroyImmediate(tempGlass.gameObject);
            CreateFinalRenderer(root.transform, "Visual_Steel", steelMesh, steel);
            CreateFinalRenderer(root.transform, "Visual_Glass", glassMesh, glass, glass: true);
            BuildSimpleColliders(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the glass shade canopy prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Glass shade canopy MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "8 x 4 x 3 m; floor-center pivot; length along X.\n" +
                "2 renderers, 2 shared materials, 6 column BoxColliders, 408 triangles.\n" +
                "Glass reflections use the scene skybox / reflection probes.");
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

    private static Vector3 ColumnPosition(int station, int side)
    {
        return new Vector3((station - 1) * (Length - ColumnSize) * 0.5f,
            Height * 0.5f, side * (Width - ColumnSize) * 0.5f);
    }

    private static void BuildSteelParts(Transform parent, Material material)
    {
        for (int station = 0; station < 3; station++)
            for (int side = -1; side <= 1; side += 2)
                CreateCubePart(parent, $"Column_{station}_{side}",
                    new Vector3(ColumnSize, Height, ColumnSize), ColumnPosition(station, side),
                    Quaternion.identity, material);

        // Roof occupies the uppermost 0.1 m so the overall height remains 3 m.
        float y = Height - RoofThickness * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            CreateCubePart(parent, "LongEdge_" + side, new Vector3(Length, RoofThickness, BeamWidth),
                new Vector3(0f, y, side * (Width - BeamWidth) * 0.5f), Quaternion.identity, material);
            CreateCubePart(parent, "EndEdge_" + side,
                new Vector3(BeamWidth, RoofThickness, Width - 2f * BeamWidth),
                new Vector3(side * (Length - BeamWidth) * 0.5f, y, 0f), Quaternion.identity, material);
        }
        for (int bay = 1; bay < LengthBays; bay++)
            CreateCubePart(parent, "CrossBeam_" + bay,
                new Vector3(BeamWidth, RoofThickness, Width - 2f * BeamWidth),
                new Vector3(-Length * 0.5f + bay * Length / LengthBays, y, 0f),
                Quaternion.identity, material);
        CreateCubePart(parent, "CenterRail", new Vector3(Length - 2f * BeamWidth, RoofThickness, BeamWidth),
            new Vector3(0f, y, 0f), Quaternion.identity, material);
    }

    private static void BuildGlassParts(Transform parent, Material material)
    {
        for (int x = 0; x < LengthBays; x++)
            for (int z = 0; z < WidthBays; z++)
            {
                // Inset panes within the steel grid; no coplanar overlap or stacked glass.
                float left = -Length * 0.5f + x * Length / LengthBays +
                    (x == 0 ? BeamWidth : BeamWidth * 0.5f);
                float right = -Length * 0.5f + (x + 1) * Length / LengthBays -
                    (x == LengthBays - 1 ? BeamWidth : BeamWidth * 0.5f);
                float back = -Width * 0.5f + z * Width / WidthBays +
                    (z == 0 ? BeamWidth : BeamWidth * 0.5f);
                float front = -Width * 0.5f + (z + 1) * Width / WidthBays -
                    (z == WidthBays - 1 ? BeamWidth : BeamWidth * 0.5f);
                CreateCubePart(parent, $"Glass_{x}_{z}",
                    new Vector3(right - left, GlassThickness, front - back),
                    new Vector3((left + right) * 0.5f, Height - GlassThickness * 0.5f - 0.005f,
                        (back + front) * 0.5f), Quaternion.identity, material);
            }
    }

    private static void BuildSimpleColliders(Transform root)
    {
        Transform collision = new GameObject("Collision").transform;
        collision.SetParent(root, false);
        // Six colliders on a single child; the open walkway has no blocking body collider.
        for (int station = 0; station < 3; station++)
            for (int side = -1; side <= 1; side += 2)
            {
                BoxCollider collider = collision.gameObject.AddComponent<BoxCollider>();
                collider.center = ColumnPosition(station, side);
                collider.size = new Vector3(ColumnSize, Height, ColumnSize);
            }
    }

    private static void CreateFinalRenderer(Transform root, string name, Mesh mesh, Material material, bool glass = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = glass ? ShadowCastingMode.Off : ShadowCastingMode.On;
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
            indexFormat = IndexFormat.UInt16
        };

        combined.CombineMeshes(combine.ToArray(), true, true, false);
        combined.RecalculateBounds();

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing != null)
        {
            // Preserve asset references when rebuilding a canopy already placed in a scene.
            EditorUtility.CopySerialized(combined, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(combined);
            return existing;
        }
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

    private static Material GetOrCreateMaterial(
        string path,
        Color baseColor,
        float metallic,
        float smoothness, bool transparent = false)
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

        material.SetFloat("_Surface", transparent ? 1f : 0f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_BlendModePreserveSpecular", 1f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_AlphaToMask", 0f);
        // Closed cubes have outward top AND bottom faces. Back-face culling lets
        // the roof read correctly from below without double-blending each pane.
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.SetFloat("_ZWrite", transparent ? 0f : 1f);
        material.SetFloat("_ZWriteControl", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        material.SetFloat("_ReceiveShadows", 1f);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.SetFloat("_SpecularHighlights", 1f);
        material.SetFloat("_QueueOffset", 0f);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
        material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if (transparent)
        {
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
        material.renderQueue = (int)(transparent ? RenderQueue.Transparent : RenderQueue.Geometry);
        // Stock Lit shadow casting would make the transparent roof cast a solid shadow.
        material.SetShaderPassEnabled("ShadowCaster", !transparent);
        material.SetShaderPassEnabled("DepthOnly", !transparent);
        material.SetShaderPassEnabled("DepthNormals", !transparent);
        material.SetShaderPassEnabled("MotionVectors", false);
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
