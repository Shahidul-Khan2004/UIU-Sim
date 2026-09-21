using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UIU.Simulator.Authentication;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.UI
{
    /// <summary>
    /// Modal daily summary shown after End Day finalization.
    /// Continue advances the day once; failed advances keep the modal open for retry.
    /// Layout: fixed header + scrollable activity rows + fixed footer (totals + Continue).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DailySummaryUI : MonoBehaviour
    {
        private const int CanvasSortOrder = 245;
        private const float PanelWidth = 580f;
        private const float PanelMinHeight = 420f;
        private const float PanelMaxHeight = 680f;
        private const float HeaderPreferredHeight = 96f;
        private const float FooterPreferredHeight = 168f;
        private const float ScrollMinHeight = 80f;
        private const float ActivityTitleFontSize = 18f;
        private const float ActivityDetailFontSize = 15f;
        private const float SectionHeadingFontSize = 20f;

        public static DailySummaryUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private GameObject overlayRoot;
        private RectTransform panelRect;
        private LayoutElement scrollLayoutElement;
        private RectTransform activityContentRect;
        private Transform activityContent;
        private ScrollRect scrollRect;
        private TextMeshProUGUI headerLabel;
        private TextMeshProUGUI resultsHeadingLabel;
        private TextMeshProUGUI totalsLabel;
        private TextMeshProUGUI errorLabel;
        private Button continueButton;

        private readonly List<GameObject> activityRows = new List<GameObject>(8);

        private DayFinalizeResult pendingSummary;
        private bool hasPendingSummary;
        private bool isAdvancing;
        private bool advanceSucceeded;
        private Coroutine continueRoutine;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private InteractionController cachedInteraction;
        private CameraFollow cachedCameraFollow;

        public static DailySummaryUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            DailySummaryUI existing = FindFirstObjectByType<DailySummaryUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("DailySummaryUI");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<DailySummaryUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildUI();
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsOpen = false;
            }
        }

        public void Show(DayFinalizeResult summary)
        {
            if (overlayRoot == null)
            {
                BuildUI();
            }

            pendingSummary = summary;
            hasPendingSummary = true;
            isAdvancing = false;
            advanceSucceeded = false;
            SetError(string.Empty);
            continueButton.interactable = true;

            if (!IsOpen)
            {
                CacheControlReferences();
                DisableGameplayControls();
                AuthUiUtility.ShowUiCursor();
                AuthUiUtility.EnsureInputSystemEventSystem();
                EnsureEventSystem();
            }

            // Activate before rendering so layout rebuilds measure real widths/heights.
            overlayRoot.SetActive(true);
            IsOpen = true;
            RenderSummary(summary);
        }

        public void Hide(bool restoreGameplayControls = true)
        {
            HideImmediate();
            if (restoreGameplayControls)
            {
                ReleaseGameplayAfterSuccessfulAdvance();
            }
        }

        /// <summary>Test seam: open with a synthetic summary without networking.</summary>
        public void ShowForTesting(DayFinalizeResult summary)
        {
            Show(summary);
        }

        /// <summary>Test seam: complete the post-advance control restore path without networking.</summary>
        public void CompleteSuccessfulAdvanceForTesting()
        {
            advanceSucceeded = true;
            HideImmediate();
            ReleaseGameplayAfterSuccessfulAdvance();
        }

        /// <summary>Test seam: concatenated activity-row text for EditMode assertions.</summary>
        public string GetActivityBodyTextForTesting()
        {
            StringBuilder sb = new StringBuilder(256);
            for (int i = 0; i < activityRows.Count; i++)
            {
                GameObject row = activityRows[i];
                if (row == null)
                {
                    continue;
                }

                TextMeshProUGUI[] labels = row.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int j = 0; j < labels.Length; j++)
                {
                    if (j > 0 || sb.Length > 0)
                    {
                        sb.Append('\n');
                    }

                    sb.Append(labels[j].text);
                }
            }

            return sb.ToString();
        }

        private void OnContinueClicked()
        {
            if (!hasPendingSummary || isAdvancing || advanceSucceeded)
            {
                return;
            }

            PlayerProgressSync sync = FindFirstObjectByType<PlayerProgressSync>();
            if (sync == null)
            {
                SetError("Progress sync is unavailable.");
                return;
            }

            if (continueRoutine != null)
            {
                StopCoroutine(continueRoutine);
            }

            continueRoutine = StartCoroutine(ContinueAdvanceRoutine(sync));
        }

        private IEnumerator ContinueAdvanceRoutine(PlayerProgressSync sync)
        {
            isAdvancing = true;
            continueButton.interactable = false;
            SetError(string.Empty);

            int expectedSemester = pendingSummary.Semester;
            int expectedDay = pendingSummary.Day;

            bool finished = false;
            bool succeeded = false;
            string failureMessage = null;

            sync.RequestAdvanceDay(
                expectedSemester,
                expectedDay,
                onSuccess: _ =>
                {
                    succeeded = true;
                    finished = true;
                },
                onFailure: error =>
                {
                    failureMessage = error;
                    finished = true;
                });

            while (!finished)
            {
                yield return null;
            }

            if (!succeeded)
            {
                isAdvancing = false;
                continueButton.interactable = true;
                SetError(string.IsNullOrWhiteSpace(failureMessage)
                    ? "Could not advance the day. Try again."
                    : failureMessage);
                continueRoutine = null;
                yield break;
            }

            // Backend already advanced — do not retry advance if spawn fails.
            advanceSucceeded = true;
            ClearTransientDayUi();

            bool respawnDone = false;
            bool respawnOk = true;
            string respawnError = null;

            PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null)
            {
                yield return spawner.RespawnExistingPlayerAtPrimarySpawnRoutine(
                    onComplete: () =>
                    {
                        respawnDone = true;
                        respawnOk = true;
                    },
                    onError: err =>
                    {
                        respawnDone = true;
                        respawnOk = false;
                        respawnError = err;
                    });
            }
            else
            {
                // Fallback: teleport to any loaded PlayerSpawnPoint without instantiating a player.
                PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
                PlayerSpawnPoint spawnPoint = FindFirstObjectByType<PlayerSpawnPoint>();
                if (player != null && spawnPoint != null)
                {
                    PlayerSpawner.TryTeleportPlayerToSpawnPoint(player, spawnPoint);
                    respawnOk = true;
                }
                else
                {
                    respawnOk = false;
                    respawnError = "Primary spawn point is unavailable.";
                }

                respawnDone = true;
            }

            while (!respawnDone)
            {
                yield return null;
            }

            HideImmediate();
            ReleaseGameplayAfterSuccessfulAdvance();

            if (!respawnOk)
            {
                string message = string.IsNullOrWhiteSpace(respawnError)
                    ? "Day advanced, but respawn failed. Return to the entrance manually."
                    : respawnError;
                SystemNotificationUI.Show(message);
                Debug.LogWarning($"[DailySummaryUI] Day advanced but respawn failed: {message}");
            }

            isAdvancing = false;
            continueRoutine = null;
        }

        private static void ClearTransientDayUi()
        {
            CanteenBreakfastCounter breakfast = FindFirstObjectByType<CanteenBreakfastCounter>();
            breakfast?.TeardownQueue(isDefensive: true);

            if (CanteenQueueUI.IsOpen)
            {
                CanteenQueueUI.Instance?.Hide();
            }

            if (DialogueUI.IsOpen)
            {
                DialogueUI.Instance?.Hide();
            }
        }

        private void RenderSummary(DayFinalizeResult summary)
        {
            headerLabel.text = $"SEMESTER {summary.Semester}\nDAY {summary.Day} COMPLETE";
            resultsHeadingLabel.text = "TODAY'S RESULTS";

            ClearActivityRows();

            DailyActivityState activityState = FindFirstObjectByType<DailyActivityState>();
            DaySummaryActivity[] activities = summary.Activities;
            for (int i = 0; i < activities.Length; i++)
            {
                CreateActivityRow(activities[i], activityState, i);
            }

            totalsLabel.text =
                "TODAY'S TOTAL\n" +
                $"Aura: {FormatSigned(summary.TotalAuraDelta)}\n" +
                $"Academic Reputation: {FormatSigned(summary.TotalAcademicReputationDelta)}";

            RefreshPanelLayout();
        }

        private void ClearActivityRows()
        {
            for (int i = 0; i < activityRows.Count; i++)
            {
                if (activityRows[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(activityRows[i]);
                }
                else
                {
                    DestroyImmediate(activityRows[i]);
                }
            }

            activityRows.Clear();
        }

        private void CreateActivityRow(DaySummaryActivity item, DailyActivityState activityState, int index)
        {
            string title = ResolveActivityTitle(item.ActivityId, activityState);
            if (ActivityIds.IsClassroomActivity(item.ActivityId))
            {
                string courseName = activityState != null
                    ? activityState.CourseNameForActivity(item.ActivityId)
                    : null;
                title = AttendIcsOutcomeApi.SummaryLabel(item.Outcome, courseName);
            }

            string marker = StatusMarkerForActivity(item);
            Color statusColor = StatusColorForActivity(item);

            GameObject row = new GameObject($"ActivityRow_{index}");
            row.transform.SetParent(activityContent, false);

            VerticalLayoutGroup rowLayout = row.AddComponent<VerticalLayoutGroup>();
            rowLayout.padding = new RectOffset(0, 0, 2, 6);
            rowLayout.spacing = 2f;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            // TMP implements ILayoutElement — row height follows wrapped title preferred size.
            // Do not nest ContentSizeFitter here; parent ActivityContent already has one.
            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.flexibleWidth = 1f;
            rowLe.minHeight = 36f;

            TextMeshProUGUI titleLabel = CreateText(
                row.transform,
                "Title",
                $"{marker} {title}",
                ActivityTitleFontSize,
                FontStyles.Bold,
                statusColor,
                TextAlignmentOptions.TopLeft);
            titleLabel.textWrappingMode = TextWrappingModes.Normal;
            titleLabel.overflowMode = TextOverflowModes.Overflow;

            TextMeshProUGUI detailLabel = CreateText(
                row.transform,
                "Details",
                $"Aura: {FormatSigned(item.AuraDelta)} | Academic: {FormatSigned(item.AcademicReputationDelta)}",
                ActivityDetailFontSize,
                FontStyles.Normal,
                UiTheme.Grey,
                TextAlignmentOptions.TopLeft);
            detailLabel.textWrappingMode = TextWrappingModes.Normal;

            activityRows.Add(row);
        }

        private void RefreshPanelLayout()
        {
            if (panelRect == null || activityContentRect == null || scrollLayoutElement == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(activityContentRect);

            float contentHeight = Mathf.Max(activityContentRect.rect.height, activityContentRect.sizeDelta.y);
            float padding = 48f; // panel VerticalLayoutGroup padding top+bottom
            float spacing = 10f * 2f; // header↔scroll and scroll↔footer
            float chrome = HeaderPreferredHeight + FooterPreferredHeight + padding + spacing;
            float desired = chrome + Mathf.Max(contentHeight, ScrollMinHeight);
            float panelHeight = Mathf.Clamp(desired, PanelMinHeight, ResolveMaxPanelHeight());

            float scrollHeight = Mathf.Max(ScrollMinHeight, panelHeight - chrome);

            panelRect.sizeDelta = new Vector2(PanelWidth, panelHeight);
            scrollLayoutElement.minHeight = ScrollMinHeight;
            scrollLayoutElement.preferredHeight = scrollHeight;
            scrollLayoutElement.flexibleHeight = 1f;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private static float ResolveMaxPanelHeight()
        {
            // Stay within ~70% of reference-height canvas units so Free Aspect still fits.
            float viewportCap = 1080f * 0.7f;
            return Mathf.Min(PanelMaxHeight, viewportCap);
        }

        private static string ResolveActivityTitle(string activityId, DailyActivityState state)
        {
            if (activityId == ActivityIds.GetIdCard)
            {
                return state != null ? state.GetIdCardTitle : "Get Your ID Card";
            }

            if (activityId == ActivityIds.Breakfast)
            {
                return state != null ? state.BreakfastTitle : "Have Breakfast";
            }

            if (ActivityIds.IsClassroomActivity(activityId))
            {
                string courseName = state != null ? state.CourseNameForActivity(activityId) : null;
                string outcome = state != null ? state.GetClassroomOutcome(activityId) : null;
                return AttendIcsOutcomeApi.SummaryLabel(outcome, courseName);
            }

            return activityId;
        }

        private static string StatusMarker(ActivityStatus status)
        {
            switch (status)
            {
                case ActivityStatus.Completed:
                    return "[x]";
                case ActivityStatus.Missed:
                    return "[X]";
                default:
                    return "[ ]";
            }
        }

        private static Color StatusColor(ActivityStatus status)
        {
            switch (status)
            {
                case ActivityStatus.Completed:
                    return UiTheme.Success;
                case ActivityStatus.Missed:
                    return UiTheme.Danger;
                default:
                    return UiTheme.White;
            }
        }

        private static string StatusMarkerForActivity(DaySummaryActivity item)
        {
            if (ActivityIds.IsClassroomActivity(item.ActivityId)
                && string.Equals(item.Outcome, "PROXY", System.StringComparison.OrdinalIgnoreCase))
            {
                return "[~]";
            }

            return StatusMarker(item.Status);
        }

        private static Color StatusColorForActivity(DaySummaryActivity item)
        {
            if (ActivityIds.IsClassroomActivity(item.ActivityId)
                && string.Equals(item.Outcome, "PROXY", System.StringComparison.OrdinalIgnoreCase))
            {
                return UiTheme.BrightOrange;
            }

            if (ActivityIds.IsClassroomActivity(item.ActivityId)
                && string.Equals(item.Outcome, "LEFT_EARLY", System.StringComparison.OrdinalIgnoreCase))
            {
                return UiTheme.Danger;
            }

            return StatusColor(item.Status);
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private void SetError(string message)
        {
            if (errorLabel == null)
            {
                return;
            }

            errorLabel.text = message ?? string.Empty;
            errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void HideImmediate()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;
            hasPendingSummary = false;
            isAdvancing = false;
        }

        private void CacheControlReferences()
        {
            cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            cachedInteraction = FindFirstObjectByType<InteractionController>();
            cachedCameraFollow = FindFirstObjectByType<CameraFollow>();
        }

        private void DisableGameplayControls()
        {
            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.enabled = false;
            }

            if (cachedFirstPersonLook != null)
            {
                cachedFirstPersonLook.enabled = false;
            }

            if (cachedInteraction != null)
            {
                cachedInteraction.enabled = false;
            }

            if (cachedCameraFollow != null)
            {
                cachedCameraFollow.enabled = false;
            }
        }

        /// <summary>
        /// Releases both DailySummaryUI and GameMenuManager input ownership using
        /// gameplay-enabled defaults (not the disabled snapshot taken after the menu opened).
        /// </summary>
        private void ReleaseGameplayAfterSuccessfulAdvance()
        {
            GameMenuManager menu = GameMenuManager.Instance != null
                ? GameMenuManager.Instance
                : FindFirstObjectByType<GameMenuManager>();

            if (menu != null)
            {
                menu.ForceCloseAndRestoreGameplayControls();
            }
            else
            {
                ForceEnableGameplayControls();
            }
        }

        private void ForceEnableGameplayControls()
        {
            CacheControlReferences();

            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.enabled = true;
            }

            if (cachedInteraction != null)
            {
                cachedInteraction.enabled = true;
            }

            if (cachedCameraFollow != null)
            {
                cachedCameraFollow.enabled = true;
            }

            if (cachedFirstPersonLook != null)
            {
                cachedFirstPersonLook.SuppressEscapeThisFrame();
                cachedFirstPersonLook.enabled = true;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject("DailySummaryCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            overlayRoot = new GameObject("DailySummaryOverlay");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            overlayRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            GameObject panel = new GameObject("DailySummaryPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelMinHeight);
            panel.AddComponent<Image>().color = UiTheme.Black;

            VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(28, 28, 24, 24);
            panelLayout.spacing = 10f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            BuildHeader(panel.transform);
            BuildScrollArea(panel.transform);
            BuildFooter(panel.transform);
        }

        private void BuildHeader(Transform parent)
        {
            GameObject header = new GameObject("HeaderRegion");
            header.transform.SetParent(parent, false);

            LayoutElement headerLe = header.AddComponent<LayoutElement>();
            headerLe.minHeight = HeaderPreferredHeight;
            headerLe.preferredHeight = HeaderPreferredHeight;
            headerLe.flexibleHeight = 0f;
            headerLe.flexibleWidth = 1f;

            VerticalLayoutGroup headerLayout = header.AddComponent<VerticalLayoutGroup>();
            headerLayout.spacing = 8f;
            headerLayout.childAlignment = TextAnchor.UpperCenter;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = false;

            headerLabel = CreateText(
                header.transform,
                "Header",
                string.Empty,
                24f,
                FontStyles.Bold,
                UiTheme.BrightOrange,
                TextAlignmentOptions.Center);
            LayoutElement headerTextLe = headerLabel.gameObject.AddComponent<LayoutElement>();
            headerTextLe.minHeight = 56f;
            headerTextLe.preferredHeight = 56f;

            resultsHeadingLabel = CreateText(
                header.transform,
                "ResultsHeading",
                "TODAY'S RESULTS",
                SectionHeadingFontSize,
                FontStyles.Bold,
                UiTheme.White,
                TextAlignmentOptions.Center);
            LayoutElement resultsLe = resultsHeadingLabel.gameObject.AddComponent<LayoutElement>();
            resultsLe.minHeight = 28f;
            resultsLe.preferredHeight = 28f;
        }

        private void BuildScrollArea(Transform parent)
        {
            GameObject scrollGo = new GameObject("ActivityScroll");
            scrollGo.transform.SetParent(parent, false);

            scrollLayoutElement = scrollGo.AddComponent<LayoutElement>();
            scrollLayoutElement.minHeight = ScrollMinHeight;
            scrollLayoutElement.preferredHeight = 200f;
            scrollLayoutElement.flexibleHeight = 1f;
            scrollLayoutElement.flexibleWidth = 1f;

            scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            // Transparent hit target so the scroll area receives pointer events.
            Image scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.01f);

            GameObject viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRect = viewportGo.AddComponent<RectTransform>();
            StretchFull(viewportRect);
            viewportGo.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRect;

            GameObject contentGo = new GameObject("ActivityContent");
            contentGo.transform.SetParent(viewportGo.transform, false);
            activityContentRect = contentGo.AddComponent<RectTransform>();
            activityContentRect.anchorMin = new Vector2(0f, 1f);
            activityContentRect.anchorMax = new Vector2(1f, 1f);
            activityContentRect.pivot = new Vector2(0.5f, 1f);
            activityContentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(4, 4, 4, 8);
            contentLayout.spacing = 10f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = activityContentRect;
            activityContent = contentGo.transform;
        }

        private void BuildFooter(Transform parent)
        {
            GameObject footer = new GameObject("FooterRegion");
            footer.transform.SetParent(parent, false);

            LayoutElement footerLe = footer.AddComponent<LayoutElement>();
            footerLe.minHeight = FooterPreferredHeight;
            footerLe.preferredHeight = FooterPreferredHeight;
            footerLe.flexibleHeight = 0f;
            footerLe.flexibleWidth = 1f;

            VerticalLayoutGroup footerLayout = footer.AddComponent<VerticalLayoutGroup>();
            footerLayout.spacing = 8f;
            footerLayout.padding = new RectOffset(0, 0, 4, 0);
            footerLayout.childAlignment = TextAnchor.UpperCenter;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childForceExpandHeight = false;

            CreateDivider(footer.transform);

            totalsLabel = CreateText(
                footer.transform,
                "Totals",
                string.Empty,
                18f,
                FontStyles.Bold,
                UiTheme.White,
                TextAlignmentOptions.Center);
            LayoutElement totalsLe = totalsLabel.gameObject.AddComponent<LayoutElement>();
            totalsLe.minHeight = 64f;
            totalsLe.preferredHeight = 64f;

            errorLabel = CreateText(
                footer.transform,
                "Error",
                string.Empty,
                15f,
                FontStyles.Bold,
                UiTheme.Red,
                TextAlignmentOptions.Center);
            LayoutElement errorLe = errorLabel.gameObject.AddComponent<LayoutElement>();
            errorLe.minHeight = 22f;
            errorLe.preferredHeight = 22f;
            errorLabel.gameObject.SetActive(false);

            continueButton = CreateButton(footer.transform, "Button_Continue", "CONTINUE", OnContinueClicked);
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.richText = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Button CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 52f;
            le.preferredHeight = 52f;
            le.flexibleWidth = 1f;

            Image image = go.AddComponent<Image>();
            image.color = UiTheme.BrightOrange;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            StretchFull(textRect);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = UiTheme.White;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = false;
            tmp.raycastTarget = false;
            return button;
        }

        private static void CreateDivider(Transform parent)
        {
            GameObject go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 1f;
            le.minHeight = 1f;
            Image image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.2f);
            image.raycastTarget = false;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = FindFirstObjectByType<EventSystem>();
            if (existing == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                InputSystemUIInputModule module = esGo.AddComponent<InputSystemUIInputModule>();
                if (InputSystem.actions != null)
                {
                    module.actionsAsset = InputSystem.actions;
                }
            }
        }
    }
}
