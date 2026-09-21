using System.Collections;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal screen-space HUD displaying the player's Aura, Academic Reputation,
/// and stacked objectives driven by <see cref="DailyActivityState"/>
/// (GET_ID_CARD first, then BREAKFAST).
/// Built at runtime — no canvas prefab required.
/// Attach to the Player prefab root alongside <see cref="PlayerStats"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStats))]
public sealed class StatsHUD : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private float fontSize = 20f;
    [SerializeField] private float feedbackFontSize = 18f;
    [SerializeField] private float objectiveTitleSize = 18f;
    [SerializeField] private float objectiveBodySize = 14f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color positiveDeltaColor = UiTheme.BrightOrange;
    [SerializeField] private Color negativeDeltaColor = UiTheme.Red;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);

    [Header("Objective Copy — ID Card (first)")]
    [SerializeField] private string idCardObjectiveTitle = "Get Your ID Card";

    [SerializeField, TextArea]
    private string idCardObjectiveDescription = "Visit the receptionist to receive your university ID card.";

    [Header("Objective Copy — Breakfast")]
    [SerializeField] private string breakfastObjectiveTitle = "Have Breakfast";

    [SerializeField, TextArea]
    private string breakfastObjectiveDescription = "Go to Neptune in the canteen to get your breakfast.";

    [Header("Layout")]
    [SerializeField] private Vector2 screenOffset = new Vector2(24f, -24f);

    [Header("Feedback")]
    [SerializeField] private float feedbackHoldDuration = 1.4f;
    [SerializeField] private float feedbackFadeDuration = 0.6f;

    private PlayerStats playerStats;
    private DailyActivityState dailyActivityState;

    private GameObject panelRoot;
    private TextMeshProUGUI auraText;
    private TextMeshProUGUI academicText;
    private TextMeshProUGUI auraFeedbackText;
    private TextMeshProUGUI academicFeedbackText;

    private TextMeshProUGUI idMarkerText;
    private TextMeshProUGUI idTitleText;
    private TextMeshProUGUI idDescriptionText;

    private GameObject breakfastObjectiveRoot;
    private TextMeshProUGUI breakfastMarkerText;
    private TextMeshProUGUI breakfastTitleText;
    private TextMeshProUGUI breakfastDescriptionText;

    private float lastAura;
    private float lastReputation;
    private Coroutine auraFeedbackRoutine;
    private Coroutine academicFeedbackRoutine;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        dailyActivityState = GetComponent<DailyActivityState>();
        BuildUI();
    }

    private void OnEnable()
    {
        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        if (dailyActivityState == null)
        {
            dailyActivityState = GetComponent<DailyActivityState>();
        }

        if (dailyActivityState == null)
        {
            dailyActivityState = FindFirstObjectByType<DailyActivityState>();
        }

        if (playerStats != null)
        {
            lastAura = playerStats.Aura;
            lastReputation = playerStats.AcademicReputation;

            playerStats.OnAuraUpdated += HandleAuraUpdated;
            playerStats.OnAcademicReputationUpdated += HandleReputationUpdated;

            UpdateAuraDisplay(lastAura);
            UpdateReputationDisplay(lastReputation);
        }

        if (dailyActivityState != null)
        {
            dailyActivityState.OnGetIdCardStatusChanged += RefreshObjectiveDisplay;
            dailyActivityState.OnBreakfastStatusChanged += RefreshObjectiveDisplay;
            dailyActivityState.OnActivitiesReset += RefreshObjectiveDisplay;
        }

        RefreshObjectiveDisplay();
    }

    private void OnDisable()
    {
        if (playerStats != null)
        {
            playerStats.OnAuraUpdated -= HandleAuraUpdated;
            playerStats.OnAcademicReputationUpdated -= HandleReputationUpdated;
        }

        if (dailyActivityState != null)
        {
            dailyActivityState.OnGetIdCardStatusChanged -= RefreshObjectiveDisplay;
            dailyActivityState.OnBreakfastStatusChanged -= RefreshObjectiveDisplay;
            dailyActivityState.OnActivitiesReset -= RefreshObjectiveDisplay;
        }

        StopAllFeedback();
    }

    private void HandleAuraUpdated(float newAura, StatUpdateSource source)
    {
        float delta = newAura - lastAura;
        lastAura = newAura;
        UpdateAuraDisplay(newAura);

        if (source == StatUpdateSource.GameplayMutation && Mathf.Abs(delta) > 0.001f)
        {
            TriggerAuraFeedback(delta);
        }
    }

    private void HandleReputationUpdated(float newReputation, StatUpdateSource source)
    {
        float delta = newReputation - lastReputation;
        lastReputation = newReputation;
        UpdateReputationDisplay(newReputation);

        if (source == StatUpdateSource.GameplayMutation && Mathf.Abs(delta) > 0.001f)
        {
            TriggerAcademicFeedback(delta);
        }
    }

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

    private void RefreshObjectiveDisplay()
    {
        if (idMarkerText == null || idTitleText == null || idDescriptionText == null)
        {
            return;
        }

        string idTitle = dailyActivityState != null ? dailyActivityState.GetIdCardTitle : idCardObjectiveTitle;
        string idDescription = dailyActivityState != null
            ? dailyActivityState.GetIdCardDescription
            : idCardObjectiveDescription;
        ActivityStatus idStatus = dailyActivityState != null
            ? dailyActivityState.GetIdCardStatus
            : ActivityStatus.Pending;

        ApplyObjectiveVisual(
            idStatus,
            idTitle,
            idDescription,
            idMarkerText,
            idTitleText,
            idDescriptionText);

        bool showBreakfast = idStatus != ActivityStatus.Pending;
        if (breakfastObjectiveRoot != null)
        {
            breakfastObjectiveRoot.SetActive(showBreakfast);
        }

        if (!showBreakfast || breakfastMarkerText == null)
        {
            return;
        }

        string title = dailyActivityState != null ? dailyActivityState.BreakfastTitle : breakfastObjectiveTitle;
        string description = dailyActivityState != null
            ? dailyActivityState.BreakfastDescription
            : breakfastObjectiveDescription;
        ActivityStatus status = dailyActivityState != null
            ? dailyActivityState.BreakfastStatus
            : ActivityStatus.Pending;

        ApplyObjectiveVisual(
            status,
            title,
            description,
            breakfastMarkerText,
            breakfastTitleText,
            breakfastDescriptionText);
    }

    private static void ApplyObjectiveVisual(
        ActivityStatus status,
        string title,
        string description,
        TextMeshProUGUI marker,
        TextMeshProUGUI titleLabel,
        TextMeshProUGUI descriptionLabel)
    {
        titleLabel.text = title;

        switch (status)
        {
            case ActivityStatus.Completed:
                marker.text = "[x]";
                marker.color = UiTheme.BrightOrange;
                titleLabel.color = UiTheme.White;
                descriptionLabel.gameObject.SetActive(false);
                break;
            case ActivityStatus.Missed:
                marker.text = "[X]";
                marker.color = UiTheme.Red;
                titleLabel.color = UiTheme.Grey;
                descriptionLabel.gameObject.SetActive(false);
                break;
            default:
                marker.text = "[ ]";
                marker.color = UiTheme.BrightOrange;
                titleLabel.color = UiTheme.White;
                descriptionLabel.text = description;
                descriptionLabel.color = UiTheme.Grey;
                descriptionLabel.gameObject.SetActive(true);
                break;
        }
    }

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

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("StatsHUDCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

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

        auraText = CreateLabel(panelRoot.transform, "AuraText", fontSize, textColor, FontStyles.Bold);
        academicText = CreateLabel(panelRoot.transform, "AcademicText", fontSize, textColor, FontStyles.Bold);

        auraFeedbackText = CreateLabel(panelRoot.transform, "AuraFeedbackText", feedbackFontSize, positiveDeltaColor, FontStyles.Bold);
        auraFeedbackText.gameObject.SetActive(false);

        academicFeedbackText = CreateLabel(panelRoot.transform, "AcademicFeedbackText", feedbackFontSize, positiveDeltaColor, FontStyles.Bold);
        academicFeedbackText.gameObject.SetActive(false);

        CreateDivider(panelRoot.transform);
        CreateLabel(panelRoot.transform, "ObjectiveHeader", 12f, UiTheme.Grey, FontStyles.Bold).text = "OBJECTIVE";

        BuildObjectiveRow(panelRoot.transform, "IdCard", out idMarkerText, out idTitleText);
        idDescriptionText = CreateLabel(panelRoot.transform, "IdCardDescription", objectiveBodySize, UiTheme.Grey, FontStyles.Normal);
        idDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        idDescriptionText.text = idCardObjectiveDescription;
        idTitleText.text = idCardObjectiveTitle;

        breakfastObjectiveRoot = new GameObject("BreakfastObjectiveBlock");
        breakfastObjectiveRoot.transform.SetParent(panelRoot.transform, false);
        VerticalLayoutGroup breakfastLayout = breakfastObjectiveRoot.AddComponent<VerticalLayoutGroup>();
        breakfastLayout.spacing = 4f;
        breakfastLayout.childAlignment = TextAnchor.UpperLeft;
        breakfastLayout.childControlWidth = true;
        breakfastLayout.childControlHeight = true;
        breakfastLayout.childForceExpandWidth = false;
        breakfastLayout.childForceExpandHeight = false;
        ContentSizeFitter breakfastFitter = breakfastObjectiveRoot.AddComponent<ContentSizeFitter>();
        breakfastFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        breakfastFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildObjectiveRow(breakfastObjectiveRoot.transform, "Breakfast", out breakfastMarkerText, out breakfastTitleText);
        breakfastDescriptionText = CreateLabel(
            breakfastObjectiveRoot.transform,
            "BreakfastDescription",
            objectiveBodySize,
            UiTheme.Grey,
            FontStyles.Normal);
        breakfastDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        breakfastDescriptionText.text = breakfastObjectiveDescription;
        breakfastTitleText.text = breakfastObjectiveTitle;
        breakfastObjectiveRoot.SetActive(false);
    }

    private void BuildObjectiveRow(
        Transform parent,
        string namePrefix,
        out TextMeshProUGUI marker,
        out TextMeshProUGUI title)
    {
        GameObject row = new GameObject(namePrefix + "ObjectiveRow");
        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        marker = CreateLabel(row.transform, namePrefix + "Marker", objectiveTitleSize, UiTheme.BrightOrange, FontStyles.Bold);
        marker.text = "[ ]";

        title = CreateLabel(row.transform, namePrefix + "Title", objectiveTitleSize, UiTheme.White, FontStyles.Bold);
    }

    private void CreateDivider(Transform parent)
    {
        GameObject go = new GameObject("ObjectiveDivider");
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1f;
        le.minWidth = 180f;
        Image image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.18f);
        image.raycastTarget = false;
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
