using UnityEngine;

/// <summary>
/// Receptionist NPC. Implements <see cref="IInteractable"/> so the player can talk to her
/// via the standard interaction system.
/// <para>
/// State gating (evaluated on every <see cref="Interact"/> call):
/// <list type="bullet">
///   <item>No ID problem → short dismissal response; no dialogue, no Aura change.</item>
///   <item>Has ID problem, but already holds a temporary ID → tells the player to use it.</item>
///   <item>Has ID problem, no temporary ID → opens a <see cref="DialogueUI"/> with two choices.</item>
/// </list>
/// </para>
/// <para>
/// The Receptionist does <b>not</b> call <see cref="PlayerInventory.ResolveIDProblem"/>.
/// The ID problem remains active while the player carries the temporary card.
/// Only <see cref="PlayerInventory.ConsumeTemporaryID"/> (called by the scanner on success)
/// resolves the problem and re-enables the permanent ID.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class Receptionist : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [Tooltip("Label shown to the player when looking at the receptionist.")]
    [SerializeField] private string prompt = "Talk to Receptionist";

    [Tooltip("Name displayed in the dialogue header.")]
    [SerializeField] private string speakerName = "Receptionist";

    // ── Lazy references ───────────────────────────────────────────────

    private PlayerInventory playerInventory;
    private PlayerStats     playerStats;

    // ── IInteractable ─────────────────────────────────────────────────

    public string InteractionPrompt => prompt;

    public string Interact()
    {
        // Guard: dialogue is already open (e.g. player pressed E twice quickly).
        if (DialogueUI.IsOpen)
        {
            return null;
        }

        if (!EnsureReferences())
        {
            return "The receptionist isn't available right now.";
        }

        // ── State: no ID problem ──────────────────────────────────────
        if (!playerInventory.HasIDProblem)
        {
            Debug.Log("[Receptionist] Player has no ID problem — dismissing.");
            return "Your ID card seems to be working fine.";
        }

        // ── State: already holds a temporary ID ───────────────────────
        if (playerInventory.TemporaryIDCount > 0)
        {
            Debug.Log("[Receptionist] Player already holds a temporary ID — not issuing another.");
            return "You already have a temporary ID. Use it at the scanner first.";
        }

        // ── State: has ID problem, no temporary ID → show choices ─────
        Debug.Log("[Receptionist] Opening dialogue — player has ID problem and no temporary ID.");

        DialogueUI.Instance.Show(
            speakerName,
            "Oh, you don't have your ID card, huh?",
            new[]
            {
                new DialogueUI.Choice(
                    "I forgot my ID card.",
                    OnForgotID),

                new DialogueUI.Choice(
                    "I lost my ID card.",
                    OnLostID),
            }
        );

        // Return null so InteractionController does not display a response message.
        return null;
    }

    // ── Choice callbacks ──────────────────────────────────────────────

    /// <summary>
    /// Player forgot their ID. Minor Aura penalty (-5). Issues a temporary ID.
    /// Does NOT resolve the ID problem — that happens when the temporary card is scanned.
    /// </summary>
    private void OnForgotID()
    {
        playerStats.ModifyAura(-5f);
        playerInventory.AddTemporaryID();
        Debug.Log("[Receptionist] Forgot ID chosen. Aura -5. Temporary ID issued. HasIDProblem remains true until scanned.");
    }

    /// <summary>
    /// Player lost their ID. Larger Aura penalty (-10). Issues a temporary ID.
    /// Does NOT resolve the ID problem — that happens when the temporary card is scanned.
    /// </summary>
    private void OnLostID()
    {
        playerStats.ModifyAura(-10f);
        playerInventory.AddTemporaryID();
        Debug.Log("[Receptionist] Lost ID chosen. Aura -10. Temporary ID issued. HasIDProblem remains true until scanned.");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Lazily resolves PlayerInventory, PlayerStats, and DialogueUI.
    /// Mirrors the same pattern used by <see cref="IDScanner"/>.
    /// </summary>
    private bool EnsureReferences()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
        }

        if (playerInventory == null)
        {
            Debug.LogError(
                "[Receptionist] PlayerInventory not found in scene. " +
                "Ensure the Player prefab has a PlayerInventory component.",
                this);
            return false;
        }

        if (playerStats == null)
        {
            Debug.LogError(
                "[Receptionist] PlayerStats not found in scene. " +
                "Ensure the Player prefab has a PlayerStats component.",
                this);
            return false;
        }

        if (DialogueUI.Instance == null)
        {
            Debug.LogError(
                "[Receptionist] DialogueUI.Instance is null. " +
                "Add the DialogueUI component to the Player prefab root.",
                this);
            return false;
        }

        return true;
    }
}
