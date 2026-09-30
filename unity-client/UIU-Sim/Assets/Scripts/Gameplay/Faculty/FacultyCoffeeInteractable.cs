using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Faculty-only coffee booth interaction. Attach to a cafe booth root whose child
    /// (or self) has a Collider so <see cref="InteractionController"/> can resolve
    /// this component via GetComponentInParent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacultyCoffeeInteractable : MonoBehaviour, IInteractable
    {
        public const string CappuccinoOption = "CAPPUCCINO";
        public const string CappuccinoCookieOption = "CAPPUCCINO_COOKIE";
        public const string CappuccinoCookieBrownieOption = "CAPPUCCINO_COOKIE_BROWNIE";

        [Header("Interaction")]
        [SerializeField] private string interactionPrompt = "Get Coffee";

        [SerializeField] private string boothSpeakerName = "Campus Cafe";

        [SerializeField, TextArea]
        private string greeting = "What would you like today?";

        [SerializeField] private string cappuccinoLabel = "Cappuccino";

        [SerializeField] private string cappuccinoCookieLabel = "Cappuccino + Cookie";

        [SerializeField] private string cappuccinoCookieBrownieLabel = "Cappuccino + Cookie + Brownie";

        [Header("Messages")]
        [SerializeField, TextArea]
        private string studentsDeniedMessage = "Faculty only.";

        [SerializeField, TextArea]
        private string idRequiredMessage = "You need a Faculty ID Card first.";

        [SerializeField, TextArea]
        private string classesRequiredMessage = "Coffee is available after your classes.";

        [SerializeField, TextArea]
        private string alreadyClaimedMessage = "You already got your coffee for today.";

        [SerializeField, TextArea]
        private string boothClosedMessage = "The cafe is closed right now.";

        public string InteractionPrompt => interactionPrompt;

        public string Interact()
        {
            if (DialogueUI.IsOpen)
            {
                return null;
            }

            PlayerSaveState save = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();

            if (save == null || !save.IsHydrated || !save.HasSave)
            {
                return boothClosedMessage;
            }

            if (!FacultyIdentity.Matches(save.Role))
            {
                return studentsDeniedMessage;
            }

            if (!save.IdCardIssued)
            {
                return idRequiredMessage;
            }

            FacultyProgress progress = FacultyProgress.Instance != null
                ? FacultyProgress.Instance
                : FacultyProgress.EnsureExists();

            if (progress == null || !progress.BothClassesCompleted)
            {
                return classesRequiredMessage;
            }

            if (progress.CoffeeClaimedForCurrentDay)
            {
                return alreadyClaimedMessage;
            }

            if (DialogueUI.Instance == null)
            {
                Debug.LogError("[FacultyCoffeeInteractable] DialogueUI.Instance is null.", this);
                return boothClosedMessage;
            }

            DialogueUI.Instance.Show(
                boothSpeakerName,
                greeting,
                new[]
                {
                    new DialogueUI.Choice(cappuccinoLabel, () => RequestCoffee(CappuccinoOption)),
                    new DialogueUI.Choice(cappuccinoCookieLabel, () => RequestCoffee(CappuccinoCookieOption)),
                    new DialogueUI.Choice(
                        cappuccinoCookieBrownieLabel,
                        () => RequestCoffee(CappuccinoCookieBrownieOption))
                });

            return null;
        }

        private static void RequestCoffee(string option)
        {
            FacultyProgressSync sync = FacultyProgressSync.Instance != null
                ? FacultyProgressSync.Instance
                : FacultyProgressSync.EnsureExists();
            if (sync == null)
            {
                return;
            }

            sync.RequestCoffee(option);
        }
    }
}
