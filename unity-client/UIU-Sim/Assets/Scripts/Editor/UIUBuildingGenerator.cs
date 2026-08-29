using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the base modular shell for the UIU building prototype.
/// This class lives in an Editor folder, so it is compiled for the Unity Editor
/// only and never included in a player build.
/// </summary>
public static class UIUBuildingGenerator
{
    private const string BuildingName = "UIU_Building";
    private const string MaterialsFolder = "Assets/Materials";
    private const string ConcreteWallPath = MaterialsFolder + "/ConcreteWall.mat";
    private const string FloorMaterialPath = MaterialsFolder + "/FloorMaterial.mat";

    // MenuItem exposes this editor command under Tools > Generate UIU Building Prototype.
    [MenuItem("Tools/Generate UIU Building Prototype")]
    private static void GenerateBuilding()
    {
        GameObject building = FindExistingBuilding();
        if (building == null)
        {
            // GameObject creation: make one empty parent to keep all generated pieces organized.
            building = new GameObject(BuildingName);
            Undo.RegisterCreatedObjectUndo(building, "Create UIU Building");
        }

        Material concreteWall = GetOrCreateMaterial(
            ConcreteWallPath,
            "ConcreteWall",
            new Color(0.52f, 0.52f, 0.50f));
        Material floorMaterial = GetOrCreateMaterial(
            FloorMaterialPath,
            "FloorMaterial",
            new Color(0.34f, 0.34f, 0.36f));

        CreateCubeIfMissing(building.transform, "GroundFloor", new Vector3(0f, 0f, 0f), new Vector3(40f, 0.2f, 20f), floorMaterial);
        CreateCubeIfMissing(building.transform, "FirstFloor", new Vector3(0f, 4f, 0f), new Vector3(40f, 0.2f, 20f), floorMaterial);
        CreateCubeIfMissing(building.transform, "SecondFloor", new Vector3(0f, 8f, 0f), new Vector3(40f, 0.2f, 20f), floorMaterial);

        CreateCubeIfMissing(building.transform, "FrontWall", new Vector3(0f, 2f, 10f), new Vector3(40f, 4f, 0.2f), concreteWall);
        CreateCubeIfMissing(building.transform, "BackWall", new Vector3(0f, 2f, -10f), new Vector3(40f, 4f, 0.2f), concreteWall);
        CreateCubeIfMissing(building.transform, "LeftWall", new Vector3(-20f, 2f, 0f), new Vector3(0.2f, 4f, 20f), concreteWall);
        CreateCubeIfMissing(building.transform, "RightWall", new Vector3(20f, 2f, 0f), new Vector3(0.2f, 4f, 20f), concreteWall);

        Selection.activeGameObject = building;
        EditorGUIUtility.PingObject(building);
    }

    private static GameObject FindExistingBuilding()
    {
        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform.parent == null && transform.gameObject.scene.IsValid() && transform.name == BuildingName)
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private static void CreateCubeIfMissing(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        Transform existingChild = parent.Find(objectName);
        if (existingChild != null)
        {
            // Existing prototype pieces are preserved and never recreated or overwritten.
            return;
        }

        // GameObject.CreatePrimitive creates a cube with both MeshRenderer and BoxCollider.
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = objectName;
        Undo.RegisterCreatedObjectUndo(piece, "Create " + objectName);
        Undo.SetTransformParent(piece.transform, parent, "Parent " + objectName);

        // Transform assignment sets the requested local placement and dimensions beneath UIU_Building.
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = localScale;

        MeshRenderer meshRenderer = piece.GetComponent<MeshRenderer>();
        Undo.RecordObject(meshRenderer, "Assign " + objectName + " Material");
        meshRenderer.sharedMaterial = material;
    }

    private static Material GetOrCreateMaterial(string assetPath, string materialName, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader)
        {
            name = materialName,
            color = color
        };
        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }
}
