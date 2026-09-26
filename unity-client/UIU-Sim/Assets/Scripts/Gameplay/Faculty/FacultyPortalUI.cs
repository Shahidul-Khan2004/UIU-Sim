using System;
using TMPro;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Faculty Portal modal. Profile hydrates from <see cref="PlayerSaveState"/>.
    /// Course management opens <see cref="FacultyCoursesUI"/>. Class routine stays
    /// in <see cref="ClassRoutineUI"/> for Faculty Hub and student schedule use.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacultyPortalUI : MonoBehaviour
    {
        public static FacultyPortalUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        public static bool HasNestedModalOpen =>
            FacultyCoursesUI.IsOpen
            || FacultyStudentListUI.IsOpen
            || FacultyMaterialsUI.IsOpen
            || ClassRoutineUI.IsOpen;

        private const int CanvasSortOrder = 230;
        private const float PanelWidth = 640f;
        private const float PanelHeight = 620f;

        private GameObject overlayRoot;
        private TextMeshProUGUI welcomeLabel;
        private TextMeshProUGUI nameValue;
        private TextMeshProUGUI facultyIdValue;
        private TextMeshProUGUI departmentValue;
        private TextMeshProUGUI designationValue;
        private TextMeshProUGUI officeValue;
        private Button viewCoursesButton;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private bool wasMovementEnabled = true;
        private bool wasLookEnabled = true;
        private CursorLockMode previousLockMode;
        private bool previousCursorVisible;

        public static FacultyPortalUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            FacultyPortalUI existing = FindFirstObjectByType<FacultyPortalUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("FacultyPortalUI");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<FacultyPortalUI>();
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
            if (!IsOpen || HasNestedModalOpen)
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

            overlayRoot.SetActive(true);
            IsOpen = true;
            LockGameplayInput();
            EnsureEventSystem();
            PopulateProfileFromSaveState();
            if (viewCoursesButton != null)
            {
                viewCoursesButton.interactable = true;
            }
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                return;
            }

            CloseNestedModals();

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;
            RestoreGameplayInput();
        }

        public void OpenCourses()
        {
            FacultyCoursesUI coursesUI = FacultyCoursesUI.Instance != null
                ? FacultyCoursesUI.Instance
                : FacultyCoursesUI.EnsureExists();
            if (coursesUI != null)
            {
                coursesUI.Show();
            }
        }

        private void PopulateProfileFromSaveState()
        {
            PlayerSaveState saveState = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();

            string playerName = saveState != null && !string.IsNullOrWhiteSpace(saveState.PlayerName)
                ? saveState.PlayerName
                : "Faculty";

            ApplyProfile(
                playerName,
                saveState != null ? saveState.UniversityId : null,
                saveState != null ? saveState.Department : null,
                FacultyIdentity.Designation,
                FacultyIdentity.OfficeRoom);
        }

        private void ApplyProfile(
            string playerName,
            string facultyId,
            string department,
            string designation,
            string office)
        {
            string resolvedName = DisplayOrDash(playerName);
            if (welcomeLabel != null)
            {
                welcomeLabel.text = $"Welcome, {resolvedName}";
            }

            if (nameValue != null)
            {
                nameValue.text = resolvedName;
            }

            if (facultyIdValue != null)
            {
                facultyIdValue.text = DisplayOrDash(facultyId);
            }

            if (departmentValue != null)
            {
                departmentValue.text = DisplayOrDash(department);
            }

            if (designationValue != null)
            {
                designationValue.text = DisplayOrDash(designation);
            }

            if (officeValue != null)
            {
                officeValue.text = FormatRoom(office);
            }
        }

        private static void CloseNestedModals()
        {
            if (FacultyMaterialsUI.IsOpen && FacultyMaterialsUI.Instance != null)
            {
                FacultyMaterialsUI.Instance.Hide();
            }

            if (FacultyStudentListUI.IsOpen && FacultyStudentListUI.Instance != null)
            {
                FacultyStudentListUI.Instance.Hide();
            }

            if (FacultyCoursesUI.IsOpen && FacultyCoursesUI.Instance != null)
            {
                FacultyCoursesUI.Instance.Hide();
            }

            if (ClassRoutineUI.IsOpen && ClassRoutineUI.Instance != null)
            {
                ClassRoutineUI.Instance.Hide();
            }
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

            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreGameplayInput()
        {
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

            Cursor.lockState = Application.isPlaying ? CursorLockMode.Locked : previousLockMode;
            Cursor.visible = Application.isPlaying ? false : previousCursorVisible;
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject(
                "FacultyPortalCanvas",
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

            overlayRoot = new GameObject("FacultyPortalOverlay");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            overlayRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            GameObject panel = new GameObject("FacultyPortalPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panel.AddComponent<Image>().color = UiTheme.Black;

            VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(28, 28, 24, 24);
            panelLayout.spacing = 10f;
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

            CreateFlowLabel(panel.transform, "Title", "FACULTY PORTAL", 26f, FontStyles.Bold, UiTheme.BrightOrange);
            welcomeLabel = CreateFlowLabel(panel.transform, "WelcomeLabel", "Welcome, Faculty", 18f, FontStyles.Normal, UiTheme.White);

            CreateFlowLabel(panel.transform, "ProfileHeader", "FACULTY INFORMATION", 16f, FontStyles.Bold, UiTheme.BrightOrange);
            CreateFlowLabel(panel.transform, "NameKey", "Name:", 14f, FontStyles.Normal, UiTheme.Grey);
            nameValue = CreateFlowLabel(panel.transform, "NameValue", "—", 20f, FontStyles.Bold, UiTheme.White);
            CreateFlowLabel(panel.transform, "FacultyIdKey", "Faculty ID:", 14f, FontStyles.Normal, UiTheme.Grey);
            facultyIdValue = CreateFlowLabel(panel.transform, "FacultyIdValue", "—", 20f, FontStyles.Bold, UiTheme.White);
            CreateFlowLabel(panel.transform, "DepartmentKey", "Department:", 14f, FontStyles.Normal, UiTheme.Grey);
            departmentValue = CreateFlowLabel(panel.transform, "DepartmentValue", "—", 20f, FontStyles.Bold, UiTheme.White);
            CreateFlowLabel(panel.transform, "DesignationKey", "Designation:", 14f, FontStyles.Normal, UiTheme.Grey);
            designationValue = CreateFlowLabel(panel.transform, "DesignationValue", "—", 20f, FontStyles.Bold, UiTheme.White);
            CreateFlowLabel(panel.transform, "OfficeKey", "Office:", 14f, FontStyles.Normal, UiTheme.Grey);
            officeValue = CreateFlowLabel(panel.transform, "OfficeValue", "—", 20f, FontStyles.Bold, UiTheme.White);

            viewCoursesButton = CreateButton(
                panel.transform,
                "Button_ViewCourses",
                "View Courses",
                OpenCourses);
            viewCoursesButton.interactable = true;
            CreateButton(panel.transform, "Button_CloseFacultyPortal", "Close", Hide);
        }

        private static Button CreateButton(
            Transform parent,
            string objectName,
            string label,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonGo = new GameObject(objectName);
            buttonGo.transform.SetParent(parent, false);
            LayoutElement layout = buttonGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 44f;
            layout.minHeight = 44f;
            buttonGo.AddComponent<Image>().color = UiTheme.BrightOrange;
            Button button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            GameObject labelGo = new GameObject(objectName + "Label");
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
            return button;
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
