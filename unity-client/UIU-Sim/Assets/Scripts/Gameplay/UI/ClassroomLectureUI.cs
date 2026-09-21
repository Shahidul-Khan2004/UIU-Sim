using System;
using System.Collections;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Classroom;
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
    /// Compressed ICS lecture UI. Local timer drives progress display; milestones and leave
    /// are confirmed by the backend before HUD stats update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClassroomLectureUI : MonoBehaviour
    {
        public static ClassroomLectureUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private GameObject panelRoot;
        private GameObject confirmRoot;
        private TextMeshProUGUI headerLabel;
        private TextMeshProUGUI timerLabel;
        private TextMeshProUGUI statusLabel;
        private RectTransform fillRect;
        private Button leaveButton;

        private IcsClassroomInteractable classroom;
        private IAttendIcsProgressSync sync;
        private Action onClosed;

        private float durationSeconds = 90f;
        private float elapsedSeconds;
        private int claimedMilestone;
        private bool lectureActive;
        private bool isMutating;
        private bool leaveConfirmOpen;
        private Coroutine tickRoutine;
        private int openedFrame = -1;

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
                Instance = null;
                IsOpen = false;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!lectureActive || sync == null)
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
            if (lectureActive && sync != null)
            {
                sync.RequestAttendIcsPause(_ => { }, () => { });
            }
        }

        private void Update()
        {
            if (!IsOpen || leaveConfirmOpen)
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
            classroom = source;
            sync = progressSync;
            onClosed = onClosedCallback;
            durationSeconds = source != null ? Mathf.Max(1f, source.LectureDurationSeconds) : 90f;
            elapsedSeconds = Mathf.Clamp(startResult.ActiveElapsedMs / 1000f, 0f, durationSeconds);
            claimedMilestone = startResult.Record.MilestoneSeconds;
            lectureActive = true;
            isMutating = false;
            leaveConfirmOpen = false;

            headerLabel.text = source != null
                ? source.CourseName.ToUpperInvariant()
                : "INTRODUCTION TO COMPUTER SCIENCE";
            statusLabel.text = "Lecture in progress";
            UpdateProgressVisual();

            panelRoot.SetActive(true);
            confirmRoot.SetActive(false);
            IsOpen = true;
            openedFrame = Time.frameCount;

            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
            }

            tickRoutine = StartCoroutine(TickRoutine());
        }

        private IEnumerator TickRoutine()
        {
            while (lectureActive && claimedMilestone < 90)
            {
                elapsedSeconds = Mathf.Min(durationSeconds, elapsedSeconds + Time.unscaledDeltaTime);
                UpdateProgressVisual();

                int nextMilestone = NextMilestone(claimedMilestone);
                if (nextMilestone > 0 && elapsedSeconds + 0.0001f >= ScaledMilestoneSeconds(nextMilestone))
                {
                    yield return ClaimMilestoneRoutine(nextMilestone);
                }

                yield return null;
            }

            if (lectureActive && claimedMilestone >= 90)
            {
                statusLabel.text = "Class complete";
                yield return new WaitForSecondsRealtime(0.75f);
                CloseLecture();
            }
        }

        private float ScaledMilestoneSeconds(int milestoneSeconds)
        {
            // Default 90s lecture maps 30/60/90 directly. Shorter/longer durations scale proportionally.
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
            if (isMutating || sync == null)
            {
                yield break;
            }

            isMutating = true;
            bool done = false;
            bool success = false;

            sync.RequestAttendIcsMilestone(
                milestoneSeconds,
                result =>
                {
                    success = true;
                    claimedMilestone = result.Record.MilestoneSeconds;
                    if (result.AppliedReputationDelta != 0)
                    {
                        statusLabel.text = $"+{result.AppliedReputationDelta} Academic Reputation";
                    }

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
                // Failed request must not grant free rewards — rewind local timer slightly and retry later.
                elapsedSeconds = Mathf.Max(0f, ScaledMilestoneSeconds(milestoneSeconds) - 0.5f);
                statusLabel.text = "Could not save progress. Retrying…";
            }
            else if (claimedMilestone >= 90)
            {
                lectureActive = false;
            }
        }

        private void OpenLeaveConfirm()
        {
            if (!lectureActive || leaveConfirmOpen || isMutating)
            {
                return;
            }

            leaveConfirmOpen = true;
            confirmRoot.SetActive(true);
            if (sync != null)
            {
                sync.RequestAttendIcsPause(_ => { }, () => { });
            }
        }

        private void StayInClass()
        {
            leaveConfirmOpen = false;
            confirmRoot.SetActive(false);
            if (sync != null)
            {
                sync.RequestAttendIcsResume(_ => { }, () => { });
            }
        }

        private void ConfirmLeave()
        {
            if (isMutating || sync == null)
            {
                return;
            }

            isMutating = true;
            sync.RequestAttendIcsLeaveEarly(
                _ =>
                {
                    isMutating = false;
                    CloseLecture();
                },
                () =>
                {
                    isMutating = false;
                    StayInClass();
                    statusLabel.text = "Could not leave class. Try again.";
                });
        }

        private void CloseLecture()
        {
            lectureActive = false;
            leaveConfirmOpen = false;
            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
                tickRoutine = null;
            }

            HideImmediate();
            Action callback = onClosed;
            onClosed = null;
            classroom = null;
            sync = null;
            callback?.Invoke();
        }

        private void HideImmediate()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }

            IsOpen = false;
        }

        private void UpdateProgressVisual()
        {
            float normalized = durationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsedSeconds / durationSeconds);
            if (fillRect != null)
            {
                fillRect.anchorMax = new Vector2(normalized, 1f);
            }

            int displayedMinutes = Mathf.FloorToInt(normalized * 90f);
            timerLabel.text = $"{displayedMinutes} / 90 min  ·  Milestone {claimedMilestone}s";
        }

        private void BuildUi()
        {
            GameObject canvasGo = new GameObject("ClassroomLectureCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            panelRoot = CreatePanel(canvasGo.transform, "LecturePanel", new Vector2(640f, 280f));
            headerLabel = CreateLabel(panelRoot.transform, "Header", "ICS", 24f, UiTheme.BrightOrange, new Vector2(0f, 90f));
            timerLabel = CreateLabel(panelRoot.transform, "Timer", "0 / 90 min", 18f, UiTheme.White, new Vector2(0f, 45f));
            statusLabel = CreateLabel(panelRoot.transform, "Status", "Lecture in progress", 16f, UiTheme.Grey, new Vector2(0f, 10f));

            GameObject barBg = new GameObject("ProgressBg");
            barBg.transform.SetParent(panelRoot.transform, false);
            RectTransform barBgRect = barBg.AddComponent<RectTransform>();
            barBgRect.sizeDelta = new Vector2(520f, 22f);
            barBgRect.anchoredPosition = new Vector2(0f, -35f);
            barBg.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

            GameObject fillGo = new GameObject("ProgressFill");
            fillGo.transform.SetParent(barBg.transform, false);
            fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillGo.AddComponent<Image>().color = UiTheme.BrightOrange;

            leaveButton = CreateButton(panelRoot.transform, "LeaveButton", "LEAVE CLASS", new Vector2(0f, -95f), OpenLeaveConfirm);

            confirmRoot = CreatePanel(canvasGo.transform, "LeaveConfirmPanel", new Vector2(560f, 260f));
            CreateLabel(
                confirmRoot.transform,
                "ConfirmText",
                "Leave this class early? You will keep the Academic Reputation you have earned, but receive a -5 Academic Reputation penalty.",
                16f,
                UiTheme.White,
                new Vector2(0f, 50f));
            CreateButton(confirmRoot.transform, "StayButton", "STAY", new Vector2(-120f, -70f), StayInClass);
            CreateButton(confirmRoot.transform, "ConfirmLeaveButton", "LEAVE", new Vector2(120f, -70f), ConfirmLeave);
            confirmRoot.SetActive(false);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.94f);
            return go;
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float size,
            Color color,
            Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(520f, 70f);
            rect.anchoredPosition = anchoredPos;
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Action onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 44f);
            rect.anchoredPosition = pos;
            go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 1f);
            Button button = go.AddComponent<Button>();
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
            tmp.fontSize = 16f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
