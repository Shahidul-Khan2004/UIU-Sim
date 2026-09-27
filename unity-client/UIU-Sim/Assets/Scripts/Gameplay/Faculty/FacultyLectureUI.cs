using System;
using System.Collections;
using TMPro;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Faculty teaching timer. Copies the ClassroomLectureUI timer pattern without inheriting it.
    /// Complete: +5 Reputation. Leave immediately: -10 Reputation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacultyLectureUI : MonoBehaviour
    {
        private const float PanelWidth = 640f;
        private const float ButtonHeight = 52f;
        public const float DefaultDurationSeconds = 90f;

        private enum LectureUiState
        {
            Closed,
            LectureActive,
            Completing,
            Completed,
            LeftEarly
        }

        public static FacultyLectureUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private GameObject overlayRoot;
        private GameObject panelRoot;
        private GameObject confirmRoot;
        private TextMeshProUGUI headerLabel;
        private TextMeshProUGUI timerLabel;
        private TextMeshProUGUI statusLabel;
        private RectTransform fillRect;
        private Button leaveButton;

        private IFacultyTeachProgressSync sync;
        private Action onClosed;

        private float durationSeconds = DefaultDurationSeconds;
        private float elapsedSeconds;
        private LectureUiState uiState = LectureUiState.Closed;
        private bool isMutating;
        private bool leaveConfirmOpen;
        private Coroutine tickRoutine;
        private int openedFrame = -1;
        private float completionCloseDelaySeconds = 0.55f;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private InteractionController cachedInteraction;
        private CameraFollow cachedCameraFollow;
        private bool ownsGameplayLock;

        public static FacultyLectureUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            FacultyLectureUI existing = FindFirstObjectByType<FacultyLectureUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("FacultyLectureUI");
            return host.AddComponent<FacultyLectureUI>();
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

        public void BeginTeaching(
            string courseName,
            IFacultyTeachProgressSync progressSync,
            float lectureDurationSeconds,
            Action onClosedCallback)
        {
            EnsureEventSystem();

            if (FacultyClassroomChoiceUI.Instance != null)
            {
                FacultyClassroomChoiceUI.Instance.ReleaseOwnershipWithoutRestore();
            }

            sync = progressSync;
            onClosed = onClosedCallback;
            durationSeconds = Mathf.Max(1f, lectureDurationSeconds);
            elapsedSeconds = 0f;
            uiState = LectureUiState.LectureActive;
            isMutating = false;
            leaveConfirmOpen = false;

            headerLabel.text = string.IsNullOrWhiteSpace(courseName)
                ? "TEACHING"
                : courseName.Trim().ToUpperInvariant();
            statusLabel.text = "Teaching in progress";
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

            tickRoutine = StartCoroutine(TickRoutine());
        }

        /// <summary>EditMode test seam: start the lecture panel without networking.</summary>
        public void BeginTeachingForTesting(string courseName, float lectureDurationSeconds = DefaultDurationSeconds)
        {
            BeginTeaching(courseName, null, lectureDurationSeconds, null);
        }

        public float ElapsedSecondsForTesting => elapsedSeconds;
        public float DurationSecondsForTesting => durationSeconds;
        public string StatusTextForTesting => statusLabel != null ? statusLabel.text : string.Empty;
        public string TimerTextForTesting => timerLabel != null ? timerLabel.text : string.Empty;
        public bool IsLecturePanelActiveForTesting => panelRoot != null && panelRoot.activeSelf;

        public void SetCompletionCloseDelayForTesting(float seconds)
        {
            completionCloseDelaySeconds = Mathf.Max(0f, seconds);
        }

        public IEnumerator TickForTesting()
        {
            return TickRoutine();
        }

        private IEnumerator TickRoutine()
        {
            while (uiState == LectureUiState.LectureActive && elapsedSeconds < durationSeconds)
            {
                elapsedSeconds = Mathf.Min(durationSeconds, elapsedSeconds + Time.unscaledDeltaTime);
                UpdateProgressVisual();

                if (elapsedSeconds + 0.0001f >= durationSeconds)
                {
                    yield return CompleteRoutine();
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator CompleteRoutine()
        {
            if (isMutating || uiState != LectureUiState.LectureActive)
            {
                yield break;
            }

            uiState = LectureUiState.Completing;
            statusLabel.text = "Completing class...";
            SetLeaveButtonInteractable(false);

            if (sync == null)
            {
                statusLabel.text = "Class complete";
                uiState = LectureUiState.Completed;
                if (completionCloseDelaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(completionCloseDelaySeconds);
                }

                CloseLecture(LectureUiState.Completed);
                yield break;
            }

            isMutating = true;
            bool done = false;
            bool success = false;
            sync.RequestCompleteLecture(
                () =>
                {
                    success = true;
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
                uiState = LectureUiState.LectureActive;
                elapsedSeconds = Mathf.Max(0f, durationSeconds - 0.5f);
                statusLabel.text = "Could not complete class. Retrying…";
                SetLeaveButtonInteractable(true);
                yield break;
            }

            uiState = LectureUiState.Completed;
            statusLabel.text = "Class complete";
            UpdateProgressVisual();
            if (completionCloseDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(completionCloseDelaySeconds);
            }

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
        }

        private void ConfirmLeave()
        {
            if (isMutating || uiState != LectureUiState.LectureActive)
            {
                return;
            }

            if (sync == null)
            {
                CloseLecture(LectureUiState.LeftEarly);
                return;
            }

            isMutating = true;
            SetLeaveButtonInteractable(false);
            sync.RequestLeaveLecture(
                () =>
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

        private void BuildUi()
        {
            GameObject canvasGo = new GameObject("FacultyLectureCanvas");
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
            headerLabel = CreateLabel(panelRoot.transform, "Header", "TEACHING", 28f, UiTheme.BrightOrange);
            timerLabel = CreateLabel(panelRoot.transform, "Timer", "0 / 90 min", 20f, UiTheme.White);
            statusLabel = CreateLabel(panelRoot.transform, "Status", "Teaching in progress", 16f, UiTheme.Grey);

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

            leaveButton = CreateButton(panelRoot.transform, "LeaveButton", "LEAVE CLASS", OpenLeaveConfirm);

            confirmRoot = CreateContentPanel(canvasGo.transform, "LeaveConfirmPanel", 600f);
            CreateLabel(
                confirmRoot.transform,
                "ConfirmText",
                "Leave this class immediately? You will lose 10 Reputation.",
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

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size, Color color)
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
    }
}
