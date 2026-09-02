using UnityEngine;

/// <summary>
/// Drop-in test interactable. Place on any GameObject with a collider
/// to verify that <see cref="InteractionController"/> detection and input work.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class InteractableObject : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string prompt = "Interact";

    [Header("Response")]
    [SerializeField, TextArea] private string message = "You interacted with this object!";

    public string InteractionPrompt => prompt;

    public string Interact()
    {
        Debug.Log($"[InteractableObject] {gameObject.name}: {message}");
        return message;
    }
}
