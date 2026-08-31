using System.Linq;
using NUnit.Framework;
using UIU.Simulator.Building.Data;
using UIU.Simulator.Building.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Building.Tests.Editor
{
    public sealed class FloorGenerationTests
    {
        private const string LayoutPath = "Assets/Data/Floors/GroundFloorLayout.asset";
        private const string GroundScenePath = "Assets/Scenes/Floors/GroundFloor.unity";
        private const string MainScenePath = "Assets/Scenes/Main/UIU_Main.unity";

        private SceneSetup[] originalSceneSetup;

        [SetUp]
        public void RecordSceneSetup()
        {
            originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        }

        [TearDown]
        public void RestoreSceneSetup()
        {
            if (originalSceneSetup != null && originalSceneSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
            }
        }

        [Test]
        public void GroundFloorData_MatchesArchitecturalContract()
        {
            FloorLayoutData layout = AssetDatabase.LoadAssetAtPath<FloorLayoutData>(LayoutPath);

            Assert.That(layout, Is.Not.Null);
            Assert.That(layout.GetValidationErrors(), Is.Empty);
            Assert.That(layout.FloorName, Is.EqualTo("Ground Floor"));
            Assert.That(layout.CeilingHeight, Is.EqualTo(3.5f));
            Assert.That(layout.WallThickness, Is.EqualTo(0.25f));
            Assert.That(layout.FootprintSize, Is.EqualTo(new Vector2(100f, 90f)));
            Assert.That(layout.EntrancePosition, Is.EqualTo(Vector3.zero));
            Assert.That(layout.Zones.Count, Is.EqualTo(2));
            Assert.That(layout.Rooms.Count, Is.EqualTo(7));
            Assert.That(layout.Elevators.Count, Is.EqualTo(2));
            Assert.That(layout.Stairs.Count, Is.EqualTo(4));

            AssertConnection(layout.Elevators, "Elevator_A", new Vector3(15f, 0f, 15f));
            AssertConnection(layout.Elevators, "Elevator_B", new Vector3(0f, 0f, 45f));
            AssertConnection(layout.Stairs, "Stair_SW", new Vector3(-40f, 0f, 20f));
            AssertConnection(layout.Stairs, "Stair_SE", new Vector3(40f, 0f, 20f));
            AssertConnection(layout.Stairs, "Stair_NW", new Vector3(-40f, 0f, 70f));
            AssertConnection(layout.Stairs, "Stair_NE", new Vector3(40f, 0f, 70f));
        }

        [Test]
        public void DoorOpeningCalculation_PreservesRequestedGap()
        {
            var segments = FloorGeometry.CreateHorizontalWallWithOpening(
                0f,
                100f,
                0f,
                0f,
                3.5f,
                0.25f,
                0f,
                4f);

            Assert.That(segments.Count, Is.EqualTo(2));
            Assert.That(segments.Sum(segment => segment.Size.x), Is.EqualTo(96f).Within(0.001f));
            Assert.That(segments[0].Center.x, Is.EqualTo(-26f).Within(0.001f));
            Assert.That(segments[1].Center.x, Is.EqualTo(26f).Within(0.001f));
        }

        [Test]
        public void GroundFloorScene_ContainsGeneratedHierarchyAndCollision()
        {
            Scene scene = EditorSceneManager.OpenScene(GroundScenePath, OpenSceneMode.Single);
            GameObject generated = FindRoot(scene, "Generated");

            Assert.That(generated, Is.Not.Null);
            Assert.That(generated.transform.Find("Floors"), Is.Not.Null);
            Assert.That(generated.transform.Find("Walls"), Is.Not.Null);
            Assert.That(generated.transform.Find("Rooms"), Is.Not.Null);
            Assert.That(generated.transform.Find("Doors"), Is.Not.Null);
            Assert.That(generated.transform.Find("Stairs"), Is.Not.Null);
            Assert.That(generated.transform.Find("Elevators"), Is.Not.Null);
            Assert.That(generated.GetComponentsInChildren<BoxCollider>(true).Length, Is.GreaterThan(100));
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)), Is.Empty);

            FloorGenerationMetadata metadata = generated.GetComponent<FloorGenerationMetadata>();
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.LayoutData, Is.Not.Null);
            Assert.That(metadata.LayoutData.name, Is.EqualTo("GroundFloorLayout"));
        }

        [Test]
        public void MainScene_OwnsPlayerCameraAndSceneLoader()
        {
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            GameObject player = FindRoot(scene, "Player");
            GameObject managers = FindRoot(scene, "Game Managers");
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Single();

            Assert.That(player, Is.Not.Null);
            Assert.That(player.transform.position, Is.EqualTo(new Vector3(0f, 0f, -8f)));
            Assert.That(player.transform.forward, Is.EqualTo(Vector3.forward));

            CharacterController controller = player.GetComponent<CharacterController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.height, Is.EqualTo(1.8f));
            Assert.That(controller.radius, Is.EqualTo(0.3f));
            Assert.That(controller.center.y, Is.EqualTo(0.9f));

            Assert.That(camera.CompareTag("MainCamera"), Is.True);
            Assert.That(
                camera.GetComponents<MonoBehaviour>().Any(component => component.GetType().Name == "CameraFollow"),
                Is.True);
            Assert.That(managers, Is.Not.Null);
            Assert.That(managers.GetComponent<FloorSceneLoader>(), Is.Not.Null);
        }

        [Test]
        public void BuildSettings_StartWithMainAndContainEveryFloor()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.EqualTo(12));
            Assert.That(scenes[0].path, Is.EqualTo(MainScenePath));
            Assert.That(scenes[1].path, Is.EqualTo(GroundScenePath));
            Assert.That(scenes.All(scene => scene.enabled), Is.True);

            for (int floorIndex = 1; floorIndex <= 10; floorIndex++)
            {
                string expectedPath = $"Assets/Scenes/Floors/Floor{floorIndex:00}.unity";
                Assert.That(scenes.Any(scene => scene.path == expectedPath), Is.True);
            }
        }

        private static void AssertConnection(
            System.Collections.Generic.IReadOnlyList<VerticalConnectionDefinition> connections,
            string id,
            Vector3 expectedPosition)
        {
            VerticalConnectionDefinition connection = connections.Single(item => item.ConnectionId == id);
            Assert.That(connection.Position, Is.EqualTo(expectedPosition));
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects().SingleOrDefault(root => root.name == objectName);
        }
    }
}
