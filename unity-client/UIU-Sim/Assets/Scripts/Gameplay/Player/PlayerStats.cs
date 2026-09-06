using System;
using UnityEngine;

public enum StatUpdateSource
{
    InitialHydration,
    GameplayMutation
}

/// <summary>
/// Tracks the player's social and academic standing.
/// Attach to the Player prefab root alongside PlayerMovement, InteractionController, and PlayerProgressSync.
/// Local source of truth for stats hydrated and confirmed by the backend PostgreSQL database.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStats : MonoBehaviour
{
    [Header("Initial Values")]
    [SerializeField, Range(0f, 100f)] private float initialAura = 50f;
    [SerializeField, Range(0f, 100f)] private float initialReputation = 50f;

    private float aura;
    private float academicReputation;

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>Current Aura value, clamped to [0, 100].</summary>
    public float Aura => aura;

    /// <summary>Current Academic Reputation value, clamped to [0, 100].</summary>
    public float AcademicReputation => academicReputation;

    /// <summary>Raised whenever Aura updates, carrying the update source.</summary>
    public event Action<float, StatUpdateSource> OnAuraUpdated;

    /// <summary>Raised whenever Academic Reputation updates, carrying the update source.</summary>
    public event Action<float, StatUpdateSource> OnAcademicReputationUpdated;

    /// <summary>Legacy event raised whenever Aura changes. Payload is the new value.</summary>
    public event Action<float> OnAuraChanged;

    /// <summary>Legacy event raised whenever Academic Reputation changes. Payload is the new value.</summary>
    public event Action<float> OnAcademicReputationChanged;

    /// <summary>
    /// Applies authoritative server state.
    /// Distinguishes InitialHydration (suppresses delta popups) from GameplayMutation (shows delta popups).
    /// </summary>
    public void ApplyServerState(float newAura, float newReputation, StatUpdateSource source)
    {
        float previousAura = aura;
        float previousReputation = academicReputation;

        aura = Mathf.Clamp(newAura, 0f, 100f);
        academicReputation = Mathf.Clamp(newReputation, 0f, 100f);

        Debug.Log($"[PlayerStats] ApplyServerState ({source}): Aura {previousAura:F1} → {aura:F1}, Reputation {previousReputation:F1} → {academicReputation:F1}");

        OnAuraUpdated?.Invoke(aura, source);
        OnAcademicReputationUpdated?.Invoke(academicReputation, source);

        if (!Mathf.Approximately(aura, previousAura))
        {
            OnAuraChanged?.Invoke(aura);
        }

        if (!Mathf.Approximately(academicReputation, previousReputation))
        {
            OnAcademicReputationChanged?.Invoke(academicReputation);
        }
    }

    /// <summary>
    /// Local stat modification for test seams / standalone testing.
    /// In production gameplay, deltas are requested through PlayerProgressSync.
    /// </summary>
    public void ModifyAura(float delta)
    {
        ApplyServerState(aura + delta, academicReputation, StatUpdateSource.GameplayMutation);
    }

    /// <summary>
    /// Local stat modification for test seams / standalone testing.
    /// In production gameplay, deltas are requested through PlayerProgressSync.
    /// </summary>
    public void ModifyReputation(float delta)
    {
        ApplyServerState(aura, academicReputation + delta, StatUpdateSource.GameplayMutation);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        aura = initialAura;
        academicReputation = initialReputation;
    }
}
