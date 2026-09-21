using System;
using System.Collections;
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
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DailySummaryUI : MonoBehaviour
    {
        private const int CanvasSortOrder = 245;
        private const float PanelWidth = 520f;
        private const float PanelHeight = 560f;

        public static DailySummaryUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private GameObject overlayRoot;
        private TextMeshProUGUI headerLabel;
        private TextMeshProUGUI bodyLabel;
        private TextMeshProUGUI totalsLabel;
        private TextMeshProUGUI errorLabel;
        private Button continueButton;

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
            RenderSummary(summary);
            continueButton.interactable = true;

            if (!IsOpen)
            {
                CacheControlReferences();
                DisableGameplayControls();
                AuthUiUtility.ShowUiCursor();
                AuthUiUtility.EnsureInputSystemEventSystem();
                EnsureEventSystem();
            }

            overlayRoot.SetActive(true);
            IsOpen = true;
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

            StringBuilder body = new StringBuilder(256);
            body.AppendLine("TODAY'S RESULTS");
            body.AppendLine();

            DailyActivityState activityState = FindFirstObjectByType<DailyActivityState>();
            DaySummaryActivity[] activities = summary.Activities;
            for (int i = 0; i < activities.Length; i++)
            {
                DaySummaryActivity item = activities[i];
                string title = ResolveActivityTitle(item.ActivityId, activityState);
                string marker = StatusMarker(item.Status);
                string hex = ColorUtility.ToHtmlStringRGB(StatusColor(item.Status));
                body.AppendLine($"<color=#{hex}>{marker} {title}</color>");
                body.AppendLine($"    Aura: {FormatSigned(item.AuraDelta)}");
                body.AppendLine($"    Academic Reputation: {FormatSigned(item.AcademicReputationDelta)}");
                body.AppendLine();
            }

            bodyLabel.richText = true;
            bodyLabel.text = body.ToString().TrimEnd();

            totalsLabel.text =
                "TODAY\n" +
                $"Aura: {FormatSigned(summary.TotalAuraDelta)}\n" +
                $"Academic Reputation: {FormatSigned(summary.TotalAcademicReputationDelta)}";
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
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.AddComponent<Image>().color = UiTheme.Black;

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            headerLabel = CreateLabel(panel.transform, "Header", 24f, FontStyles.Bold, UiTheme.BrightOrange, 64f);
            headerLabel.alignment = TextAlignmentOptions.Center;

            bodyLabel = CreateLabel(panel.transform, "Body", 16f, FontStyles.Normal, UiTheme.White, 260f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.textWrappingMode = TextWrappingModes.Normal;
            bodyLabel.richText = true;

            CreateDivider(panel.transform);

            totalsLabel = CreateLabel(panel.transform, "Totals", 18f, FontStyles.Bold, UiTheme.White, 72f);
            totalsLabel.alignment = TextAlignmentOptions.Center;

            errorLabel = CreateLabel(panel.transform, "Error", 15f, FontStyles.Bold, UiTheme.Red, 28f);
            errorLabel.alignment = TextAlignmentOptions.Center;
            errorLabel.gameObject.SetActive(false);

            continueButton = CreateButton(panel.transform, "Button_Continue", "CONTINUE", OnContinueClicked);
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            float fontSize,
            FontStyles style,
            Color color,
            float height)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.richText = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Button CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 48f;
            le.preferredHeight = 48f;

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
