using UnityEngine;

namespace UIU.Simulator.Gameplay.Advisor
{
    /// <summary>
    /// Attach to any NPC GameObject (such as a Capsule on the Third Floor) with a Collider.
    /// Interacts with the player via InteractionController and opens the AdvisorUI.
    /// Contains no network or Gemini provider logic.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class AcademicAdvisorInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string interactionPrompt = "Talk to Academic Advisor";

        public string InteractionPrompt => interactionPrompt;

        public string Interact()
        {
            AdvisorUI advisorUI = AdvisorUI.Instance != null ? AdvisorUI.Instance : AdvisorUI.EnsureExists();
            if (advisorUI != null)
            {
                advisorUI.Show();
            }
            else
            {
                Debug.LogError("[AcademicAdvisorInteractable] Failed to initialize or locate AdvisorUI.");
            }

            return null;
        }
    }
}
