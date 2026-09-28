using TMPro;
using UIU.Simulator.Authentication;
using UIU.Simulator.Gameplay.Admission;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.UI
{
    /// <summary>
    /// First-visit welcome modal for unidentified players (no ID card yet).
    /// Guides the player to the receptionist; does not open registration itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WelcomeCardUI : MonoBehaviour
    {
        public static WelcomeCardUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private const int CanvasSortOrder = 230;
        private const float PanelWidth = 560f;
        private const float PanelHeight = 360f;
        private const float ButtonHeight = 48f;

        private const string TitleCopy = "WELCOME TO UNITED INTERNATIONAL UNIVERSITY";
        private const string BodyCopy =
            "Welcome to United International University.\n\nPlease collect your ID card from the receptionist to begin.";
        private const string GuideTitleCopy = "GET YOUR ID CARD";
        private const string GuideBodyCopy = "Please visit the receptionist to get your ID card.";

        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color panelColor = UiTheme.Black;
        [SerializeField] private Color buttonNormalColor = UiTheme.BrightOrange;
        [SerializeField] private Color buttonHighlightedColor = new Color(1f, 0.65f, 0.22f, 1f);
        [SerializeField] private Color buttonPressedColor = new Color(0.82f, 0.42f, 0.05f, 1f);
        [SerializeField] private Color buttonDisabledColor = new Color(0.22f, 0.22f, 0.22f, 0.85f);
        [SerializeField] private Color secondaryButtonColor = new Color(0.25f, 0.25f, 0.25f, 1f);

        private GameObject overlayRoot;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI bodyLabel;
        private Button getIdCardButton;
        private Button goToReceptionistButton;
        private PlayerSaveState playerSaveState;
        private bool dismissedThisVisit;
        private bool hadIssuedIdentity;
        private bool saveEventsBound;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private CameraSwitcher cachedCameraSwitcher;
        private bool wasCameraSwitcherEnabled = true;
        private bool ownsGameplayLock;

        public static WelcomeCardUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            WelcomeCardUI existing = FindFirstObjectByType<WelcomeCardUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("WelcomeCardUI");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<WelcomeCardUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                SafeDestroy(gameObject);
                return;
            }

            Instance = this;
            BuildUI();
            HideImmediate();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            BindSaveEvents();
            EvaluateForCurrentSave();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindSaveEvents();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindSaveEvents();
            if (Instance == this)
            {
                Instance = null;
                IsOpen = false;
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
            }
        }

        public void Show()
        {
            if (IsOpen)
            {
                return;
            }

            if (overlayRoot == null)
            {
                BuildUI();
            }

            ShowWelcomeContent();
            overlayRoot.SetActive(true);
            IsOpen = true;
            LockPlayerInput();
            AuthUiUtility.EnsureInputSystemEventSystem();
            EnsureEventSystem();
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                HideImmediate();
                return;
            }

            dismissedThisVisit = true;
            HideImmediate();
            RestorePlayerInput();
        }

        /// <summary>
        /// Shows the welcome card when the hydrated save has no ID card, and hides it after issuance.
        /// </summary>
        public void EvaluateForCurrentSave()
        {
            BindSaveEvents();

            if (IsAuthOrBootstrapScene())
            {
                dismissedThisVisit = false;
                if (IsOpen)
                {
                    HideImmediate();
                    RestorePlayerInput();
                }

                return;
            }

            if (playerSaveState == null || !playerSaveState.IsHydrated)
            {
                return;
            }

            bool hasId = playerSaveState.HasIssuedIdCard;
            if (hadIssuedIdentity && !hasId)
            {
                dismissedThisVisit = false;
            }

            hadIssuedIdentity = hasId;

            if (hasId)
            {
                dismissedThisVisit = false;
                if (IsOpen)
                {
                    HideImmediate();
                    RestorePlayerInput();
                }

                FacultyHUD.Instance?.ApplyRoleVisibility();
                return;
            }

            if (dismissedThisVisit || IsOpen || AdmissionUI.IsOpen)
            {
                return;
            }

            Show();
        }

        private void OnGetIdCardClicked()
        {
            if (!IsOpen)
            {
                return;
            }

            ShowReceptionistGuideContent();
        }

        private void OnGoToReceptionistClicked()
        {
            // Dismiss so the player can walk to the receptionist.
            // Registration remains receptionist-only — never open AdmissionUI here.
            Hide();
        }

        private void ShowWelcomeContent()
        {
            if (titleLabel != null)
            {
                titleLabel.text = TitleCopy;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = BodyCopy;
            }

            if (getIdCardButton != null)
            {
                getIdCardButton.gameObject.SetActive(true);
            }

            if (goToReceptionistButton != null)
            {
                goToReceptionistButton.gameObject.SetActive(false);
            }
        }

        private void ShowReceptionistGuideContent()
        {
            if (titleLabel != null)
            {
                titleLabel.text = GuideTitleCopy;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = GuideBodyCopy;
            }

            if (getIdCardButton != null)
            {
                getIdCardButton.gameObject.SetActive(false);
            }

            if (goToReceptionistButton != null)
            {
                goToReceptionistButton.gameObject.SetActive(true);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                return;
            }

            EvaluateForCurrentSave();
        }

        private void BindSaveEvents()
        {
            if (playerSaveState == null)
            {
                playerSaveState = PlayerSaveState.Instance != null
                    ? PlayerSaveState.Instance
                    : FindFirstObjectByType<PlayerSaveState>();
            }

            if (playerSaveState == null || saveEventsBound)
            {
                return;
            }

            playerSaveState.OnHydrated += EvaluateForCurrentSave;
            playerSaveState.OnAdmissionCompleted += EvaluateForCurrentSave;
            playerSaveState.OnDayProgressChanged += EvaluateForCurrentSave;
            saveEventsBound = true;
        }

        private void UnbindSaveEvents()
        {
            if (playerSaveState == null || !saveEventsBound)
            {
                return;
            }

            playerSaveState.OnHydrated -= EvaluateForCurrentSave;
            playerSaveState.OnAdmissionCompleted -= EvaluateForCurrentSave;
            playerSaveState.OnDayProgressChanged -= EvaluateForCurrentSave;
            saveEventsBound = false;
        }

        private void LockPlayerInput()
        {
            if (ownsGameplayLock)
            {
                return;
            }

            cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.enabled = false;
            }

            cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            if (cachedFirstPersonLook != null)
            {
                cachedFirstPersonLook.enabled = false;
            }

            cachedCameraSwitcher = FindFirstObjectByType<CameraSwitcher>();
            if (cachedCameraSwitcher != null)
            {
                wasCameraSwitcherEnabled = cachedCameraSwitcher.enabled;
                cachedCameraSwitcher.enabled = false;
            }

            ownsGameplayLock = true;
            AuthUiUtility.ShowUiCursor();
        }

        private void RestorePlayerInput()
        {
            if (!ownsGameplayLock)
            {
                return;
            }

            if (cachedPlayerMovement == null)
            {
                cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            }

            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.enabled = true;
            }

            if (cachedFirstPersonLook == null)
            {
                cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            }

            if (cachedFirstPersonLook != null)
            {
                cachedFirstPersonLook.SuppressEscapeThisFrame();
                cachedFirstPersonLook.enabled = true;
            }

            if (cachedCameraSwitcher == null)
            {
                cachedCameraSwitcher = FindFirstObjectByType<CameraSwitcher>();
            }

            if (cachedCameraSwitcher != null)
            {
                cachedCameraSwitcher.enabled = wasCameraSwitcherEnabled;
            }

            ownsGameplayLock = false;

            if (AdmissionUI.IsOpen || GameMenuManager.IsOpen)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void HideImmediate()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;
        }

        private static bool IsAuthOrBootstrapScene()
        {
            string name = SceneManager.GetActiveScene().name;
            return name == AuthSceneNames.Login
                || name == AuthSceneNames.SaveSelection
                || name == AuthSceneNames.Bootstrap;
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject("WelcomeCardCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            overlayRoot = new GameObject("WelcomeCardOverlay");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            overlayRoot.AddComponent<Image>().color = overlayColor;

            GameObject panel = new GameObject("WelcomeCardPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.AddComponent<Image>().color = panelColor;

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 32, 28);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleLabel = CreateTmpLabel(panel.transform, "Title", TitleCopy, 24f, FontStyles.Bold, UiTheme.BrightOrange, 64f);
            bodyLabel = CreateTmpLabel(panel.transform, "Message", BodyCopy, 18f, FontStyles.Normal, UiTheme.White, 110f);

            getIdCardButton = CreateMenuButton(
                panel.transform, "Button_GetIdCard", "GET YOUR ID CARD", OnGetIdCardClicked, buttonNormalColor);
            goToReceptionistButton = CreateMenuButton(
                panel.transform, "Button_GoToReceptionist", "GO TO RECEPTIONIST", OnGoToReceptionistClicked, buttonNormalColor);
            goToReceptionistButton.gameObject.SetActive(false);
            CreateMenuButton(panel.transform, "Button_CloseWelcome", "CLOSE", Hide, secondaryButtonColor);
        }

        private Button CreateMenuButton(
            Transform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction onClick,
            Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = ButtonHeight;
            le.preferredHeight = ButtonHeight;

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
            tmp.textWrappingMode = TextWrappingModes.Normal;
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
