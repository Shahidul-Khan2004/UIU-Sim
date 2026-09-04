using UnityEngine;

/// <summary>
/// Reusable interactable component for the two side canteen stores.
/// Implements <see cref="IInteractable"/> to offer simple ambient food orders
/// (Shawarma, Hot Dog, Fried Rice Meal, Sandwich).
/// <para>
/// Orders complete immediately with no queue, no Aura/Reputation changes,
/// and no complex inventory overhead.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class CanteenSideStore : MonoBehaviour, IInteractable
{
    [Header("Store Identity")]
    [Tooltip("Store label shown in the DialogueUI header.")]
    [SerializeField] private string storeName = "Canteen Store";

    [Tooltip("Prompt displayed when looking at this store.")]
    [SerializeField] private string prompt = "Order Food";

    [Tooltip("NPC greeting line in DialogueUI.")]
    [SerializeField, TextArea] private string greeting = "Welcome! What can I get for you?";

    [Header("Menu Items")]
    [Tooltip("Menu choice label for Shawarma.")]
    [SerializeField] private string shawarmaLabel = "Shawarma";

    [Tooltip("Menu choice label for Hot Dog.")]
    [SerializeField] private string hotDogLabel = "Hot Dog";

    [Tooltip("Menu choice label for Fried Rice Meal.")]
    [SerializeField] private string friedRiceMealLabel = "Fried Rice Meal";

    [Tooltip("Menu choice label for Sandwich.")]
    [SerializeField] private string sandwichLabel = "Sandwich";

    [Header("Feedback")]
    [Tooltip("Response text displayed/logged when the order is prepared.")]
    [SerializeField, TextArea] private string orderCompleteResponse = "Here's your order.";

    [Tooltip("Message displayed if the store/dialogue cannot be opened.")]
    [SerializeField, TextArea] private string storeClosedMessage = "The store is closed right now.";

    private InteractionFeedback feedback;

    // ── IInteractable ──────────────────────────────────────────────────

    public string InteractionPrompt => prompt;

    public string Interact()
    {
        // Guard: Dialogue or queue UI already active
        if (DialogueUI.IsOpen || CanteenQueueUI.IsOpen)
        {
            return null;
        }

        if (DialogueUI.Instance == null)
        {
            Debug.LogError("[CanteenSideStore] DialogueUI.Instance is null.", this);
            return storeClosedMessage;
        }

        DialogueUI.Instance.Show(
            storeName,
            greeting,
            new[]
            {
                new DialogueUI.Choice(shawarmaLabel, () => OnFoodOrdered(shawarmaLabel)),
                new DialogueUI.Choice(hotDogLabel, () => OnFoodOrdered(hotDogLabel)),
                new DialogueUI.Choice(friedRiceMealLabel, () => OnFoodOrdered(friedRiceMealLabel)),
                new DialogueUI.Choice(sandwichLabel, () => OnFoodOrdered(sandwichLabel))
            }
        );

        return null;
    }

    // ── Order Handling ─────────────────────────────────────────────────

    private void OnFoodOrdered(string itemName)
    {
        if (feedback != null)
        {
            feedback.PlaySuccess();
        }

        Debug.Log($"[CanteenSideStore] '{storeName}' prepared order: {itemName}. {orderCompleteResponse}");
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        feedback = GetComponent<InteractionFeedback>();
    }
}
