using System.Collections;
using UnityEngine;

/// <summary>
/// Scanner-specific visual feedback: real-time Light color changes and body mesh
/// emission flashes. Companion to <see cref="InteractionFeedback"/> which handles
/// audio and simple LED color.
/// <para>
/// This is NOT a general-purpose component — it exists because the ID scanner
/// has richer visual requirements (a dedicated Light source, body glow) that other
/// interactables do not need.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class ScannerVisuals : MonoBehaviour
{
    [Header("Light")]
    [Tooltip("Point or Spot Light that illuminates the scan area.")]
    [SerializeField] private Light indicatorLight;

    [SerializeField] private Color successLightColor = Color.green;
    [SerializeField] private Color failureLightColor = Color.red;
    [SerializeField] private Color idleLightColor = Color.white;

    [Tooltip("Seconds the light stays in the success/failure color before resetting.")]
    [SerializeField, Min(0.1f)] private float lightFlashDuration = 2f;

    [Header("Emission")]
    [Tooltip("The scanner body mesh. Its emission color is flashed on scan results.")]
    [SerializeField] private Renderer scannerBody;

    [SerializeField] private Color successEmission = Color.green;
    [SerializeField] private Color failureEmission = Color.red;
    [SerializeField] private Color idleEmission = Color.gray;

    private MaterialPropertyBlock propertyBlock;
    private Coroutine resetCoroutine;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>
    /// Flashes the light and body emission green, then resets after <see cref="lightFlashDuration"/>.
    /// </summary>
    public void ShowSuccess()
    {
        Flash(successLightColor, successEmission);
    }

    /// <summary>
    /// Flashes the light and body emission red, then resets after <see cref="lightFlashDuration"/>.
    /// </summary>
    public void ShowFailure()
    {
        Flash(failureLightColor, failureEmission);
    }

    /// <summary>
    /// Immediately resets the light and emission to their idle state.
    /// </summary>
    public void ResetToIdle()
    {
        CancelPendingReset();
        ApplyState(idleLightColor, idleEmission);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ApplyState(idleLightColor, idleEmission);
    }

    // ── Internals ──────────────────────────────────────────────────────

    private void Flash(Color lightColor, Color emissionColor)
    {
        CancelPendingReset();
        ApplyState(lightColor, emissionColor);
        resetCoroutine = StartCoroutine(ResetAfterDelay());
    }

    private void ApplyState(Color lightColor, Color emissionColor)
    {
        if (indicatorLight != null)
        {
            indicatorLight.color = lightColor;
        }

        if (scannerBody != null)
        {
            scannerBody.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, emissionColor);
            scannerBody.SetPropertyBlock(propertyBlock);
        }
    }

    private IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(lightFlashDuration);
        ApplyState(idleLightColor, idleEmission);
        resetCoroutine = null;
    }

    private void CancelPendingReset()
    {
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }
    }
}
