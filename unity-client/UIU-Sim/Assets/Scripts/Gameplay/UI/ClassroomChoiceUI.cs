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
    /// Compact ICS classroom choice modal: Attend / Punch ID and Leave / Back.
    /// Owns gameplay input lock + unlockable cursor while open (ElevatorUI / AdmissionUI pattern).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClassroomChoiceUI : MonoBehaviour
    {
        private const float PanelWidth = 620f;
        private const float ButtonHeight = 52f;
        private const float ButtonWidth = 460f;

        public static ClassroomChoiceUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private GameObject overlayRoot;
        private GameObject panelRoot;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI roomLabel;
        private Action onAttend;
        private Action onProxy;
        private Action onBack;
        private int openedFrame = -1;
        private bool isArmed;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private InteractionController cachedInteraction;
        private CameraFollow cachedCameraFollow;
        private bool ownsGameplayLock;

        public static ClassroomChoiceUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            ClassroomChoiceUI existing = FindFirstObjectByType<ClassroomChoiceUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("ClassroomChoiceUI");
            return host.AddComponent<ClassroomChoiceUI>();
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
            if (!IsOpen)
            {
                return;
            }

            if (!isArmed)
            {
                if (Time.frameCount > openedFrame)
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

        public void Show(string title, string roomLine, Action onAttendClass, Action onPunchId, Action onBackChoice)
        {
            EnsureEventSystem();
            onAttend = onAttendClass;
            onProxy = onPunchId;
            onBack = onBackChoice;
            titleLabel.text = title;
            roomLabel.text = roomLine;

            overlayRoot.SetActive(true);
            panelRoot.SetActive(true);
            IsOpen = true;
            openedFrame = Time.frameCount;
            isArmed = false;

            LockGameplayControls();
        }

        /// <summary>
        /// Closes the choice modal. When <paramref name="restoreGameplay"/> is false, keeps
        /// movement/look locked and cursor unlocked for a seamless handoff to ClassroomLectureUI.
        /// </summary>
        public void Hide(bool restoreGameplay = true)
        {
            HideImmediate();
            onAttend = null;
            onProxy = null;
            onBack = null;

            if (restoreGameplay)
            {
                RestoreGameplayControls();
            }
            else
            {
                // Keep ownership until lecture UI claims it, but ensure cursor stays usable.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// Lecture UI calls this after taking ownership so choice UI does not later restore
        /// controls while the lecture is still open.
        /// </summary>
        public void ReleaseOwnershipWithoutRestore()
        {
            ownsGameplayLock = false;
        }

        private void InvokeAttend()
        {
            Action callback = onAttend;
            Hide(restoreGameplay: false);
            callback?.Invoke();
        }

        private void InvokeProxy()
        {
            Action callback = onProxy;
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

            // Do not restore while lecture UI (or another classroom modal) still needs ownership.
            if (ClassroomLectureUI.IsOpen)
            {
                ownsGameplayLock = false;
                return;
            }

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

        /// <summary>Used after proxy success/failure when choice UI already closed without restore.</summary>
        public void RestoreGameplayControlsIfOwned()
        {
            RestoreGameplayControls();
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
            GameObject canvasGo = new GameObject("ClassroomChoiceCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 215;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            overlayRoot = new GameObject("Backdrop");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            Image overlayImage = overlayRoot.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.55f);
            overlayImage.raycastTarget = true;

            panelRoot = new GameObject("ChoicePanel");
            panelRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, 0f);

            Image bg = panelRoot.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.94f);

            VerticalLayoutGroup layout = panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 28, 28);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panelRoot.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleLabel = CreateLabel(panelRoot.transform, "Title", "INTRODUCTION TO COMPUTER SCIENCE", 30f, UiTheme.BrightOrange);
            roomLabel = CreateLabel(panelRoot.transform, "Room", "Room: —", 22f, UiTheme.White);

            CreateButton(panelRoot.transform, "AttendButton", "ATTEND CLASS", InvokeAttend);
            CreateButton(panelRoot.transform, "ProxyButton", "PUNCH ID AND LEAVE", InvokeProxy);
            CreateButton(panelRoot.transform, "BackButton", "BACK", InvokeBack);
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
            le.minHeight = size + 10f;
            le.preferredHeight = size + 14f;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static void CreateButton(Transform parent, string name, string label, Action onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = ButtonHeight;
            le.preferredHeight = ButtonHeight;
            le.preferredWidth = ButtonWidth;

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            colors.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            colors.selectedColor = colors.highlightedColor;
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
