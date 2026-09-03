using System;
using UnityEngine;

/// <summary>
/// Tracks the player's social and academic standing.
/// Attach to the Player prefab root alongside <see cref="PlayerMovement"/> and
/// <see cref="InteractionController"/>.
/// <para>
/// Values are runtime-only — they reset when the scene reloads.
/// Persistence will be added in a later sprint via the backend API.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStats : MonoBehaviour
{
    [Header("Initial Values")]
    [SerializeField, Range(0f, 100f)] private float initialAura = 100f;
    [SerializeField, Range(0f, 100f)] private float initialReputation = 100f;

    private float aura;
    private float academicReputation;

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>Current Aura value, clamped to [0, 100].</summary>
    public float Aura => aura;

    /// <summary>Current Academic Reputation value, clamped to [0, 100].</summary>
    public float AcademicReputation => academicReputation;

    /// <summary>Raised whenever <see cref="Aura"/> changes. Payload is the new value.</summary>
    public event Action<float> OnAuraChanged;

    /// <summary>Raised whenever <see cref="AcademicReputation"/> changes. Payload is the new value.</summary>
    public event Action<float> OnReputationChanged;

    /// <summary>
    /// Adds <paramref name="delta"/> to Aura (negative values reduce it).
    /// The result is clamped to [0, 100].
    /// </summary>
    public void ModifyAura(float delta)
    {
        float previous = aura;
        aura = Mathf.Clamp(aura + delta, 0f, 100f);

        if (!Mathf.Approximately(aura, previous))
        {
            Debug.Log($"[PlayerStats] Aura: {previous:F1} → {aura:F1} (Δ{delta:+0.#;-0.#})");
            OnAuraChanged?.Invoke(aura);
        }
    }

    /// <summary>
    /// Adds <paramref name="delta"/> to Academic Reputation (negative values reduce it).
    /// The result is clamped to [0, 100].
    /// </summary>
    public void ModifyReputation(float delta)
    {
        float previous = academicReputation;
        academicReputation = Mathf.Clamp(academicReputation + delta, 0f, 100f);

        if (!Mathf.Approximately(academicReputation, previous))
        {
            Debug.Log($"[PlayerStats] Reputation: {previous:F1} → {academicReputation:F1} (Δ{delta:+0.#;-0.#})");
            OnReputationChanged?.Invoke(academicReputation);
        }
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        aura = initialAura;
        academicReputation = initialReputation;
    }
}
