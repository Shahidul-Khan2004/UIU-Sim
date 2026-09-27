#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// This MUST live inside a folder named "Editor" anywhere under Assets.
// Run it once from the Tools menu to generate Assets/Prefabs/Whiteboard.prefab.
public static class WhiteboardPrefabCreator
{
    [MenuItem("Tools/Classroom Props/Create Whiteboard Prefab")]
    public static void CreateWhiteboardPrefab()
    {
        GameObject root = new GameObject("Whiteboard");
        WhiteboardResize resize = root.AddComponent<WhiteboardResize>();
        resize.Rebuild();

        AssignMaterials(root);

        EnsureFolder("Assets/Prefabs");

        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Prefabs/Whiteboard.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);

        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = prefab;

        Debug.Log("Whiteboard prefab created at: " + path);
    }

    static void AssignMaterials(GameObject root)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        Material white = new Material(shader) { name = "Whiteboard_Surface" };
        SetColor(white, Color.white);

        Material dark = new Material(shader) { name = "Whiteboard_Frame" };
        SetColor(dark, new Color(0.13f, 0.13f, 0.13f));

        Apply(root, "Board Surface", white);
        Apply(root, "Frame Top", dark);
        Apply(root, "Frame Bottom", dark);
        Apply(root, "Frame Left", dark);
        Apply(root, "Frame Right", dark);
        Apply(root, "Marker Tray", dark);

        EnsureFolder("Assets/Prefabs/Materials");
        SaveMaterial(white, "Assets/Prefabs/Materials");
        SaveMaterial(dark, "Assets/Prefabs/Materials");
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); // URP
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);         // Built-in/Standard
    }

    static void Apply(GameObject root, string childName, Material mat)
    {
        Transform t = root.transform.Find(childName);
        if (t == null) return;
        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;
    }

    static void SaveMaterial(Material mat, string folder)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + mat.name + ".mat");
        AssetDatabase.CreateAsset(mat, path);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = System.IO.Path.GetDirectoryName(folder).Replace("\\", "/");
        string newFolderName = System.IO.Path.GetFileName(folder);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, newFolderName);
    }
}
#endif
