using System.Reflection;
using NUnit.Framework;
using UIU.Simulator.Gameplay.Doors;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class DoorTeleportInteractableTests
    {
        private GameObject playerObject;
        private CharacterController characterController;
        private PlayerMovement playerMovement;

        private GameObject doorObject;
        private DoorTeleportInteractable door;

        private GameObject secondDoorObject;
        private DoorTeleportInteractable secondDoor;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("TestDoorPlayer");
            playerObject.transform.position = new Vector3(0f, 1f, -2f);
            playerObject.transform.rotation = Quaternion.identity;

            characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            playerMovement = playerObject.AddComponent<PlayerMovement>();

            doorObject = new GameObject("TestDoor");
            doorObject.transform.position = Vector3.zero;
            doorObject.transform.rotation = Quaternion.identity;
            doorObject.AddComponent<BoxCollider>();
            door = doorObject.AddComponent<DoorTeleportInteractable>();
            SetExitDistance(door, 1.1f);
            SetCooldown(door, 0f);

            secondDoorObject = new GameObject("TestDoorB");
            secondDoorObject.transform.position = new Vector3(10f, 0f, 0f);
            secondDoorObject.transform.rotation = Quaternion.identity;
            secondDoorObject.AddComponent<BoxCollider>();
            secondDoor = secondDoorObject.AddComponent<DoorTeleportInteractable>();
            SetExitDistance(secondDoor, 1.1f);
            SetCooldown(secondDoor, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (secondDoorObject != null) Object.DestroyImmediate(secondDoorObject);
            if (doorObject != null) Object.DestroyImmediate(doorObject);
            if (playerObject != null) Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void Prompt_DefaultsToUseDoor()
        {
            Assert.That(door.InteractionPrompt, Is.EqualTo("Use Door"));
        }

        [Test]
        public void PositiveSide_TeleportsToNegativeSide()
        {
            playerObject.transform.position = new Vector3(0.4f, 1f, 2f);

            door.Interact();

            Assert.That(playerObject.transform.position.z, Is.EqualTo(-1.1f).Within(0.01f));
            Assert.That(playerObject.transform.position.x, Is.EqualTo(0.4f).Within(0.01f));
            Assert.That(playerObject.transform.position.y, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void NegativeSide_TeleportsToPositiveSide()
        {
            playerObject.transform.position = new Vector3(-0.3f, 1f, -2f);

            door.Interact();

            Assert.That(playerObject.transform.position.z, Is.EqualTo(1.1f).Within(0.01f));
            Assert.That(playerObject.transform.position.x, Is.EqualTo(-0.3f).Within(0.01f));
            Assert.That(playerObject.transform.position.y, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void RotatedDoor_UsesDoorLocalForward_NotWorldAxes()
        {
            // Door faces East/West: local forward becomes world +X.
            doorObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            playerObject.transform.position = new Vector3(2f, 1f, 0.25f);

            door.Interact();

            Assert.That(playerObject.transform.position.x, Is.EqualTo(-1.1f).Within(0.01f));
            Assert.That(playerObject.transform.position.z, Is.EqualTo(0.25f).Within(0.01f));
            Assert.That(playerObject.transform.position.y, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void MultipleDoors_UseTheirOwnTransformsIndependently()
        {
            playerObject.transform.position = new Vector3(10f, 1f, -2f);

            secondDoor.Interact();

            Assert.That(playerObject.transform.position.x, Is.EqualTo(10f).Within(0.01f));
            Assert.That(playerObject.transform.position.z, Is.EqualTo(1.1f).Within(0.01f));

            // Original door was never involved.
            Assert.That(doorObject.transform.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Interact_PreservesPlayerRotation()
        {
            Quaternion facing = Quaternion.Euler(0f, 45f, 0f);
            playerObject.transform.position = new Vector3(0f, 1f, -2f);
            playerObject.transform.rotation = facing;

            door.Interact();

            Assert.That(playerObject.transform.rotation.eulerAngles.y, Is.EqualTo(45f).Within(0.1f));
        }

        [Test]
        public void Interact_ReenablesCharacterController()
        {
            playerObject.transform.position = new Vector3(0f, 1f, -2f);
            Assert.That(characterController.enabled, Is.True);

            door.Interact();

            Assert.That(characterController.enabled, Is.True);
            Assert.That(playerObject.transform.position.z, Is.EqualTo(1.1f).Within(0.01f));
        }

        [Test]
        public void Interact_DoesNotMoveDoorModel()
        {
            Vector3 doorPos = doorObject.transform.position;
            Quaternion doorRot = doorObject.transform.rotation;
            Vector3 doorScale = doorObject.transform.localScale;
            playerObject.transform.position = new Vector3(0f, 1f, -2f);

            door.Interact();

            Assert.That(doorObject.transform.position, Is.EqualTo(doorPos));
            Assert.That(doorObject.transform.rotation, Is.EqualTo(doorRot));
            Assert.That(doorObject.transform.localScale, Is.EqualTo(doorScale));
        }

        [Test]
        public void MissingPlayer_FailsSafely()
        {
            Object.DestroyImmediate(playerObject);
            playerObject = null;

            Assert.DoesNotThrow(() => door.Interact());
        }

        [Test]
        public void ExitDistance_IsRespected()
        {
            SetExitDistance(door, 1.5f);
            playerObject.transform.position = new Vector3(0f, 1f, -3f);

            door.Interact();

            Assert.That(playerObject.transform.position.z, Is.EqualTo(1.5f).Within(0.01f));
            Assert.That(door.ExitDistance, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void CalculateDestination_NearPlane_UsesPlayerFacingFallback()
        {
            Vector3 destination = DoorTeleportInteractable.CalculateOppositeSideDestination(
                doorPosition: Vector3.zero,
                doorForward: Vector3.forward,
                playerPosition: new Vector3(0.2f, 1f, 0.01f),
                playerForward: Vector3.forward,
                exitDistance: 1.1f);

            Assert.That(destination.z, Is.EqualTo(1.1f).Within(0.01f));
            Assert.That(destination.y, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void ImplementsIInteractable_AndIsDetectableViaParentLookup()
        {
            Assert.That(door, Is.InstanceOf<IInteractable>());

            BoxCollider leafCollider = doorObject.GetComponent<BoxCollider>();
            IInteractable found = leafCollider.GetComponentInParent<IInteractable>();
            Assert.That(found, Is.SameAs(door));
        }

        [Test]
        public void InteractionArchitecture_StillCompilesWithDoorComponent()
        {
            Assert.That(typeof(IInteractable).IsAssignableFrom(typeof(DoorTeleportInteractable)), Is.True);
            Assert.That(typeof(InteractionController), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PlayerMovement>(), Is.SameAs(playerMovement));
        }

        private static void SetExitDistance(DoorTeleportInteractable target, float value)
        {
            FieldInfo field = typeof(DoorTeleportInteractable).GetField(
                "exitDistance",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void SetCooldown(DoorTeleportInteractable target, float value)
        {
            FieldInfo field = typeof(DoorTeleportInteractable).GetField(
                "interactionCooldown",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
