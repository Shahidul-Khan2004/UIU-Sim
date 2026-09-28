#if UNITY_EDITOR
using NUnit.Framework;
using UIU.Simulator.Gameplay.Teleport;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    public sealed class FloorTeleportTests
    {
        [Test]
        public void DestinationSceneName_ParsesGroundAndNumberedFloors()
        {
            Assert.That(FloorTeleportDestination.TryParseFloorSceneName("GroundFloor", out int ground), Is.True);
            Assert.That(ground, Is.EqualTo(0));

            Assert.That(FloorTeleportDestination.TryParseFloorSceneName("Floor07", out int seventh), Is.True);
            Assert.That(seventh, Is.EqualTo(7));

            Assert.That(FloorTeleportDestination.TryParseFloorSceneName("UIU_Main", out _), Is.False);
        }

        [Test]
        public void FindDestination_MatchesLinkAndFloorInsideScene()
        {
            GameObject matchObject = new GameObject("FloorTeleportDestination_Match");
            GameObject otherObject = new GameObject("FloorTeleportDestination_Other");
            try
            {
                FloorTeleportDestination match = matchObject.AddComponent<FloorTeleportDestination>();
                match.Initialize(4, 7);

                FloorTeleportDestination other = otherObject.AddComponent<FloorTeleportDestination>();
                other.Initialize(4, 2);

                Scene scene = matchObject.scene;
                FloorTeleportDestination found = FloorTeleportTravelController.FindDestinationInScene(scene, 4, 7);

                Assert.That(found, Is.SameAs(match));
                Assert.That(FloorTeleportTravelController.FindDestinationInScene(scene, 9, 7), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void Interactable_ExposesPromptFloorAndLink()
        {
            GameObject host = new GameObject("FloorTeleportInteractable_Host");
            try
            {
                FloorTeleportInteractable interactable = host.AddComponent<FloorTeleportInteractable>();
                interactable.Initialize(3, 2, "Go to floor 3");

                Assert.That(interactable.InteractionPrompt, Is.EqualTo("Go to floor 3"));
                Assert.That(interactable.UpFloor, Is.EqualTo(3));
                Assert.That(interactable.UpLinkId, Is.EqualTo(2));
                Assert.That(interactable.GoesDown, Is.False);
                Assert.That(typeof(IInteractable).IsAssignableFrom(typeof(FloorTeleportInteractable)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BothDirections_OnAMiddleFloor_OffersUpAndDown()
        {
            FloorTeleportInteractable.ResolveAvailableDirections(
                2, true, 3, true, 1, out bool upAvailable, out bool downAvailable);

            Assert.That(upAvailable, Is.True);
            Assert.That(downAvailable, Is.True);
        }

        [Test]
        public void BothDirections_AlreadyOnTheUpFloor_OffersOnlyDown()
        {
            FloorTeleportInteractable.ResolveAvailableDirections(
                3, true, 3, true, 1, out bool upAvailable, out bool downAvailable);

            Assert.That(upAvailable, Is.False);
            Assert.That(downAvailable, Is.True);
        }

        [Test]
        public void BothDirections_SameDestination_OffersOnlyUp()
        {
            FloorTeleportInteractable.ResolveAvailableDirections(
                2, true, 4, true, 4, out bool upAvailable, out bool downAvailable);

            Assert.That(upAvailable, Is.True);
            Assert.That(downAvailable, Is.False);
        }

        [Test]
        public void ClearSpawn_LiftsAPoseThatIsUnderTheFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject body = new GameObject("FloorTeleportCapsuleUnderFloor");
            try
            {
                floor.transform.position = new Vector3(0f, 1f, 0f);
                floor.transform.localScale = new Vector3(6f, 0.2f, 6f);

                CharacterController controller = body.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = 0.3f;
                controller.center = Vector3.zero;
                controller.skinWidth = 0.05f;

                Vector3 underFloor = new Vector3(0f, 0f, 0f);
                Vector3 cleared = FloorTeleportTravelController.ClearSpawnPosition(underFloor, controller);

                float feetY = cleared.y - (controller.height * 0.5f);
                Assert.That(feetY, Is.GreaterThan(1.05f));
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void StandingPosition_PutsCapsuleAboveTheFeetMarker()
        {
            GameObject body = new GameObject("FloorTeleportCapsule");
            try
            {
                body.transform.localScale = new Vector3(1.25f, 1.2f, 1f);
                CharacterController controller = body.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.center = Vector3.zero;
                controller.skinWidth = 0.05f;

                Vector3 feet = new Vector3(2f, 5f, -3f);
                Vector3 standing = FloorTeleportTravelController.ResolveStandingPosition(feet, controller);

                Assert.That(standing.x, Is.EqualTo(feet.x));
                Assert.That(standing.z, Is.EqualTo(feet.z));
                Assert.That(standing.y, Is.GreaterThan(feet.y + 0.9f));
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void BothDirections_PromptIsUseStairs()
        {
            GameObject host = new GameObject("FloorTeleportInteractable_Both");
            try
            {
                FloorTeleportInteractable interactable = host.AddComponent<FloorTeleportInteractable>();
                interactable.InitializeBoth(3, 1, 1, 1);

                Assert.That(interactable.InteractionPrompt, Is.EqualTo("Use Stairs"));
                Assert.That(interactable.GoesUp, Is.True);
                Assert.That(interactable.GoesDown, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
