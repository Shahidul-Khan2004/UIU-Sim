using System;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.UI;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Library
{
    /// <summary>Optional station: start, launch an adapter, submit only its score, then release the modal.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class LibraryStudyInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Study";
        [Tooltip("Production minigame component implementing ILibraryStudyMinigame.")]
        [SerializeField] private MonoBehaviour minigameAdapter;
        private ILibraryStudyMinigame minigame;
        private ILibraryStudyMinigame activeMinigame;
        private ILibraryStudyProgressSync progressSync;
        private LibraryStudyUI controlOwner;
        private bool isBusy, awaitingResult, isClosing;
        private int sessionVersion;
        private int pendingScore = -1;
        private int pendingDay;

        public string InteractionPrompt => prompt;
        private static int CurrentDay => PlayerSaveState.Instance != null ? PlayerSaveState.Instance.CurrentDay : 1;

        public void SetMinigameForTesting(ILibraryStudyMinigame adapter) => minigame = adapter;
        public void SetProgressSyncForTesting(ILibraryStudyProgressSync sync) => progressSync = sync;

        public string Interact()
        {
            if (isBusy || LibraryStudyUI.BlocksGameplay || DialogueUI.IsOpen || GameMenuManager.IsOpen
                || DailySummaryUI.IsOpen || ClassroomChoiceUI.IsOpen || ClassroomLectureUI.IsOpen) return null;
            DailyActivityState activities = FindFirstObjectByType<DailyActivityState>();
            if (activities != null && activities.IsLibraryStudyCompleted)
            {
                pendingScore = -1;
                return LibraryStudyUI.StudiedEnoughMessage;
            }
            if (pendingDay != CurrentDay) pendingScore = -1;
            controlOwner = LibraryStudyUI.EnsureExists();
            if (pendingScore >= 0) controlOwner.ShowRetry(pendingScore, RetrySave, () => controlOwner = null);
            else controlOwner.Show(BeginStudy, () => controlOwner = null);
            return null;
        }

        private void BeginStudy()
        {
            if (isBusy) return;
            ILibraryStudyMinigame adapter = ResolveMinigame();
            if (!adapter.IsAvailable)
            {
                EndSession(LibraryStudyUI.NotInstalledMessage);
                return;
            }
            EnsureSync();
            if (progressSync == null)
            {
                EndSession("Progress sync is not available.");
                return;
            }
            isBusy = true;
            int version = ++sessionVersion;
            progressSync.RequestLibraryStudyStart(
                onSuccess: result =>
                {
                    if (!IsCurrent(version)) return;
                    if (result.AlreadyCompleted || result.Record.Status == ActivityStatus.Completed)
                    {
                        EndSession(LibraryStudyUI.StudiedEnoughMessage);
                        return;
                    }
                    pendingDay = result.Record.DayNumber;
                    activeMinigame = adapter;
                    awaitingResult = true;
                    try
                    {
                        adapter.Launch(value => OnMinigameFinished(version, value));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[LibraryStudy] Minigame launch failed: {exception.Message}");
                        awaitingResult = false;
                        EndSession("Rocket Study could not open. Please try again.");
                    }
                },
                onFailure: () => { if (IsCurrent(version)) EndSession(); });
        }

        private void OnMinigameFinished(int version, LibraryStudyMinigameResult result)
        {
            if (!IsCurrent(version) || !awaitingResult) return;
            awaitingResult = false;
            if (!result.Completed)
            {
                EndSession();
                return;
            }
            if (result.Score < 0 || result.Score > 100)
            {
                EndSession("The minigame returned an invalid study score. Please try again.");
                return;
            }
            pendingScore = result.Score;
            SubmitScore(version);
        }

        private void RetrySave()
        {
            if (isBusy) return;
            if (pendingScore < 0 || pendingDay != CurrentDay)
            {
                pendingScore = -1;
                EndSession("That study attempt belongs to a previous day.");
                return;
            }
            isBusy = true;
            SubmitScore(++sessionVersion);
        }

        private void SubmitScore(int version)
        {
            EnsureSync();
            if (progressSync == null)
            {
                EndSession("Progress sync is not available. Interact again to retry saving your score.");
                return;
            }
            progressSync.RequestLibraryStudyComplete(pendingScore,
                onSuccess: result =>
                {
                    if (!IsCurrent(version)) return;
                    pendingScore = -1;
                    string title = result.AlreadyCompleted ? "Library study already saved!" : "Library study complete!";
                    EndSession($"{title}\nScore: {result.Score}/100\nAcademic Reputation +{result.Record.ReputationDelta}");
                },
                onFailure: () =>
                {
                    if (IsCurrent(version)) EndSession("Could not save library study. Interact again to RETRY SAVE with the same score.");
                });
        }

        private bool IsCurrent(int version) => this != null && isActiveAndEnabled && isBusy && sessionVersion == version;

        private void EndSession(string notification = null)
        {
            if (isClosing) return;
            isClosing = true;
            LibraryStudyUI ui = controlOwner;
            if (ui != null && LibraryStudyUI.IsOpen) ui.Hide(restoreGameplay: false);
            ILibraryStudyMinigame adapter = activeMinigame;
            activeMinigame = null;
            awaitingResult = false;
            // Invalidate callbacks immediately, including duplicate results while unloading.
            sessionVersion++;
            Action finish = () =>
            {
                if (this != null)
                {
                    isBusy = false;
                    isClosing = false;
                    controlOwner = null;
                }
                if (ui != null) ui.RestoreGameplayControlsIfOwned();
                if (!string.IsNullOrEmpty(notification)) SystemNotificationUI.Show(notification, 5f);
            };
            if (adapter is ILibraryStudyMinigameSession session) session.Close(finish);
            else finish();
        }

        private void OnDisable()
        {
            if (isBusy || (controlOwner != null && LibraryStudyUI.BlocksGameplay)) EndSession();
        }

        private ILibraryStudyMinigame ResolveMinigame()
        {
            if (minigame != null) return minigame;
            return minigameAdapter is ILibraryStudyMinigame assigned ? assigned : UnavailableLibraryStudyMinigame.Instance;
        }

        private void EnsureSync()
        {
            if (progressSync == null) progressSync = FindFirstObjectByType<PlayerProgressSync>();
        }
    }
}
