using System;
using System.Collections;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Admission;
using UIU.Simulator.Gameplay.Advisor;
using UIU.Simulator.Gameplay.Classroom;
using UIU.Simulator.Gameplay.Elevator;
using UIU.Simulator.Gameplay.IDCard;
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
    /// Compact ICS lecture timer modal. Owns gameplay lock + visible cursor while open.
    /// Terminal full-attendance closes automatically after the final milestone is confirmed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClassroomLectureUI : MonoBehaviour
    {
        private const float PanelWidth = 640f;
        private const float ButtonHeight = 52f;

        private enum LectureUiState
        {
            Closed,
            LectureActive,
            Completing,
            Completed,
            LeftEarly
        }

        public static ClassroomLectureUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private GameObject overlayRoot;
        private GameObject panelRoot;
        private GameObject confirmRoot;
        private TextMeshProUGUI headerLabel;
        private TextMeshProUGUI timerLabel;
        private TextMeshProUGUI statusLabel;
        private TextMeshProUGUI nextMilestoneLabel;
        private TextMeshProUGUI currentRewardLabel;
        private TextMeshProUGUI milestone30Label;
        private TextMeshProUGUI milestone60Label;
        private TextMeshProUGUI milestone90Label;
        private RectTransform fillRect;
        private Button leaveButton;

        private IcsClassroomInteractable classroom;
        private IAttendIcsProgressSync sync;
        private Action onClosed;

        private float durationSeconds = 90f;
        private float elapsedSeconds;
        private int claimedMilestone;
        private LectureUiState uiState = LectureUiState.Closed;
        private bool isMutating;
        private bool leaveConfirmOpen;
        private Coroutine tickRoutine;
        private int openedFrame = -1;
        private float completionCloseDelaySeconds = 0.55f;
        private int finalMilestoneAttempts;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private InteractionController cachedInteraction;
        private CameraFollow cachedCameraFollow;
        private bool ownsGameplayLock;

        public static ClassroomLectureUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            ClassroomLectureUI existing = FindFirstObjectByType<ClassroomLectureUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("ClassroomLectureUI");
            return host.AddComponent<ClassroomLectureUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildUi();
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (ownsGameplayLock)
                {
                    RestoreGameplayControls();
                }

                Instance = null;
                IsOpen = false;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (uiState != LectureUiState.LectureActive || sync == null)
            {
                return;
            }

            if (pauseStatus)
            {
                sync.RequestAttendIcsPause(_ => { }, () => { });
            }
            else
            {
                sync.RequestAttendIcsResume(_ => { }, () => { });
            }
        }

        private void OnDisable()
        {
            if (uiState == LectureUiState.LectureActive && sync != null)
            {
                sync.RequestAttendIcsPause(_ => { }, () => { });
            }
        }

        private void Update()
        {
            if (!IsOpen || leaveConfirmOpen || uiState != LectureUiState.LectureActive)
            {
                return;
            }

            if (Time.frameCount <= openedFrame)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                OpenLeaveConfirm();
            }
        }

        public void BeginLecture(
            IcsClassroomInteractable source,
            IAttendIcsProgressSync progressSync,
            AttendIcsSessionResult startResult,
            Action onClosedCallback)
        {
            EnsureEventSystem();

            // Choice UI handed off without restoring; claim ownership here.
            if (ClassroomChoiceUI.Instance != null)
            {
                ClassroomChoiceUI.Instance.ReleaseOwnershipWithoutRestore();
            }

            classroom = source;
            sync = progressSync;
            onClosed = onClosedCallback;
            durationSeconds = source != null ? Mathf.Max(1f, source.LectureDurationSeconds) : 90f;
            elapsedSeconds = Mathf.Clamp(startResult.ActiveElapsedMs / 1000f, 0f, durationSeconds);
            claimedMilestone = startResult.Record.MilestoneSeconds;
            uiState = LectureUiState.LectureActive;
            isMutating = false;
            leaveConfirmOpen = false;
            finalMilestoneAttempts = 0;

            headerLabel.text = source != null
                ? source.CourseName.ToUpperInvariant()
                : "INTRODUCTION TO COMPUTER SCIENCE";
            statusLabel.text = "Lecture in progress";
            SetLeaveButtonInteractable(true);
            UpdateProgressVisual();

            overlayRoot.SetActive(true);
            panelRoot.SetActive(true);
            confirmRoot.SetActive(false);
            IsOpen = true;
            openedFrame = Time.frameCount;

            LockGameplayControls();

            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
            }

            // Already fully complete from hydration / idempotent start — close after confirm.
            if (claimedMilestone >= 90 || startResult.Record.IsResolved)
            {
                tickRoutine = StartCoroutine(FinalizeSuccessfulCompletionRoutine());
                return;
            }

            tickRoutine = StartCoroutine(TickRoutine());
        }

        private IEnumerator TickRoutine()
        {
            while (uiState == LectureUiState.LectureActive && claimedMilestone < 90)
            {
                elapsedSeconds = Mathf.Min(durationSeconds, elapsedSeconds + Time.unscaledDeltaTime);
                UpdateProgressVisual();

                int nextMilestone = NextMilestone(claimedMilestone);
                if (nextMilestone > 0 && elapsedSeconds + 0.0001f >= ScaledMilestoneSeconds(nextMilestone))
                {
                    yield return ClaimMilestoneRoutine(nextMilestone);

                    if (uiState == LectureUiState.Completed || uiState == LectureUiState.LeftEarly)
                    {
                        yield break;
                    }
                }

                yield return null;
            }
        }

        private float ScaledMilestoneSeconds(int milestoneSeconds)
        {
            return durationSeconds * (milestoneSeconds / 90f);
        }

        private static int NextMilestone(int claimed)
        {
            if (claimed < 30)
            {
                return 30;
            }

            if (claimed < 60)
            {
                return 60;
            }

            if (claimed < 90)
            {
                return 90;
            }

            return 0;
        }

        private IEnumerator ClaimMilestoneRoutine(int milestoneSeconds)
        {
            if (isMutating || sync == null || uiState != LectureUiState.LectureActive)
            {
                yield break;
            }

            // Never re-submit a milestone the server already confirmed.
            if (claimedMilestone >= milestoneSeconds)
            {
                yield break;
            }

            bool isFinalMilestone = milestoneSeconds >= 90;
            if (isFinalMilestone)
            {
                uiState = LectureUiState.Completing;
                statusLabel.text = "Completing class...";
                SetLeaveButtonInteractable(false);
                if (leaveConfirmOpen)
                {
                    leaveConfirmOpen = false;
                    confirmRoot.SetActive(false);
                }

                finalMilestoneAttempts++;
            }

            isMutating = true;
            bool done = false;
            bool success = false;
            AttendIcsSessionResult? successResult = null;

            sync.RequestAttendIcsMilestone(
                milestoneSeconds,
                result =>
                {
                    success = true;
                    successResult = result;
                    claimedMilestone = Mathf.Max(claimedMilestone, result.Record.MilestoneSeconds);
                    if (result.AppliedReputationDelta != 0)
                    {
                        statusLabel.text = $"+{result.AppliedReputationDelta} Academic Reputation";
                    }

                    UpdateProgressVisual();
                    done = true;
                },
                () =>
                {
                    success = false;
                    done = true;
                });

            while (!done)
            {
                yield return null;
            }

            isMutating = false;

            if (!success)
            {
                elapsedSeconds = Mathf.Max(0f, ScaledMilestoneSeconds(milestoneSeconds) - 0.5f);

                if (isFinalMilestone)
                {
                    // Stay on the lecture UI; do not mark completed or close.
                    uiState = LectureUiState.LectureActive;
                    statusLabel.text = "Could not complete class. Retrying…";
                    SetLeaveButtonInteractable(true);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    statusLabel.text = "Could not save progress. Retrying…";
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                yield break;
            }

            // Idempotent recovery: server may already be COMPLETED.
            if (successResult.HasValue && successResult.Value.Record.IsResolved
                && string.Equals(
                    successResult.Value.Record.Outcome,
                    "COMPLETED",
                    StringComparison.OrdinalIgnoreCase))
            {
                claimedMilestone = Mathf.Max(claimedMilestone, 90);
            }

            if (claimedMilestone >= 90)
            {
                yield return FinalizeSuccessfulCompletionRoutine();
            }
        }

        private IEnumerator FinalizeSuccessfulCompletionRoutine()
        {
            uiState = LectureUiState.Completed;
            SetLeaveButtonInteractable(false);
            leaveConfirmOpen = false;
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }

            statusLabel.text = "Class complete";
            UpdateProgressVisual();

            if (completionCloseDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(completionCloseDelaySeconds);
            }

            int reward = ConfirmedReputationReward(Mathf.Max(claimedMilestone, 90));
            string courseName = classroom != null && !string.IsNullOrWhiteSpace(classroom.CourseName)
                ? classroom.CourseName.Trim()
                : "Class";
            SystemNotificationUI.Show($"{courseName} completed! +{reward} Academic Reputation earned.");

            CloseLecture(LectureUiState.Completed);
        }

        private void OpenLeaveConfirm()
        {
            if (uiState != LectureUiState.LectureActive || leaveConfirmOpen || isMutating)
            {
                return;
            }

            leaveConfirmOpen = true;
            confirmRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (sync != null)
            {
                sync.RequestAttendIcsPause(_ => { }, () => { });
            }
        }

        private void StayInClass()
        {
            if (uiState != LectureUiState.LectureActive)
            {
                return;
            }

            leaveConfirmOpen = false;
            confirmRoot.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (sync != null)
            {
                sync.RequestAttendIcsResume(_ => { }, () => { });
            }
        }

        private void ConfirmLeave()
        {
            if (isMutating || sync == null || uiState != LectureUiState.LectureActive)
            {
                return;
            }

            isMutating = true;
            SetLeaveButtonInteractable(false);
            sync.RequestAttendIcsLeaveEarly(
                _ =>
                {
                    isMutating = false;
                    CloseLecture(LectureUiState.LeftEarly);
                },
                () =>
                {
                    isMutating = false;
                    SetLeaveButtonInteractable(true);
                    StayInClass();
                    statusLabel.text = "Could not leave class. Try again.";
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                });
        }

        private void CloseLecture(LectureUiState terminalState)
        {
            uiState = terminalState;
            leaveConfirmOpen = false;
            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
                tickRoutine = null;
            }

            HideImmediate();
            RestoreGameplayControls();

            Action callback = onClosed;
            onClosed = null;
            classroom = null;
            sync = null;
            callback?.Invoke();
        }

        private void HideImmediate()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }

            IsOpen = false;
            if (uiState != LectureUiState.Completed && uiState != LectureUiState.LeftEarly)
            {
                uiState = LectureUiState.Closed;
            }
        }

        private void SetLeaveButtonInteractable(bool interactable)
        {
            if (leaveButton != null)
            {
                leaveButton.interactable = interactable;
            }
        }

        private void LockGameplayControls()
        {
            CacheControlReferences();

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

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ownsGameplayLock = true;
        }

        private void RestoreGameplayControls()
        {
            if (!ownsGameplayLock)
            {
                return;
            }

            // Do not force-enable gameplay if another legitimate modal still owns input.
            if (IsAnotherBlockingModalOpen())
            {
                ownsGameplayLock = false;
                return;
            }

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
            ownsGameplayLock = false;
        }

        private static bool IsAnotherBlockingModalOpen()
        {
            return ClassroomChoiceUI.IsOpen
                || DialogueUI.IsOpen
                || CanteenQueueUI.IsOpen
                || AdmissionUI.IsOpen
                || IdCardUI.IsOpen
                || DailySummaryUI.IsOpen
                || ElevatorUI.IsOpen
                || AdvisorUI.IsOpen
                || (GameMenuManager.Instance != null && GameMenuManager.IsOpen);
        }

        private void CacheControlReferences()
        {
            if (cachedPlayerMovement == null)
            {
                cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            }

            if (cachedFirstPersonLook == null)
            {
                cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            }

            if (cachedInteraction == null)
            {
                cachedInteraction = FindFirstObjectByType<InteractionController>();
            }

            if (cachedCameraFollow == null)
            {
                cachedCameraFollow = FindFirstObjectByType<CameraFollow>();
            }
        }

        private void UpdateProgressVisual()
        {
            float normalized = durationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsedSeconds / durationSeconds);
            if (fillRect != null)
            {
                fillRect.anchorMax = new Vector2(normalized, 1f);
            }

            int displayedMinutes = Mathf.FloorToInt(normalized * 90f);
            if (timerLabel != null)
            {
                timerLabel.text = $"{displayedMinutes} / 90 min";
            }

            if (nextMilestoneLabel != null)
            {
                nextMilestoneLabel.text = FormatNextMilestoneCountdown(
                    elapsedSeconds,
                    durationSeconds,
                    claimedMilestone);
            }

            if (currentRewardLabel != null)
            {
                currentRewardLabel.text =
                    $"Current class reward: +{ConfirmedReputationReward(claimedMilestone)} Academic Reputation";
            }

            ApplyMilestoneRowColors(claimedMilestone);
        }

        /// <summary>
        /// Confirmed cumulative Academic Reputation from awarded milestones only (+2 each).
        /// </summary>
        public static int ConfirmedReputationReward(int confirmedMilestoneSeconds)
        {
            if (confirmedMilestoneSeconds >= 90)
            {
                return 6;
            }

            if (confirmedMilestoneSeconds >= 60)
            {
                return 4;
            }

            if (confirmedMilestoneSeconds >= 30)
            {
                return 2;
            }

            return 0;
        }

        /// <summary>
        /// Countdown to the next unclaimed milestone using the existing lecture elapsed time.
        /// </summary>
        public static string FormatNextMilestoneCountdown(
            float elapsedSeconds,
            float durationSeconds,
            int confirmedMilestoneSeconds)
        {
            if (confirmedMilestoneSeconds >= 90)
            {
                return "All milestones completed!";
            }

            int next = NextMilestone(confirmedMilestoneSeconds);
            if (next <= 0)
            {
                return "All milestones completed!";
            }

            float target = durationSeconds <= 0f
                ? next
                : durationSeconds * (next / 90f);
            int remaining = Mathf.Max(0, Mathf.CeilToInt(target - elapsedSeconds));
            string unit = remaining == 1 ? "second" : "seconds";
            return $"Next milestone in {remaining} {unit}";
        }

        private void ApplyMilestoneRowColors(int confirmedMilestoneSeconds)
        {
            int next = NextMilestone(confirmedMilestoneSeconds);
            SetMilestoneRowColor(milestone30Label, 30, confirmedMilestoneSeconds, next);
            SetMilestoneRowColor(milestone60Label, 60, confirmedMilestoneSeconds, next);
            SetMilestoneRowColor(milestone90Label, 90, confirmedMilestoneSeconds, next);
        }

        private static void SetMilestoneRowColor(
            TextMeshProUGUI label,
            int milestoneSeconds,
            int confirmedMilestoneSeconds,
            int nextMilestoneSeconds)
        {
            if (label == null)
            {
                return;
            }

            if (confirmedMilestoneSeconds >= milestoneSeconds)
            {
                label.color = UiTheme.Success;
            }
            else if (nextMilestoneSeconds == milestoneSeconds)
            {
                label.color = UiTheme.BrightOrange;
            }
            else
            {
                label.color = UiTheme.Grey;
            }
        }

        private void BuildUi()
        {
            GameObject canvasGo = new GameObject("ClassroomLectureCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            overlayRoot = CreateBackdrop(canvasGo.transform);

            panelRoot = CreateContentPanel(canvasGo.transform, "LecturePanel", PanelWidth);
            headerLabel = CreateLabel(panelRoot.transform, "Header", "ICS", 28f, UiTheme.BrightOrange);
            timerLabel = CreateLabel(panelRoot.transform, "Timer", "0 / 90 min", 20f, UiTheme.White);
            statusLabel = CreateLabel(panelRoot.transform, "Status", "Lecture in progress", 16f, UiTheme.Grey);

            GameObject barBg = new GameObject("ProgressBg");
            barBg.transform.SetParent(panelRoot.transform, false);
            LayoutElement barLe = barBg.AddComponent<LayoutElement>();
            barLe.minHeight = 22f;
            barLe.preferredHeight = 22f;
            barBg.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

            GameObject fillGo = new GameObject("ProgressFill");
            fillGo.transform.SetParent(barBg.transform, false);
            fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillGo.AddComponent<Image>().color = UiTheme.BrightOrange;

            nextMilestoneLabel = CreateLabel(
                panelRoot.transform,
                "NextMilestone",
                "Next milestone in 30 seconds",
                18f,
                UiTheme.BrightOrange);
            currentRewardLabel = CreateLabel(
                panelRoot.transform,
                "CurrentReward",
                "Current class reward: +0 Academic Reputation",
                17f,
                UiTheme.White);

            milestone30Label = CreateLabel(
                panelRoot.transform,
                "Milestone30",
                "30s milestone — +2 Academic Reputation",
                16f,
                UiTheme.BrightOrange);
            milestone60Label = CreateLabel(
                panelRoot.transform,
                "Milestone60",
                "60s milestone — +2 Academic Reputation",
                16f,
                UiTheme.Grey);
            milestone90Label = CreateLabel(
                panelRoot.transform,
                "Milestone90",
                "90s milestone — +2 Academic Reputation",
                16f,
                UiTheme.Grey);

            leaveButton = CreateButton(panelRoot.transform, "LeaveButton", "LEAVE CLASS", OpenLeaveConfirm);

            confirmRoot = CreateContentPanel(canvasGo.transform, "LeaveConfirmPanel", 600f);
            CreateLabel(
                confirmRoot.transform,
                "ConfirmText",
                "Leave this class early? You will keep the Academic Reputation you have earned, but receive a -4 Academic Reputation penalty.",
                18f,
                UiTheme.White);
            CreateButton(confirmRoot.transform, "StayButton", "STAY", StayInClass);
            CreateButton(confirmRoot.transform, "ConfirmLeaveButton", "LEAVE", ConfirmLeave);
            confirmRoot.SetActive(false);
        }

        private static GameObject CreateBackdrop(Transform parent)
        {
            GameObject go = new GameObject("Backdrop");
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = true;
            return go;
        }

        private static GameObject CreateContentPanel(Transform parent, string name, float width)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.94f);

            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 22, 22);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float size,
            Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = size + 4f;
            le.preferredHeight = size + 10f;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = ButtonHeight;
            le.preferredHeight = ButtonHeight;

            go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 1f);
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                {
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                return;
            }

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // ── EditMode test seams ──────────────────────────────────────────

        public void SetCompletionCloseDelayForTesting(float seconds)
        {
            completionCloseDelaySeconds = Mathf.Max(0f, seconds);
        }

        public bool IsLeaveButtonInteractableForTesting()
        {
            return leaveButton != null && leaveButton.interactable;
        }

        public string StatusTextForTesting()
        {
            return statusLabel != null ? statusLabel.text : string.Empty;
        }

        public int ClaimedMilestoneForTesting()
        {
            return claimedMilestone;
        }

        public int FinalMilestoneAttemptsForTesting()
        {
            return finalMilestoneAttempts;
        }

        public bool IsLecturePanelActiveForTesting()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }

        /// <summary>
        /// Drives ClaimMilestoneRoutine synchronously for EditMode tests (no WaitForSeconds).
        /// </summary>
        public IEnumerator ClaimMilestoneForTesting(int milestoneSeconds)
        {
            return ClaimMilestoneRoutine(milestoneSeconds);
        }

        public IEnumerator FinalizeSuccessfulCompletionForTesting()
        {
            return FinalizeSuccessfulCompletionRoutine();
        }
    }
}
