using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    /// Library table self-study: confirm, pick a local PDF, then a 90s study session.
    /// Locks movement, look, and world interaction while open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LibrarySelfStudyUI : MonoBehaviour
    {
        public const float DefaultDurationSeconds = 90f;
        private const float PanelWidth = 640f;
        private const float ButtonHeight = 52f;
        private const float MaterialsListHeight = 240f;
        private const float MaterialButtonHeight = 56f;

        public static LibrarySelfStudyUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }
        public static bool BlocksGameplay => Instance != null && Instance.ownsGameplayLock;

        private enum Step
        {
            Closed,
            Confirm,
            Materials,
            Session
        }

        private GameObject overlayRoot;
        private GameObject confirmRoot;
        private GameObject materialsRoot;
        private GameObject sessionRoot;
        private Transform materialsList;
        private TextMeshProUGUI headerLabel;
        private TextMeshProUGUI materialNameLabel;
        private TextMeshProUGUI timerLabel;
        private TextMeshProUGUI statusLabel;
        private TextMeshProUGUI nextMilestoneLabel;
        private TextMeshProUGUI currentRewardLabel;
        private TextMeshProUGUI milestone30Label;
        private TextMeshProUGUI milestone60Label;
        private TextMeshProUGUI milestone90Label;
        private RectTransform fillRect;
        private Button leaveButton;
        private readonly List<GameObject> materialButtons = new List<GameObject>();
        private string selectedMaterialName = string.Empty;

        private ILibrarySelfStudyProgressSync progressSync;
        private Step step = Step.Closed;
        private int openedFrame = -1;
        private bool isArmed;
        private bool isMutating;
        private float durationSeconds = DefaultDurationSeconds;
        private float elapsedSeconds;
        private int claimedMilestone;
        private Coroutine tickRoutine;
        private int sessionVersion;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private InteractionController cachedInteraction;
        private CameraFollow cachedCameraFollow;
        private bool ownsGameplayLock;

        public static LibrarySelfStudyUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            LibrarySelfStudyUI existing = FindFirstObjectByType<LibrarySelfStudyUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("LibrarySelfStudyUI");
            return host.AddComponent<LibrarySelfStudyUI>();
        }

        public void SetProgressSyncForTesting(ILibrarySelfStudyProgressSync sync) => progressSync = sync;

        public void SetDurationForTesting(float seconds)
        {
            durationSeconds = Mathf.Max(0.1f, seconds);
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

        private void OnDisable()
        {
            if (step == Step.Session && progressSync != null)
            {
                progressSync.RequestLibrarySelfStudyAbandon(_ => { }, () => { });
            }
        }

        private void Update()
        {
            if (!IsOpen || !isArmed)
            {
                if (IsOpen && Time.frameCount > openedFrame)
                {
                    isArmed = true;
                }

                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (step == Step.Session)
                {
                    LeaveSession();
                }
                else
                {
                    Close(restoreGameplay: true);
                }
            }
        }

        public void ShowConfirm()
        {
            EnsureEventSystem();
            HidePanels();
            confirmRoot.SetActive(true);
            overlayRoot.SetActive(true);
            step = Step.Confirm;
            IsOpen = true;
            openedFrame = Time.frameCount;
            isArmed = false;
            isMutating = false;
            LockGameplayControls();
        }

        private void ShowMaterials()
        {
            HidePanels();
            RebuildMaterialButtons();
            materialsRoot.SetActive(true);
            overlayRoot.SetActive(true);
            step = Step.Materials;
            IsOpen = true;
        }

        private void BeginSession(LibrarySelfStudySessionResult startResult)
        {
            HidePanels();
            sessionRoot.SetActive(true);
            overlayRoot.SetActive(true);
            step = Step.Session;
            IsOpen = true;
            claimedMilestone = startResult.Record.MilestoneSeconds;
            elapsedSeconds = Mathf.Clamp(startResult.ActiveElapsedMs / 1000f, 0f, durationSeconds);
            if (headerLabel != null)
            {
                headerLabel.text = "SELF STUDY";
            }

            if (materialNameLabel != null)
            {
                materialNameLabel.text = string.IsNullOrWhiteSpace(selectedMaterialName)
                    ? "Study Material"
                    : selectedMaterialName;
            }

            statusLabel.text = "Studying...";
            SetLeaveButtonInteractable(true);
            UpdateProgressVisual();

            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
            }

            if (claimedMilestone >= 90 || startResult.AlreadyCompleted || startResult.Record.Status == ActivityStatus.Completed)
            {
                FinishCompleted();
                return;
            }

            int version = ++sessionVersion;
            if (!startResult.SessionActive)
            {
                EnsureSync();
                progressSync?.RequestLibrarySelfStudyResume(
                    result =>
                    {
                        if (!IsCurrentSession(version))
                        {
                            return;
                        }

                        elapsedSeconds = Mathf.Clamp(result.ActiveElapsedMs / 1000f, 0f, durationSeconds);
                        tickRoutine = StartCoroutine(TickRoutine(version));
                    },
                    () =>
                    {
                        if (IsCurrentSession(version))
                        {
                            Close(restoreGameplay: true);
                            SystemNotificationUI.Show("Could not resume studying. Try again.");
                        }
                    });
                return;
            }

            tickRoutine = StartCoroutine(TickRoutine(version));
        }

        private IEnumerator TickRoutine(int version)
        {
            while (step == Step.Session && claimedMilestone < 90 && IsCurrentSession(version))
            {
                elapsedSeconds = Mathf.Min(durationSeconds, elapsedSeconds + Time.unscaledDeltaTime);
                UpdateProgressVisual();

                int next = NextMilestone(claimedMilestone);
                float target = ScaledMilestoneSeconds(next);
                if (next > 0 && elapsedSeconds + 0.0001f >= target)
                {
                    yield return ClaimMilestoneRoutine(version, next);
                    if (step != Step.Session)
                    {
                        yield break;
                    }
                }

                yield return null;
            }
        }

        private IEnumerator ClaimMilestoneRoutine(int version, int milestoneSeconds)
        {
            if (isMutating || !IsCurrentSession(version) || step != Step.Session)
            {
                yield break;
            }

            EnsureSync();
            if (progressSync == null)
            {
                yield break;
            }

            isMutating = true;
            bool done = false;
            bool success = false;
            LibrarySelfStudySessionResult result = default;

            progressSync.RequestLibrarySelfStudyMilestone(
                milestoneSeconds,
                value =>
                {
                    success = true;
                    result = value;
                    claimedMilestone = Mathf.Max(claimedMilestone, value.Record.MilestoneSeconds);
                    if (value.AppliedReputationDelta != 0)
                    {
                        statusLabel.text = $"+{value.AppliedReputationDelta} Academic Reputation";
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
            if (!IsCurrentSession(version) || step != Step.Session)
            {
                yield break;
            }

            if (!success)
            {
                elapsedSeconds = Mathf.Max(0f, ScaledMilestoneSeconds(milestoneSeconds) - 0.5f);
                statusLabel.text = "Could not save progress. Retrying…";
                yield break;
            }

            if (result.AlreadyCompleted || result.Record.Status == ActivityStatus.Completed || claimedMilestone >= 90)
            {
                FinishCompleted();
            }
        }

        private void FinishCompleted()
        {
            statusLabel.text = "Study complete";
            elapsedSeconds = durationSeconds;
            SetLeaveButtonInteractable(false);
            UpdateProgressVisual();
            sessionVersion++;
            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
                tickRoutine = null;
            }

            Close(restoreGameplay: true);
            SystemNotificationUI.Show(
                "Study session completed.\nYou have studied enough for today. Good job.");
        }

        private void OnYes()
        {
            ShowMaterials();
        }

        private void OnNo()
        {
            Close(restoreGameplay: true);
        }

        private void OnSelectMaterial(Library.StudyMaterial material)
        {
            if (isMutating)
            {
                return;
            }

            selectedMaterialName = material.DisplayName;
            if (!Library.StudyMaterialManager.Open(material))
            {
                SystemNotificationUI.Show("Could not open that study material.");
                return;
            }

            EnsureSync();
            if (progressSync == null)
            {
                SystemNotificationUI.Show("Progress sync is not available.");
                Close(restoreGameplay: true);
                return;
            }

            isMutating = true;
            progressSync.RequestLibrarySelfStudyStart(
                onSuccess: result =>
                {
                    isMutating = false;
                    if (result.AlreadyCompleted || result.Record.Status == ActivityStatus.Completed)
                    {
                        Close(restoreGameplay: true);
                        SystemNotificationUI.Show(Library.LibrarySelfStudyInteractable.StudiedEnoughMessage);
                        return;
                    }

                    BeginSession(result);
                },
                onFailure: () =>
                {
                    isMutating = false;
                    Close(restoreGameplay: true);
                });
        }

        private void LeaveSession()
        {
            if (step != Step.Session || isMutating)
            {
                return;
            }

            sessionVersion++;
            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
                tickRoutine = null;
            }

            EnsureSync();
            progressSync?.RequestLibrarySelfStudyAbandon(_ => { }, () => { });
            Close(restoreGameplay: true);
            SystemNotificationUI.Show(Library.LibrarySelfStudyInteractable.FinishedStudyingMessage);
        }

        private void Close(bool restoreGameplay)
        {
            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
                tickRoutine = null;
            }

            HideImmediate();
            step = Step.Closed;
            isMutating = false;
            if (restoreGameplay)
            {
                RestoreGameplayControls();
            }
        }

        private bool IsCurrentSession(int version) => this != null && isActiveAndEnabled && sessionVersion == version && step == Step.Session;

        private float ScaledMilestoneSeconds(int milestoneSeconds)
        {
            return durationSeconds * (milestoneSeconds / 90f);
        }

        private static int NextMilestone(int claimed)
        {
            if (claimed < 30) return 30;
            if (claimed < 60) return 60;
            if (claimed < 90) return 90;
            return 0;
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
                nextMilestoneLabel.text = ClassroomLectureUI.FormatNextMilestoneCountdown(
                    elapsedSeconds,
                    durationSeconds,
                    claimedMilestone);
            }

            if (currentRewardLabel != null)
            {
                currentRewardLabel.text = $"Academic Reputation: +{ConfirmedStudyReward(claimedMilestone)}";
            }

            ApplyMilestoneRowColors(claimedMilestone);
        }

        private static int ConfirmedStudyReward(int confirmedMilestoneSeconds)
        {
            if (confirmedMilestoneSeconds >= 90)
            {
                return 3;
            }

            if (confirmedMilestoneSeconds >= 60)
            {
                return 2;
            }

            if (confirmedMilestoneSeconds >= 30)
            {
                return 1;
            }

            return 0;
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

        private void SetLeaveButtonInteractable(bool interactable)
        {
            if (leaveButton != null)
            {
                leaveButton.interactable = interactable;
            }
        }

        private void HideImmediate()
        {
            HidePanels();
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;
        }

        private void HidePanels()
        {
            if (confirmRoot != null) confirmRoot.SetActive(false);
            if (materialsRoot != null) materialsRoot.SetActive(false);
            if (sessionRoot != null) sessionRoot.SetActive(false);
        }

        private void LockGameplayControls()
        {
            CacheControlReferences();
            if (cachedPlayerMovement != null) cachedPlayerMovement.enabled = false;
            if (cachedFirstPersonLook != null) cachedFirstPersonLook.enabled = false;
            if (cachedInteraction != null) cachedInteraction.enabled = false;
            if (cachedCameraFollow != null) cachedCameraFollow.enabled = false;
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
            if (cachedPlayerMovement != null) cachedPlayerMovement.enabled = true;
            if (cachedInteraction != null) cachedInteraction.enabled = true;
            if (cachedCameraFollow != null) cachedCameraFollow.enabled = true;
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
            if (cachedPlayerMovement == null) cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            if (cachedFirstPersonLook == null) cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            if (cachedInteraction == null) cachedInteraction = FindFirstObjectByType<InteractionController>();
            if (cachedCameraFollow == null) cachedCameraFollow = FindFirstObjectByType<CameraFollow>();
        }

        private void EnsureSync()
        {
            if (progressSync == null)
            {
                progressSync = FindFirstObjectByType<PlayerProgressSync>();
            }
        }

        private void RebuildMaterialButtons()
        {
            for (int i = materialsList.childCount - 1; i >= 0; i--)
            {
                Destroy(materialsList.GetChild(i).gameObject);
            }

            materialButtons.Clear();
            Library.StudyMaterial[] materials = Library.StudyMaterialManager.ListMaterials();
            if (materials.Length == 0)
            {
                CreateLabel(materialsList, "EmptyMaterials", "No study materials found.", 18f, UiTheme.Grey);
                return;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Library.StudyMaterial material = materials[i];
                Button button = CreateButton(
                    materialsList,
                    "MaterialButton_" + i,
                    material.DisplayName,
                    () => OnSelectMaterial(material),
                    wrapLabel: true);
                materialButtons.Add(button.gameObject);
            }
        }

        private void BuildUi()
        {
            GameObject canvasGo = new GameObject("LibrarySelfStudyCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 217;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            overlayRoot = CreateStretch(canvasGo.transform, "Backdrop", new Color(0f, 0f, 0f, 0.55f));

            confirmRoot = CreateContentPanel(canvasGo.transform, "ConfirmPanel");
            CreateLabel(confirmRoot.transform, "Title", "STUDY", 28f, UiTheme.BrightOrange);
            CreateLabel(confirmRoot.transform, "Prompt", "Do you want to study?", 22f, UiTheme.White);
            CreateButton(confirmRoot.transform, "YesButton", "YES", OnYes);
            CreateButton(confirmRoot.transform, "NoButton", "NO", OnNo);

            materialsRoot = CreateContentPanel(canvasGo.transform, "MaterialsPanel");
            CreateLabel(
                materialsRoot.transform,
                "MaterialsTitle",
                "SELECT STUDY MATERIAL",
                26f,
                UiTheme.BrightOrange);
            materialsList = CreateMaterialsScroll(materialsRoot.transform);
            CreateButton(materialsRoot.transform, "MaterialsBackButton", "NO", OnNo);

            sessionRoot = CreateContentPanel(canvasGo.transform, "SessionPanel");
            headerLabel = CreateLabel(sessionRoot.transform, "Header", "SELF STUDY", 28f, UiTheme.BrightOrange);
            materialNameLabel = CreateWrappingLabel(
                sessionRoot.transform,
                "MaterialName",
                "Study Material",
                18f,
                UiTheme.White);
            CreateLabel(sessionRoot.transform, "TimeRemainingCaption", "Time Remaining", 16f, UiTheme.Grey);
            timerLabel = CreateLabel(sessionRoot.transform, "Timer", "0 / 90 min", 20f, UiTheme.White);
            statusLabel = CreateLabel(sessionRoot.transform, "Status", "Studying...", 16f, UiTheme.Grey);

            GameObject barBg = new GameObject("ProgressBg");
            barBg.transform.SetParent(sessionRoot.transform, false);
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
                sessionRoot.transform,
                "NextMilestone",
                "Next milestone in 30 seconds",
                18f,
                UiTheme.BrightOrange);
            currentRewardLabel = CreateLabel(
                sessionRoot.transform,
                "CurrentReward",
                "Academic Reputation: +0",
                17f,
                UiTheme.White);
            milestone30Label = CreateLabel(
                sessionRoot.transform,
                "Milestone30",
                "30s milestone — +1 Academic Reputation",
                16f,
                UiTheme.BrightOrange);
            milestone60Label = CreateLabel(
                sessionRoot.transform,
                "Milestone60",
                "60s milestone — +1 Academic Reputation",
                16f,
                UiTheme.Grey);
            milestone90Label = CreateLabel(
                sessionRoot.transform,
                "Milestone90",
                "90s milestone — +1 Academic Reputation",
                16f,
                UiTheme.Grey);
            leaveButton = CreateButton(sessionRoot.transform, "LeaveButton", "LEAVE", LeaveSession);
        }

        private static Transform CreateMaterialsScroll(Transform parent)
        {
            GameObject scrollGo = new GameObject("MaterialsScroll");
            scrollGo.transform.SetParent(parent, false);
            LayoutElement scrollLe = scrollGo.AddComponent<LayoutElement>();
            scrollLe.minHeight = 120f;
            scrollLe.preferredHeight = MaterialsListHeight;
            Image scrollImage = scrollGo.AddComponent<Image>();
            scrollImage.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            scrollImage.raycastTarget = true;
            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.white;
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = new GameObject("MaterialsList");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup listLayout = content.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(8, 8, 8, 8);
            listLayout.spacing = 8f;
            listLayout.childAlignment = TextAnchor.UpperCenter;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            return content.transform;
        }

        private static GameObject CreateContentPanel(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(PanelWidth, 0f);
            go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.94f);
            go.AddComponent<RectMask2D>();

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

        private static GameObject CreateStretch(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
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
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static TextMeshProUGUI CreateWrappingLabel(
            Transform parent,
            string name,
            string text,
            float size,
            Color color)
        {
            TextMeshProUGUI label = CreateLabel(parent, name, text, size, color);
            LayoutElement le = label.GetComponent<LayoutElement>();
            le.minHeight = size + 8f;
            le.preferredHeight = size * 2.4f;
            return label;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Action onClick,
            bool wrapLabel = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = wrapLabel ? MaterialButtonHeight : ButtonHeight;
            le.preferredHeight = wrapLabel ? MaterialButtonHeight : ButtonHeight;
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 1f);
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
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20f;
            tmp.color = UiTheme.White;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            if (wrapLabel)
            {
                tmp.maxVisibleLines = 2;
            }

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
