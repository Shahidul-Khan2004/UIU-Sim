/// <summary>
/// Contract for any object the player can interact with.
/// Attach a concrete implementation to a GameObject with a collider
/// so the <see cref="InteractionController"/> raycast can detect it.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Short text shown to the player when looking at this object (e.g. "Open Door", "Talk").
    /// </summary>
    string InteractionPrompt { get; }

    /// <summary>
    /// Called once when the player performs the Interact action while this object is targeted.
    /// </summary>
    /// <returns>A response message to display on screen, or null for no message.</returns>
    string Interact();
}
