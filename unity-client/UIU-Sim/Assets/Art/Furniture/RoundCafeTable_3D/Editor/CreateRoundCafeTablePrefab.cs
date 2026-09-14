#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CreateRoundCafeTablePrefab
{
    private const string Root = "Assets/Art/Furniture/RoundCafeTable_3D";
    private const string ModelPath = Root + "/Models/RoundCafeTable.obj";
    private const string TexturePath = Root + "/Textures/RoundTable_Wood_BaseColor.png";
    private const string MaterialsFolder = Root + "/Materials";
    private const string PrefabFolder = Root + "/Prefabs";
    private const string WoodMaterialPath = MaterialsFolder + "/M_RoundCafeTable_Wood.mat";
    private const string MetalMaterialPath = MaterialsFolder + "/M_RoundCafeTable_BlackMetal.mat";
    private const string PrefabPath = PrefabFolder + "/PF_RoundCafeTable_3D.prefab";

    [MenuItem("Tools/UIU Simulator/Assets/Create Round Cafe Table 3D Prefab")]
    public static void Create()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("Round table model not found at: " + ModelPath);
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
            new Color(0.69f, 0.57f, 0.43f, 1f),
            metallic: 0.0f,
            smoothness: 0.28f);

        var woodTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (woodTexture != null)
        {
            if (wood.HasProperty("_BaseMap"))
                wood.SetTexture("_BaseMap", woodTexture);
            else if (wood.HasProperty("_MainTex"))
                wood.SetTexture("_MainTex", woodTexture);
            EditorUtility.SetDirty(wood);
        }

        var blackMetal = GetOrCreateMaterial(
            MetalMaterialPath,
            shader,
            new Color(0.09f, 0.09f, 0.10f, 1f),
            metallic: 0.16f,
            smoothness: 0.24f);

        var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
        root.name = "PF_RoundCafeTable_3D";
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                if (n.Contains("black") || n.Contains("metal"))
                    mats[i] = blackMetal;
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

        Debug.Log("Created actual mesh-based round cafe table prefab:\n" + PrefabPath);
    }

    private static void AddColliders(Transform root)
    {
        Transform old = root.Find("Collision");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var collision = new GameObject("Collision");
        collision.transform.SetParent(root, false);

        var top = new GameObject("TopCollider");
        top.transform.SetParent(collision.transform, false);
        top.transform.localPosition = new Vector3(0f, 0.998f, 0f);
        var topBox = top.AddComponent<BoxCollider>();
        topBox.size = new Vector3(0.74f, 0.06f, 0.74f);

        var lower = new GameObject("LowerBodyCollider");
        lower.transform.SetParent(collision.transform, false);
        lower.transform.localPosition = new Vector3(0f, 0.50f, 0f);
        var lowerCapsule = lower.AddComponent<CapsuleCollider>();
        lowerCapsule.direction = 1;
        lowerCapsule.radius = 0.30f;
        lowerCapsule.height = 0.96f;
    }

    private static Material GetOrCreateMaterial(
        string path,
        Shader shader,
        Color color,
        float metallic,
        float smoothness)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
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
