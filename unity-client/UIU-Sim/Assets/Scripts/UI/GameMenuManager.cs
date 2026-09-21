using System.Collections;
using TMPro;
using UIU.Simulator.Authentication;
using UIU.Simulator.Core;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Admission;
using UIU.Simulator.Gameplay.Advisor;
using UIU.Simulator.Gameplay.Elevator;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UIU.Simulator.UI
{
    /// <summary>
    /// Default player-facing in-game menu. Escape opens and closes it.
    /// Soft-pauses the local player only — the world keeps running (no Time.timeScale change).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class GameMenuManager : MonoBehaviour
    {
        public static GameMenuManager Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private const string SavePath = "api/players/me/save";
        private const float PanelWidth = 420f;
        private const float PanelHeight = 700f;
        private const float ButtonHeight = 44f;
        private const int CanvasSortOrder = 240;

        [Header("Appearance")]
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color panelColor = UiTheme.Black;
        [SerializeField] private Color buttonNormalColor = UiTheme.BrightOrange;
        [SerializeField] private Color buttonHighlightedColor = new Color(1f, 0.65f, 0.22f, 1f);
        [SerializeField] private Color buttonPressedColor = new Color(0.82f, 0.42f, 0.05f, 1f);
        [SerializeField] private Color buttonDisabledColor = new Color(0.22f, 0.22f, 0.22f, 0.85f);
        [SerializeField] private Color secondaryButtonColor = new Color(0.25f, 0.25f, 0.25f, 1f);

        private GameObject overlayRoot;
        private GameObject confirmRoot;
        private GameObject endDayConfirmRoot;
        private TextMeshProUGUI statusLabel;
        private TextMeshProUGUI endDayTitleLabel;
        private TextMeshProUGUI endDayBodyLabel;
        private Button[] menuButtons;
        private Button nextDayButton;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private InteractionController cachedInteraction;
        private CameraFollow cachedCameraFollow;
        private bool wasMovementEnabled = true;
        private bool wasLookEnabled = true;
        private bool wasInteractionEnabled = true;
        private bool wasCameraFollowEnabled = true;

        private bool isBusy;
        private bool isNavigating;
        private Coroutine saveRoutine;
        private Coroutine newGameRoutine;
        private Coroutine nextDayRoutine;

        public bool IsConfirmOpen { get; private set; }
        public bool IsEndDayConfirmOpen { get; private set; }

        public static GameMenuManager EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameMenuManager existing = FindFirstObjectByType<GameMenuManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("GameMenuManager");
            return host.AddComponent<GameMenuManager>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBeforeFirstScene()
        {
            EnsureExists();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                SafeDestroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            BuildUI();
            HideImmediate();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
            {
                Instance = null;
                IsOpen = false;
            }
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            HandleEscape();
        }

        /// <summary>
        /// Escape: open menu, close confirmation, or resume. Ignored on auth screens and over other modals.
        /// </summary>
        public void HandleEscape()
        {
            if (isNavigating)
            {
                return;
            }

            if (IdCardUI.IsOpen)
            {
                IdCardUI.Instance?.Hide();
                return;
            }

            if (IsConfirmOpen)
            {
                HideConfirm();
                return;
            }

            if (IsEndDayConfirmOpen)
            {
                HideEndDayConfirm();
                return;
            }

            if (DailySummaryUI.IsOpen)
            {
                return;
            }

            if (isBusy)
            {
                return;
            }

            if (IsOpen)
            {
                Resume();
                return;
            }

            if (!CanOpenMenu())
            {
                return;
            }

            Open();
        }

        public void Open()
        {
            if (IsOpen || isNavigating)
            {
                return;
            }

            if (overlayRoot == null)
            {
                BuildUI();
            }

            CacheAndDisablePlayerControls();
            AuthUiUtility.ShowUiCursor();
            AuthUiUtility.EnsureInputSystemEventSystem();
            EnsureEventSystem();

            HideConfirm();
            HideEndDayConfirm();
            SetStatus(string.Empty, UiTheme.Grey);
            SetMenuInteractable(true);

            overlayRoot.SetActive(true);
            IsOpen = true;
        }

        public void Resume()
        {
            Close(restoreGameplayControls: true);
        }

        public void Close(bool restoreGameplayControls = true)
        {
            if (IdCardUI.IsOpen)
            {
                IdCardUI.Instance?.Hide();
            }

            HideConfirm();
            HideEndDayConfirm();
            StopInFlightMenuWork();
            SetStatus(string.Empty, UiTheme.Grey);

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;

            if (restoreGameplayControls)
            {
                RestorePlayerControls();
            }
        }

        public void ShowSaveConfirmation()
        {
            SetStatus("Game Saved ✓", UiTheme.BrightOrange);
        }

        /// <summary>
        /// Closes any open menu overlays and restores gameplay controls to enabled defaults.
        /// Used after DailySummaryUI finishes a successful day advance so we do not restore
        /// a stale snapshot from when the menu first disabled input.
        /// </summary>
        public void ForceCloseAndRestoreGameplayControls()
        {
            HideConfirm();
            HideEndDayConfirm();
            StopInFlightMenuWork();
            SetStatus(string.Empty, UiTheme.Grey);

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;

            // Re-resolve live references — do not trust the Open() disabled snapshot.
            cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            cachedInteraction = FindFirstObjectByType<InteractionController>();
            cachedCameraFollow = FindFirstObjectByType<CameraFollow>();

            wasMovementEnabled = true;
            wasLookEnabled = true;
            wasInteractionEnabled = true;
            wasCameraFollowEnabled = true;
            RestorePlayerControls();
        }

        private bool CanOpenMenu()
        {
            if (Application.isPlaying && IsAuthOrBootstrapScene())
            {
                return false;
            }

            if (IsBlockingModalOpen())
            {
                return false;
            }

            return FindFirstObjectByType<PlayerMovement>() != null;
        }

        private static bool IsAuthOrBootstrapScene()
        {
            string name = SceneManager.GetActiveScene().name;
            return name == AuthSceneNames.Login
                || name == AuthSceneNames.SaveSelection
                || name == AuthSceneNames.Bootstrap;
        }

        private static bool IsBlockingModalOpen()
        {
            return ElevatorUI.IsOpen
                || AdvisorUI.IsOpen
                || DialogueUI.IsOpen
                || CanteenQueueUI.IsOpen
                || AdmissionUI.IsOpen
                || IdCardUI.IsOpen
                || DailySummaryUI.IsOpen
                || ClassroomChoiceUI.IsOpen
                || ClassroomLectureUI.IsOpen;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                return;
            }

            isNavigating = false;

            if (IsOpen || IsConfirmOpen || IsEndDayConfirmOpen)
            {
                Close(restoreGameplayControls: false);
            }

            if (IsAuthOrBootstrapScene())
            {
                AuthUiUtility.ShowUiCursor();
            }
        }

        private void CacheAndDisablePlayerControls()
        {
            cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            if (cachedPlayerMovement != null)
            {
                wasMovementEnabled = cachedPlayerMovement.enabled;
                cachedPlayerMovement.enabled = false;
            }

            cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            if (cachedFirstPersonLook != null)
            {
                wasLookEnabled = cachedFirstPersonLook.enabled;
                cachedFirstPersonLook.enabled = false;
            }

            cachedInteraction = FindFirstObjectByType<InteractionController>();
            if (cachedInteraction != null)
            {
                wasInteractionEnabled = cachedInteraction.enabled;
                cachedInteraction.enabled = false;
            }

            cachedCameraFollow = FindFirstObjectByType<CameraFollow>();
            if (cachedCameraFollow != null)
            {
                wasCameraFollowEnabled = cachedCameraFollow.enabled;
                cachedCameraFollow.enabled = false;
            }
        }

        private void RestorePlayerControls()
        {
            if (cachedPlayerMovement == null)
            {
                cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            }

            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.enabled = wasMovementEnabled;
            }

            if (cachedInteraction == null)
            {
                cachedInteraction = FindFirstObjectByType<InteractionController>();
            }

            if (cachedInteraction != null)
            {
                cachedInteraction.enabled = wasInteractionEnabled;
            }

            if (cachedCameraFollow == null)
            {
                cachedCameraFollow = FindFirstObjectByType<CameraFollow>();
            }

            if (cachedCameraFollow != null)
            {
                cachedCameraFollow.enabled = wasCameraFollowEnabled;
            }

            if (cachedFirstPersonLook == null)
            {
                cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            }

            if (cachedFirstPersonLook != null)
            {
                cachedFirstPersonLook.SuppressEscapeThisFrame();
                cachedFirstPersonLook.enabled = wasLookEnabled;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnResumeClicked()
        {
            if (isBusy || isNavigating)
            {
                return;
            }

            Resume();
        }

        private void OnSaveGameClicked()
        {
            if (isBusy || isNavigating || IsConfirmOpen)
            {
                return;
            }

            if (saveRoutine != null)
            {
                StopCoroutine(saveRoutine);
            }

            saveRoutine = StartCoroutine(SaveGameRoutine());
        }

        private void OnNewGameClicked()
        {
            if (isBusy || isNavigating || IsEndDayConfirmOpen)
            {
                return;
            }

            ShowConfirm();
        }

        private void OnNextDayClicked()
        {
            if (isBusy || isNavigating || IsConfirmOpen || IsEndDayConfirmOpen)
            {
                return;
            }

            PlayerSaveState saveState = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();

            if (saveState == null || !saveState.IsHydrated)
            {
                SetStatus("Checking save…", UiTheme.Grey);
                PlayerSaveState.EnsureExists().RefreshFromServer();
                return;
            }

            if (!saveState.HasActiveUniversityDay)
            {
                SetStatus("Complete admission before ending the day.", UiTheme.Red);
                return;
            }

            if (saveState.CurrentDay >= 6)
            {
                SetStatus("Semester progression is not available yet.", UiTheme.Grey);
                return;
            }

            ShowEndDayConfirm(saveState.CurrentDay);
        }

        private void OnIdCardClicked()
        {
            if (isBusy || isNavigating || IsConfirmOpen || IsEndDayConfirmOpen)
            {
                return;
            }

            PlayerSaveState saveState = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();

            if (saveState == null || !saveState.IsHydrated)
            {
                SetStatus("Checking ID card…", UiTheme.Grey);
                PlayerSaveState.EnsureExists().RefreshFromServer();
                return;
            }

            if (!saveState.HasSave || !saveState.IdCardIssued)
            {
                SetStatus("No ID card yet. Visit the receptionist.", UiTheme.Red);
                return;
            }

            SetStatus(string.Empty, UiTheme.Grey);
            IdCardUI.EnsureExists().Show();
        }

        private void OnPlaceholderClicked(string featureName)
        {
            if (isBusy || isNavigating)
            {
                return;
            }

            SetStatus($"{featureName} — coming soon", UiTheme.Grey);
        }

        private void StopInFlightMenuWork()
        {
            if (saveRoutine != null)
            {
                StopCoroutine(saveRoutine);
                saveRoutine = null;
            }

            if (nextDayRoutine != null)
            {
                StopCoroutine(nextDayRoutine);
                nextDayRoutine = null;
            }

            isBusy = false;
        }

        private void OnLogoutClicked()
        {
            if (isBusy || isNavigating)
            {
                return;
            }

            isNavigating = true;
            SetMenuInteractable(false);
            HideConfirm();

            AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
            host.AuthManager?.Logout();

            Close(restoreGameplayControls: false);
            AuthUiUtility.ShowUiCursor();
            SceneManager.LoadScene(AuthSceneNames.Login);
        }

        private void OnQuitClicked()
        {
            if (isNavigating)
            {
                return;
            }

            isNavigating = true;
            GameManager.EnsureExists().Quit();
        }

        private IEnumerator SaveGameRoutine()
        {
            isBusy = true;
            SetMenuInteractable(false);
            SetStatus("Saving…", UiTheme.Grey);

            AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
            ApiClient apiClient = host.ApiClient;
            UserSession session = host.AuthManager != null ? host.AuthManager.Session : null;

            if (apiClient == null || session == null || !session.HasToken)
            {
                SetStatus("Could not save — not authenticated", UiTheme.Red);
                isBusy = false;
                SetMenuInteractable(true);
                saveRoutine = null;
                yield break;
            }

            bool succeeded = false;
            string responseBody = null;
            string errorMessage = null;
            long errorCode = 0;

            yield return apiClient.Get(
                SavePath,
                session.JwtToken,
                body =>
                {
                    succeeded = true;
                    responseBody = body;
                },
                (error, code) =>
                {
                    errorMessage = error;
                    errorCode = code;
                });

            if (!succeeded)
            {
                string suffix = errorCode > 0 ? $" (HTTP {errorCode})" : string.Empty;
                SetStatus($"Could not save{suffix}: {errorMessage}", UiTheme.Red);
            }
            else
            {
                bool hasSave = false;
                try
                {
                    ApiClient.PlayerSaveStatusDto status = JsonUtility.FromJson<ApiClient.PlayerSaveStatusDto>(responseBody);
                    hasSave = status != null && status.hasSave;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GameMenuManager] Failed to parse save status: {ex.Message}");
                }

                if (hasSave)
                {
                    ShowSaveConfirmation();
                }
                else
                {
                    SetStatus("No save found", UiTheme.Grey);
                }
            }

            isBusy = false;
            SetMenuInteractable(true);
            saveRoutine = null;
        }

        private IEnumerator NewGameRoutine()
        {
            isBusy = true;
            SetMenuInteractable(false);
            SetStatus("Resetting progress…", UiTheme.Grey);

            AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
            ApiClient apiClient = host.ApiClient;
            UserSession session = host.AuthManager != null ? host.AuthManager.Session : null;

            if (apiClient == null || session == null || !session.HasToken)
            {
                SetStatus("Could not reset — not authenticated", UiTheme.Red);
                isBusy = false;
                SetMenuInteractable(true);
                isNavigating = false;
                newGameRoutine = null;
                yield break;
            }

            bool deleteOk = false;
            string deleteError = null;
            long deleteCode = 0;

            yield return apiClient.Delete(
                SavePath,
                session.JwtToken,
                _ => deleteOk = true,
                (error, code) =>
                {
                    deleteError = error;
                    deleteCode = code;
                });

            if (!deleteOk)
            {
                string suffix = deleteCode > 0 ? $" (HTTP {deleteCode})" : string.Empty;
                SetStatus($"Could not reset save{suffix}: {deleteError}", UiTheme.Red);
                isBusy = false;
                SetMenuInteractable(true);
                isNavigating = false;
                newGameRoutine = null;
                yield break;
            }

            DailyActivityState activityState = FindFirstObjectByType<DailyActivityState>();
            activityState?.ResetForNewGame();

            isNavigating = true;
            AuthUiUtility.ShowUiCursor();
            SceneManager.LoadScene(AuthSceneNames.SaveSelection);
            newGameRoutine = null;
        }

        private void ShowConfirm()
        {
            if (confirmRoot == null)
            {
                return;
            }

            IsConfirmOpen = true;
            confirmRoot.SetActive(true);
        }

        private void HideConfirm()
        {
            IsConfirmOpen = false;
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(false);
            }
        }

        private void ShowEndDayConfirm(int currentDay)
        {
            if (endDayConfirmRoot == null)
            {
                return;
            }

            if (endDayTitleLabel != null)
            {
                endDayTitleLabel.text = $"End Day {currentDay}?";
            }

            if (endDayBodyLabel != null)
            {
                endDayBodyLabel.text =
                    "Any unfinished required activities will be marked as missed and their penalties will be applied.";
            }

            IsEndDayConfirmOpen = true;
            endDayConfirmRoot.SetActive(true);
        }

        private void HideEndDayConfirm()
        {
            IsEndDayConfirmOpen = false;
            if (endDayConfirmRoot != null)
            {
                endDayConfirmRoot.SetActive(false);
            }
        }

        private void OnConfirmNewGameClicked()
        {
            if (isBusy || isNavigating)
            {
                return;
            }

            HideConfirm();
            if (newGameRoutine != null)
            {
                StopCoroutine(newGameRoutine);
            }

            newGameRoutine = StartCoroutine(NewGameRoutine());
        }

        private void OnConfirmEndDayClicked()
        {
            if (isBusy || isNavigating)
            {
                return;
            }

            HideEndDayConfirm();
            if (nextDayRoutine != null)
            {
                StopCoroutine(nextDayRoutine);
            }

            nextDayRoutine = StartCoroutine(EndDayRoutine());
        }

        private IEnumerator EndDayRoutine()
        {
            isBusy = true;
            SetMenuInteractable(false);
            SetStatus("Ending day…", UiTheme.Grey);

            PlayerProgressSync sync = FindFirstObjectByType<PlayerProgressSync>();
            if (sync == null || !sync.IsHydrated)
            {
                SetStatus("Progress is still loading.", UiTheme.Red);
                isBusy = false;
                SetMenuInteractable(true);
                nextDayRoutine = null;
                yield break;
            }

            bool finished = false;
            bool succeeded = false;
            DayFinalizeResult summary = default;
            string failureMessage = null;

            sync.RequestFinalizeDay(
                onSuccess: result =>
                {
                    succeeded = true;
                    summary = result;
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
                SetStatus(string.IsNullOrWhiteSpace(failureMessage)
                    ? "Could not end the day."
                    : failureMessage, UiTheme.Red);
                isBusy = false;
                SetMenuInteractable(true);
                nextDayRoutine = null;
                yield break;
            }

            // Keep controls locked while transferring to the summary modal.
            Close(restoreGameplayControls: false);
            DailySummaryUI.EnsureExists().Show(summary);

            isBusy = false;
            nextDayRoutine = null;
        }

        private void SetMenuInteractable(bool interactable)
        {
            if (menuButtons == null)
            {
                return;
            }

            for (int i = 0; i < menuButtons.Length; i++)
            {
                if (menuButtons[i] != null)
                {
                    menuButtons[i].interactable = interactable;
                }
            }
        }

        private void SetStatus(string message, Color color)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = message ?? string.Empty;
            statusLabel.color = color;
        }

        private void HideImmediate()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            HideConfirm();
            HideEndDayConfirm();
            IsOpen = false;
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject("GameMenuCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            overlayRoot = new GameObject("GameMenuOverlay");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            overlayRoot.AddComponent<Image>().color = overlayColor;

            GameObject panel = new GameObject("GameMenuPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.AddComponent<Image>().color = panelColor;

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 28, 24);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateTmpLabel(panel.transform, "Title", "GAME MENU", 28f, FontStyles.Bold, UiTheme.BrightOrange, 36f);
            CreateTmpLabel(panel.transform, "Subtitle", "Press Esc to resume", 14f, FontStyles.Normal, UiTheme.Grey, 22f);

            Button resume = CreateMenuButton(panel.transform, "Button_Resume", "Resume", OnResumeClicked);
            Button save = CreateMenuButton(panel.transform, "Button_SaveGame", "Save Game", OnSaveGameClicked);
            Button nextDay = CreateMenuButton(panel.transform, "Button_NextDay", "Next Day", OnNextDayClicked);
            Button newGame = CreateMenuButton(panel.transform, "Button_NewGame", "New Game", OnNewGameClicked);
            Button idCard = CreateMenuButton(panel.transform, "Button_IdCard", "ID Card", OnIdCardClicked);
            Button classRoutine = CreateMenuButton(panel.transform, "Button_ClassRoutine", "Class Routine", () => OnPlaceholderClicked("Class Routine"));
            Button settings = CreateMenuButton(panel.transform, "Button_Settings", "Settings", () => OnPlaceholderClicked("Settings"));
            Button logout = CreateMenuButton(panel.transform, "Button_Logout", "Logout", OnLogoutClicked);
            Button quit = CreateMenuButton(panel.transform, "Button_QuitGame", "Quit Game", OnQuitClicked);

            nextDayButton = nextDay;
            menuButtons = new[] { resume, save, nextDay, newGame, idCard, classRoutine, settings, logout, quit };

            GameObject statusGo = new GameObject("StatusLabel");
            statusGo.transform.SetParent(panel.transform, false);
            LayoutElement statusLe = statusGo.AddComponent<LayoutElement>();
            statusLe.minHeight = 28f;
            statusLe.preferredHeight = 28f;
            statusLabel = statusGo.AddComponent<TextMeshProUGUI>();
            statusLabel.fontSize = 16f;
            statusLabel.fontStyle = FontStyles.Bold;
            statusLabel.color = UiTheme.Grey;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.richText = false;
            statusLabel.text = string.Empty;

            BuildConfirmPopup(canvasGo.transform);
            BuildEndDayConfirmPopup(canvasGo.transform);
        }

        private void BuildConfirmPopup(Transform canvasTransform)
        {
            confirmRoot = new GameObject("NewGameConfirmOverlay");
            confirmRoot.transform.SetParent(canvasTransform, false);
            RectTransform dimRect = confirmRoot.AddComponent<RectTransform>();
            StretchFull(dimRect);
            confirmRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            GameObject box = new GameObject("ConfirmBox");
            box.transform.SetParent(confirmRoot.transform, false);
            RectTransform boxRect = box.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(440f, 220f);
            box.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.06f, 0.98f);

            GameObject messageGo = new GameObject("ConfirmMessage");
            messageGo.transform.SetParent(box.transform, false);
            RectTransform messageRect = messageGo.AddComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0f, 1f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.pivot = new Vector2(0.5f, 1f);
            messageRect.anchoredPosition = new Vector2(0f, -24f);
            messageRect.sizeDelta = new Vector2(-32f, 90f);
            TextMeshProUGUI message = messageGo.AddComponent<TextMeshProUGUI>();
            message.text = "Delete current university progress?";
            message.fontSize = 20f;
            message.fontStyle = FontStyles.Bold;
            message.color = UiTheme.White;
            message.alignment = TextAlignmentOptions.Center;
            message.richText = false;

            CreateAbsoluteButton(
                box.transform,
                "Button_CancelNewGame",
                "Cancel",
                new Vector2(-100f, -58f),
                new Vector2(160f, 44f),
                secondaryButtonColor,
                HideConfirm);

            CreateAbsoluteButton(
                box.transform,
                "Button_ConfirmNewGame",
                "Confirm",
                new Vector2(100f, -58f),
                new Vector2(160f, 44f),
                buttonNormalColor,
                OnConfirmNewGameClicked);

            confirmRoot.SetActive(false);
            IsConfirmOpen = false;
        }

        private void BuildEndDayConfirmPopup(Transform canvasTransform)
        {
            endDayConfirmRoot = new GameObject("EndDayConfirmOverlay");
            endDayConfirmRoot.transform.SetParent(canvasTransform, false);
            RectTransform dimRect = endDayConfirmRoot.AddComponent<RectTransform>();
            StretchFull(dimRect);
            endDayConfirmRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            GameObject box = new GameObject("EndDayConfirmBox");
            box.transform.SetParent(endDayConfirmRoot.transform, false);
            RectTransform boxRect = box.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(460f, 260f);
            box.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.06f, 0.98f);

            GameObject titleGo = new GameObject("EndDayTitle");
            titleGo.transform.SetParent(box.transform, false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -20f);
            titleRect.sizeDelta = new Vector2(-32f, 36f);
            endDayTitleLabel = titleGo.AddComponent<TextMeshProUGUI>();
            endDayTitleLabel.text = "End Day 1?";
            endDayTitleLabel.fontSize = 22f;
            endDayTitleLabel.fontStyle = FontStyles.Bold;
            endDayTitleLabel.color = UiTheme.BrightOrange;
            endDayTitleLabel.alignment = TextAlignmentOptions.Center;
            endDayTitleLabel.richText = false;

            GameObject messageGo = new GameObject("EndDayMessage");
            messageGo.transform.SetParent(box.transform, false);
            RectTransform messageRect = messageGo.AddComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0f, 1f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.pivot = new Vector2(0.5f, 1f);
            messageRect.anchoredPosition = new Vector2(0f, -64f);
            messageRect.sizeDelta = new Vector2(-36f, 90f);
            endDayBodyLabel = messageGo.AddComponent<TextMeshProUGUI>();
            endDayBodyLabel.text =
                "Any unfinished required activities will be marked as missed and their penalties will be applied.";
            endDayBodyLabel.fontSize = 16f;
            endDayBodyLabel.fontStyle = FontStyles.Normal;
            endDayBodyLabel.color = UiTheme.White;
            endDayBodyLabel.alignment = TextAlignmentOptions.Center;
            endDayBodyLabel.textWrappingMode = TextWrappingModes.Normal;
            endDayBodyLabel.richText = false;

            CreateAbsoluteButton(
                box.transform,
                "Button_CancelEndDay",
                "CANCEL",
                new Vector2(-100f, -78f),
                new Vector2(160f, 44f),
                secondaryButtonColor,
                HideEndDayConfirm);

            CreateAbsoluteButton(
                box.transform,
                "Button_ConfirmEndDay",
                "END DAY",
                new Vector2(100f, -78f),
                new Vector2(160f, 44f),
                buttonNormalColor,
                OnConfirmEndDayClicked);

            endDayConfirmRoot.SetActive(false);
            IsEndDayConfirmOpen = false;
        }

        private Button CreateMenuButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = ButtonHeight;
            le.preferredHeight = ButtonHeight;

            Image image = go.AddComponent<Image>();
            image.color = buttonNormalColor;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyButtonColors(button, buttonNormalColor);
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

        private Button CreateAbsoluteButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = color;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyButtonColors(button, color);
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

        private void ApplyButtonColors(Button button, Color normal)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = buttonHighlightedColor;
            colors.pressedColor = buttonPressedColor;
            colors.disabledColor = buttonDisabledColor;
            colors.selectedColor = buttonHighlightedColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static TextMeshProUGUI CreateTmpLabel(
            Transform parent,
            string name,
            string text,
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
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = false;
            tmp.raycastTarget = false;
            return tmp;
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

                return;
            }

#pragma warning disable CS0618
            StandaloneInputModule legacy = existing.GetComponent<StandaloneInputModule>();
#pragma warning restore CS0618
            if (legacy != null)
            {
                SafeDestroy(legacy);
            }

            if (existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                InputSystemUIInputModule module = existing.gameObject.AddComponent<InputSystemUIInputModule>();
                if (InputSystem.actions != null)
                {
                    module.actionsAsset = InputSystem.actions;
                }
            }
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
