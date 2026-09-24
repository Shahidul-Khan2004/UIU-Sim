using System;
using TMPro;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.UI
{
    /// <summary>
    /// Compact Library Study confirmation. Locks movement, look, and world interaction while open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LibraryStudyUI : MonoBehaviour
    {
        public const string NotInstalledMessage = "Library study minigame is not installed yet.";
        public const string StudiedEnoughMessage = "You have studied enough for today.";

        private const float PanelWidth = 520f;
        private const float ButtonHeight = 48f;

        public static LibraryStudyUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private GameObject overlayRoot;
        private GameObject panelRoot;
        private Action onStart;
        private Action onBack;
        private int openedFrame = -1;
        private bool isArmed;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private InteractionController cachedInteraction;
        private CameraFollow cachedCameraFollow;
        private bool ownsGameplayLock;

        public static LibraryStudyUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            LibraryStudyUI existing = FindFirstObjectByType<LibraryStudyUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("LibraryStudyUI");
            return host.AddComponent<LibraryStudyUI>();
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
                InvokeBack();
            }
        }

        public void Show(Action onStartStudy, Action onBackChoice)
        {
            EnsureEventSystem();
            onStart = onStartStudy;
            onBack = onBackChoice;
            overlayRoot.SetActive(true);
            panelRoot.SetActive(true);
            IsOpen = true;
            openedFrame = Time.frameCount;
            isArmed = false;
            LockGameplayControls();
        }

        /// <summary>
        /// Closes the panel. When <paramref name="restoreGameplay"/> is false, controls stay locked
        /// so a future minigame can take over without a gameplay frame in between.
        /// </summary>
        public void Hide(bool restoreGameplay = true)
        {
            HideImmediate();
            onStart = null;
            onBack = null;
            if (restoreGameplay)
            {
                RestoreGameplayControls();
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void RestoreGameplayControlsIfOwned()
        {
            RestoreGameplayControls();
        }

        private void InvokeStart()
        {
            Action callback = onStart;
            Hide(restoreGameplay: false);
            callback?.Invoke();
        }

        private void InvokeBack()
        {
            Action callback = onBack;
            Hide(restoreGameplay: true);
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

            IsOpen = false;
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
            GameObject canvasGo = new GameObject("LibraryStudyCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 216;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            overlayRoot = CreateStretch(canvasGo.transform, "Backdrop", new Color(0f, 0f, 0f, 0.55f));

            panelRoot = new GameObject("LibraryStudyPanel");
            panelRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, 0f);
            Image bg = panelRoot.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.94f);
            VerticalLayoutGroup layout = panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = panelRoot.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel(panelRoot.transform, "Title", "LIBRARY STUDY", 28f, UiTheme.BrightOrange);
            CreateLabel(panelRoot.transform, "Prompt", "Spend some time studying?", 22f, UiTheme.White);
            CreateLabel(panelRoot.transform, "Limit", "You can study once per day.", 18f, UiTheme.Grey);
            CreateButton(panelRoot.transform, "StartStudyButton", "START STUDY", InvokeStart);
            CreateButton(panelRoot.transform, "BackButton", "BACK", InvokeBack);
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

        private static void CreateLabel(Transform parent, string name, string text, float size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = size + 8f;
            le.preferredHeight = size + 12f;
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
        }

        private static void CreateButton(Transform parent, string name, string label, Action onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = ButtonHeight;
            le.preferredHeight = ButtonHeight;
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 1f);
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
            tmp.fontSize = 20f;
            tmp.color = UiTheme.White;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
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
