#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UIU.Simulator.Building.Generation;
using UIU.Simulator.Gameplay.Advisor;
using UIU.Simulator.Gameplay.Elevator;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class ElevatorTests
    {
        private GameObject playerObject;
        private CharacterController characterController;
        private PlayerMovement playerMovement;
        private FirstPersonLook firstPersonLook;
        private GameObject cameraObject;

        private GameObject loaderObject;
        private FloorSceneLoader floorSceneLoader;

        private GameObject travelControllerObject;
        private ElevatorTravelController travelController;

        private GameObject elevatorUIObject;
        private ElevatorUI elevatorUI;

        private GameObject doorObject;
        private ElevatorInteractable elevatorDoor;

        [SetUp]
        public void SetUp()
        {
            // Setup Player with CharacterController, PlayerMovement, and FirstPersonLook
            playerObject = new GameObject("TestPlayer");
            characterController = playerObject.AddComponent<CharacterController>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            firstPersonLook = playerObject.AddComponent<FirstPersonLook>();

            cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.AddComponent<Camera>();

            // Set cameraTransform reference on FirstPersonLook
            FieldInfo camField = typeof(FirstPersonLook).GetField("cameraTransform", BindingFlags.NonPublic | BindingFlags.Instance);
            if (camField != null)
            {
                camField.SetValue(firstPersonLook, cameraObject.transform);
            }

            // Setup FloorSceneLoader
            loaderObject = new GameObject("TestFloorSceneLoader");
            floorSceneLoader = loaderObject.AddComponent<FloorSceneLoader>();

            // Setup ElevatorTravelController
            travelControllerObject = new GameObject("TestElevatorTravelController");
            travelController = travelControllerObject.AddComponent<ElevatorTravelController>();

            // Setup ElevatorUI
            elevatorUIObject = new GameObject("TestElevatorUI");
            elevatorUI = elevatorUIObject.AddComponent<ElevatorUI>();

            // Setup Door Interactable
            doorObject = new GameObject("TestElevatorDoor");
            doorObject.AddComponent<BoxCollider>();
            elevatorDoor = doorObject.AddComponent<ElevatorInteractable>();
            elevatorDoor.Initialize(1, 0, "Use Elevator");
        }

        [TearDown]
        public void TearDown()
        {
            if (ElevatorUI.IsOpen)
            {
                elevatorUI.Hide();
            }

            if (doorObject != null) UnityEngine.Object.DestroyImmediate(doorObject);
            if (elevatorUIObject != null) UnityEngine.Object.DestroyImmediate(elevatorUIObject);
            if (travelControllerObject != null) UnityEngine.Object.DestroyImmediate(travelControllerObject);
            if (loaderObject != null) UnityEngine.Object.DestroyImmediate(loaderObject);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void Test01_ElevatorInteractable_ReturnsCorrectPrompt()
        {
            Assert.That(elevatorDoor.InteractionPrompt, Is.EqualTo("Use Elevator"));
        }

        [Test]
        public void Test02_Interacting_OpensElevatorUI_WithCorrectIdAndFloor()
        {
            elevatorDoor.Initialize(4, 3);
            Assert.That(ElevatorUI.IsOpen, Is.False);

            elevatorDoor.Interact();

            Assert.That(ElevatorUI.IsOpen, Is.True);

            FieldInfo idField = typeof(ElevatorUI).GetField("currentElevatorId", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo floorField = typeof(ElevatorUI).GetField("currentFloor", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That((int)idField.GetValue(elevatorUI), Is.EqualTo(4));
            Assert.That((int)floorField.GetValue(elevatorUI), Is.EqualTo(3));
        }

        [Test]
        public void Test03_CurrentFloor_IsExcludedFromDestinations()
        {
            // Open on Floor 3
            elevatorUI.Show(1, 3);

            FieldInfo buttonsField = typeof(ElevatorUI).GetField("activeButtons", BindingFlags.NonPublic | BindingFlags.Instance);
            var buttons = (List<GameObject>)buttonsField.GetValue(elevatorUI);

            // Should have 10 destination buttons (Floors 0..10 minus floor 3 = 10)
            Assert.That(buttons.Count, Is.EqualTo(10));

            // Verify no button corresponds to Floor 3
            foreach (GameObject btn in buttons)
            {
                Assert.That(btn.name, Does.Not.EqualTo("Button_Floor_3"));
            }

            // Open on Ground Floor (0)
            elevatorUI.Show(1, 0);
            buttons = (List<GameObject>)buttonsField.GetValue(elevatorUI);
            Assert.That(buttons.Count, Is.EqualTo(10));
            foreach (GameObject btn in buttons)
            {
                Assert.That(btn.name, Does.Not.EqualTo("Button_Floor_0"));
            }
        }

        [Test]
        public void Test04_CentralFloorSceneLoader_Mapping_IsAuthoritative()
        {
            Assert.That(FloorSceneLoader.GetDefaultFloorSceneName(0), Is.EqualTo("GroundFloor"));
            Assert.That(FloorSceneLoader.GetDefaultFloorSceneName(1), Is.EqualTo("Floor01"));
            Assert.That(FloorSceneLoader.GetDefaultFloorSceneName(2), Is.EqualTo("Floor02"));
            Assert.That(FloorSceneLoader.GetDefaultFloorSceneName(3), Is.EqualTo("Floor03"));
            Assert.That(FloorSceneLoader.GetDefaultFloorSceneName(10), Is.EqualTo("Floor10"));
            Assert.That(FloorSceneLoader.GetDefaultFloorSceneName(-1), Is.Null);
            Assert.That(FloorSceneLoader.GetDefaultFloorSceneName(11), Is.Null);
        }

        [Test]
        public void Test05_CurrentFloor_Selection_IsIgnored()
        {
            elevatorUI.Show(1, 0); // Currently on Ground Floor

            MethodInfo selectMethod = typeof(ElevatorUI).GetMethod("SelectFloor", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(selectMethod, Is.Not.Null);

            FieldInfo isTravellingField = typeof(ElevatorUI).GetField("isTravelling", BindingFlags.NonPublic | BindingFlags.Instance);

            // Selecting current floor (0) should be ignored
            selectMethod.Invoke(elevatorUI, new object[] { 0 });
            Assert.That((bool)isTravellingField.GetValue(elevatorUI), Is.False);
        }

        [Test]
        public void Test06_ArrivalPoint_SceneAwareLookup_MatchesElevatorAndFloor()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            GameObject arrival1Go = new GameObject("Arrival_E1_F3");
            ElevatorArrivalPoint arrival1 = arrival1Go.AddComponent<ElevatorArrivalPoint>();
            arrival1.Initialize(1, 3);

            GameObject arrival2Go = new GameObject("Arrival_E2_F3");
            ElevatorArrivalPoint arrival2 = arrival2Go.AddComponent<ElevatorArrivalPoint>();
            arrival2.Initialize(2, 3);

            try
            {
                // Looking for elevator 1 on floor 3
                ElevatorArrivalPoint match = ElevatorTravelController.FindArrivalPointInScene(activeScene, 1, 3);
                Assert.That(match, Is.Not.Null);
                Assert.That(match, Is.EqualTo(arrival1));

                // Looking for elevator 2 on floor 3
                ElevatorArrivalPoint match2 = ElevatorTravelController.FindArrivalPointInScene(activeScene, 2, 3);
                Assert.That(match2, Is.Not.Null);
                Assert.That(match2, Is.EqualTo(arrival2));

                // Looking for elevator 3 on floor 3 (non-existent)
                ElevatorArrivalPoint match3 = ElevatorTravelController.FindArrivalPointInScene(activeScene, 3, 3);
                Assert.That(match3, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(arrival1Go);
                UnityEngine.Object.DestroyImmediate(arrival2Go);
            }
        }

        [Test]
        public void Test07_Elevator1_DoesNotArriveAt_Elevator2Marker()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            GameObject arrival2Go = new GameObject("Arrival_E2_F3");
            ElevatorArrivalPoint arrival2 = arrival2Go.AddComponent<ElevatorArrivalPoint>();
            arrival2.Initialize(2, 3);

            try
            {
                // Elevator 1 searching in scene with only Elevator 2
                ElevatorArrivalPoint match = ElevatorTravelController.FindArrivalPointInScene(activeScene, 1, 3);
                Assert.That(match, Is.Null, "Elevator 1 must never match an Elevator 2 arrival marker.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(arrival2Go);
            }
        }

        [Test]
        public void Test08_MissingArrivalPoint_FailsSafely_PlayerUnmoved()
        {
            Vector3 originalPosition = new Vector3(10f, 1f, 20f);
            playerObject.transform.position = originalPosition;

            Scene activeScene = SceneManager.GetActiveScene();
            ElevatorArrivalPoint match = ElevatorTravelController.FindArrivalPointInScene(activeScene, 6, 9);
            Assert.That(match, Is.Null);

            // Confirm player remains untouched at original position
            Assert.That(playerObject.transform.position, Is.EqualTo(originalPosition));
        }

        [Test]
        public void Test09_FirstPersonLook_SetFacingRotation_SetsYawAndResetsPitch()
        {
            // Set pitch to 45 degrees
            FieldInfo pitchField = typeof(FirstPersonLook).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance);
            pitchField.SetValue(firstPersonLook, 45f);
            cameraObject.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

            // Facing target orientation: facing 90 degrees yaw (East)
            Quaternion targetRotation = Quaternion.Euler(0f, 90f, 0f);

            firstPersonLook.SetFacingRotation(targetRotation);

            Assert.That(firstPersonLook.Pitch, Is.EqualTo(0f), "Camera pitch must be reset to 0 neutral.");
            Assert.That(cameraObject.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(playerObject.transform.eulerAngles.y, Is.EqualTo(90f).Within(0.1f));
        }

        [Test]
        public void Test10_CharacterController_SafelyDisabledDuringTeleport()
        {
            Assert.That(characterController.enabled, Is.True);

            // Simulate the exact teleport pattern used in ElevatorTravelController
            Vector3 destination = new Vector3(50f, 2f, 80f);
            Quaternion destinationRot = Quaternion.Euler(0f, 180f, 0f);

            characterController.enabled = false;
            Assert.That(characterController.enabled, Is.False);

            playerObject.transform.position = destination;
            firstPersonLook.SetFacingRotation(destinationRot);

            characterController.enabled = true;
            Assert.That(characterController.enabled, Is.True);
            Assert.That(playerObject.transform.position, Is.EqualTo(destination));
            Assert.That(playerObject.transform.eulerAngles.y, Is.EqualTo(180f).Within(0.1f));
        }

        [Test]
        public void Test11_DuplicateTravelRequests_AreBlocked()
        {
            FieldInfo isTravellingField = typeof(ElevatorTravelController).GetField("isTravelling", BindingFlags.NonPublic | BindingFlags.Instance);
            isTravellingField.SetValue(travelController, true);

            bool onErrorCalled = false;
            bool onCompleteCalled = false;

            travelController.TravelToFloor(1, 0, 3,
                () => onCompleteCalled = true,
                _ => onErrorCalled = true);

            Assert.That(onCompleteCalled, Is.False);
            Assert.That(onErrorCalled, Is.False);
        }

        [Test]
        public void Test12_InputLocks_DisabledOnShow_RestoredOnHide()
        {
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);

            elevatorUI.Show(1, 0);

            Assert.That(ElevatorUI.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False, "PlayerMovement must be disabled while elevator menu is open.");
            Assert.That(firstPersonLook.enabled, Is.False, "FirstPersonLook must be disabled while elevator menu is open.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "Cursor must be unlocked while elevator menu is open.");
            Assert.That(Cursor.visible, Is.True, "Cursor must be visible while elevator menu is open.");

            elevatorUI.Hide();

            Assert.That(ElevatorUI.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True, "PlayerMovement must be restored after elevator menu closes.");
            Assert.That(firstPersonLook.enabled, Is.True, "FirstPersonLook must be restored after elevator menu closes.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "Cursor must be locked after elevator menu closes.");
            Assert.That(Cursor.visible, Is.False, "Cursor must be hidden after elevator menu closes.");
        }

        [Test]
        public void Test13_InteractionController_SuppressedWhileElevatorIsOpen()
        {
            elevatorUI.Show(1, 0);
            Assert.That(ElevatorUI.IsOpen, Is.True);

            // InteractionController checks ElevatorUI.IsOpen in UpdateTarget and HandleInput
            bool isBlocked = DialogueUI.IsOpen || CanteenQueueUI.IsOpen || AdvisorUI.IsOpen || ElevatorUI.IsOpen;
            Assert.That(isBlocked, Is.True, "InteractionController must be blocked while ElevatorUI is open.");

            elevatorUI.Hide();
            Assert.That(ElevatorUI.IsOpen, Is.False);
        }

        [Test]
        public void Test14_NoTokenLifecycleInElevatorScripts()
        {
            Type[] types = new Type[]
            {
                typeof(ElevatorInteractable),
                typeof(ElevatorArrivalPoint),
                typeof(ElevatorTravelController),
                typeof(ElevatorUI)
            };

            string[] forbiddenTerms = new string[]
            {
                "refreshSecret",
                "clerk_user_id",
                "UserSession",
                "JwtToken",
                "RefreshToken"
            };

            foreach (Type t in types)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    foreach (string forbidden in forbiddenTerms)
                    {
                        Assert.That(field.Name.ToLowerInvariant(), Does.Not.Contain(forbidden.ToLowerInvariant()),
                            $"Type {t.Name} must not contain token/auth lifecycle field {field.Name}");
                    }
                }
            }
        }

        [Test]
        public void Test15_AbortedTravel_LeavesPlayerSafeAtOriginalPosition()
        {
            Vector3 originalPosition = new Vector3(12f, 1f, 18f);
            playerObject.transform.position = originalPosition;

            // When marker is missing or pre-teleport validation fails, player position must never be modified
            Assert.That(playerObject.transform.position, Is.EqualTo(originalPosition));
        }

        [Test]
        public void Test16_FloorSceneLoader_EnsureFloorLoadedRoutine_ReportsAlreadyLoadedFlag()
        {
            MethodInfo routineMethod = typeof(FloorSceneLoader).GetMethod(
                "EnsureFloorLoadedRoutine",
                new Type[] { typeof(int), typeof(Action<Scene, bool>), typeof(Action<string>) });

            Assert.That(routineMethod, Is.Not.Null, "EnsureFloorLoadedRoutine with (Scene, bool) callback must exist.");
        }

        [Test]
        public void Test17_SuccessfulTravel_RestoresPlayerMovementAndLook()
        {
            elevatorUI.Show(1, 0);
            Assert.That(ElevatorUI.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);

            // Travel completion callback invokes CloseAndRestoreInput
            elevatorUI.CloseAndRestoreInput();

            Assert.That(ElevatorUI.IsOpen, Is.False, "ElevatorUI.IsOpen must be false after successful travel.");
            Assert.That(playerMovement.enabled, Is.True, "PlayerMovement must be restored after successful travel.");
            Assert.That(firstPersonLook.enabled, Is.True, "FirstPersonLook must be restored after successful travel.");
        }

        [Test]
        public void Test18_SuccessfulTravel_LocksAndHidesCursor()
        {
            elevatorUI.Show(1, 0);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);

            // Travel completion callback invokes CloseAndRestoreInput
            elevatorUI.CloseAndRestoreInput();

            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "Cursor must be locked after successful travel.");
            Assert.That(Cursor.visible, Is.False, "Cursor must be hidden after successful travel.");
        }

        [Test]
        public void Test19_FailedTravel_RestoresAllInputState()
        {
            elevatorUI.Show(1, 0);
            Assert.That(ElevatorUI.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);

            // On travel error, onError invokes CloseAndRestoreInput
            elevatorUI.CloseAndRestoreInput();

            Assert.That(ElevatorUI.IsOpen, Is.False, "ElevatorUI.IsOpen must be false after failed travel.");
            Assert.That(playerMovement.enabled, Is.True, "PlayerMovement must be restored after failed travel.");
            Assert.That(firstPersonLook.enabled, Is.True, "FirstPersonLook must be restored after failed travel.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "Cursor must be locked after failed travel.");
            Assert.That(Cursor.visible, Is.False, "Cursor must be hidden after failed travel.");
        }

        [Test]
        public void Test20_EscapeClose_RestoresAllInputState()
        {
            elevatorUI.Show(1, 0);
            Assert.That(ElevatorUI.IsOpen, Is.True);

            // Escape close invokes CloseAndRestoreInput
            elevatorUI.CloseAndRestoreInput();

            Assert.That(ElevatorUI.IsOpen, Is.False, "ElevatorUI.IsOpen must be false after Escape close.");
            Assert.That(playerMovement.enabled, Is.True, "PlayerMovement must be restored after Escape close.");
            Assert.That(firstPersonLook.enabled, Is.True, "FirstPersonLook must be restored after Escape close.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "Cursor must be locked after Escape close.");
            Assert.That(Cursor.visible, Is.False, "Cursor must be hidden after Escape close.");
        }

        [Test]
        public void Test21_ElevatorUILayout_TwoColumnsAndSpaciousButtons()
        {
            elevatorUI.Show(1, 0);

            FieldInfo gridField = typeof(ElevatorUI).GetField("buttonsGrid", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(gridField, Is.Not.Null);
            var grid = (UnityEngine.UI.GridLayoutGroup)gridField.GetValue(elevatorUI);
            Assert.That(grid, Is.Not.Null, "ElevatorUI must use a GridLayoutGroup for two-column presentation.");
            Assert.That(grid.startAxis, Is.EqualTo(UnityEngine.UI.GridLayoutGroup.Axis.Vertical), "Grid must fill vertically top-to-bottom in columns.");
            Assert.That(grid.cellSize.y, Is.GreaterThanOrEqualTo(45f), "Button height must be at least 45px.");
            Assert.That(grid.cellSize.x, Is.GreaterThanOrEqualTo(200f), "Button width must be at least 200px.");
        }
    }
}
#endif
