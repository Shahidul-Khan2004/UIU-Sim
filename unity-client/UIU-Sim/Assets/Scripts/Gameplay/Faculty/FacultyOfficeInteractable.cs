using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Faculty workstation. Attach to FacultyDesk_Room335
    /// (or any collider that represents the Faculty computer).
    /// Uses the existing InteractionController / InteractionUI path.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FacultyOfficeInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string interactionPrompt = "Use Faculty Computer";
        [SerializeField, TextArea] private string accessDeniedMessage =
            "You need faculty access to use this workstation.";

        public string InteractionPrompt => interactionPrompt;

        public string Interact()
        {
            PlayerSaveState save = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();

            if (save == null
                || !save.IsHydrated
                || !save.HasSave
                || !FacultyIdentity.Matches(save.Role)
                || !save.IdCardIssued)
            {
                return accessDeniedMessage;
            }

            FacultyPortalUI portal = FacultyPortalUI.Instance != null
                ? FacultyPortalUI.Instance
                : FacultyPortalUI.EnsureExists();
            if (portal != null)
            {
                portal.Show();
            }

            FacultyProgressSync sync = FacultyProgressSync.Instance != null
                ? FacultyProgressSync.Instance
                : FacultyProgressSync.EnsureExists();
            if (sync != null && Application.isPlaying)
            {
                sync.RequestComputerUse();
            }

            return null;
        }
    }
}
