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
/// (GET_ID_CARD, BREAKFAST, ATTEND_ICS).
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

    [Header("Objective Copy — ICS Classroom")]
    [SerializeField] private string icsObjectiveTitle = "Attend Introduction to Computer Science";

    [SerializeField, TextArea]
    private string icsObjectiveDescriptionFallback =
        "ICS classroom is not configured. Assign floor, room number, and classroom interaction in the Inspector.";

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

    private TextMeshProUGUI dayHeaderText;
    private TextMeshProUGUI todayHeaderText;
    private GameObject idCardObjectiveRoot;
    private TextMeshProUGUI idMarkerText;
    private TextMeshProUGUI idTitleText;
    private TextMeshProUGUI idDescriptionText;

    private GameObject breakfastObjectiveRoot;
    private TextMeshProUGUI breakfastMarkerText;
    private TextMeshProUGUI breakfastTitleText;
    private TextMeshProUGUI breakfastDescriptionText;

    private GameObject icsObjectiveRoot;
    private TextMeshProUGUI icsMarkerText;
    private TextMeshProUGUI icsTitleText;
    private TextMeshProUGUI icsDescriptionText;

    private float lastAura;
    private float lastReputation;
    private Coroutine auraFeedbackRoutine;
    private Coroutine academicFeedbackRoutine;
    private PlayerSaveState playerSaveState;

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

        if (playerSaveState == null)
        {
            playerSaveState = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();
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
            dailyActivityState.OnAttendIcsStatusChanged += RefreshObjectiveDisplay;
            dailyActivityState.OnActivitiesReset += RefreshObjectiveDisplay;
        }

        if (playerSaveState != null)
        {
            playerSaveState.OnHydrated += RefreshObjectiveDisplay;
            playerSaveState.OnAdmissionCompleted += RefreshObjectiveDisplay;
            playerSaveState.OnDayProgressChanged += RefreshObjectiveDisplay;
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
            dailyActivityState.OnAttendIcsStatusChanged -= RefreshObjectiveDisplay;
            dailyActivityState.OnActivitiesReset -= RefreshObjectiveDisplay;
        }

        if (playerSaveState != null)
        {
            playerSaveState.OnHydrated -= RefreshObjectiveDisplay;
            playerSaveState.OnAdmissionCompleted -= RefreshObjectiveDisplay;
            playerSaveState.OnDayProgressChanged -= RefreshObjectiveDisplay;
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
        if (dayHeaderText == null || todayHeaderText == null)
        {
            return;
        }

        int semester = ResolveSemester();
        int dayNumber = ResolveDayNumber();
        bool hasActiveDay = playerSaveState != null && playerSaveState.HasActiveUniversityDay;

        if (hasActiveDay && semester > 0 && dayNumber > 0)
        {
            dayHeaderText.gameObject.SetActive(true);
            dayHeaderText.text = $"SEMESTER {semester} · DAY {dayNumber}";
            todayHeaderText.gameObject.SetActive(true);
        }
        else
        {
            dayHeaderText.gameObject.SetActive(false);
            todayHeaderText.gameObject.SetActive(false);
        }

        ActivityStatus idStatus = dailyActivityState != null
            ? dailyActivityState.GetIdCardStatus
            : ActivityStatus.Pending;

        // GET_ID_CARD is a Day 1 / admission objective only.
        bool showIdCard = dayNumber <= 1;
        if (idCardObjectiveRoot != null)
        {
            idCardObjectiveRoot.SetActive(showIdCard);
        }

        if (showIdCard && idMarkerText != null)
        {
            string idTitle = dailyActivityState != null ? dailyActivityState.GetIdCardTitle : idCardObjectiveTitle;
            string idDescription = dailyActivityState != null
                ? dailyActivityState.GetIdCardDescription
                : idCardObjectiveDescription;

            ApplyObjectiveVisual(
                idStatus,
                idTitle,
                idDescription,
                idMarkerText,
                idTitleText,
                idDescriptionText);
        }

        bool showBreakfast = !showIdCard || idStatus != ActivityStatus.Pending;
        if (breakfastObjectiveRoot != null)
        {
            breakfastObjectiveRoot.SetActive(showBreakfast);
        }

        if (!showBreakfast || breakfastMarkerText == null)
        {
            RefreshIcsObjective(showBreakfast);
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

        RefreshIcsObjective(showBreakfast);
    }

    private void RefreshIcsObjective(bool breakfastVisible)
    {
        bool showIcs = breakfastVisible
            && dailyActivityState != null
            && dailyActivityState.ShouldShowAttendIcsObjective(playerSaveState);

        if (icsObjectiveRoot != null)
        {
            icsObjectiveRoot.SetActive(showIcs);
        }

        if (!showIcs || icsMarkerText == null || dailyActivityState == null)
        {
            return;
        }

        ActivityStatus icsStatus = dailyActivityState.AttendIcsStatus;
        string outcome = dailyActivityState.AttendIcsOutcome;
        string title = dailyActivityState.BuildAttendIcsObjectiveTitle();
        string description = dailyActivityState.BuildAttendIcsObjectiveDescription();

        if (string.Equals(outcome, "PROXY", System.StringComparison.OrdinalIgnoreCase))
        {
            ApplyProxyObjectiveVisual(
                title,
                "Proxy — Punched ID and Left",
                icsMarkerText,
                icsTitleText,
                icsDescriptionText);
            return;
        }

        if (string.Equals(outcome, "LEFT_EARLY", System.StringComparison.OrdinalIgnoreCase))
        {
            ApplyObjectiveVisual(
                ActivityStatus.Missed,
                title,
                description,
                icsMarkerText,
                icsTitleText,
                icsDescriptionText);
            return;
        }

        ApplyObjectiveVisual(
            icsStatus == ActivityStatus.InProgress ? ActivityStatus.Pending : icsStatus,
            title,
            description,
            icsMarkerText,
            icsTitleText,
            icsDescriptionText);
    }

    private static void ApplyProxyObjectiveVisual(
        string title,
        string description,
        TextMeshProUGUI marker,
        TextMeshProUGUI titleLabel,
        TextMeshProUGUI descriptionLabel)
    {
        marker.text = "[~]";
        marker.color = UiTheme.BrightOrange;
        titleLabel.text = title;
        titleLabel.color = UiTheme.BrightOrange;
        if (descriptionLabel != null)
        {
            descriptionLabel.text = description;
            descriptionLabel.color = UiTheme.Grey;
        }
    }

    private int ResolveSemester()
    {
        if (playerSaveState != null && playerSaveState.HasActiveUniversityDay)
        {
            return Mathf.Max(1, playerSaveState.Semester);
        }

        return 0;
    }

    private int ResolveDayNumber()
    {
        if (playerSaveState != null && playerSaveState.HasActiveUniversityDay)
        {
            return Mathf.Max(1, playerSaveState.CurrentDay);
        }

        if (dailyActivityState != null)
        {
            return Mathf.Max(1, dailyActivityState.DayNumber);
        }

        return 1;
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
                marker.color = UiTheme.Success;
                titleLabel.color = UiTheme.Success;
                descriptionLabel.gameObject.SetActive(false);
                break;
            case ActivityStatus.Missed:
                marker.text = "[X]";
                marker.color = UiTheme.Danger;
                titleLabel.color = UiTheme.Danger;
                descriptionLabel.gameObject.SetActive(false);
                break;
            default:
                marker.text = "[ ]";
                marker.color = UiTheme.White;
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
        dayHeaderText = CreateLabel(panelRoot.transform, "DayHeader", 14f, UiTheme.BrightOrange, FontStyles.Bold);
        dayHeaderText.text = "SEMESTER 1 · DAY 1";
        dayHeaderText.gameObject.SetActive(false);

        todayHeaderText = CreateLabel(panelRoot.transform, "TodayHeader", 12f, UiTheme.Grey, FontStyles.Bold);
        todayHeaderText.text = "TODAY";
        todayHeaderText.gameObject.SetActive(false);

        idCardObjectiveRoot = new GameObject("IdCardObjectiveBlock");
        idCardObjectiveRoot.transform.SetParent(panelRoot.transform, false);
        VerticalLayoutGroup idLayout = idCardObjectiveRoot.AddComponent<VerticalLayoutGroup>();
        idLayout.spacing = 4f;
        idLayout.childAlignment = TextAnchor.UpperLeft;
        idLayout.childControlWidth = true;
        idLayout.childControlHeight = true;
        idLayout.childForceExpandWidth = false;
        idLayout.childForceExpandHeight = false;
        ContentSizeFitter idFitter = idCardObjectiveRoot.AddComponent<ContentSizeFitter>();
        idFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        idFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildObjectiveRow(idCardObjectiveRoot.transform, "IdCard", out idMarkerText, out idTitleText);
        idDescriptionText = CreateLabel(idCardObjectiveRoot.transform, "IdCardDescription", objectiveBodySize, UiTheme.Grey, FontStyles.Normal);
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

        icsObjectiveRoot = new GameObject("IcsObjectiveBlock");
        icsObjectiveRoot.transform.SetParent(panelRoot.transform, false);
        VerticalLayoutGroup icsLayout = icsObjectiveRoot.AddComponent<VerticalLayoutGroup>();
        icsLayout.spacing = 4f;
        icsLayout.childAlignment = TextAnchor.UpperLeft;
        icsLayout.childControlWidth = true;
        icsLayout.childControlHeight = true;
        icsLayout.childForceExpandWidth = false;
        icsLayout.childForceExpandHeight = false;
        ContentSizeFitter icsFitter = icsObjectiveRoot.AddComponent<ContentSizeFitter>();
        icsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        icsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildObjectiveRow(icsObjectiveRoot.transform, "Ics", out icsMarkerText, out icsTitleText);
        icsDescriptionText = CreateLabel(
            icsObjectiveRoot.transform,
            "IcsDescription",
            objectiveBodySize,
            UiTheme.Grey,
            FontStyles.Normal);
        icsDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        icsDescriptionText.text = icsObjectiveDescriptionFallback;
        icsTitleText.text = icsObjectiveTitle;
        icsObjectiveRoot.SetActive(false);
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
