using System.Collections;
using System.Collections.Generic;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal screen-space HUD displaying the player's Aura, Academic Reputation,
/// and stacked objectives driven by <see cref="DailyActivityState"/>
/// (GET_ID_CARD, BREAKFAST, and each configured CSE classroom).
/// Built at runtime — no canvas prefab required.
/// Attach to the Player prefab root alongside <see cref="PlayerStats"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStats))]
public sealed class StatsHUD : MonoBehaviour
{
    private const float PanelContentWidth = 380f;

    public static StatsHUD Instance { get; private set; }

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

    private GameObject canvasRoot;
    private GameObject panelRoot;
    private Image panelBackground;
    private RectTransform panelRect;
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
    private readonly List<ExtraObjectiveBlock> extraObjectiveBlocks = new List<ExtraObjectiveBlock>();

    private float lastAura;
    private float lastReputation;
    private Coroutine auraFeedbackRoutine;
    private Coroutine academicFeedbackRoutine;
    private PlayerSaveState playerSaveState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[StatsHUD] Duplicate player disabled. A floor scene contained another Player, " +
                "so classroom results were written to a DailyActivityState the visible HUD does not read.");
            enabled = false;
            gameObject.SetActive(false);
            return;
        }

        Instance = this;
        playerStats = GetComponent<PlayerStats>();
        dailyActivityState = GetComponent<DailyActivityState>();
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

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
        if (dayHeaderText == null || todayHeaderText == null || panelRoot == null)
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

        if (showBreakfast && breakfastMarkerText != null)
        {
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

        RefreshIcsObjective(showBreakfast);
        RefreshAdditionalClassrooms(showBreakfast);
        RebuildPanelLayout();
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
        if (string.IsNullOrWhiteSpace(description))
        {
            description = icsObjectiveDescriptionFallback;
        }

        ApplyClassroomOutcomeVisual(
            title,
            description,
            icsStatus,
            outcome,
            icsMarkerText,
            icsTitleText,
            icsDescriptionText);
    }

    private void RefreshAdditionalClassrooms(bool breakfastVisible)
    {
        if (panelRoot == null)
        {
            return;
        }

        ClassroomObjectiveView[] objectives = breakfastVisible && dailyActivityState != null
            ? dailyActivityState.BuildAdditionalClassroomObjectives(playerSaveState)
            : System.Array.Empty<ClassroomObjectiveView>();

        while (extraObjectiveBlocks.Count < objectives.Length)
        {
            int index = extraObjectiveBlocks.Count;
            string activityId = objectives[index].ActivityId;
            ExtraObjectiveBlock created = new ExtraObjectiveBlock { ActivityId = activityId };
            created.Root = CreateObjectiveBlock(
                panelRoot.transform,
                "Classroom_" + activityId,
                activityId,
                objectives[index].Title,
                objectives[index].Description,
                out created.Marker,
                out created.Title,
                out created.Description);
            extraObjectiveBlocks.Add(created);
        }

        for (int i = 0; i < extraObjectiveBlocks.Count; i++)
        {
            ExtraObjectiveBlock block = extraObjectiveBlocks[i];
            bool show = i < objectives.Length;
            if (block.Root != null)
            {
                block.Root.SetActive(show);
            }

            if (!show)
            {
                continue;
            }

            ClassroomObjectiveView view = objectives[i];
            block.ActivityId = view.ActivityId;
            ApplyClassroomObjectiveVisual(view, block.Marker, block.Title, block.Description);
        }
    }

    private static void ApplyClassroomObjectiveVisual(
        ClassroomObjectiveView view,
        TextMeshProUGUI marker,
        TextMeshProUGUI titleLabel,
        TextMeshProUGUI descriptionLabel)
    {
        ApplyClassroomOutcomeVisual(
            view.Title,
            view.Description,
            view.Status,
            view.Outcome,
            marker,
            titleLabel,
            descriptionLabel);
    }

    private static void ApplyClassroomOutcomeVisual(
        string baseTitle,
        string description,
        ActivityStatus status,
        string outcome,
        TextMeshProUGUI marker,
        TextMeshProUGUI titleLabel,
        TextMeshProUGUI descriptionLabel)
    {
        if (string.Equals(outcome, "PROXY", System.StringComparison.OrdinalIgnoreCase))
        {
            ApplyProxyObjectiveVisual(
                baseTitle,
                "Proxy — Punched ID and Left",
                marker,
                titleLabel,
                descriptionLabel);
            return;
        }

        ActivityStatus visualStatus = status == ActivityStatus.InProgress
            ? ActivityStatus.Pending
            : status;
        if (string.Equals(outcome, "LEFT_EARLY", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(outcome, "SKIPPED", System.StringComparison.OrdinalIgnoreCase))
        {
            visualStatus = ActivityStatus.Missed;
        }

        ApplyObjectiveVisual(
            visualStatus,
            AttendIcsOutcomeApi.HudTitle(baseTitle, outcome),
            description,
            marker,
            titleLabel,
            descriptionLabel);
    }

    private void RebuildPanelLayout()
    {
        if (panelRect == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
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
            descriptionLabel.gameObject.SetActive(true);
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
        RebuildPanelLayout();

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
        RebuildPanelLayout();
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
        if (panelRoot != null)
        {
            return;
        }

        // Remove any stale runtime HUD children from a previous domain-reload edge case.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == "StatsHUDCanvas")
            {
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        canvasRoot = new GameObject("StatsHUDCanvas");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        panelRoot = new GameObject("StatsPanel");
        panelRoot.transform.SetParent(canvasRoot.transform, false);

        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = screenOffset;

        panelBackground = panelRoot.AddComponent<Image>();
        panelBackground.color = backgroundColor;
        panelBackground.raycastTarget = false;

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

        LayoutElement panelWidth = panelRoot.AddComponent<LayoutElement>();
        panelWidth.preferredWidth = PanelContentWidth;

        auraText = CreateLabel(panelRoot.transform, "AuraText", fontSize, textColor, FontStyles.Bold, wrap: false);
        academicText = CreateLabel(panelRoot.transform, "AcademicText", fontSize, textColor, FontStyles.Bold, wrap: false);

        auraFeedbackText = CreateLabel(panelRoot.transform, "AuraFeedbackText", feedbackFontSize, positiveDeltaColor, FontStyles.Bold, wrap: false);
        auraFeedbackText.gameObject.SetActive(false);

        academicFeedbackText = CreateLabel(panelRoot.transform, "AcademicFeedbackText", feedbackFontSize, positiveDeltaColor, FontStyles.Bold, wrap: false);
        academicFeedbackText.gameObject.SetActive(false);

        CreateDivider(panelRoot.transform);
        dayHeaderText = CreateLabel(panelRoot.transform, "DayHeader", 14f, UiTheme.BrightOrange, FontStyles.Bold, wrap: false);
        dayHeaderText.text = "SEMESTER 1 · DAY 1";
        dayHeaderText.gameObject.SetActive(false);

        todayHeaderText = CreateLabel(panelRoot.transform, "TodayHeader", 12f, UiTheme.Grey, FontStyles.Bold, wrap: false);
        todayHeaderText.text = "TODAY";
        todayHeaderText.gameObject.SetActive(false);

        idCardObjectiveRoot = CreateObjectiveBlock(
            panelRoot.transform,
            "IdCardObjectiveBlock",
            "IdCard",
            idCardObjectiveTitle,
            idCardObjectiveDescription,
            out idMarkerText,
            out idTitleText,
            out idDescriptionText);

        breakfastObjectiveRoot = CreateObjectiveBlock(
            panelRoot.transform,
            "BreakfastObjectiveBlock",
            "Breakfast",
            breakfastObjectiveTitle,
            breakfastObjectiveDescription,
            out breakfastMarkerText,
            out breakfastTitleText,
            out breakfastDescriptionText);
        breakfastObjectiveRoot.SetActive(false);

        icsObjectiveRoot = CreateObjectiveBlock(
            panelRoot.transform,
            "IcsObjectiveBlock",
            "Ics",
            icsObjectiveTitle,
            icsObjectiveDescriptionFallback,
            out icsMarkerText,
            out icsTitleText,
            out icsDescriptionText);
        icsObjectiveRoot.SetActive(false);
    }

    private GameObject CreateObjectiveBlock(
        Transform parent,
        string rootName,
        string namePrefix,
        string title,
        string description,
        out TextMeshProUGUI marker,
        out TextMeshProUGUI titleLabel,
        out TextMeshProUGUI descriptionLabel)
    {
        GameObject root = new GameObject(rootName);
        root.transform.SetParent(parent, false);

        // One VerticalLayoutGroup per objective — no nested ContentSizeFitter.
        // Nested CSF was causing the parent Image to under-size so ICS text looked
        // like a second HUD floating outside the background.
        VerticalLayoutGroup blockLayout = root.AddComponent<VerticalLayoutGroup>();
        blockLayout.spacing = 4f;
        blockLayout.childAlignment = TextAnchor.UpperLeft;
        blockLayout.childControlWidth = true;
        blockLayout.childControlHeight = true;
        blockLayout.childForceExpandWidth = true;
        blockLayout.childForceExpandHeight = false;

        BuildObjectiveRow(root.transform, namePrefix, out marker, out titleLabel);
        titleLabel.text = title;

        descriptionLabel = CreateLabel(
            root.transform,
            namePrefix + "Description",
            objectiveBodySize,
            UiTheme.Grey,
            FontStyles.Normal,
            wrap: true);
        descriptionLabel.text = description;
        return root;
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
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        marker = CreateLabel(row.transform, namePrefix + "Marker", objectiveTitleSize, UiTheme.BrightOrange, FontStyles.Bold, wrap: false);
        marker.text = "[ ]";
        LayoutElement markerLe = marker.gameObject.AddComponent<LayoutElement>();
        markerLe.minWidth = 28f;
        markerLe.preferredWidth = 28f;
        markerLe.flexibleWidth = 0f;

        title = CreateLabel(row.transform, namePrefix + "Title", objectiveTitleSize, UiTheme.White, FontStyles.Bold, wrap: true);
        LayoutElement titleLe = title.gameObject.AddComponent<LayoutElement>();
        titleLe.flexibleWidth = 1f;
        titleLe.minWidth = 120f;
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

    private TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        float size,
        Color color,
        FontStyles style,
        bool wrap)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
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

    /// <summary>EditMode test seam: count Image backgrounds under this HUD.</summary>
    public int CountBackgroundImagesForTesting()
    {
        if (panelRoot == null)
        {
            return 0;
        }

        int count = 0;
        Image[] images = panelRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            // Divider is a 1px Image; the panel background is the only full HUD plate.
            if (images[i] != null && images[i].gameObject.name == "StatsPanel")
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>EditMode test seam: whether the ICS block lives under the same StatsPanel.</summary>
    public bool IsIcsInsideStatsPanelForTesting()
    {
        return icsObjectiveRoot != null
            && panelRoot != null
            && icsObjectiveRoot.transform.IsChildOf(panelRoot.transform);
    }

    private sealed class ExtraObjectiveBlock
    {
        public string ActivityId;
        public GameObject Root;
        public TextMeshProUGUI Marker;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Description;
    }
}
