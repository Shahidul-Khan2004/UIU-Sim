using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Course materials list and add-material form.
    /// Data comes from /api/players/me/faculty-courses/{courseId}/materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacultyMaterialsUI : MonoBehaviour
    {
        public const string TitleRequiredMessage = "Please provide required information.";
        public const string LinkRequiredMessage = "Please provide required information.";
        public const string SaveSuccessMessage = "Material saved.";
        public const string EmptyMaterialsMessage = "No materials added yet.";
        public const string DeleteConfirmMessage = "Delete this material?";
        public const string OpenFailedMessage = "Unable to open material.";
        public const string DeleteFailedMessage = "Unable to delete material.";

        public static FacultyMaterialsUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }
        public static bool IsAddFormOpen { get; private set; }
        public static bool IsDeleteConfirmOpen { get; private set; }
        public static string LastOpenedUrl { get; private set; }

        private const int CanvasSortOrder = 260;
        private const float PanelWidth = 680f;
        private const float PanelHeight = 680f;

        private GameObject overlayRoot;
        private GameObject listPanel;
        private GameObject addPanel;
        private GameObject confirmOverlay;
        private Transform materialContainer;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI listStatusLabel;
        private TextMeshProUGUI addStatusLabel;
        private TMP_InputField titleField;
        private TMP_InputField urlField;

        private string currentCourseId;
        private string pendingDeleteMaterialId;
        private readonly List<ApiClient.FacultyMaterialDto> displayedMaterials = new List<ApiClient.FacultyMaterialDto>();
        private Coroutine loadMaterials;
        private Coroutine saveMaterial;
        private Coroutine openMaterial;
        private Coroutine deleteMaterial;

        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;
        private bool wasMovementEnabled = true;
        private bool wasLookEnabled = true;
        private bool ownsGameplayLock;

        public static FacultyMaterialsUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            FacultyMaterialsUI existing = FindFirstObjectByType<FacultyMaterialsUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("FacultyMaterialsUI");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<FacultyMaterialsUI>();
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
                IsAddFormOpen = false;
                IsDeleteConfirmOpen = false;
                LastOpenedUrl = null;
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
                if (IsDeleteConfirmOpen)
                {
                    CancelDelete();
                }
                else if (IsAddFormOpen)
                {
                    HideAddForm();
                }
                else
                {
                    Hide();
                }
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
                titleLabel.text = $"{code} MATERIALS";
            }

            overlayRoot.SetActive(true);
            HideDeleteConfirm();
            LastOpenedUrl = null;
            ShowListPanel();
            IsOpen = true;
            LockGameplayInput();
            EnsureEventSystem();
            ClearMaterials();
            SetListStatus("Loading materials…", UiTheme.Grey);
            BeginMaterialsLoad();
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                return;
            }

            HideAddForm();
            HideDeleteConfirm();
            StopPendingRequests();

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;
            RestoreGameplayInput();
        }

        public void ApplyMaterials(ApiClient.FacultyMaterialDto[] materials)
        {
            displayedMaterials.Clear();
            ClearMaterials();
            if (materials == null || materials.Length == 0)
            {
                SetListStatus(EmptyMaterialsMessage, UiTheme.Grey);
                return;
            }

            int rowIndex = 0;
            for (int i = 0; i < materials.Length; i++)
            {
                ApiClient.FacultyMaterialDto material = materials[i];
                if (material == null)
                {
                    continue;
                }

                displayedMaterials.Add(material);
                CreateMaterialRow(material, rowIndex);
                rowIndex++;
            }

            if (displayedMaterials.Count == 0)
            {
                SetListStatus(EmptyMaterialsMessage, UiTheme.Grey);
                return;
            }

            SetListStatus(string.Empty, UiTheme.Grey);
        }

        public void ShowAddForm()
        {
            if (addPanel == null)
            {
                return;
            }

            ResetAddForm();
            listPanel.SetActive(false);
            addPanel.SetActive(true);
            IsAddFormOpen = true;
        }

        public void HideAddForm()
        {
            IsAddFormOpen = false;
            if (addPanel != null)
            {
                addPanel.SetActive(false);
            }

            if (listPanel != null)
            {
                listPanel.SetActive(true);
            }
        }

        public void SetAddMaterialTitle(string title)
        {
            if (titleField != null)
            {
                titleField.text = title ?? string.Empty;
            }
        }

        public void SetAddMaterialUrl(string url)
        {
            if (urlField != null)
            {
                urlField.text = url ?? string.Empty;
            }
        }

        public string ValidateAddMaterial()
        {
            string title = titleField != null ? titleField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                return TitleRequiredMessage;
            }

            if (!IsValidHttpUrl(urlField != null ? urlField.text : null))
            {
                return LinkRequiredMessage;
            }

            return null;
        }

        public void TrySaveMaterial()
        {
            string error = ValidateAddMaterial();
            if (!string.IsNullOrEmpty(error))
            {
                SetAddStatus(error, UiTheme.Red);
                return;
            }

            if (!Application.isPlaying)
            {
                SetAddStatus(SaveSuccessMessage, UiTheme.Success);
                return;
            }

            if (saveMaterial != null)
            {
                StopCoroutine(saveMaterial);
            }

            saveMaterial = StartCoroutine(SaveMaterialRoutine());
        }

        public void OpenMaterial(ApiClient.FacultyMaterialDto material)
        {
            if (material == null)
            {
                return;
            }

            HideDeleteConfirm();
            if (!Application.isPlaying)
            {
                ApplyOpenUrl(ResolveOfflineOpenUrl(material));
                return;
            }

            if (openMaterial != null)
            {
                StopCoroutine(openMaterial);
            }

            openMaterial = StartCoroutine(OpenMaterialRoutine(material));
        }

        public void RequestDeleteMaterial(string materialId)
        {
            if (string.IsNullOrWhiteSpace(materialId) || confirmOverlay == null)
            {
                return;
            }

            pendingDeleteMaterialId = materialId.Trim();
            confirmOverlay.SetActive(true);
            IsDeleteConfirmOpen = true;
        }

        public void CancelDelete()
        {
            HideDeleteConfirm();
        }

        public void ConfirmDelete()
        {
            string materialId = pendingDeleteMaterialId;
            HideDeleteConfirm();
            if (string.IsNullOrWhiteSpace(materialId))
            {
                return;
            }

            if (!Application.isPlaying)
            {
                RemoveDisplayedMaterial(materialId);
                return;
            }

            if (deleteMaterial != null)
            {
                StopCoroutine(deleteMaterial);
            }

            deleteMaterial = StartCoroutine(DeleteMaterialRoutine(materialId));
        }

        private void ShowListPanel()
        {
            IsAddFormOpen = false;
            if (addPanel != null)
            {
                addPanel.SetActive(false);
            }

            if (listPanel != null)
            {
                listPanel.SetActive(true);
            }
        }

        private void BeginMaterialsLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (loadMaterials != null)
            {
                StopCoroutine(loadMaterials);
            }

            loadMaterials = StartCoroutine(LoadMaterialsRoutine());
        }

        private IEnumerator LoadMaterialsRoutine()
        {
            if (!TryResolveApi(out ApiClient apiClient, out UserSession userSession))
            {
                Debug.LogWarning("[FacultyMaterialsUI] Missing auth while loading materials.");
                ApplyMaterials(Array.Empty<ApiClient.FacultyMaterialDto>());
                loadMaterials = null;
                yield break;
            }

            bool succeeded = false;
            string responseBody = null;
            string path = $"api/players/me/faculty-courses/{currentCourseId}/materials";

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
                    Debug.LogWarning($"[FacultyMaterialsUI] GET materials failed: {error} (HTTP {code})");
                });

            if (!IsOpen)
            {
                loadMaterials = null;
                yield break;
            }

            if (!succeeded)
            {
                ApplyMaterials(Array.Empty<ApiClient.FacultyMaterialDto>());
                loadMaterials = null;
                yield break;
            }

            try
            {
                ApplyMaterials(ApiClient.ParseFacultyMaterials(responseBody));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacultyMaterialsUI] Failed to parse materials: {ex.Message}");
                ApplyMaterials(Array.Empty<ApiClient.FacultyMaterialDto>());
            }

            loadMaterials = null;
        }

        private IEnumerator SaveMaterialRoutine()
        {
            SetAddStatus("Saving…", UiTheme.Grey);
            if (!TryResolveApi(out ApiClient apiClient, out UserSession userSession))
            {
                SetAddStatus("Unable to save material.", UiTheme.Red);
                saveMaterial = null;
                yield break;
            }

            string title = titleField.text.Trim();
            string url = urlField != null ? urlField.text.Trim() : string.Empty;
            string path = $"api/players/me/faculty-courses/{currentCourseId}/materials";
            bool succeeded = false;
            string errorMessage = "Unable to save material.";
            string json = JsonUtility.ToJson(new ApiClient.CreateCourseMaterialRequestDto(title, url));
            yield return apiClient.Post(
                path,
                json,
                userSession.JwtToken,
                _ => succeeded = true,
                (error, _) => errorMessage = error);

            if (!IsOpen)
            {
                saveMaterial = null;
                yield break;
            }

            if (!succeeded)
            {
                SetAddStatus(errorMessage, UiTheme.Red);
                saveMaterial = null;
                yield break;
            }

            SetAddStatus(SaveSuccessMessage, UiTheme.Success);
            HideAddForm();
            SetListStatus("Loading materials…", UiTheme.Grey);
            FacultyProgressSync progressSync = FacultyProgressSync.Instance != null
                ? FacultyProgressSync.Instance
                : FindFirstObjectByType<FacultyProgressSync>();
            progressSync?.RefreshAfterMaterialSaved();
            BeginMaterialsLoad();
            saveMaterial = null;
        }

        private IEnumerator OpenMaterialRoutine(ApiClient.FacultyMaterialDto material)
        {
            if (!TryResolveApi(out ApiClient apiClient, out UserSession userSession))
            {
                SetListStatus(OpenFailedMessage, UiTheme.Grey);
                openMaterial = null;
                yield break;
            }

            bool succeeded = false;
            string responseBody = null;
            string path = $"api/players/me/faculty-courses/{currentCourseId}/materials/{material.id}/open";

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
                    Debug.LogWarning($"[FacultyMaterialsUI] GET material open failed: {error} (HTTP {code})");
                });

            if (!IsOpen)
            {
                openMaterial = null;
                yield break;
            }

            if (!succeeded)
            {
                SetListStatus(OpenFailedMessage, UiTheme.Grey);
                openMaterial = null;
                yield break;
            }

            try
            {
                ApiClient.FacultyMaterialOpenDto open = ApiClient.ParseFacultyMaterialOpen(responseBody);
                ApplyOpenUrl(open != null ? open.url : null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacultyMaterialsUI] Failed to parse material open: {ex.Message}");
                SetListStatus(OpenFailedMessage, UiTheme.Grey);
            }

            openMaterial = null;
        }

        private IEnumerator DeleteMaterialRoutine(string materialId)
        {
            if (!TryResolveApi(out ApiClient apiClient, out UserSession userSession))
            {
                SetListStatus(DeleteFailedMessage, UiTheme.Grey);
                deleteMaterial = null;
                yield break;
            }

            bool succeeded = false;
            string path = $"api/players/me/faculty-courses/{currentCourseId}/materials/{materialId}";

            yield return apiClient.Delete(
                path,
                userSession.JwtToken,
                _ => succeeded = true,
                (error, code) =>
                {
                    Debug.LogWarning($"[FacultyMaterialsUI] DELETE material failed: {error} (HTTP {code})");
                });

            if (!IsOpen)
            {
                deleteMaterial = null;
                yield break;
            }

            if (!succeeded)
            {
                SetListStatus(DeleteFailedMessage, UiTheme.Grey);
                deleteMaterial = null;
                yield break;
            }

            SetListStatus("Loading materials…", UiTheme.Grey);
            BeginMaterialsLoad();
            deleteMaterial = null;
        }

        private void ApplyOpenUrl(string url)
        {
            if (!IsValidHttpUrl(url))
            {
                LastOpenedUrl = null;
                SetListStatus(OpenFailedMessage, UiTheme.Grey);
                return;
            }

            LastOpenedUrl = url.Trim();
            SetListStatus(string.Empty, UiTheme.Grey);
            if (!Application.isPlaying)
            {
                return;
            }

            try
            {
                Application.OpenURL(LastOpenedUrl);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacultyMaterialsUI] Application.OpenURL failed: {ex.Message}");
                SetListStatus(OpenFailedMessage, UiTheme.Grey);
            }
        }

        private static string ResolveOfflineOpenUrl(ApiClient.FacultyMaterialDto material)
        {
            return material != null ? material.url : null;
        }

        private void RemoveDisplayedMaterial(string materialId)
        {
            displayedMaterials.RemoveAll(item =>
                item != null && string.Equals(item.id, materialId, StringComparison.Ordinal));
            ApplyMaterials(displayedMaterials.ToArray());
        }

        private void HideDeleteConfirm()
        {
            pendingDeleteMaterialId = null;
            IsDeleteConfirmOpen = false;
            if (confirmOverlay != null)
            {
                confirmOverlay.SetActive(false);
            }
        }

        private static bool TryResolveApi(out ApiClient apiClient, out UserSession userSession)
        {
            AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
            apiClient = host != null ? host.ApiClient : null;
            userSession = host != null && host.AuthManager != null ? host.AuthManager.Session : null;
            return apiClient != null && userSession != null && userSession.HasToken;
        }

        private void StopPendingRequests()
        {
            if (loadMaterials != null)
            {
                StopCoroutine(loadMaterials);
                loadMaterials = null;
            }

            if (saveMaterial != null)
            {
                StopCoroutine(saveMaterial);
                saveMaterial = null;
            }

            if (openMaterial != null)
            {
                StopCoroutine(openMaterial);
                openMaterial = null;
            }

            if (deleteMaterial != null)
            {
                StopCoroutine(deleteMaterial);
                deleteMaterial = null;
            }
        }

        private void ResetAddForm()
        {
            if (titleField != null)
            {
                titleField.text = string.Empty;
            }

            if (urlField != null)
            {
                urlField.text = string.Empty;
            }

            SetAddStatus(string.Empty, UiTheme.Grey);
        }

        private void ClearMaterials()
        {
            if (materialContainer == null)
            {
                return;
            }

            for (int i = materialContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = materialContainer.GetChild(i);
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

        private void CreateMaterialRow(ApiClient.FacultyMaterialDto material, int index)
        {
            GameObject row = new GameObject($"Material_{index}");
            row.transform.SetParent(materialContainer, false);
            VerticalLayoutGroup layout = row.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 2f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            row.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

            CreateFlowLabel(
                row.transform,
                "MaterialTitle",
                $"{index + 1}. {DisplayOrDash(material.title)}",
                18f,
                FontStyles.Bold,
                UiTheme.White);
            CreateFlowLabel(
                row.transform,
                "MaterialUrl",
                DisplayOrDash(material.url),
                14f,
                FontStyles.Normal,
                UiTheme.Grey);

            GameObject actions = new GameObject("MaterialActions");
            actions.transform.SetParent(row.transform, false);
            LayoutElement actionsLayout = actions.AddComponent<LayoutElement>();
            actionsLayout.minHeight = 40f;
            actionsLayout.preferredHeight = 40f;
            HorizontalLayoutGroup actionGroup = actions.AddComponent<HorizontalLayoutGroup>();
            actionGroup.spacing = 12f;
            actionGroup.childControlWidth = true;
            actionGroup.childControlHeight = true;
            actionGroup.childForceExpandWidth = true;

            ApiClient.FacultyMaterialDto captured = material;
            CreateButton(actions.transform, $"Button_OpenMaterial_{index}", "Open", () => OpenMaterial(captured));
            CreateButton(actions.transform, $"Button_DeleteMaterial_{index}", "Delete", () => RequestDeleteMaterial(captured.id));
        }

        private void SetListStatus(string message, Color color)
        {
            if (listStatusLabel == null)
            {
                return;
            }

            listStatusLabel.text = message ?? string.Empty;
            listStatusLabel.color = color;
            listStatusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private void SetAddStatus(string message, Color color)
        {
            if (addStatusLabel == null)
            {
                return;
            }

            addStatusLabel.text = message ?? string.Empty;
            addStatusLabel.color = color;
            addStatusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
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
                "FacultyMaterialsCanvas",
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

            overlayRoot = new GameObject("FacultyMaterialsOverlay");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            overlayRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            listPanel = CreateCenteredPanel(overlayRoot.transform, "FacultyMaterialsPanel", PanelWidth, PanelHeight);
            addPanel = CreateCenteredPanel(overlayRoot.transform, "FacultyAddMaterialPanel", PanelWidth, PanelHeight);
            addPanel.SetActive(false);

            BuildListPanel(listPanel.transform);
            BuildAddPanel(addPanel.transform);
            BuildDeleteConfirm(overlayRoot.transform);
        }

        private void BuildDeleteConfirm(Transform parent)
        {
            confirmOverlay = new GameObject("FacultyDeleteConfirmOverlay");
            confirmOverlay.transform.SetParent(parent, false);
            RectTransform overlayRect = confirmOverlay.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            confirmOverlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            GameObject panel = CreateCenteredPanel(confirmOverlay.transform, "FacultyDeleteConfirmPanel", 460f, 240f);
            CreateAccent(panel.transform);
            CreateFlowLabel(panel.transform, "DeleteConfirmLabel", DeleteConfirmMessage, 20f, FontStyles.Bold, UiTheme.White);
            CreateButton(panel.transform, "Button_CancelDelete", "Cancel", CancelDelete);
            CreateButton(panel.transform, "Button_ConfirmDelete", "Delete", ConfirmDelete);
            confirmOverlay.SetActive(false);
        }

        private void BuildListPanel(Transform panel)
        {
            CreateAccent(panel);
            titleLabel = CreateFlowLabel(panel, "Title", "COURSE MATERIALS", 26f, FontStyles.Bold, UiTheme.BrightOrange);
            CreateFlowLabel(panel, "ExistingHeader", "Existing Materials:", 16f, FontStyles.Bold, UiTheme.White);
            listStatusLabel = CreateFlowLabel(panel, "StatusLabel", string.Empty, 14f, FontStyles.Italic, UiTheme.Grey);
            listStatusLabel.gameObject.SetActive(false);

            GameObject scrollGo = new GameObject("MaterialsScroll");
            scrollGo.transform.SetParent(panel, false);
            LayoutElement scrollLayout = scrollGo.AddComponent<LayoutElement>();
            scrollLayout.minHeight = 360f;
            scrollLayout.preferredHeight = 400f;
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

            GameObject content = new GameObject("MaterialsContent");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 8, 8, 8);
            contentLayout.spacing = 8f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRect;
            materialContainer = content.transform;

            CreateButton(panel, "Button_AddMaterial", "Add Material", ShowAddForm);
            CreateButton(panel, "Button_BackFacultyMaterials", "Back", Hide);
        }

        private void BuildAddPanel(Transform panel)
        {
            CreateAccent(panel);
            CreateFlowLabel(panel, "AddTitle", "ADD MATERIAL", 26f, FontStyles.Bold, UiTheme.BrightOrange);
            CreateFlowLabel(panel, "TitleKey", "Title:", 14f, FontStyles.Normal, UiTheme.Grey);
            titleField = CreateInputField(panel, "TitleField", "Material title");

            CreateFlowLabel(panel, "UrlKey", "Drive / External Link:", 14f, FontStyles.Normal, UiTheme.Grey);
            urlField = CreateInputField(panel, "UrlField", "Paste Link");

            addStatusLabel = CreateFlowLabel(panel, "AddStatusLabel", string.Empty, 14f, FontStyles.Italic, UiTheme.Grey);
            addStatusLabel.gameObject.SetActive(false);

            CreateButton(panel, "Button_SaveMaterial", "Save", TrySaveMaterial);
            CreateButton(panel, "Button_BackAddMaterial", "Back", HideAddForm);
        }

        private static GameObject CreateCenteredPanel(Transform parent, string name, float width, float height)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(width, height);
            panel.AddComponent<Image>().color = UiTheme.Black;

            VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(28, 28, 24, 24);
            panelLayout.spacing = 10f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            return panel;
        }

        private static void CreateAccent(Transform parent)
        {
            GameObject accent = new GameObject("AccentBar");
            accent.transform.SetParent(parent, false);
            LayoutElement accentLayout = accent.AddComponent<LayoutElement>();
            accentLayout.minHeight = 8f;
            accentLayout.preferredHeight = 8f;
            accent.AddComponent<Image>().color = UiTheme.BrightOrange;
        }

        private static TMP_InputField CreateInputField(Transform parent, string objectName, string placeholder)
        {
            GameObject fieldGo = new GameObject(objectName);
            fieldGo.transform.SetParent(parent, false);
            LayoutElement layout = fieldGo.AddComponent<LayoutElement>();
            layout.minHeight = 40f;
            layout.preferredHeight = 40f;
            fieldGo.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);

            TMP_InputField input = fieldGo.AddComponent<TMP_InputField>();
            input.richText = false;

            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(fieldGo.transform, false);
            RectTransform areaRect = textArea.AddComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(12f, 4f);
            areaRect.offsetMax = new Vector2(-12f, -4f);
            textArea.AddComponent<RectMask2D>();

            GameObject placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textArea.transform, false);
            RectTransform phRect = placeholderGo.AddComponent<RectTransform>();
            StretchFull(phRect);
            TextMeshProUGUI ph = placeholderGo.AddComponent<TextMeshProUGUI>();
            ph.text = placeholder;
            ph.fontSize = 16f;
            ph.fontStyle = FontStyles.Italic;
            ph.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            input.placeholder = ph;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            StretchFull(textRect);
            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.color = UiTheme.White;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = areaRect;
            input.textComponent = text;
            return input;
        }

        private static Button CreateButton(Transform parent, string objectName, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonGo = new GameObject(objectName);
            buttonGo.transform.SetParent(parent, false);
            LayoutElement layout = buttonGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 40f;
            layout.minHeight = 40f;
            buttonGo.AddComponent<Image>().color = UiTheme.BrightOrange;
            Button button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            GameObject labelGo = new GameObject(objectName + "Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
            StretchFull(labelRect);
            TextMeshProUGUI text = labelGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 16f;
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

        private static bool IsValidHttpUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
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
