using System.Collections;
using System.Collections.Generic;
using TMPro;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Networking;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Faculty-only HUD: Reputation, current objective, next activity, and reputation popups.
    /// Built at runtime — no canvas prefab required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacultyHUD : MonoBehaviour
    {
        private const float PanelContentWidth = 380f;

        public static FacultyHUD Instance { get; private set; }

        [SerializeField] private float fontSize = 20f;
        [SerializeField] private float feedbackFontSize = 18f;
        [SerializeField] private float objectiveTitleSize = 18f;
        [SerializeField] private float objectiveBodySize = 14f;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color positiveDeltaColor = UiTheme.BrightOrange;
        [SerializeField] private Color negativeDeltaColor = UiTheme.Red;
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] private Vector2 screenOffset = new Vector2(24f, -24f);
        [SerializeField] private float feedbackHoldDuration = 1.4f;
        [SerializeField] private float feedbackFadeDuration = 0.6f;

        private FacultyProgress facultyProgress;
        private FacultyAssignedSchedule assignedSchedule;
        private PlayerSaveState playerSaveState;

        private GameObject canvasRoot;
        private GameObject panelRoot;
        private TextMeshProUGUI reputationText;
        private TextMeshProUGUI reputationFeedbackText;
        private TextMeshProUGUI currentObjectiveText;
        private TextMeshProUGUI nextActivityText;
        private TextMeshProUGUI idMarkerText;
        private TextMeshProUGUI idTitleText;
        private TextMeshProUGUI officeMarkerText;
        private TextMeshProUGUI officeTitleText;
        private TextMeshProUGUI materialsMarkerText;
        private TextMeshProUGUI materialsTitleText;
        private readonly List<GameObject> assignedCourseRows = new List<GameObject>();
        private readonly List<TextMeshProUGUI> assignedCourseMarkers = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> assignedCourseTitles = new List<TextMeshProUGUI>();

        private int lastReputation = FacultyProgress.DefaultReputation;
        private Coroutine feedbackRoutine;

        public static FacultyHUD EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            FacultyHUD existing = FindFirstObjectByType<FacultyHUD>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            FacultyProgress progress = FacultyProgress.EnsureExists();
            FacultyHUD hud = progress.GetComponent<FacultyHUD>();
            if (hud == null)
            {
                hud = progress.gameObject.AddComponent<FacultyHUD>();
            }

            FacultyProgressSync.EnsureExists();
            FacultyAssignedSchedule.EnsureExists();
            return hud;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
            facultyProgress = GetComponent<FacultyProgress>();
            if (facultyProgress == null)
            {
                facultyProgress = FacultyProgress.EnsureExists();
            }

            assignedSchedule = GetComponent<FacultyAssignedSchedule>();
            if (assignedSchedule == null)
            {
                assignedSchedule = FacultyAssignedSchedule.EnsureExists();
            }

            BuildUI();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (facultyProgress == null)
            {
                facultyProgress = GetComponent<FacultyProgress>() ?? FacultyProgress.Instance;
            }

            if (playerSaveState == null)
            {
                playerSaveState = PlayerSaveState.Instance != null
                    ? PlayerSaveState.Instance
                    : FindFirstObjectByType<PlayerSaveState>();
            }

            if (assignedSchedule == null)
            {
                assignedSchedule = GetComponent<FacultyAssignedSchedule>() ?? FacultyAssignedSchedule.Instance;
            }

            if (facultyProgress != null)
            {
                lastReputation = facultyProgress.Reputation;
                facultyProgress.OnReputationUpdated += HandleReputationUpdated;
                facultyProgress.OnObjectivesChanged += RefreshObjectiveDisplay;
                UpdateReputationDisplay(lastReputation);
            }

            if (assignedSchedule != null)
            {
                assignedSchedule.OnScheduleChanged += HandleScheduleChanged;
            }

            if (playerSaveState != null)
            {
                playerSaveState.OnHydrated += ApplyRoleVisibility;
                playerSaveState.OnAdmissionCompleted += ApplyRoleVisibility;
                playerSaveState.OnDayProgressChanged += RefreshObjectiveDisplay;
            }

            ApplyRoleVisibility();
        }

        private void OnDisable()
        {
            if (facultyProgress != null)
            {
                facultyProgress.OnReputationUpdated -= HandleReputationUpdated;
                facultyProgress.OnObjectivesChanged -= RefreshObjectiveDisplay;
            }

            if (assignedSchedule != null)
            {
                assignedSchedule.OnScheduleChanged -= HandleScheduleChanged;
            }

            if (playerSaveState != null)
            {
                playerSaveState.OnHydrated -= ApplyRoleVisibility;
                playerSaveState.OnAdmissionCompleted -= ApplyRoleVisibility;
                playerSaveState.OnDayProgressChanged -= RefreshObjectiveDisplay;
            }

            StopFeedback();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetVisible(bool visible)
        {
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(visible);
            }
        }

        public bool IsVisible => canvasRoot != null && canvasRoot.activeSelf;

        public void ApplyRoleVisibility()
        {
            bool isFaculty = playerSaveState != null
                && playerSaveState.IsHydrated
                && FacultyIdentity.Matches(playerSaveState.Role);

            SetVisible(isFaculty);
            if (StatsHUD.Instance != null)
            {
                StatsHUD.Instance.SetVisible(!isFaculty);
            }

            if (isFaculty)
            {
                if (assignedSchedule != null)
                {
                    assignedSchedule.RequestRoutineRefresh();
                }

                RefreshObjectiveDisplay();
            }
        }

        private void HandleReputationUpdated(int newReputation, FacultyUpdateSource source)
        {
            int delta = newReputation - lastReputation;
            lastReputation = newReputation;
            UpdateReputationDisplay(newReputation);

            if (source == FacultyUpdateSource.GameplayMutation && delta != 0)
            {
                TriggerReputationFeedback(delta);
            }
        }

        private void UpdateReputationDisplay(int value)
        {
            if (reputationText != null)
            {
                reputationText.text = value.ToString();
            }
        }

        private void HandleScheduleChanged()
        {
            RebuildAssignedCourseRows();
            RefreshObjectiveDisplay();
        }

        private void RefreshObjectiveDisplay()
        {
            if (currentObjectiveText == null || nextActivityText == null)
            {
                return;
            }

            bool idIssued = facultyProgress != null && facultyProgress.FacultyIdIssued;
            bool officeDone = facultyProgress != null && facultyProgress.OfficeSetup;
            bool materialsDone = facultyProgress != null && facultyProgress.CourseMaterialsPrepared;

            SetObjectiveVisual(idMarkerText, idTitleText, idIssued);
            SetObjectiveVisual(officeMarkerText, officeTitleText, officeDone);
            SetObjectiveVisual(materialsMarkerText, materialsTitleText, materialsDone);
            UpdateAssignedCourseMarkers();

            if (!idIssued)
            {
                currentObjectiveText.text = "Current Objective:\nCollect Faculty ID Card";
                nextActivityText.text = "Next Activity:\nVisit the receptionist";
                return;
            }

            if (!officeDone)
            {
                currentObjectiveText.text = "Current Objective:\nSetup Faculty Office";
                nextActivityText.text = "Next Activity:\nGo to Faculty Room 335";
                return;
            }

            if (!materialsDone)
            {
                ApiClient.FacultyRoutineItemDto firstClass = assignedSchedule != null
                    ? assignedSchedule.GetClassAt(0)
                    : null;
                currentObjectiveText.text = "Current Objective:\nPrepare course materials";
                nextActivityText.text = firstClass != null
                    ? $"Next Activity:\nGo to {FacultyAssignedSchedule.BuildClassroomLabel(firstClass)} Room {firstClass.classroomNumber}"
                    : "Next Activity:\nPrepare materials for your assigned courses";
                return;
            }

            ApiClient.FacultyRoutineItemDto currentClass = assignedSchedule != null
                ? assignedSchedule.GetCurrentClass(facultyProgress)
                : null;
            if (currentClass == null)
            {
                currentObjectiveText.text = "Current Objective:\nAll classes completed.";
                nextActivityText.text = "Next Activity:\n—";
                return;
            }

            currentObjectiveText.text = FacultyAssignedSchedule.BuildCurrentObjective(currentClass);
            nextActivityText.text = FacultyAssignedSchedule.BuildNextActivity(currentClass);
        }

        private void UpdateAssignedCourseMarkers()
        {
            for (int i = 0; i < assignedCourseMarkers.Count; i++)
            {
                ApiClient.FacultyRoutineItemDto item = assignedSchedule != null
                    ? assignedSchedule.GetClassAt(i)
                    : null;
                bool complete = facultyProgress != null
                    && item != null
                    && facultyProgress.IsCourseCompleted(item.courseId);
                TextMeshProUGUI title = i < assignedCourseTitles.Count ? assignedCourseTitles[i] : null;
                SetObjectiveVisual(assignedCourseMarkers[i], title, complete);
            }
        }

        private static void SetObjectiveVisual(TextMeshProUGUI marker, TextMeshProUGUI title, bool complete)
        {
            if (marker != null)
            {
                marker.text = complete ? "✓" : "○";
                marker.color = complete ? UiTheme.Success : UiTheme.BrightOrange;
            }

            if (title != null)
            {
                title.color = complete ? UiTheme.Success : UiTheme.White;
            }
        }

        private void TriggerReputationFeedback(int delta)
        {
            if (reputationFeedbackText == null)
            {
                return;
            }

            string sign = delta > 0 ? "+" : string.Empty;
            reputationFeedbackText.text = $"{sign}{delta} Reputation";
            reputationFeedbackText.color = delta > 0 ? positiveDeltaColor : negativeDeltaColor;
            reputationFeedbackText.gameObject.SetActive(true);

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackRoutine = StartCoroutine(FeedbackRoutine());
        }

        private IEnumerator FeedbackRoutine()
        {
            Color start = reputationFeedbackText.color;
            start.a = 1f;
            reputationFeedbackText.color = start;
            yield return new WaitForSecondsRealtime(feedbackHoldDuration);

            float elapsed = 0f;
            while (elapsed < feedbackFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                Color faded = reputationFeedbackText.color;
                faded.a = Mathf.Lerp(1f, 0f, elapsed / feedbackFadeDuration);
                reputationFeedbackText.color = faded;
                yield return null;
            }

            reputationFeedbackText.gameObject.SetActive(false);
            feedbackRoutine = null;
        }

        private void StopFeedback()
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }

            if (reputationFeedbackText != null)
            {
                reputationFeedbackText.gameObject.SetActive(false);
            }
        }

        private void BuildUI()
        {
            if (panelRoot != null)
            {
                return;
            }

            canvasRoot = new GameObject("FacultyHUDCanvas");
            canvasRoot.transform.SetParent(transform, false);

            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            panelRoot = new GameObject("FacultyStatsPanel");
            panelRoot.transform.SetParent(canvasRoot.transform, false);

            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = screenOffset;

            Image panelBackground = panelRoot.AddComponent<Image>();
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

            CreateLabel(panelRoot.transform, "ReputationHeader", "FACULTY REPUTATION", fontSize, UiTheme.BrightOrange, FontStyles.Bold);
            reputationText = CreateLabel(panelRoot.transform, "ReputationValue", "50", 28f, textColor, FontStyles.Bold);
            reputationFeedbackText = CreateLabel(panelRoot.transform, "ReputationFeedback", "+5 Reputation", feedbackFontSize, positiveDeltaColor, FontStyles.Bold);
            reputationFeedbackText.gameObject.SetActive(false);

            CreateDivider(panelRoot.transform);
            currentObjectiveText = CreateLabel(
                panelRoot.transform,
                "CurrentObjective",
                "Current Objective:\nPrepare course materials",
                objectiveTitleSize,
                textColor,
                FontStyles.Bold,
                wrap: true);
            nextActivityText = CreateLabel(
                panelRoot.transform,
                "NextActivity",
                "Next Activity:\nGo to ICS Classroom Room 427",
                objectiveBodySize,
                UiTheme.Grey,
                FontStyles.Normal,
                wrap: true);

            CreateDivider(panelRoot.transform);
            CreateObjectiveRow(panelRoot.transform, "IdCard", "Faculty ID Card", out idMarkerText, out idTitleText);
            CreateObjectiveRow(panelRoot.transform, "Office", "Setup Faculty Office", out officeMarkerText, out officeTitleText);
            CreateObjectiveRow(panelRoot.transform, "Materials", "Prepare Course Materials", out materialsMarkerText, out materialsTitleText);
            RebuildAssignedCourseRows();
        }

        private void RebuildAssignedCourseRows()
        {
            if (panelRoot == null)
            {
                return;
            }

            for (int i = 0; i < assignedCourseRows.Count; i++)
            {
                if (assignedCourseRows[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(assignedCourseRows[i]);
                }
                else
                {
                    DestroyImmediate(assignedCourseRows[i]);
                }
            }

            assignedCourseRows.Clear();
            assignedCourseMarkers.Clear();
            assignedCourseTitles.Clear();

            IReadOnlyList<ApiClient.FacultyRoutineItemDto> classes = assignedSchedule != null
                ? assignedSchedule.AssignedClasses
                : FacultyAssignedSchedule.CreateFallbackAssigned();
            if (classes == null)
            {
                return;
            }

            for (int i = 0; i < classes.Count; i++)
            {
                ApiClient.FacultyRoutineItemDto item = classes[i];
                if (item == null)
                {
                    continue;
                }

                CreateObjectiveRow(
                    panelRoot.transform,
                    "Course" + i,
                    FacultyAssignedSchedule.BuildChecklistTitle(item),
                    out TextMeshProUGUI marker,
                    out TextMeshProUGUI title);
                assignedCourseMarkers.Add(marker);
                assignedCourseTitles.Add(title);
                Transform row = marker != null && marker.transform.parent != null
                    ? marker.transform.parent
                    : null;
                if (row != null)
                {
                    assignedCourseRows.Add(row.gameObject);
                }
            }
        }

        private void CreateObjectiveRow(
            Transform parent,
            string prefix,
            string title,
            out TextMeshProUGUI marker,
            out TextMeshProUGUI titleLabel)
        {
            GameObject row = new GameObject(prefix + "ObjectiveRow");
            row.transform.SetParent(parent, false);

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            marker = CreateLabel(row.transform, prefix + "Marker", "○", objectiveTitleSize, UiTheme.BrightOrange, FontStyles.Bold);
            LayoutElement markerLe = marker.gameObject.AddComponent<LayoutElement>();
            markerLe.minWidth = 28f;
            markerLe.preferredWidth = 28f;

            titleLabel = CreateLabel(row.transform, prefix + "Title", title, objectiveTitleSize, UiTheme.White, FontStyles.Bold, wrap: true);
            LayoutElement titleLe = titleLabel.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;
            titleLe.minWidth = 120f;
        }

        private static void CreateDivider(Transform parent)
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

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float size,
            Color color,
            FontStyles style,
            bool wrap = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
