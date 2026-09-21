using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class DayProgressTests
    {
        private GameObject playerObject;
        private DailyActivityState dailyActivityState;
        private CampusDayState campusDayState;
        private PlayerSaveState saveState;
        private StatsHUD statsHud;
        private GameObject menuObject;
        private GameMenuManager menu;
        private GameObject summaryObject;
        private DailySummaryUI summaryUi;
        private PlayerMovement playerMovement;
        private FirstPersonLook firstPersonLook;
        private InteractionController interactionController;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;

            if (GameMenuManager.Instance != null)
            {
                Object.DestroyImmediate(GameMenuManager.Instance.gameObject);
            }

            if (DailySummaryUI.Instance != null)
            {
                Object.DestroyImmediate(DailySummaryUI.Instance.gameObject);
            }

            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            playerObject = new GameObject("DayProgressPlayer");
            playerObject.AddComponent<CharacterController>();
            playerObject.AddComponent<PlayerStats>();
            dailyActivityState = playerObject.AddComponent<DailyActivityState>();
            campusDayState = playerObject.AddComponent<CampusDayState>();
            statsHud = playerObject.AddComponent<StatsHUD>();
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            firstPersonLook = playerObject.AddComponent<FirstPersonLook>();
            interactionController = playerObject.AddComponent<InteractionController>();

            GameObject cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.AddComponent<Camera>();

            saveState = playerObject.AddComponent<PlayerSaveState>();
            saveState.SetDayProgressForTesting(1, 1);

            menuObject = new GameObject("TestGameMenuManager");
            menu = menuObject.AddComponent<GameMenuManager>();

            summaryObject = new GameObject("TestDailySummaryUI");
            summaryUi = summaryObject.AddComponent<DailySummaryUI>();
        }

        [TearDown]
        public void TearDown()
        {
            if (GameMenuManager.IsOpen && menu != null)
            {
                menu.Close(restoreGameplayControls: true);
            }

            if (DailySummaryUI.IsOpen && summaryUi != null)
            {
                summaryUi.Hide(restoreGameplayControls: true);
            }

            Time.timeScale = 1f;

            if (summaryObject != null)
            {
                Object.DestroyImmediate(summaryObject);
            }

            if (menuObject != null)
            {
                Object.DestroyImmediate(menuObject);
            }

            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void Hud_ShowsSemesterDayHeader_WhenSaveActive()
        {
            dailyActivityState.SetGetIdCardStatusForTesting(ActivityStatus.Completed, "COMPLETED");
            dailyActivityState.SetBreakfastStatusForTesting(ActivityStatus.Pending);
            saveState.SetDayProgressForTesting(1, 1);

            statsHud.enabled = false;
            statsHud.enabled = true;

            TextMeshProUGUI dayHeader = FindLabel(statsHud.transform, "DayHeader");
            TextMeshProUGUI todayHeader = FindLabel(statsHud.transform, "TodayHeader");

            Assert.That(dayHeader, Is.Not.Null);
            Assert.That(dayHeader.gameObject.activeSelf, Is.True);
            Assert.That(dayHeader.text, Is.EqualTo("SEMESTER 1 · DAY 1"));
            Assert.That(todayHeader, Is.Not.Null);
            Assert.That(todayHeader.gameObject.activeSelf, Is.True);
            Assert.That(todayHeader.text, Is.EqualTo("TODAY"));
        }

        [Test]
        public void Hud_HidesGetIdCard_OnDay2()
        {
            dailyActivityState.SetGetIdCardStatusForTesting(ActivityStatus.Completed, "COMPLETED");
            dailyActivityState.ResetForNewDay(2);
            saveState.SetDayProgressForTesting(1, 2);

            statsHud.enabled = false;
            statsHud.enabled = true;

            Transform idRoot = FindChild(statsHud.transform, "IdCardObjectiveBlock");
            Transform breakfastRoot = FindChild(statsHud.transform, "BreakfastObjectiveBlock");

            Assert.That(idRoot, Is.Not.Null);
            Assert.That(idRoot.gameObject.activeSelf, Is.False, "GET_ID_CARD must not appear as a Day 2 objective.");
            Assert.That(breakfastRoot, Is.Not.Null);
            Assert.That(breakfastRoot.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ResetForNewDay_PreservesIdOwnership_AndClearsBreakfast()
        {
            dailyActivityState.SetGetIdCardStatusForTesting(ActivityStatus.Completed, "COMPLETED");
            dailyActivityState.SetBreakfastStatusForTesting(ActivityStatus.Completed, "RICE", 5);
            saveState.SetStateForTesting(true, true);
            saveState.SetDayProgressForTesting(1, 1);

            campusDayState.BeginCampusDay(2);
            saveState.ApplyAdvancedDay(1, 2, idCardIssuedValue: true);

            Assert.That(dailyActivityState.GetIdCardStatus, Is.EqualTo(ActivityStatus.Completed));
            Assert.That(dailyActivityState.BreakfastStatus, Is.EqualTo(ActivityStatus.Pending));
            Assert.That(dailyActivityState.DayNumber, Is.EqualTo(2));
            Assert.That(saveState.IdCardIssued, Is.True);
            Assert.That(saveState.CurrentDay, Is.EqualTo(2));
            Assert.That(campusDayState.HasCompletedBreakfastEvent, Is.False);
        }

        [Test]
        public void Hud_ActivityColors_CompletedGreen_MissedRed_PendingNeutral()
        {
            dailyActivityState.SetGetIdCardStatusForTesting(ActivityStatus.Completed, "COMPLETED");
            dailyActivityState.SetBreakfastStatusForTesting(ActivityStatus.Missed, "SKIP_BREAKFAST", -5);
            saveState.SetDayProgressForTesting(1, 1);

            statsHud.enabled = false;
            statsHud.enabled = true;

            TextMeshProUGUI idMarker = FindLabel(statsHud.transform, "IdCardMarker");
            TextMeshProUGUI idTitle = FindLabel(statsHud.transform, "IdCardTitle");
            TextMeshProUGUI breakfastMarker = FindLabel(statsHud.transform, "BreakfastMarker");
            TextMeshProUGUI breakfastTitle = FindLabel(statsHud.transform, "BreakfastTitle");

            Assert.That(idMarker.color, Is.EqualTo(UiTheme.Success));
            Assert.That(idTitle.color, Is.EqualTo(UiTheme.Success));
            Assert.That(breakfastMarker.color, Is.EqualTo(UiTheme.Danger));
            Assert.That(breakfastTitle.color, Is.EqualTo(UiTheme.Danger));

            dailyActivityState.SetBreakfastStatusForTesting(ActivityStatus.Pending);
            Assert.That(FindLabel(statsHud.transform, "BreakfastMarker").color, Is.EqualTo(UiTheme.White));
            Assert.That(FindLabel(statsHud.transform, "BreakfastTitle").color, Is.EqualTo(UiTheme.White));
        }

        [Test]
        public void Summary_DifferentiatesCompletedAndMissedWithRichTextColors()
        {
            DaySummaryActivity[] activities =
            {
                new DaySummaryActivity(ActivityIds.GetIdCard, ActivityStatus.Completed, "COMPLETED", 0, 0),
                new DaySummaryActivity(ActivityIds.Breakfast, ActivityStatus.Missed, "SKIP_BREAKFAST", -5, 0)
            };
            DayFinalizeResult summary = new DayFinalizeResult(1, 1, activities, -5, 0, 45, 50);

            summaryUi.ShowForTesting(summary);

            TextMeshProUGUI body = FindLabel(summaryUi.transform, "Body");
            string successHex = ColorUtility.ToHtmlStringRGB(UiTheme.Success);
            string dangerHex = ColorUtility.ToHtmlStringRGB(UiTheme.Danger);

            Assert.That(body.richText, Is.True);
            Assert.That(body.text, Does.Contain($"<color=#{successHex}>[x] Get Your ID Card</color>"));
            Assert.That(body.text, Does.Contain($"<color=#{dangerHex}>[X] Have Breakfast</color>"));
        }

        [Test]
        public void SummaryModal_BlocksControls_AndRendersServerOutcomes()
        {
            DaySummaryActivity[] activities =
            {
                new DaySummaryActivity(ActivityIds.GetIdCard, ActivityStatus.Completed, "COMPLETED", 0, 0),
                new DaySummaryActivity(ActivityIds.Breakfast, ActivityStatus.Missed, "SKIP_BREAKFAST", -5, 0)
            };
            DayFinalizeResult summary = new DayFinalizeResult(1, 1, activities, -5, 0, 45, 50);

            summaryUi.ShowForTesting(summary);

            Assert.That(DailySummaryUI.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);
            Assert.That(interactionController.enabled, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            TextMeshProUGUI header = FindLabel(summaryUi.transform, "Header");
            TextMeshProUGUI body = FindLabel(summaryUi.transform, "Body");
            TextMeshProUGUI totals = FindLabel(summaryUi.transform, "Totals");

            Assert.That(header.text, Does.Contain("SEMESTER 1"));
            Assert.That(header.text, Does.Contain("DAY 1 COMPLETE"));
            Assert.That(body.text, Does.Contain("[x] Get Your ID Card"));
            Assert.That(body.text, Does.Contain("[X] Have Breakfast"));
            Assert.That(body.text, Does.Contain("Aura: -5"));
            Assert.That(totals.text, Does.Contain("Aura: -5"));
        }

        [Test]
        public void MenuThenSummary_SuccessfulContinue_RestoresGameplayControls()
        {
            GameObject cameraHost = new GameObject("WorldCamera");
            CameraFollow cameraFollow = cameraHost.AddComponent<CameraFollow>();

            try
            {
                menu.Open();
                Assert.That(playerMovement.enabled, Is.False);
                Assert.That(cameraFollow.enabled, Is.False);

                // Same transfer path as End Day: close menu without restoring.
                menu.Close(restoreGameplayControls: false);
                Assert.That(playerMovement.enabled, Is.False);

                DayFinalizeResult summary = new DayFinalizeResult(
                    1,
                    1,
                    new[]
                    {
                        new DaySummaryActivity(ActivityIds.GetIdCard, ActivityStatus.Completed, "COMPLETED", 0, 0)
                    },
                    0,
                    0,
                    50,
                    50);

                summaryUi.ShowForTesting(summary);
                Assert.That(DailySummaryUI.IsOpen, Is.True);
                Assert.That(playerMovement.enabled, Is.False);

                summaryUi.CompleteSuccessfulAdvanceForTesting();

                Assert.That(DailySummaryUI.IsOpen, Is.False);
                Assert.That(GameMenuManager.IsOpen, Is.False);
                Assert.That(playerMovement.enabled, Is.True);
                Assert.That(firstPersonLook.enabled, Is.True);
                Assert.That(interactionController.enabled, Is.True);
                Assert.That(cameraFollow.enabled, Is.True);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
                Assert.That(Cursor.visible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cameraHost);
            }
        }

        [Test]
        public void FailedAdvance_KeepsModalOpen_AndGameplayBlocked()
        {
            summaryUi.ShowForTesting(new DayFinalizeResult(
                1,
                1,
                new[]
                {
                    new DaySummaryActivity(ActivityIds.Breakfast, ActivityStatus.Completed, "RICE", 5, 0)
                },
                5,
                0,
                55,
                50));

            Button continueButton = FindButton(summaryUi.transform, "Button_Continue");
            continueButton.onClick.Invoke();

            Assert.That(DailySummaryUI.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);
            Assert.That(interactionController.enabled, Is.False);
        }

        [Test]
        public void Respawn_TeleportsExistingPlayer_WithoutDuplicating()
        {
            playerObject.transform.position = new Vector3(40f, 1f, 40f);
            GameObject spawnGo = new GameObject("PrimarySpawn");
            spawnGo.transform.position = new Vector3(1f, 1f, 15f);
            spawnGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            PlayerSpawnPoint spawnPoint = spawnGo.AddComponent<PlayerSpawnPoint>();

            try
            {
                int playerCountBefore = Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).Length;
                bool teleported = PlayerSpawner.TryTeleportPlayerToSpawnPoint(playerMovement, spawnPoint);

                Assert.That(teleported, Is.True);
                Assert.That(playerObject.transform.position, Is.EqualTo(spawnGo.transform.position));
                Assert.That(
                    Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).Length,
                    Is.EqualTo(playerCountBefore));
                Assert.That(saveState.IdCardIssued, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(spawnGo);
            }
        }

        [Test]
        public void SummaryContinue_GuardsDuplicateAdvanceClicks()
        {
            DayFinalizeResult summary = new DayFinalizeResult(
                1,
                1,
                new[]
                {
                    new DaySummaryActivity(ActivityIds.Breakfast, ActivityStatus.Completed, "RICE", 5, 0)
                },
                5,
                0,
                55,
                50);

            summaryUi.ShowForTesting(summary);
            Button continueButton = FindButton(summaryUi.transform, "Button_Continue");
            Assert.That(continueButton, Is.Not.Null);
            Assert.That(continueButton.interactable, Is.True);

            // Without PlayerProgressSync, continue shows error and stays open for retry.
            continueButton.onClick.Invoke();
            Assert.That(DailySummaryUI.IsOpen, Is.True);
            Assert.That(continueButton.interactable, Is.True);

            TextMeshProUGUI error = FindLabel(summaryUi.transform, "Error");
            Assert.That(error.gameObject.activeSelf, Is.True);
            Assert.That(error.text, Does.Contain("Progress sync"));
        }

        [Test]
        public void GameMenu_NextDayButton_Exists()
        {
            menu.Open();
            Button nextDay = FindButton(menu.transform, "Button_NextDay");
            Assert.That(nextDay, Is.Not.Null);
            Assert.That(FindLabel(nextDay.transform, "Label").text, Is.EqualTo("Next Day"));
        }

        private static TextMeshProUGUI FindLabel(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        private static Button FindButton(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
