using System;
using TMPro;
using UIU.Simulator.Networking;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Reusable class-routine modal. Displays course, classroom, and floor only.
    /// Callers supply already-fetched routine items; this UI does not call the backend.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClassRoutineUI : MonoBehaviour
    {
        public static ClassRoutineUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private const int CanvasSortOrder = 240;
        private const float PanelWidth = 620f;
        private const float PanelHeight = 640f;

        private GameObject overlayRoot;
        private Transform routineContainer;
        private TextMeshProUGUI statusLabel;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private bool wasMovementEnabled = true;
        private bool wasLookEnabled = true;
        private bool ownsGameplayLock;

        public static ClassRoutineUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            ClassRoutineUI existing = FindFirstObjectByType<ClassRoutineUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("ClassRoutineUI");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<ClassRoutineUI>();
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
            overlayRoot.SetActive(false);
        }

        private void OnDestroy()
        {
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

        public void Show(ApiClient.FacultyRoutineItemDto[] routine)
        {
            if (overlayRoot == null)
            {
                BuildUI();
            }

            RebuildRoutine(routine);
            overlayRoot.SetActive(true);
            IsOpen = true;
            LockGameplayInput();
            EnsureEventSystem();
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                return;
            }

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;
            RestoreGameplayInput();
        }

        private void RebuildRoutine(ApiClient.FacultyRoutineItemDto[] routine)
        {
            ClearRoutineCards();
            if (routine == null || routine.Length == 0)
            {
                SetStatus("No assigned classes.", UiTheme.Grey);
                return;
            }

            int created = 0;
            for (int i = 0; i < routine.Length; i++)
            {
                ApiClient.FacultyRoutineItemDto item = routine[i];
                if (item == null)
                {
                    continue;
                }

                CreateRoutineCard(item);
                created++;
            }

            SetStatus(created == 0 ? "No assigned classes." : string.Empty, UiTheme.Grey);
        }

        private void ClearRoutineCards()
        {
            if (routineContainer == null)
            {
                return;
            }

            for (int i = routineContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = routineContainer.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void CreateRoutineCard(ApiClient.FacultyRoutineItemDto item)
        {
            string courseKey = string.IsNullOrWhiteSpace(item.courseId) ? "Course" : item.courseId.Trim();
            GameObject card = new GameObject($"Routine_{courseKey}");
            card.transform.SetParent(routineContainer, false);

            VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(16, 16, 14, 14);
            cardLayout.spacing = 4f;
            cardLayout.childAlignment = TextAnchor.UpperLeft;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            card.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

            CreateFlowLabel(
                card.transform,
                "CourseValue",
                DisplayOrDash(item.courseName),
                20f,
                FontStyles.Bold,
                UiTheme.White);
            CreateFlowLabel(card.transform, "ClassroomKey", "Classroom:", 14f, FontStyles.Normal, UiTheme.Grey);
            CreateFlowLabel(
                card.transform,
                "ClassroomValue",
                FormatRoom(item.classroomNumber),
                20f,
                FontStyles.Bold,
                UiTheme.White);
            CreateFlowLabel(card.transform, "FloorKey", "Floor:", 14f, FontStyles.Normal, UiTheme.Grey);
            CreateFlowLabel(
                card.transform,
                "FloorValue",
                item.floor > 0 ? item.floor.ToString() : "—",
                20f,
                FontStyles.Bold,
                UiTheme.White);
        }

        private void SetStatus(string message, Color color)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = message ?? string.Empty;
            statusLabel.color = color;
            statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private void LockGameplayInput()
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

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ownsGameplayLock = !FacultyPortalUI.IsOpen;
        }

        private void RestoreGameplayInput()
        {
            // Faculty Portal keeps the gameplay lock when this UI was opened from it.
            if (!ownsGameplayLock || FacultyPortalUI.IsOpen)
            {
                return;
            }

            if (cachedPlayerMovement == null)
            {
                cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            }

            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.enabled = wasMovementEnabled;
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

            if (Application.isPlaying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject(
                "ClassRoutineCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            overlayRoot = new GameObject("ClassRoutineOverlay");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            overlayRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            GameObject panel = new GameObject("ClassRoutinePanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.AddComponent<Image>().color = UiTheme.Black;

            VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(28, 28, 24, 24);
            panelLayout.spacing = 12f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            GameObject accent = new GameObject("AccentBar");
            accent.transform.SetParent(panel.transform, false);
            LayoutElement accentLayout = accent.AddComponent<LayoutElement>();
            accentLayout.minHeight = 8f;
            accentLayout.preferredHeight = 8f;
            accent.AddComponent<Image>().color = UiTheme.BrightOrange;

            CreateFlowLabel(panel.transform, "Title", "CLASS ROUTINE", 26f, FontStyles.Bold, UiTheme.BrightOrange);
            statusLabel = CreateFlowLabel(panel.transform, "StatusLabel", string.Empty, 14f, FontStyles.Italic, UiTheme.Grey);
            statusLabel.gameObject.SetActive(false);

            GameObject scrollGo = new GameObject("RoutineScroll");
            scrollGo.transform.SetParent(panel.transform, false);
            LayoutElement scrollLayout = scrollGo.AddComponent<LayoutElement>();
            scrollLayout.minHeight = 360f;
            scrollLayout.preferredHeight = 420f;
            scrollLayout.flexibleHeight = 1f;
            scrollGo.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.05f, 0.9f);

            ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            StretchFull(viewportRect);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRect;

            GameObject content = new GameObject("RoutineContent");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 8, 8, 8);
            contentLayout.spacing = 12f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scrollRect.content = contentRect;
            routineContainer = content.transform;

            CreateButton(panel.transform, "Button_CloseClassRoutine", "Close", Hide);
        }

        private static void CreateButton(Transform parent, string objectName, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonGo = new GameObject(objectName);
            buttonGo.transform.SetParent(parent, false);
            LayoutElement layout = buttonGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 44f;
            layout.minHeight = 44f;
            buttonGo.AddComponent<Image>().color = UiTheme.BrightOrange;
            Button button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            GameObject labelGo = new GameObject("CloseLabel");
            labelGo.transform.SetParent(buttonGo.transform, false);
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
            StretchFull(labelRect);
            TextMeshProUGUI text = labelGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.color = UiTheme.White;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateFlowLabel(
            Transform parent,
            string objectName,
            string text,
            float fontSize,
            FontStyles style,
            Color color)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.minHeight = fontSize + 8f;
            layout.preferredHeight = fontSize + 10f;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.richText = false;
            return label;
        }

        private static string DisplayOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        private static string FormatRoom(string room)
        {
            if (string.IsNullOrWhiteSpace(room))
            {
                return "—";
            }

            string trimmed = room.Trim();
            if (trimmed.StartsWith("Room ", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return $"Room {trimmed}";
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
                GameObject host = new GameObject("EventSystem");
                host.AddComponent<EventSystem>();
                InputSystemUIInputModule module = host.AddComponent<InputSystemUIInputModule>();
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
                Destroy(legacy);
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
    }
}
