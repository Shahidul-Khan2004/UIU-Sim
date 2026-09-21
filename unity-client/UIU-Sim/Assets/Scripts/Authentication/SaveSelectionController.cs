using System.Collections;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Networking;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Post-login save hub: Continue existing journey or start a new one.
    /// New Game deletes any existing save and loads the world as an unregistered visitor;
    /// admission (save creation + ID card) happens in-world at the receptionist.
    /// </summary>
    public sealed class SaveSelectionController : MonoBehaviour
    {
        private const string SavePath = "api/players/me/save";

        private ApiClient apiClient;
        private UserSession userSession;

        private Text infoText;
        private Text statusText;
        private Button continueButton;
        private Button newGameButton;
        private GameObject confirmPanel;

        private bool hasSave;
        private bool isBusy;

        private void Start()
        {
            AuthUiUtility.ShowUiCursor();

            AuthHost host = AuthHost.EnsureExists();
            apiClient = host.ApiClient;
            userSession = host.AuthManager != null ? host.AuthManager.Session : null;

            if (userSession == null || !userSession.IsAuthenticated)
            {
                if (userSession == null || !userSession.TryRestoreAuthenticatedSession())
                {
                    Debug.LogWarning("[SaveSelection] No authenticated session — returning to Login.");
                    SceneManager.LoadScene(AuthSceneNames.Login);
                    return;
                }

                host.TokenProvider?.HydrateAccessToken(userSession.JwtToken);
            }

            AuthUiUtility.EnsureInputSystemEventSystem();
            BuildUi();
            StartCoroutine(LoadSaveStatusRoutine());
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                AuthUiUtility.ShowUiCursor();
            }
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                AuthUiUtility.ShowUiCursor();
            }
        }

        private void BuildUi()
        {
            GameObject canvasObject = new GameObject(
                "SaveSelectionCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject backdrop = AuthUiUtility.CreateRect("Backdrop", canvasObject.transform);
            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = UiTheme.Black;
            StretchFull(backdrop.GetComponent<RectTransform>());

            GameObject panel = AuthUiUtility.CreateRect("Panel", canvasObject.transform);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(UiTheme.Black.r, UiTheme.Black.g, UiTheme.Black.b, 0.96f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(520f, 380f);

            GameObject titleObject = AuthUiUtility.CreateRect("Title", panel.transform);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -28f);
            titleRect.sizeDelta = new Vector2(-40f, 48f);
            Text title = titleObject.AddComponent<Text>();
            title.font = AuthUiUtility.ResolveUiFont();
            title.fontSize = 30;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = UiTheme.White;
            title.text = "Save Selection";

            GameObject infoObject = AuthUiUtility.CreateRect("Info", panel.transform);
            RectTransform infoRect = infoObject.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, 1f);
            infoRect.anchorMax = new Vector2(1f, 1f);
            infoRect.pivot = new Vector2(0.5f, 1f);
            infoRect.anchoredPosition = new Vector2(0f, -92f);
            infoRect.sizeDelta = new Vector2(-48f, 72f);
            infoText = infoObject.AddComponent<Text>();
            infoText.font = AuthUiUtility.ResolveUiFont();
            infoText.fontSize = 18;
            infoText.alignment = TextAnchor.MiddleCenter;
            infoText.color = UiTheme.Grey;
            infoText.text = "Loading save…";

            continueButton = AuthUiUtility.CreateButton(
                panel.transform,
                "ContinueButton",
                "Continue",
                new Vector2(-120f, -20f),
                new Vector2(200f, 52f));
            continueButton.onClick.AddListener(OnContinueClicked);
            continueButton.interactable = false;

            newGameButton = AuthUiUtility.CreateButton(
                panel.transform,
                "NewGameButton",
                "New Game",
                new Vector2(120f, -20f),
                new Vector2(200f, 52f));
            newGameButton.onClick.AddListener(OnNewGameClicked);
            newGameButton.interactable = false;

            GameObject statusObject = AuthUiUtility.CreateRect("Status", panel.transform);
            RectTransform statusRect = statusObject.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 28f);
            statusRect.sizeDelta = new Vector2(-40f, 40f);
            statusText = statusObject.AddComponent<Text>();
            statusText.font = AuthUiUtility.ResolveUiFont();
            statusText.fontSize = 15;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = UiTheme.White;
            statusText.text = string.Empty;

            BuildConfirmPopup(canvasObject.transform);
        }

        private void BuildConfirmPopup(Transform canvasTransform)
        {
            confirmPanel = AuthUiUtility.CreateRect("ConfirmPanel", canvasTransform);
            Image dim = confirmPanel.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            StretchFull(confirmPanel.GetComponent<RectTransform>());
            confirmPanel.SetActive(false);

            GameObject box = AuthUiUtility.CreateRect("ConfirmBox", confirmPanel.transform);
            Image boxImage = box.AddComponent<Image>();
            boxImage.color = new Color(0.08f, 0.08f, 0.08f, 0.98f);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(460f, 220f);

            GameObject messageObject = AuthUiUtility.CreateRect("ConfirmMessage", box.transform);
            RectTransform messageRect = messageObject.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0f, 1f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.pivot = new Vector2(0.5f, 1f);
            messageRect.anchoredPosition = new Vector2(0f, -28f);
            messageRect.sizeDelta = new Vector2(-36f, 90f);
            Text message = messageObject.AddComponent<Text>();
            message.font = AuthUiUtility.ResolveUiFont();
            message.fontSize = 18;
            message.alignment = TextAnchor.MiddleCenter;
            message.color = UiTheme.White;
            message.text = "Start New Game?\n\nYour current university progress will be deleted.";

            Button cancelButton = AuthUiUtility.CreateButton(
                box.transform,
                "CancelButton",
                "Cancel",
                new Vector2(-100f, -55f),
                new Vector2(160f, 44f));
            StyleSecondaryButton(cancelButton);
            cancelButton.onClick.AddListener(HideConfirm);

            Button confirmButton = AuthUiUtility.CreateButton(
                box.transform,
                "ConfirmButton",
                "Confirm",
                new Vector2(100f, -55f),
                new Vector2(160f, 44f));
            confirmButton.onClick.AddListener(OnConfirmNewGameClicked);
        }

        private static void StyleSecondaryButton(Button button)
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            }
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private IEnumerator LoadSaveStatusRoutine()
        {
            SetBusy(true);
            SetStatus("Checking save…");
            infoText.text = "Loading save…";
            infoText.color = UiTheme.Grey;

            if (apiClient == null || userSession == null || !userSession.HasToken)
            {
                hasSave = false;
                ApplySaveUi();
                SetStatus("Not authenticated");
                SetBusy(false);
                yield break;
            }

            bool succeeded = false;
            string responseBody = null;
            string errorMessage = null;
            long errorCode = 0;

            yield return apiClient.Get(
                SavePath,
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
                hasSave = false;
                infoText.text = "Could not load save status";
                infoText.color = UiTheme.Red;
                ApplySaveUi();
                SetStatus(FormatApiError("Failed to load save", errorMessage, errorCode));
                SetBusy(false);
                yield break;
            }

            try
            {
                ApiClient.PlayerSaveStatusDto status = JsonUtility.FromJson<ApiClient.PlayerSaveStatusDto>(responseBody);
                hasSave = status != null && status.hasSave && status.save != null;

                if (hasSave)
                {
                    infoText.color = UiTheme.White;
                    infoText.text = $"Semester: {status.save.semester}\nDay: {status.save.currentDay}";
                    SetStatus("Save found");
                }
                else
                {
                    infoText.color = UiTheme.Grey;
                    infoText.text = "No previous save found";
                    SetStatus("No save yet");
                }
            }
            catch (System.Exception ex)
            {
                hasSave = false;
                infoText.text = "Could not parse save status";
                infoText.color = UiTheme.Red;
                SetStatus($"Parse error: {ex.Message}");
            }

            ApplySaveUi();
            SetBusy(false);
        }

        private void ApplySaveUi()
        {
            if (continueButton != null)
            {
                continueButton.interactable = !isBusy && hasSave;
            }

            if (newGameButton != null)
            {
                newGameButton.interactable = !isBusy;
            }
        }

        private void OnContinueClicked()
        {
            if (isBusy || !hasSave)
            {
                return;
            }

            Debug.Log("[SaveSelection] Continue — loading Main.");
            SceneManager.LoadScene(AuthSceneNames.Main);
        }

        private void OnNewGameClicked()
        {
            if (isBusy)
            {
                return;
            }

            ShowConfirm();
        }

        private void ShowConfirm()
        {
            if (confirmPanel != null)
            {
                confirmPanel.SetActive(true);
            }
        }

        private void HideConfirm()
        {
            if (confirmPanel != null)
            {
                confirmPanel.SetActive(false);
            }
        }

        private void OnConfirmNewGameClicked()
        {
            if (isBusy)
            {
                return;
            }

            HideConfirm();
            StartCoroutine(StartNewGameRoutine());
        }

        private IEnumerator StartNewGameRoutine()
        {
            SetBusy(true);
            SetStatus("Starting new game…");

            if (apiClient == null || userSession == null || !userSession.HasToken)
            {
                SetStatus("Not authenticated");
                SetBusy(false);
                yield break;
            }

            // DELETE existing save (safe when none exists). Do not create a placeholder save —
            // admission happens in-world at the receptionist.
            bool deleteOk = false;
            string deleteError = null;
            long deleteCode = 0;

            yield return apiClient.Delete(
                SavePath,
                userSession.JwtToken,
                _ => deleteOk = true,
                (error, code) =>
                {
                    deleteError = error;
                    deleteCode = code;
                });

            if (!deleteOk)
            {
                SetStatus(FormatApiError("Failed to reset save", deleteError, deleteCode));
                SetBusy(false);
                yield break;
            }

            hasSave = false;
            infoText.text = "No previous save found";
            infoText.color = UiTheme.Grey;
            ApplySaveUi();

            // Clear any cached admission state so Main loads as an unregistered visitor.
            PlayerSaveState existingState = PlayerSaveState.Instance;
            if (existingState != null)
            {
                existingState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false, markHydrated: true);
            }

            Debug.Log("[SaveSelection] New Game reset complete — loading Main as unregistered visitor.");
            SceneManager.LoadScene(AuthSceneNames.Main);
        }

        private void SetBusy(bool busy)
        {
            isBusy = busy;
            ApplySaveUi();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

        private static string FormatApiError(string prefix, string error, long code)
        {
            if (code > 0)
            {
                return $"{prefix} (HTTP {code}): {error}";
            }

            return $"{prefix}: {error}";
        }
    }
}
