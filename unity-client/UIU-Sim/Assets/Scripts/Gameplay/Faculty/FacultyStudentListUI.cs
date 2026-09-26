using System;
using System.Collections;
using TMPro;
using UIU.Simulator.Authentication;
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
    /// Department-eligible student list for one faculty course.
    /// Data comes from GET /api/players/me/faculty-courses/{courseId}/students.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacultyStudentListUI : MonoBehaviour
    {
        public const string EmptyStudentsMessage = "No students enrolled.";

        public static FacultyStudentListUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private const int CanvasSortOrder = 260;
        private const float PanelWidth = 680f;
        private const float PanelHeight = 640f;

        private GameObject overlayRoot;
        private Transform studentContainer;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI statusLabel;
        private string currentCourseId;
        private Coroutine loadStudents;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private bool wasMovementEnabled = true;
        private bool wasLookEnabled = true;
        private bool ownsGameplayLock;

        public static FacultyStudentListUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            FacultyStudentListUI existing = FindFirstObjectByType<FacultyStudentListUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("FacultyStudentListUI");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<FacultyStudentListUI>();
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

        public void Show(string courseId, string courseName)
        {
            if (overlayRoot == null)
            {
                BuildUI();
            }

            currentCourseId = string.IsNullOrWhiteSpace(courseId) ? "" : courseId.Trim();
            if (titleLabel != null)
            {
                string code = string.IsNullOrWhiteSpace(currentCourseId) ? "COURSE" : currentCourseId.ToUpperInvariant();
                titleLabel.text = $"{code} STUDENTS";
            }

            overlayRoot.SetActive(true);
            IsOpen = true;
            LockGameplayInput();
            EnsureEventSystem();
            ClearRows();
            SetStatus("Loading students…", UiTheme.Grey);
            BeginStudentsLoad();
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                return;
            }

            if (loadStudents != null)
            {
                StopCoroutine(loadStudents);
                loadStudents = null;
            }

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;
            RestoreGameplayInput();
        }

        public void ApplyStudents(ApiClient.FacultyStudentDto[] students)
        {
            ClearRows();
            if (students == null || students.Length == 0)
            {
                SetStatus(EmptyStudentsMessage, UiTheme.Grey);
                return;
            }

            for (int i = 0; i < students.Length; i++)
            {
                ApiClient.FacultyStudentDto student = students[i];
                if (student == null)
                {
                    continue;
                }

                CreateStudentRow(student, i);
            }

            SetStatus(string.Empty, UiTheme.Grey);
        }

        private void BeginStudentsLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (loadStudents != null)
            {
                StopCoroutine(loadStudents);
            }

            loadStudents = StartCoroutine(LoadStudentsRoutine());
        }

        private IEnumerator LoadStudentsRoutine()
        {
            AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
            ApiClient apiClient = host != null ? host.ApiClient : null;
            UserSession userSession = host != null && host.AuthManager != null
                ? host.AuthManager.Session
                : null;

            if (apiClient == null || userSession == null || !userSession.HasToken)
            {
                Debug.LogWarning("[FacultyStudentListUI] Missing auth while loading students.");
                ApplyStudents(Array.Empty<ApiClient.FacultyStudentDto>());
                loadStudents = null;
                yield break;
            }

            bool succeeded = false;
            string responseBody = null;
            string path = $"api/players/me/faculty-courses/{currentCourseId}/students";

            yield return apiClient.Get(
                path,
                userSession.JwtToken,
                body =>
                {
                    succeeded = true;
                    responseBody = body;
                },
                (error, code) =>
                {
                    Debug.LogWarning($"[FacultyStudentListUI] GET students failed: {error} (HTTP {code})");
                });

            if (!IsOpen)
            {
                loadStudents = null;
                yield break;
            }

            if (!succeeded)
            {
                ApplyStudents(Array.Empty<ApiClient.FacultyStudentDto>());
                loadStudents = null;
                yield break;
            }

            try
            {
                ApplyStudents(ApiClient.ParseFacultyStudents(responseBody));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacultyStudentListUI] Failed to parse students: {ex.Message}");
                ApplyStudents(Array.Empty<ApiClient.FacultyStudentDto>());
            }

            loadStudents = null;
        }

        private void ClearRows()
        {
            if (studentContainer == null)
            {
                return;
            }

            for (int i = studentContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = studentContainer.GetChild(i);
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

        private void CreateStudentRow(ApiClient.FacultyStudentDto student, int index)
        {
            GameObject row = new GameObject($"Student_{index}");
            row.transform.SetParent(studentContainer, false);
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 36f;
            rowLayout.preferredHeight = 36f;

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            row.AddComponent<Image>().color = index % 2 == 0
                ? new Color(0.08f, 0.08f, 0.1f, 0.95f)
                : new Color(0.11f, 0.11f, 0.13f, 0.95f);

            CreateRowLabel(row.transform, "StudentName", DisplayOrDash(student.studentName), TextAlignmentOptions.MidlineLeft);
            CreateRowLabel(row.transform, "StudentId", DisplayOrDash(student.studentId), TextAlignmentOptions.MidlineRight);
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
                "FacultyStudentListCanvas",
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

            overlayRoot = new GameObject("FacultyStudentListOverlay");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            overlayRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            GameObject panel = new GameObject("FacultyStudentListPanel");
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

            titleLabel = CreateFlowLabel(panel.transform, "Title", "COURSE STUDENTS", 26f, FontStyles.Bold, UiTheme.BrightOrange);
            statusLabel = CreateFlowLabel(panel.transform, "StatusLabel", string.Empty, 14f, FontStyles.Italic, UiTheme.Grey);
            statusLabel.gameObject.SetActive(false);

            GameObject header = new GameObject("ColumnHeader");
            header.transform.SetParent(panel.transform, false);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.minHeight = 28f;
            headerLayout.preferredHeight = 28f;
            HorizontalLayoutGroup headerGroup = header.AddComponent<HorizontalLayoutGroup>();
            headerGroup.padding = new RectOffset(8, 8, 0, 0);
            headerGroup.spacing = 16f;
            headerGroup.childControlWidth = true;
            headerGroup.childForceExpandWidth = true;
            CreateRowLabel(header.transform, "HeaderName", "Student Name", TextAlignmentOptions.MidlineLeft);
            CreateRowLabel(header.transform, "HeaderId", "Student ID", TextAlignmentOptions.MidlineRight);

            GameObject divider = new GameObject("HeaderDivider");
            divider.transform.SetParent(panel.transform, false);
            LayoutElement dividerLayout = divider.AddComponent<LayoutElement>();
            dividerLayout.minHeight = 2f;
            dividerLayout.preferredHeight = 2f;
            divider.AddComponent<Image>().color = UiTheme.BrightOrange;

            GameObject scrollGo = new GameObject("StudentsScroll");
            scrollGo.transform.SetParent(panel.transform, false);
            LayoutElement scrollLayout = scrollGo.AddComponent<LayoutElement>();
            scrollLayout.minHeight = 380f;
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

            GameObject content = new GameObject("StudentsContent");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(4, 4, 4, 4);
            contentLayout.spacing = 4f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scrollRect.content = contentRect;
            studentContainer = content.transform;

            CreateButton(panel.transform, "Button_BackFacultyStudents", "Back", Hide);
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

        private static TextMeshProUGUI CreateRowLabel(
            Transform parent,
            string objectName,
            string text,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 16f;
            label.fontStyle = FontStyles.Bold;
            label.color = UiTheme.White;
            label.alignment = alignment;
            label.richText = false;
            return label;
        }

        private static string DisplayOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
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
