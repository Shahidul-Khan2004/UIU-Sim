#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CreateWoodSlottedCafeChairPrefab
{
    private const string Root =
        "Assets/Art/Furniture/WoodSlottedCafeChair_3D";
    private const string ModelPath =
        Root + "/Models/WoodSlottedCafeChair.obj";
    private const string TexturePath =
        Root + "/Textures/Wood_BaseColor.png";
    private const string MaterialsFolder =
        Root + "/Materials";
    private const string PrefabFolder =
        Root + "/Prefabs";
    private const string WoodMaterialPath =
        MaterialsFolder + "/M_WoodSlottedChair_Wood.mat";
    private const string ChromeMaterialPath =
        MaterialsFolder + "/M_WoodSlottedChair_Chrome.mat";
    private const string PrefabPath =
        PrefabFolder + "/PF_WoodSlottedCafeChair_3D.prefab";

    [MenuItem("Tools/UIU Simulator/Assets/Create Wood Slotted Cafe Chair 3D Prefab")]
    public static void Create()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("Chair model not found at: " + ModelPath);
            return;
        }

        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabFolder);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError("Could not find URP/Lit or Standard shader.");
            return;
        }

        var wood = GetOrCreateMaterial(
            WoodMaterialPath,
            shader,
            new Color(0.72f, 0.43f, 0.14f, 1f),
            metallic: 0.0f,
            smoothness: 0.34f);

        var woodTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (woodTexture != null)
        {
            if (wood.HasProperty("_BaseMap"))
                wood.SetTexture("_BaseMap", woodTexture);
            else if (wood.HasProperty("_MainTex"))
                wood.SetTexture("_MainTex", woodTexture);
            EditorUtility.SetDirty(wood);
        }

        var chrome = GetOrCreateMaterial(
            ChromeMaterialPath,
            shader,
            new Color(0.72f, 0.74f, 0.78f, 1f),
            metallic: 0.92f,
            smoothness: 0.82f);

        var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
        root.name = "PF_WoodSlottedCafeChair_3D";
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;

            for (int i = 0; i < mats.Length; i++)
            {
                string materialName = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";

                if (materialName.Contains("chrome") || materialName.Contains("metal"))
                    mats[i] = chrome;
                else
                    mats[i] = wood;
            }

            renderer.sharedMaterials = mats;
        }

        AddColliders(root.transform);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log(
            "Created actual mesh-based chair prefab:\n" +
            PrefabPath +
            "\nThe script only creates materials/colliders/prefab. " +
            "The geometry comes from WoodSlottedCafeChair.obj.");
    }

    private static void AddColliders(Transform root)
    {
        Transform old = root.Find("Collision");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var collision = new GameObject("Collision");
        collision.transform.SetParent(root, false);

        var seat = new GameObject("SeatCollider");
        seat.transform.SetParent(collision.transform, false);
        seat.transform.localPosition = new Vector3(0f, 0.47f, 0.03f);
        seat.transform.localRotation = Quaternion.Euler(-3f, 0f, 0f);
        var seatBox = seat.AddComponent<BoxCollider>();
        seatBox.size = new Vector3(0.50f, 0.08f, 0.46f);

        var back = new GameObject("BackCollider");
        back.transform.SetParent(collision.transform, false);
        back.transform.localPosition = new Vector3(0f, 0.82f, -0.21f);
        back.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        var backBox = back.AddComponent<BoxCollider>();
        backBox.size = new Vector3(0.46f, 0.36f, 0.05f);
    }

    private static Material GetOrCreateMaterial(
        string path,
        Shader shader,
        Color color,
        float metallic,
        float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
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

        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string child = path.Substring(slash + 1);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
