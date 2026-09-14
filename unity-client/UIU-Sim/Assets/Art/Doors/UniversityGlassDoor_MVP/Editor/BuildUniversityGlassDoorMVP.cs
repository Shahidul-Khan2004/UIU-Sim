#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Texture-free, metre-scale glass doorway. Rebuilding preserves asset GUIDs.</summary>
public static class BuildUniversityGlassDoorMVP
{
    public const string AssetRoot = "Assets/Art/Doors/UniversityGlassDoor_MVP";
    public const string PrefabPath = AssetRoot + "/Prefabs/PF_UniversityGlassDoor_MVP.prefab";

    // Root is centred across the opening, at floor level. Front faces local -Z.
    public const float OverallWidth = 1.10f;
    public const float OverallHeight = 2.65f;
    public const float FrameDepth = 0.09f;
    public const float OverallDepth = 0.134f; // Includes the pulls on both sides.
    public const float WallOpeningWidth = 1.12f;
    public const float WallOpeningHeight = 2.66f;

    private const float FrameWidth = 0.035f;
    private const float LeafWidth = OverallWidth - 2f * FrameWidth - 0.01f;
    private const float LeafHeight = 2.10f;
    private const float LeafBottom = 0.01f;
    private const float LeafDepth = 0.02f;
    private const float RailWidth = 0.014f;
    private const float GlassDepth = 0.01f;
    private const float DividerBottom = LeafBottom + LeafHeight + 0.005f;
    private const float HandleOffset = 0.056f;

    [MenuItem("Tools/UIU Simulator/Build University Glass Door MVP")]
    public static void BuildDoor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("University Glass Door MVP: exit Play Mode before building assets.");
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("University Glass Door MVP requires Universal Render Pipeline/Lit.");

        EnsureFolder(AssetRoot + "/Editor");
        EnsureFolder(AssetRoot + "/Materials");
        EnsureFolder(AssetRoot + "/Prefabs");

        Material frame = CreateMaterial("MAT_Frame_Aluminum", shader,
            new Color(0.115f, 0.12f, 0.125f, 1f), 0.55f, 0.62f);
        Material glass = CreateMaterial("MAT_Glass_Clear", shader,
            new Color(0.97f, 0.975f, 0.98f, 0.07f), 0f, 0.94f, true, true);
        Material handle = CreateMaterial("MAT_Handle_Metal", shader,
            new Color(0.72f, 0.73f, 0.74f, 1f), 0.88f, 0.57f);
        Material stripe = CreateMaterial("MAT_Door_Stripe", shader,
            new Color(0.80f, 0.16f, 0.035f, 1f), 0f, 0.32f);
        Material marking = CreateMaterial("MAT_Door_FrostedMarking", shader,
            new Color(0.95f, 0.95f, 0.93f, 0.28f), 0f, 0.18f, true, false, 1);

        // Temporary geometry lives in a preview scene, including on batch-mode builds.
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject prefab;
        try
        {
            GameObject root = new GameObject("PF_UniversityGlassDoor_MVP");
            SceneManager.MoveGameObjectToScene(root, preview);
            BuildGeometry(root.transform, frame, glass, handle, stripe, marking);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success || prefab == null)
                throw new InvalidOperationException("Could not save glass door prefab at " + PrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        bool exampleCreated = TryCreateSceneExample(prefab);
        if (!Application.isBatchMode && !exampleCreated)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        Debug.Log("University Glass Door MVP created/updated: " + PrefabPath +
            "\nOverall: 1.100 x 2.650 x 0.134 m (W x H x D, including handles); frame depth 0.090 m." +
            "\nRecommended wall opening: 1.120 x 2.660 m; bottom at floor level." +
            (exampleCreated ? "\nAdded one scene example; Undo removes it. The scene has not been saved."
                : "\nDrag the prefab into the scene and place its root on the floor. Rotate DoorPivot around local Y to open."),
            prefab);
    }

    private static void BuildGeometry(Transform root, Material frame, Material glass,
        Material metal, Material stripe, Material marking)
    {
        float innerWidth = OverallWidth - 2f * FrameWidth;
        Transform surround = CreateGroup(root, "Frame");
        CreateCube(surround, "Frame_Left", new Vector3(-(OverallWidth - FrameWidth) / 2f, OverallHeight / 2f, 0f),
            new Vector3(FrameWidth, OverallHeight, FrameDepth), frame, true);
        CreateCube(surround, "Frame_Right", new Vector3((OverallWidth - FrameWidth) / 2f, OverallHeight / 2f, 0f),
            new Vector3(FrameWidth, OverallHeight, FrameDepth), frame, true);
        CreateCube(surround, "Frame_Top", new Vector3(0f, OverallHeight - FrameWidth / 2f, 0f),
            new Vector3(innerWidth, FrameWidth, FrameDepth), frame, true);
        CreateCube(surround, "Frame_TransomDivider", new Vector3(0f, DividerBottom + FrameWidth / 2f, 0f),
            new Vector3(innerWidth, FrameWidth, FrameDepth), frame, true);

        float transomBottom = DividerBottom + FrameWidth;
        float transomTop = OverallHeight - FrameWidth;
        CreateCube(root, "TransomGlass", new Vector3(0f, (transomBottom + transomTop) / 2f, 0f),
            new Vector3(innerWidth, transomTop - transomBottom, GlassDepth), glass, true);

        Transform pivot = CreateGroup(root, "DoorPivot");
        // Hinge axis lies on the front-left edge: +Y rotation opens toward -Z.
        // Offsetting the axis by half the rail depth prevents the hinge corner
        // clipping the jamb at 90 degrees while retaining a 5 mm side gap.
        pivot.localPosition = new Vector3(-LeafWidth / 2f, 0f, -LeafDepth / 2f);
        Transform leaf = CreateGroup(pivot, "DoorLeaf");
        leaf.localPosition = new Vector3(0f, 0f, LeafDepth / 2f);
        Vector3 leafCentre = new Vector3(LeafWidth / 2f, LeafBottom + LeafHeight / 2f, 0f);
        BoxCollider panelCollider = leaf.gameObject.AddComponent<BoxCollider>();
        panelCollider.center = leafCentre;
        panelCollider.size = new Vector3(LeafWidth, LeafHeight, LeafDepth);

        CreateCube(leaf, "Glass", leafCentre,
            new Vector3(LeafWidth - 2f * RailWidth, LeafHeight - 2f * RailWidth, GlassDepth), glass);
        CreateCube(leaf, "Rail_Left", new Vector3(RailWidth / 2f, leafCentre.y, 0f),
            new Vector3(RailWidth, LeafHeight, LeafDepth), frame);
        CreateCube(leaf, "Rail_Right", new Vector3(LeafWidth - RailWidth / 2f, leafCentre.y, 0f),
            new Vector3(RailWidth, LeafHeight, LeafDepth), frame);
        CreateCube(leaf, "Rail_Top", new Vector3(leafCentre.x, LeafBottom + LeafHeight - RailWidth / 2f, 0f),
            new Vector3(LeafWidth - 2f * RailWidth, RailWidth, LeafDepth), frame);
        CreateCube(leaf, "Rail_Bottom", new Vector3(leafCentre.x, LeafBottom + RailWidth / 2f, 0f),
            new Vector3(LeafWidth - 2f * RailWidth, RailWidth, LeafDepth), frame);

        Transform handles = CreateGroup(leaf, "Handle");
        CreatePull(handles, "Front", -1f, metal);
        CreatePull(handles, "Back", 1f, metal);

        // Thin closed cubes expose one outward-facing surface from either side.
        // Glass faces are at +/-5 mm, band at +/-6.5 mm, stripe at +/-8.5 mm.
        // This avoids coincident surfaces and doubled alpha from double-sided cubes.
        Transform markings = CreateGroup(leaf, "Markings");
        float markingWidth = LeafWidth - 2f * RailWidth - 0.008f;
        CreateCube(markings, "FrostedBand", new Vector3(leafCentre.x, 1.015f, 0f),
            new Vector3(markingWidth, 0.10f, 0.013f), marking);
        CreateCube(markings, "OrangeStripe", new Vector3(leafCentre.x, 1.05f, 0f),
            new Vector3(markingWidth, 0.014f, 0.017f), stripe);
    }

    private static void CreatePull(Transform parent, string side, float sign, Material material)
    {
        Transform pull = CreateGroup(parent, side);
        float x = LeafWidth - 0.10f;
        CreateCylinder(pull, side + "_PullBar", new Vector3(x, 1.05f, sign * HandleOffset),
            0.022f, 0.50f, Quaternion.identity, material);
        float mountLength = HandleOffset - GlassDepth / 2f;
        float mountZ = sign * (HandleOffset + GlassDepth / 2f) / 2f;
        CreateCylinder(pull, side + "_Mount_Lower", new Vector3(x, 0.85f, mountZ),
            0.014f, mountLength, Quaternion.Euler(90f, 0f, 0f), material);
        CreateCylinder(pull, side + "_Mount_Upper", new Vector3(x, 1.25f, mountZ),
            0.014f, mountLength, Quaternion.Euler(90f, 0f, 0f), material);
    }

    private static Transform CreateGroup(Transform parent, string name)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    private static void CreateCube(Transform parent, string name, Vector3 position,
        Vector3 size, Material material, bool keepCollider = false)
    {
        CreatePrimitive(parent, name, PrimitiveType.Cube, position, size, Quaternion.identity, material, keepCollider);
    }

    private static void CreateCylinder(Transform parent, string name, Vector3 position,
        float diameter, float length, Quaternion rotation, Material material)
    {
        // Unity's cylinder has unit diameter and a height of two units.
        CreatePrimitive(parent, name, PrimitiveType.Cylinder, position,
            new Vector3(diameter, length / 2f, diameter), rotation, material, false);
    }

    private static void CreatePrimitive(Transform parent, string name, PrimitiveType type,
        Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool keepCollider)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;
        part.transform.localScale = scale;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        if (material.GetFloat("_Surface") > 0f)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        if (!keepCollider)
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
    }

    private static Material CreateMaterial(string name, Shader shader, Color colour,
        float metallic, float smoothness, bool transparent = false, bool preserveSpecular = false, int queueOffset = 0)
    {
        // Reset generated properties on every run without replacing existing asset GUIDs.
        Material settings = new Material(shader) { name = name };
        try
        {
            settings.SetColor("_BaseColor", colour);
            settings.SetColor("_Color", settings.GetColor("_BaseColor"));
            settings.SetFloat("_Metallic", metallic);
            settings.SetFloat("_Smoothness", smoothness);
            settings.SetFloat("_Surface", transparent ? 1f : 0f);
            settings.SetFloat("_Blend", 0f); // Alpha, with URP's optional preserve-specular path.
            settings.SetFloat("_BlendModePreserveSpecular", preserveSpecular ? 1f : 0f);
            settings.SetFloat("_AlphaClip", 0f);
            settings.SetFloat("_AlphaToMask", 0f);
            // Closed primitive cubes are visible from both sides with back-face culling.
            settings.SetFloat("_Cull", (float)CullMode.Back);
            settings.SetFloat("_ZWrite", transparent ? 0f : 1f);
            settings.SetFloat("_SrcBlend", (float)(transparent && !preserveSpecular ? BlendMode.SrcAlpha : BlendMode.One));
            settings.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            settings.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            settings.SetFloat("_DstBlendAlpha", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            settings.SetFloat("_ReceiveShadows", transparent ? 0f : 1f);
            settings.SetFloat("_QueueOffset", queueOffset);
            settings.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            settings.renderQueue = (transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry) + queueOffset;
            settings.SetShaderPassEnabled("ShadowCaster", !transparent);
            settings.SetShaderPassEnabled("DepthOnly", !transparent);
            // Stock URP Lit disables this pass when no precomputed velocity is used.
            settings.SetShaderPassEnabled("MotionVectors", false);
            if (transparent)
            {
                settings.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                settings.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                if (preserveSpecular)
                    settings.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            string path = AssetRoot + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(settings) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
                material.CopyPropertiesFromMaterial(settings);
            }
            // Pass overrides are explicit on the saved material as well as the
            // template; repeated builds also repair Inspector changes to them.
            material.name = name;
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            material.SetShaderPassEnabled("DepthOnly", !transparent);
            material.SetShaderPassEnabled("MotionVectors", false);
            EditorUtility.SetDirty(material);
            return material;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }

    private static bool TryCreateSceneExample(GameObject prefab)
    {
        if (Application.isBatchMode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            return false;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.isDirty || EditorSceneManager.IsPreviewScene(scene))
            return false;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) == PrefabPath)
                    return false;
            }
        }

        GameObject example = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(example, "Create University Glass Door MVP example");
        Selection.activeGameObject = example;
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }
}
#endif
