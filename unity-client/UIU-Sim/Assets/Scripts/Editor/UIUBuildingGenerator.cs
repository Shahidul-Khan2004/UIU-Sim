using System;
using System.Collections.Generic;
using UIU.Simulator.Building.Data;
using UIU.Simulator.Building.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Building.Editor
{
    public sealed class UIUBuildingGeneratorWindow : EditorWindow
    {
        private FloorLayoutData layoutData;
        private Vector2 scrollPosition;

        [MenuItem("Tools/UIU Simulator/Building Generator")]
        public static void OpenWindow()
        {
            GetWindow<UIUBuildingGeneratorWindow>("UIU Building Generator");
        }

        private void OnEnable()
        {
            TryUseSelectedLayout();
        }

        private void OnSelectionChange()
        {
            if (TryUseSelectedLayout())
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("UIU Building Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates only the active floor scene. Layout coordinates remain in the selected FloorLayoutData asset.",
                MessageType.Info);

            layoutData = (FloorLayoutData)EditorGUILayout.ObjectField(
                "Floor Layout",
                layoutData,
                typeof(FloorLayoutData),
                false);

            FloorLayoutData sceneLayout = UIUFloorGenerator.FindGeneratedLayout(SceneManager.GetActiveScene());
            if (layoutData == null && sceneLayout != null)
            {
                layoutData = sceneLayout;
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(layoutData == null))
            {
                if (GUILayout.Button("Generate Floor", GUILayout.Height(32f)))
                {
                    RunGeneration();
                }

                if (GUILayout.Button("Regenerate Floor", GUILayout.Height(26f)))
                {
                    RunGeneration();
                }
            }

            using (new EditorGUI.DisabledScope(!UIUFloorGenerator.HasGeneratedRoot(SceneManager.GetActiveScene())))
            {
                if (GUILayout.Button("Clear Generated Objects", GUILayout.Height(26f)))
                {
                    UIUFloorGenerator.ClearGeneratedObjects(SceneManager.GetActiveScene(), true);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active Scene", SceneManager.GetActiveScene().path);
            EditorGUILayout.EndScrollView();
        }

        private bool TryUseSelectedLayout()
        {
            if (Selection.activeObject is not FloorLayoutData selectedLayout)
            {
                return false;
            }

            layoutData = selectedLayout;
            return true;
        }

        private void RunGeneration()
        {
            try
            {
                UIUFloorGenerator.GenerateFloor(layoutData, SceneManager.GetActiveScene(), true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("UIU Building Generator", exception.Message, "OK");
            }
        }
    }

    /// <summary>
    /// Converts FloorLayoutData into editable primitive GameObjects in one floor scene.
    /// </summary>
    public static class UIUFloorGenerator
    {
        public const string GeneratedRootName = "Generated";
        public const string GeneratorVersion = "1.0.0";

        private const string MaterialsFolder = "Assets/Materials/UIUBlockout";
        private const float DoorHeight = 2.2f;
        private const float RoomFloorOverlayThickness = 0.03f;
        private const float TargetStairRise = 0.2f;

        public static void GenerateFloor(FloorLayoutData layout, Scene scene, bool registerUndo)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                throw new InvalidOperationException("Save and activate a floor scene before generating it.");
            }

            List<string> validationErrors = layout.GetValidationErrors();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Floor layout '{layout.name}' is invalid:\n- " + string.Join("\n- ", validationErrors));
            }

            int undoGroup = -1;
            if (registerUndo)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"Generate {layout.FloorName}");
            }

            ClearGeneratedObjects(scene, registerUndo);
            BlockoutMaterials materials = LoadOrCreateMaterials();

            GameObject generatedRoot = CreateEmpty(GeneratedRootName, null, registerUndo);
            var metadata = generatedRoot.AddComponent<FloorGenerationMetadata>();
            metadata.Initialize(layout, GeneratorVersion);

            Transform floorsRoot = CreateEmpty("Floors", generatedRoot.transform, registerUndo).transform;
            Transform wallsRoot = CreateEmpty("Walls", generatedRoot.transform, registerUndo).transform;
            Transform roomsRoot = CreateEmpty("Rooms", generatedRoot.transform, registerUndo).transform;
            Transform doorsRoot = CreateEmpty("Doors", generatedRoot.transform, registerUndo).transform;
            Transform stairsRoot = CreateEmpty("Stairs", generatedRoot.transform, registerUndo).transform;
            Transform elevatorsRoot = CreateEmpty("Elevators", generatedRoot.transform, registerUndo).transform;

            GenerateFloorSlabs(layout, floorsRoot, materials, registerUndo);
            GenerateExteriorWalls(layout, wallsRoot, doorsRoot, materials, registerUndo);
            GenerateRooms(layout, wallsRoot, roomsRoot, doorsRoot, materials, registerUndo);
            GenerateElevators(layout, elevatorsRoot, doorsRoot, materials, registerUndo);
            GenerateStairs(layout, stairsRoot, materials, registerUndo);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = generatedRoot;
            EditorGUIUtility.PingObject(generatedRoot);

            if (registerUndo)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        public static bool ClearGeneratedObjects(Scene scene, bool registerUndo)
        {
            GameObject generatedRoot = FindGeneratedRoot(scene);
            if (generatedRoot == null)
            {
                return false;
            }

            if (registerUndo)
            {
                Undo.DestroyObjectImmediate(generatedRoot);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(generatedRoot);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        public static bool HasGeneratedRoot(Scene scene)
        {
            return FindGeneratedRoot(scene) != null;
        }

        public static FloorLayoutData FindGeneratedLayout(Scene scene)
        {
            GameObject root = FindGeneratedRoot(scene);
            return root == null ? null : root.GetComponent<FloorGenerationMetadata>()?.LayoutData;
        }

        private static GameObject FindGeneratedRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GeneratedRootName)
                {
                    return root;
                }
            }

            return null;
        }

        private static void GenerateFloorSlabs(
            FloorLayoutData layout,
            Transform floorsRoot,
            BlockoutMaterials materials,
            bool registerUndo)
        {
            float baseY = layout.BaseElevation;
            Vector3 entrance = layout.EntrancePosition;
            Vector2 footprint = layout.FootprintSize;
            Vector3 footprintCenter = new Vector3(
                entrance.x,
                baseY - layout.FloorSlabThickness * 0.5f,
                entrance.z + footprint.y * 0.5f);

            CreateCube(
                floorsRoot,
                "Building_Floor_Slab",
                footprintCenter,
                new Vector3(footprint.x, layout.FloorSlabThickness, footprint.y),
                materials.Floor,
                true,
                registerUndo);

            Vector2 approach = layout.EntranceApproachSize;
            CreateCube(
                floorsRoot,
                "Entrance_Approach",
                new Vector3(
                    entrance.x,
                    baseY - layout.FloorSlabThickness * 0.5f,
                    entrance.z - approach.y * 0.5f),
                new Vector3(approach.x, layout.FloorSlabThickness, approach.y),
                materials.Approach,
                true,
                registerUndo);

            foreach (ZoneDefinition zone in layout.Zones)
            {
                CreateCube(
                    floorsRoot,
                    $"Zone_{SafeName(zone.Name)}",
                    new Vector3(
                        zone.Position.x,
                        baseY + zone.Position.y + RoomFloorOverlayThickness * 0.5f,
                        zone.Position.z),
                    new Vector3(zone.Size.x, RoomFloorOverlayThickness, zone.Size.z),
                    materials.Zone,
                    true,
                    registerUndo);
            }
        }

        private static void GenerateExteriorWalls(
            FloorLayoutData layout,
            Transform wallsRoot,
            Transform doorsRoot,
            BlockoutMaterials materials,
            bool registerUndo)
        {
            Vector3 entrance = layout.EntrancePosition;
            Vector2 footprint = layout.FootprintSize;
            float baseY = layout.BaseElevation;
            float height = layout.CeilingHeight;
            float thickness = layout.WallThickness;
            float centerZ = entrance.z + footprint.y * 0.5f;
            float westX = entrance.x - footprint.x * 0.5f;
            float eastX = entrance.x + footprint.x * 0.5f;
            float northZ = entrance.z + footprint.y;

            CreateHorizontalWallWithDoor(
                wallsRoot,
                doorsRoot,
                "Exterior_South",
                "Main_Entrance",
                entrance.x,
                footprint.x,
                entrance.z,
                baseY,
                height,
                thickness,
                entrance.x,
                layout.EntranceWidth,
                materials,
                registerUndo);

            CreateCube(
                wallsRoot,
                "Exterior_North",
                new Vector3(entrance.x, baseY + height * 0.5f, northZ),
                new Vector3(footprint.x, height, thickness),
                materials.Wall,
                true,
                registerUndo);
            CreateCube(
                wallsRoot,
                "Exterior_West",
                new Vector3(westX, baseY + height * 0.5f, centerZ),
                new Vector3(thickness, height, footprint.y),
                materials.Wall,
                true,
                registerUndo);
            CreateCube(
                wallsRoot,
                "Exterior_East",
                new Vector3(eastX, baseY + height * 0.5f, centerZ),
                new Vector3(thickness, height, footprint.y),
                materials.Wall,
                true,
                registerUndo);
        }

        private static void GenerateRooms(
            FloorLayoutData layout,
            Transform wallsRoot,
            Transform roomsRoot,
            Transform doorsRoot,
            BlockoutMaterials materials,
            bool registerUndo)
        {
            foreach (RoomDefinition room in layout.Rooms)
            {
                float baseY = layout.BaseElevation + room.Position.y;
                string roomName = SafeName(room.Name);

                CreateCube(
                    roomsRoot,
                    $"Room_{roomName}",
                    new Vector3(
                        room.Position.x,
                        baseY + RoomFloorOverlayThickness * 0.5f,
                        room.Position.z),
                    new Vector3(room.Size.x, RoomFloorOverlayThickness, room.Size.z),
                    materials.Room,
                    true,
                    registerUndo);

                float halfWidth = room.Size.x * 0.5f;
                float halfDepth = room.Size.z * 0.5f;
                float wallHeight = room.Size.y;
                float thickness = layout.WallThickness;

                GenerateRoomHorizontalWall(
                    room,
                    DoorSide.North,
                    wallsRoot,
                    doorsRoot,
                    $"{roomName}_North",
                    room.Position.z + halfDepth,
                    baseY,
                    wallHeight,
                    thickness,
                    materials,
                    registerUndo);
                GenerateRoomHorizontalWall(
                    room,
                    DoorSide.South,
                    wallsRoot,
                    doorsRoot,
                    $"{roomName}_South",
                    room.Position.z - halfDepth,
                    baseY,
                    wallHeight,
                    thickness,
                    materials,
                    registerUndo);
                GenerateRoomVerticalWall(
                    room,
                    DoorSide.East,
                    wallsRoot,
                    doorsRoot,
                    $"{roomName}_East",
                    room.Position.x + halfWidth,
                    baseY,
                    wallHeight,
                    thickness,
                    materials,
                    registerUndo);
                GenerateRoomVerticalWall(
                    room,
                    DoorSide.West,
                    wallsRoot,
                    doorsRoot,
                    $"{roomName}_West",
                    room.Position.x - halfWidth,
                    baseY,
                    wallHeight,
                    thickness,
                    materials,
                    registerUndo);
            }
        }

        private static void GenerateRoomHorizontalWall(
            RoomDefinition room,
            DoorSide side,
            Transform wallsRoot,
            Transform doorsRoot,
            string name,
            float z,
            float baseY,
            float wallHeight,
            float thickness,
            BlockoutMaterials materials,
            bool registerUndo)
        {
            if (room.DoorSide == side)
            {
                CreateHorizontalWallWithDoor(
                    wallsRoot,
                    doorsRoot,
                    name,
                    $"{SafeName(room.Name)}_Door",
                    room.Position.x,
                    room.Size.x,
                    z,
                    baseY,
                    wallHeight,
                    thickness,
                    room.Position.x + room.DoorOffset,
                    room.DoorWidth,
                    materials,
                    registerUndo);
                return;
            }

            CreateCube(
                wallsRoot,
                name,
                new Vector3(room.Position.x, baseY + wallHeight * 0.5f, z),
                new Vector3(room.Size.x, wallHeight, thickness),
                materials.Wall,
                true,
                registerUndo);
        }

        private static void GenerateRoomVerticalWall(
            RoomDefinition room,
            DoorSide side,
            Transform wallsRoot,
            Transform doorsRoot,
            string name,
            float x,
            float baseY,
            float wallHeight,
            float thickness,
            BlockoutMaterials materials,
            bool registerUndo)
        {
            if (room.DoorSide == side)
            {
                CreateVerticalWallWithDoor(
                    wallsRoot,
                    doorsRoot,
                    name,
                    $"{SafeName(room.Name)}_Door",
                    x,
                    room.Position.z,
                    room.Size.z,
                    baseY,
                    wallHeight,
                    thickness,
                    room.Position.z + room.DoorOffset,
                    room.DoorWidth,
                    materials,
                    registerUndo);
                return;
            }

            CreateCube(
                wallsRoot,
                name,
                new Vector3(x, baseY + wallHeight * 0.5f, room.Position.z),
                new Vector3(thickness, wallHeight, room.Size.z),
                materials.Wall,
                true,
                registerUndo);
        }

        private static void GenerateElevators(
            FloorLayoutData layout,
            Transform elevatorsRoot,
            Transform doorsRoot,
            BlockoutMaterials materials,
            bool registerUndo)
        {
            foreach (VerticalConnectionDefinition elevator in layout.Elevators)
            {
                string connectionId = SafeName(elevator.ConnectionId);
                Transform elevatorRoot = CreateEmpty(
                    connectionId,
                    elevatorsRoot,
                    registerUndo).transform;
                float baseY = layout.BaseElevation + elevator.Position.y;
                float halfWidth = elevator.Size.x * 0.5f;
                float halfDepth = elevator.Size.z * 0.5f;
                float thickness = layout.WallThickness;

                CreateCube(
                    elevatorRoot,
                    "Floor",
                    new Vector3(elevator.Position.x, baseY + 0.025f, elevator.Position.z),
                    new Vector3(elevator.Size.x, 0.05f, elevator.Size.z),
                    materials.Elevator,
                    true,
                    registerUndo);
                CreateCube(
                    elevatorRoot,
                    "North_Wall",
                    new Vector3(
                        elevator.Position.x,
                        baseY + elevator.Size.y * 0.5f,
                        elevator.Position.z + halfDepth),
                    new Vector3(elevator.Size.x, elevator.Size.y, thickness),
                    materials.Elevator,
                    true,
                    registerUndo);
                CreateCube(
                    elevatorRoot,
                    "West_Wall",
                    new Vector3(
                        elevator.Position.x - halfWidth,
                        baseY + elevator.Size.y * 0.5f,
                        elevator.Position.z),
                    new Vector3(thickness, elevator.Size.y, elevator.Size.z),
                    materials.Elevator,
                    true,
                    registerUndo);
                CreateCube(
                    elevatorRoot,
                    "East_Wall",
                    new Vector3(
                        elevator.Position.x + halfWidth,
                        baseY + elevator.Size.y * 0.5f,
                        elevator.Position.z),
                    new Vector3(thickness, elevator.Size.y, elevator.Size.z),
                    materials.Elevator,
                    true,
                    registerUndo);

                float doorWidth = Mathf.Min(1.5f, elevator.Size.x - thickness * 2f);
                CreateHorizontalWallWithDoor(
                    elevatorRoot,
                    doorsRoot,
                    "South_Wall",
                    $"{connectionId}_Door",
                    elevator.Position.x,
                    elevator.Size.x,
                    elevator.Position.z - halfDepth,
                    baseY,
                    elevator.Size.y,
                    thickness,
                    elevator.Position.x,
                    doorWidth,
                    materials,
                    registerUndo);
            }
        }

        private static void GenerateStairs(
            FloorLayoutData layout,
            Transform stairsRoot,
            BlockoutMaterials materials,
            bool registerUndo)
        {
            foreach (VerticalConnectionDefinition stair in layout.Stairs)
            {
                Transform stairRoot = CreateEmpty(
                    SafeName(stair.ConnectionId),
                    stairsRoot,
                    registerUndo).transform;
                float baseY = layout.BaseElevation + stair.Position.y;
                int stepCount = Mathf.Clamp(Mathf.CeilToInt(stair.Size.y / TargetStairRise), 1, 32);
                float stepRise = stair.Size.y / stepCount;
                float stepDepth = stair.Size.z / stepCount;
                float southZ = stair.Position.z - stair.Size.z * 0.5f;

                for (int index = 0; index < stepCount; index++)
                {
                    float stepHeight = stepRise * (index + 1);
                    CreateCube(
                        stairRoot,
                        $"Step_{index + 1:00}",
                        new Vector3(
                            stair.Position.x,
                            baseY + stepHeight * 0.5f,
                            southZ + stepDepth * (index + 0.5f)),
                        new Vector3(stair.Size.x, stepHeight, stepDepth),
                        materials.Stair,
                        true,
                        registerUndo);
                }
            }
        }

        private static void CreateHorizontalWallWithDoor(
            Transform wallsRoot,
            Transform doorsRoot,
            string wallName,
            string doorName,
            float centerX,
            float length,
            float z,
            float baseY,
            float height,
            float thickness,
            float openingCenterX,
            float openingWidth,
            BlockoutMaterials materials,
            bool registerUndo)
        {
            IReadOnlyList<WallSegment> segments = FloorGeometry.CreateHorizontalWallWithOpening(
                centerX,
                length,
                z,
                baseY,
                height,
                thickness,
                openingCenterX,
                openingWidth);

            for (int index = 0; index < segments.Count; index++)
            {
                WallSegment segment = segments[index];
                CreateCube(
                    wallsRoot,
                    $"{wallName}_{index + 1}",
                    segment.Center,
                    segment.Size,
                    materials.Wall,
                    true,
                    registerUndo);
            }

            CreateDoorHeader(
                doorsRoot,
                doorName,
                new Vector3(openingCenterX, 0f, z),
                openingWidth,
                height,
                thickness,
                true,
                baseY,
                materials.Door,
                registerUndo);
        }

        private static void CreateVerticalWallWithDoor(
            Transform wallsRoot,
            Transform doorsRoot,
            string wallName,
            string doorName,
            float x,
            float centerZ,
            float length,
            float baseY,
            float height,
            float thickness,
            float openingCenterZ,
            float openingWidth,
            BlockoutMaterials materials,
            bool registerUndo)
        {
            IReadOnlyList<WallSegment> segments = FloorGeometry.CreateVerticalWallWithOpening(
                x,
                centerZ,
                length,
                baseY,
                height,
                thickness,
                openingCenterZ,
                openingWidth);

            for (int index = 0; index < segments.Count; index++)
            {
                WallSegment segment = segments[index];
                CreateCube(
                    wallsRoot,
                    $"{wallName}_{index + 1}",
                    segment.Center,
                    segment.Size,
                    materials.Wall,
                    true,
                    registerUndo);
            }

            CreateDoorHeader(
                doorsRoot,
                doorName,
                new Vector3(x, 0f, openingCenterZ),
                openingWidth,
                height,
                thickness,
                false,
                baseY,
                materials.Door,
                registerUndo);
        }

        private static void CreateDoorHeader(
            Transform doorsRoot,
            string name,
            Vector3 horizontalPosition,
            float openingWidth,
            float wallHeight,
            float wallThickness,
            bool runsAlongX,
            float baseY,
            Material material,
            bool registerUndo)
        {
            float usableDoorHeight = Mathf.Min(DoorHeight, wallHeight);
            float headerHeight = wallHeight - usableDoorHeight;
            if (headerHeight <= Mathf.Epsilon)
            {
                return;
            }

            Vector3 size = runsAlongX
                ? new Vector3(openingWidth, headerHeight, wallThickness)
                : new Vector3(wallThickness, headerHeight, openingWidth);
            horizontalPosition.y = baseY + usableDoorHeight + headerHeight * 0.5f;
            CreateCube(
                doorsRoot,
                name,
                horizontalPosition,
                size,
                material,
                true,
                registerUndo);
        }

        private static GameObject CreateEmpty(string objectName, Transform parent, bool registerUndo)
        {
            var gameObject = new GameObject(objectName);
            if (registerUndo)
            {
                Undo.RegisterCreatedObjectUndo(gameObject, $"Create {objectName}");
            }

            if (parent != null)
            {
                if (registerUndo)
                {
                    Undo.SetTransformParent(gameObject.transform, parent, $"Parent {objectName}");
                }
                else
                {
                    gameObject.transform.SetParent(parent, false);
                }
            }

            return gameObject;
        }

        private static GameObject CreateCube(
            Transform parent,
            string objectName,
            Vector3 position,
            Vector3 size,
            Material material,
            bool includeCollider,
            bool registerUndo)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = objectName;
            if (registerUndo)
            {
                Undo.RegisterCreatedObjectUndo(piece, $"Create {objectName}");
                Undo.SetTransformParent(piece.transform, parent, $"Parent {objectName}");
            }
            else
            {
                piece.transform.SetParent(parent, false);
            }

            piece.transform.position = position;
            piece.transform.rotation = Quaternion.identity;
            piece.transform.localScale = size;
            GameObjectUtility.SetStaticEditorFlags(piece, StaticEditorFlags.BatchingStatic);

            MeshRenderer renderer = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            if (!includeCollider)
            {
                BoxCollider collider = piece.GetComponent<BoxCollider>();
                if (registerUndo)
                {
                    Undo.DestroyObjectImmediate(collider);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            return piece;
        }

        private static BlockoutMaterials LoadOrCreateMaterials()
        {
            EnsureAssetFolder(MaterialsFolder);
            var materials = new BlockoutMaterials(
                GetOrCreateMaterial("Floor", new Color(0.30f, 0.32f, 0.35f)),
                GetOrCreateMaterial("Approach", new Color(0.24f, 0.26f, 0.29f)),
                GetOrCreateMaterial("Wall", new Color(0.74f, 0.76f, 0.78f)),
                GetOrCreateMaterial("Zone", new Color(0.34f, 0.52f, 0.43f)),
                GetOrCreateMaterial("Room", new Color(0.26f, 0.44f, 0.62f)),
                GetOrCreateMaterial("Door", new Color(0.86f, 0.52f, 0.18f)),
                GetOrCreateMaterial("Stair", new Color(0.70f, 0.60f, 0.28f)),
                GetOrCreateMaterial("Elevator", new Color(0.32f, 0.58f, 0.62f)));
            AssetDatabase.SaveAssets();
            return materials;
        }

        private static Material GetOrCreateMaterial(string materialName, Color color)
        {
            string path = $"{MaterialsFolder}/{materialName}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("The URP Lit shader is unavailable.");
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] segments = folderPath.Split('/');
            string currentPath = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string nextPath = $"{currentPath}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "Unnamed"
                : value.Replace('/', '_').Replace('\\', '_');
        }

        private readonly struct BlockoutMaterials
        {
            public BlockoutMaterials(
                Material floor,
                Material approach,
                Material wall,
                Material zone,
                Material room,
                Material door,
                Material stair,
                Material elevator)
            {
                Floor = floor;
                Approach = approach;
                Wall = wall;
                Zone = zone;
                Room = room;
                Door = door;
                Stair = stair;
                Elevator = elevator;
            }

            public Material Floor { get; }
            public Material Approach { get; }
            public Material Wall { get; }
            public Material Zone { get; }
            public Material Room { get; }
            public Material Door { get; }
            public Material Stair { get; }
            public Material Elevator { get; }
        }
    }
}
