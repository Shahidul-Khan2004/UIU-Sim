using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal screen-space HUD displaying the player's Aura and Academic Reputation.
/// Built at runtime — no canvas prefab required.
/// Attach to the Player prefab root alongside <see cref="PlayerStats"/>.
/// <para>
/// Canvas sorting order 50 places this behind InteractionUI (100) and DialogueUI (200).
/// Reacts to <see cref="PlayerStats"/> change events with temporary delta feedback.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStats))]
public sealed class StatsHUD : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private float fontSize = 20f;
    [SerializeField] private float feedbackFontSize = 18f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color positiveDeltaColor = new Color(0.35f, 1f, 0.45f, 1f);
    [SerializeField] private Color negativeDeltaColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);

    [Header("Layout")]
    [Tooltip("Offset in pixels from top-left screen corner.")]
    [SerializeField] private Vector2 screenOffset = new Vector2(24f, -24f);

    [Header("Feedback")]
    [Tooltip("Total duration in seconds before change feedback disappears.")]
    [SerializeField] private float feedbackHoldDuration = 1.4f;
    [SerializeField] private float feedbackFadeDuration = 0.6f;

    private PlayerStats playerStats;

    // UI elements
    private GameObject panelRoot;
    private TextMeshProUGUI auraText;
    private TextMeshProUGUI academicText;
    private TextMeshProUGUI auraFeedbackText;
    private TextMeshProUGUI academicFeedbackText;

    // State tracking
    private float lastAura;
    private float lastReputation;
    private Coroutine auraFeedbackRoutine;
    private Coroutine academicFeedbackRoutine;

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        BuildUI();
    }

    private void OnEnable()
    {
        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        if (playerStats != null)
        {
            lastAura = playerStats.Aura;
            lastReputation = playerStats.AcademicReputation;

            playerStats.OnAuraChanged += HandleAuraChanged;
            playerStats.OnAcademicReputationChanged += HandleReputationChanged;

            UpdateAuraDisplay(lastAura);
            UpdateReputationDisplay(lastReputation);
        }
    }

    private void OnDisable()
    {
        if (playerStats != null)
        {
            playerStats.OnAuraChanged -= HandleAuraChanged;
            playerStats.OnAcademicReputationChanged -= HandleReputationChanged;
        }

        StopAllFeedback();
    }

    // ── Event Handlers ─────────────────────────────────────────────────

    private void HandleAuraChanged(float newAura)
    {
        float delta = newAura - lastAura;
        lastAura = newAura;
        UpdateAuraDisplay(newAura);

        if (Mathf.Abs(delta) > 0.001f)
        {
            TriggerAuraFeedback(delta);
        }
    }

    private void HandleReputationChanged(float newReputation)
    {
        float delta = newReputation - lastReputation;
        lastReputation = newReputation;
        UpdateReputationDisplay(newReputation);

        if (Mathf.Abs(delta) > 0.001f)
        {
            TriggerAcademicFeedback(delta);
        }
    }

    // ── Display Updates ────────────────────────────────────────────────

    private void UpdateAuraDisplay(float value)
    {
        if (auraText != null)
        {
            auraText.text = $"AURA: {FormatStatValue(value)}";
        }
    }

    private void UpdateReputationDisplay(float value)
    {
        if (academicText != null)
        {
            academicText.text = $"ACADEMIC: {FormatStatValue(value)}";
        }
    }

    // ── Temporary Feedback ─────────────────────────────────────────────

    private void TriggerAuraFeedback(float delta)
    {
        if (auraFeedbackRoutine != null)
        {
            StopCoroutine(auraFeedbackRoutine);
        }

        auraFeedbackRoutine = StartCoroutine(ShowFeedbackRoutine(
            auraFeedbackText,
            FormatDelta("AURA", delta),
            delta >= 0f ? positiveDeltaColor : negativeDeltaColor));
    }

    private void TriggerAcademicFeedback(float delta)
    {
        if (academicFeedbackRoutine != null)
        {
            StopCoroutine(academicFeedbackRoutine);
        }

        academicFeedbackRoutine = StartCoroutine(ShowFeedbackRoutine(
            academicFeedbackText,
            FormatDelta("ACADEMIC", delta),
            delta >= 0f ? positiveDeltaColor : negativeDeltaColor));
    }

    private IEnumerator ShowFeedbackRoutine(TextMeshProUGUI label, string text, Color targetColor)
    {
        if (label == null)
        {
            yield break;
        }

        label.text = text;
        label.color = targetColor;
        label.gameObject.SetActive(true);

        yield return new WaitForSeconds(feedbackHoldDuration);

        float elapsed = 0f;
        while (elapsed < feedbackFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / feedbackFadeDuration);
            label.color = new Color(targetColor.r, targetColor.g, targetColor.b, alpha);
            yield return null;
        }

        label.gameObject.SetActive(false);
    }

    private void StopAllFeedback()
    {
        if (auraFeedbackRoutine != null)
        {
            StopCoroutine(auraFeedbackRoutine);
            auraFeedbackRoutine = null;
        }

        if (academicFeedbackRoutine != null)
        {
            StopCoroutine(academicFeedbackRoutine);
            academicFeedbackRoutine = null;
        }

        if (auraFeedbackText != null)
        {
            auraFeedbackText.gameObject.SetActive(false);
        }

        if (academicFeedbackText != null)
        {
            academicFeedbackText.gameObject.SetActive(false);
        }
    }

    // ── UI Construction ────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasGo = new GameObject("StatsHUDCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // Below InteractionUI (100) and DialogueUI (200)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ── Panel (Top-Left) ──
        panelRoot = new GameObject("StatsPanel");
        panelRoot.transform.SetParent(canvasGo.transform, false);

        RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = screenOffset;

        Image bg = panelRoot.AddComponent<Image>();
        bg.color = backgroundColor;
        bg.raycastTarget = false;

        VerticalLayoutGroup layout = panelRoot.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelRoot.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── Stat Labels ──
        auraText = CreateLabel(panelRoot.transform, "AuraText", fontSize, textColor, FontStyles.Bold);
        academicText = CreateLabel(panelRoot.transform, "AcademicText", fontSize, textColor, FontStyles.Bold);

        // ── Feedback Labels ──
        auraFeedbackText = CreateLabel(panelRoot.transform, "AuraFeedbackText", feedbackFontSize, positiveDeltaColor, FontStyles.Bold);
        auraFeedbackText.gameObject.SetActive(false);

        academicFeedbackText = CreateLabel(panelRoot.transform, "AcademicFeedbackText", feedbackFontSize, positiveDeltaColor, FontStyles.Bold);
        academicFeedbackText.gameObject.SetActive(false);
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, float size, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;

        return tmp;
    }

    // ── Formatting Helpers ─────────────────────────────────────────────

    private static string FormatStatValue(float value)
    {
        return Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("F1");
    }

    private static string FormatDelta(string statName, float delta)
    {
        string sign = delta > 0f ? "+" : "";
        string formattedValue = Mathf.Approximately(delta, Mathf.Round(delta))
            ? Mathf.RoundToInt(delta).ToString()
            : delta.ToString("F1");
        return $"{statName} {sign}{formattedValue}";
    }
}
