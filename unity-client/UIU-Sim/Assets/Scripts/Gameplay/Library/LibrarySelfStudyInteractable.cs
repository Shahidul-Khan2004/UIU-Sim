using System;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.UI;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Library
{
    /// <summary>
    /// Reusable study location. Attach to any table, desk, or reading area collider.
    /// Opens LibrarySelfStudyUI; does not own PDF or timer logic.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class LibrarySelfStudyInteractable : MonoBehaviour, IInteractable
    {
        public const string StudiedEnoughMessage = "You have studied enough for today. Good job.";
        public const string FinishedStudyingMessage = "You have finished studying for today. Good job.";
        public const string StudentsOnlyMessage = "Only students can study here.";
        public const string AdmissionRequiredMessage = "Complete admission before studying.";

        [SerializeField] private string prompt = "Study";

        public string InteractionPrompt => prompt;

        public string Interact()
        {
            if (LibrarySelfStudyUI.IsOpen || LibrarySelfStudyUI.BlocksGameplay
                || DialogueUI.IsOpen || GameMenuManager.IsOpen || DailySummaryUI.IsOpen
                || ClassroomChoiceUI.IsOpen || ClassroomLectureUI.IsOpen)
            {
                return null;
            }

            PlayerSaveState save = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();
            if (save != null && save.IsHydrated)
            {
                if (!string.IsNullOrEmpty(save.Role)
                    && !string.Equals(save.Role, "STUDENT", StringComparison.OrdinalIgnoreCase))
                {
                    return StudentsOnlyMessage;
                }

                if (!save.HasActiveUniversityDay)
                {
                    return AdmissionRequiredMessage;
                }
            }

            DailyActivityState activities = FindFirstObjectByType<DailyActivityState>();
            if (activities != null && activities.IsLibrarySelfStudyUsedToday)
            {
                return StudiedEnoughMessage;
            }

            LibrarySelfStudyUI.EnsureExists().ShowConfirm();
            return null;
        }
    }
}
