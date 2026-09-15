#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CreateSimpleBenchPrefab
{
    private const string Root = "Assets/Art/Furniture/SimpleBench_3D";
    private const string ModelPath = Root + "/Models/SimpleBench.obj";
    private const string MaterialsFolder = Root + "/Materials";
    private const string PrefabsFolder = Root + "/Prefabs";

    private const string GreyMaterialPath =
        MaterialsFolder + "/M_SimpleBench_GreyMetal.mat";
    private const string OrangeMaterialPath =
        MaterialsFolder + "/M_SimpleBench_OrangeFrame.mat";

    private const string PrefabPath =
        PrefabsFolder + "/PF_SimpleBench_3D.prefab";

    [MenuItem("Tools/UIU Simulator/Assets/Create Simple Bench 3D Prefab")]
    public static void Create()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("Simple bench model not found at: " + ModelPath);
            return;
        }

        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsFolder);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError("Could not find URP/Lit or Standard shader.");
            return;
        }

        Material grey = GetOrCreateMaterial(
            GreyMaterialPath,
            shader,
            new Color(0.54f, 0.57f, 0.59f, 1f),
            metallic: 0.12f,
            smoothness: 0.28f);

        Material orange = GetOrCreateMaterial(
            OrangeMaterialPath,
            shader,
            new Color(0.70f, 0.28f, 0.09f, 1f),
            metallic: 0.03f,
            smoothness: 0.24f);

        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(model);
        root.name = "PF_SimpleBench_3D";
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                string materialName =
                    materials[i] != null
                        ? materials[i].name.ToLowerInvariant()
                        : "";

                if (materialName.Contains("orange"))
                    materials[i] = orange;
                else
                    materials[i] = grey;
            }

            renderer.sharedMaterials = materials;
        }

        AddSimpleColliders(root.transform);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log(
            "Created Simple Bench 3D prefab:\n" +
            PrefabPath +
            "\nThe OBJ contains the actual geometry. " +
            "This helper only assigns materials, colliders, and saves the prefab.");
    }

    private static void AddSimpleColliders(Transform root)
    {
        Transform oldCollision = root.Find("Collision");
        if (oldCollision != null)
            Object.DestroyImmediate(oldCollision.gameObject);

        GameObject collision = new GameObject("Collision");
        collision.transform.SetParent(root, false);

        GameObject seat = new GameObject("SeatCollider");
        seat.transform.SetParent(collision.transform, false);
        seat.transform.localPosition = new Vector3(0f, 0.45f, 0f);

        BoxCollider seatCollider = seat.AddComponent<BoxCollider>();
        seatCollider.size = new Vector3(1.34f, 0.08f, 0.54f);

        GameObject back = new GameObject("BackCollider");
        back.transform.SetParent(collision.transform, false);
        back.transform.localPosition = new Vector3(0f, 0.69f, -0.22f);

        BoxCollider backCollider = back.AddComponent<BoxCollider>();
        backCollider.size = new Vector3(1.34f, 0.25f, 0.08f);

        GameObject body = new GameObject("BodyCollider");
        body.transform.SetParent(collision.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.38f, 0f);

        BoxCollider bodyCollider = body.AddComponent<BoxCollider>();
        bodyCollider.size = new Vector3(1.50f, 0.76f, 0.58f);
    }

    private static Material GetOrCreateMaterial(
        string path,
        Shader shader,
        Color color,
        float metallic,
        float smoothness)
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(path);

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
