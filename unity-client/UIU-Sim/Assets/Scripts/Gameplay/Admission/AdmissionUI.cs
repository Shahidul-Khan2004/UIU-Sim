using System.Collections;
using UIU.Simulator.Authentication;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.Networking;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Admission
{
    /// <summary>
    /// MVP university admission form. Creates the player save and issues an ID card boolean.
    /// Built at runtime; sorting order 225 (above ElevatorUI / AdvisorUI).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdmissionUI : MonoBehaviour
    {
        // Seeded department IDs from V5 migration.
        private const string CseDepartmentId = "a1000000-0000-4000-8000-000000000001";
        private const string BbaDepartmentId = "a1000000-0000-4000-8000-000000000002";
        private const string SavePath = "api/players/me/save";

        public static AdmissionUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private GameObject panelRoot;
        private Text statusText;
        private InputField nameField;
        private InputField universityIdField;
        private Button studentButton;
        private Button facultyButton;
        private Button cseButton;
        private Button bbaButton;
        private Button submitButton;
        private Button closeButton;

        private string selectedRole;
        private string selectedDepartmentId;
        private bool isSubmitting;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private ApiClient apiClient;
        private UserSession userSession;

        public static AdmissionUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            AdmissionUI existing = FindFirstObjectByType<AdmissionUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("AdmissionUI");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<AdmissionUI>();
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
            panelRoot.SetActive(false);
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

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !isSubmitting)
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

            if (panelRoot == null)
            {
                BuildUI();
            }

            ResetForm();
            EnsureAuth();
            panelRoot.SetActive(true);
            IsOpen = true;
            LockPlayerInput();
            EnsureEventSystem();
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            isSubmitting = false;
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            RestorePlayerInput();
        }

        private void ResetForm()
        {
            selectedRole = null;
            selectedDepartmentId = null;
            isSubmitting = false;

            if (nameField != null)
            {
                nameField.text = string.Empty;
            }

            if (universityIdField != null)
            {
                universityIdField.text = string.Empty;
            }

            RefreshToggleStyles();
            SetStatus(string.Empty, UiTheme.Grey);
            SetFormInteractable(true);
        }

        private void OnSubmitClicked()
        {
            if (isSubmitting)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedRole))
            {
                SetStatus("Please select a role.", UiTheme.Red);
                return;
            }

            string playerName = nameField != null ? nameField.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(playerName))
            {
                SetStatus("Please enter your name.", UiTheme.Red);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedDepartmentId))
            {
                SetStatus("Please select a department.", UiTheme.Red);
                return;
            }

            string universityId = universityIdField != null ? universityIdField.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(universityId))
            {
                SetStatus("Please enter your university ID.", UiTheme.Red);
                return;
            }

            EnsureAuth();
            if (apiClient == null || userSession == null || !userSession.HasToken)
            {
                SetStatus("Not authenticated.", UiTheme.Red);
                return;
            }

            StartCoroutine(SubmitAdmissionRoutine(playerName, universityId));
        }

        private IEnumerator SubmitAdmissionRoutine(string playerName, string universityId)
        {
            isSubmitting = true;
            SetFormInteractable(false);
            SetStatus("Registering…", UiTheme.Grey);

            ApiClient.PlayerSaveCreateRequestDto request = new ApiClient.PlayerSaveCreateRequestDto(
                selectedRole,
                playerName,
                selectedDepartmentId,
                universityId);
            string jsonBody = JsonUtility.ToJson(request);

            bool succeeded = false;
            string responseBody = null;
            string errorMessage = null;
            long errorCode = 0;

            yield return apiClient.Post(
                SavePath,
                jsonBody,
                userSession.JwtToken,
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
                isSubmitting = false;
                SetFormInteractable(true);
                if (errorCode == 409)
                {
                    SetStatus("A save already exists. Use Continue from Save Selection.", UiTheme.Red);
                }
                else
                {
                    SetStatus(
                        errorCode > 0
                            ? $"Registration failed (HTTP {errorCode}): {errorMessage}"
                            : $"Registration failed: {errorMessage}",
                        UiTheme.Red);
                }

                yield break;
            }

            ApiClient.PlayerSaveStatusDto status = null;
            try
            {
                status = JsonUtility.FromJson<ApiClient.PlayerSaveStatusDto>(responseBody);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AdmissionUI] Failed to parse create save response: {ex.Message}");
            }

            PlayerSaveState saveState = PlayerSaveState.EnsureExists();
            if (status != null)
            {
                saveState.ApplyCreatedSave(status);
            }
            else
            {
                saveState.SetStateForTesting(hasSaveValue: true, idCardIssuedValue: true);
            }

            ApplyLocalIdCardReady();

            SystemNotificationUI.Show("ID Card Issued Successfully");
            Debug.Log("[AdmissionUI] Admission complete — ID card issued. Player must walk to the scanner.");
            Hide();
        }

        private void ApplyLocalIdCardReady()
        {
            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory != null)
            {
                inventory.GrantPermanentID();
                inventory.SetTriggeredInitialIDFailure(true);
                inventory.ResolveIDProblem();
            }

            // Mark the one-time tutorial as consumed so Continue sessions do not re-trigger it.
            PlayerProgressSync progressSync = FindFirstObjectByType<PlayerProgressSync>();
            progressSync?.RequestConsumeInitialIdTutorial();
        }

        private void SelectRole(string role)
        {
            selectedRole = role;
            RefreshToggleStyles();
        }

        private void SelectDepartment(string departmentId)
        {
            selectedDepartmentId = departmentId;
            RefreshToggleStyles();
        }

        private void RefreshToggleStyles()
        {
            StyleToggle(studentButton, selectedRole == "STUDENT");
            StyleToggle(facultyButton, selectedRole == "FACULTY");
            StyleToggle(cseButton, selectedDepartmentId == CseDepartmentId);
            StyleToggle(bbaButton, selectedDepartmentId == BbaDepartmentId);
        }

        private static void StyleToggle(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? UiTheme.BrightOrange
                    : new Color(0.22f, 0.22f, 0.22f, 1f);
            }
        }

        private void SetFormInteractable(bool interactable)
        {
            if (nameField != null) nameField.interactable = interactable;
            if (universityIdField != null) universityIdField.interactable = interactable;
            if (studentButton != null) studentButton.interactable = interactable;
            if (facultyButton != null) facultyButton.interactable = interactable;
            if (cseButton != null) cseButton.interactable = interactable;
            if (bbaButton != null) bbaButton.interactable = interactable;
            if (submitButton != null) submitButton.interactable = interactable;
            if (closeButton != null) closeButton.interactable = interactable;
        }

        private void SetStatus(string message, Color color)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message ?? string.Empty;
            statusText.color = color;
        }

        private void LockPlayerInput()
        {
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

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestorePlayerInput()
        {
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

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void EnsureAuth()
        {
            if (apiClient != null && userSession != null)
            {
                return;
            }

            AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
            if (host == null)
            {
                return;
            }

            apiClient ??= host.ApiClient;
            if (userSession == null && host.AuthManager != null)
            {
                userSession = host.AuthManager.Session;
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(eventSystemObject);
            }
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject(
                "AdmissionCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 225;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            panelRoot = CreateRect("PanelRoot", canvasObject.transform);
            Image dim = panelRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            StretchFull(panelRoot.GetComponent<RectTransform>());

            GameObject panel = CreateRect("Panel", panelRoot.transform);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.96f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 560f);

            CreateLabel(panel.transform, "Title", "University Admission", 28, UiTheme.BrightOrange, new Vector2(0f, -28f), 48f);
            CreateLabel(panel.transform, "Subtitle", "Register to receive your ID card", 16, UiTheme.Grey, new Vector2(0f, -72f), 28f);

            CreateLabel(panel.transform, "RoleLabel", "Role", 16, UiTheme.White, new Vector2(0f, -118f), 24f);
            studentButton = CreateToggleButton(panel.transform, "StudentButton", "Student", new Vector2(-110f, -156f));
            facultyButton = CreateToggleButton(panel.transform, "FacultyButton", "Faculty", new Vector2(110f, -156f));
            studentButton.onClick.AddListener(() => SelectRole("STUDENT"));
            facultyButton.onClick.AddListener(() => SelectRole("FACULTY"));

            CreateLabel(panel.transform, "NameLabel", "Name", 16, UiTheme.White, new Vector2(0f, -204f), 24f);
            nameField = CreateInputField(panel.transform, "NameField", new Vector2(0f, -240f));

            CreateLabel(panel.transform, "DeptLabel", "Department", 16, UiTheme.White, new Vector2(0f, -288f), 24f);
            cseButton = CreateToggleButton(panel.transform, "CseButton", "CSE", new Vector2(-110f, -326f));
            bbaButton = CreateToggleButton(panel.transform, "BbaButton", "BBA", new Vector2(110f, -326f));
            cseButton.onClick.AddListener(() => SelectDepartment(CseDepartmentId));
            bbaButton.onClick.AddListener(() => SelectDepartment(BbaDepartmentId));

            CreateLabel(panel.transform, "IdLabel", "University ID", 16, UiTheme.White, new Vector2(0f, -374f), 24f);
            universityIdField = CreateInputField(panel.transform, "UniversityIdField", new Vector2(0f, -410f));

            submitButton = AuthUiUtility.CreateButton(
                panel.transform,
                "SubmitButton",
                "Submit",
                new Vector2(-100f, -55f),
                new Vector2(180f, 48f));
            RectTransform submitRect = submitButton.GetComponent<RectTransform>();
            submitRect.anchorMin = new Vector2(0.5f, 0f);
            submitRect.anchorMax = new Vector2(0.5f, 0f);
            submitRect.pivot = new Vector2(0.5f, 0f);
            submitRect.anchoredPosition = new Vector2(-100f, 28f);
            submitButton.onClick.AddListener(OnSubmitClicked);

            closeButton = AuthUiUtility.CreateButton(
                panel.transform,
                "CloseButton",
                "Close",
                new Vector2(100f, -55f),
                new Vector2(180f, 48f));
            Image closeImage = closeButton.GetComponent<Image>();
            if (closeImage != null)
            {
                closeImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            }

            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(100f, 28f);
            closeButton.onClick.AddListener(Hide);

            GameObject statusObject = CreateRect("Status", panel.transform);
            RectTransform statusRect = statusObject.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 88f);
            statusRect.sizeDelta = new Vector2(-40f, 36f);
            statusText = statusObject.AddComponent<Text>();
            statusText.font = AuthUiUtility.ResolveUiFont();
            statusText.fontSize = 15;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = UiTheme.Grey;
            statusText.text = string.Empty;
        }

        private static void CreateLabel(
            Transform parent,
            string name,
            string text,
            int fontSize,
            Color color,
            Vector2 anchoredPosition,
            float height)
        {
            GameObject labelObject = CreateRect(name, parent);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-48f, height);
            Text label = labelObject.AddComponent<Text>();
            label.font = AuthUiUtility.ResolveUiFont();
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
        }

        private static Button CreateToggleButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            Button button = AuthUiUtility.CreateButton(
                parent,
                name,
                label,
                anchoredPosition,
                new Vector2(200f, 40f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.22f, 0.22f, 0.22f, 1f);
            }

            return button;
        }

        private static InputField CreateInputField(Transform parent, string name, Vector2 anchoredPosition)
        {
            GameObject fieldObject = CreateRect(name, parent);
            RectTransform fieldRect = fieldObject.GetComponent<RectTransform>();
            fieldRect.anchorMin = new Vector2(0.5f, 1f);
            fieldRect.anchorMax = new Vector2(0.5f, 1f);
            fieldRect.pivot = new Vector2(0.5f, 1f);
            fieldRect.anchoredPosition = anchoredPosition;
            fieldRect.sizeDelta = new Vector2(420f, 36f);
            fieldObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            InputField inputField = fieldObject.AddComponent<InputField>();
            Text fieldText = AuthUiUtility.CreateText(fieldObject.transform, "Text", string.Empty, 16, TextAnchor.MiddleLeft);
            fieldText.supportRichText = false;
            RectTransform textRect = fieldText.GetComponent<RectTransform>();
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            inputField.textComponent = fieldText;
            inputField.caretColor = UiTheme.BrightOrange;
            return inputField;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
