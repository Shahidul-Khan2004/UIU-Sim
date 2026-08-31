using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Player-facing login scene. Debug JWT controls are Editor-only.
    /// </summary>
    public sealed class LoginSceneController : MonoBehaviour
    {
        private ClerkAuthManager authManager;
        private AuthCallbackHandler callbackHandler;
        private Text statusText;
        private Button loginButton;

#if UNITY_EDITOR
        private InputField manualTokenField;
#endif

        private void Start()
        {
            // CameraFollow locks the cursor in Main; always unlock on the login screen.
            AuthUiUtility.ShowUiCursor();

            AuthHost host = AuthHost.EnsureExists();
            authManager = host.AuthManager;
            callbackHandler = host.CallbackHandler;

            AuthUiUtility.EnsureInputSystemEventSystem();
            BuildPlayerUi();

#if UNITY_EDITOR
            BuildEditorDebugPanel();
#endif

            authManager.SessionChanged += OnSessionChanged;
            OnSessionChanged(authManager.Session);

            if (authManager.Session.IsAuthenticated)
            {
                EnterGame();
            }
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
            // Keep the cursor free if something else locked it (e.g. leftover gameplay state).
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                AuthUiUtility.ShowUiCursor();
            }
        }

        private void OnDestroy()
        {
            if (authManager != null)
            {
                authManager.SessionChanged -= OnSessionChanged;
            }
        }

        private void BuildPlayerUi()
        {
            GameObject canvasObject = new GameObject(
                "LoginCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject panel = AuthUiUtility.CreateRect("Panel", canvasObject.transform);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.11f, 0.09f, 0.96f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(480f, 320f);

            GameObject titleObject = AuthUiUtility.CreateRect("Title", panel.transform);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -28f);
            titleRect.sizeDelta = new Vector2(-40f, 48f);
            Text title = titleObject.AddComponent<Text>();
            title.font = AuthUiUtility.ResolveUiFont();
            title.fontSize = 32;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.text = "UIU Simulator";

            GameObject subtitleObject = AuthUiUtility.CreateRect("Subtitle", panel.transform);
            RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0f, 1f);
            subtitleRect.anchorMax = new Vector2(1f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.anchoredPosition = new Vector2(0f, -84f);
            subtitleRect.sizeDelta = new Vector2(-48f, 40f);
            Text subtitle = subtitleObject.AddComponent<Text>();
            subtitle.font = AuthUiUtility.ResolveUiFont();
            subtitle.fontSize = 16;
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.color = new Color(0.75f, 0.85f, 0.8f, 1f);
            subtitle.text = "Sign in to explore the campus";

            loginButton = AuthUiUtility.CreateButton(
                panel.transform,
                "SignInButton",
                "Sign In",
                new Vector2(0f, -10f),
                new Vector2(220f, 52f));
            loginButton.onClick.AddListener(OnSignInClicked);

            GameObject statusObject = AuthUiUtility.CreateRect("Status", panel.transform);
            RectTransform statusRect = statusObject.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 28f);
            statusRect.sizeDelta = new Vector2(-40f, 36f);
            statusText = statusObject.AddComponent<Text>();
            statusText.font = AuthUiUtility.ResolveUiFont();
            statusText.fontSize = 15;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = new Color(0.85f, 0.9f, 0.87f, 1f);
            statusText.text = "Ready";
        }

        private void OnSignInClicked()
        {
            Debug.Log("[LoginSceneController] Sign In clicked");
            AuthUiUtility.ShowUiCursor();

            if (authManager == null)
            {
                Debug.LogError("[LoginSceneController] ClerkAuthManager missing");
                SetStatus("Auth manager missing");
                return;
            }

            authManager.StartLogin();
            SetStatus("Browser opened — finish sign-in there, then return here");
        }

        private void OnSessionChanged(UserSession session)
        {
            if (session == null)
            {
                SetStatus("Logged out");
                return;
            }

            switch (session.State)
            {
                case AuthenticationState.LoggedOut:
                    SetStatus("Sign in to continue");
                    break;
                case AuthenticationState.Authenticating:
                    SetStatus("Browser open — sign in there; Unity waits automatically");
                    AuthUiUtility.ShowUiCursor();
                    break;
                case AuthenticationState.Authenticated:
                    SetStatus($"Welcome, {session.Username}");
                    EnterGame();
                    break;
                case AuthenticationState.Failed:
                    SetStatus("Sign-in failed — try again");
                    break;
            }

            if (loginButton != null)
            {
                // Keep Sign In clickable so the player can re-open the browser if needed.
                loginButton.interactable = session.State != AuthenticationState.Authenticated;
            }
        }

        private void EnterGame()
        {
            Debug.Log("[LoginSceneController] Authentication complete — loading Main.");
            SceneManager.LoadScene(AuthSceneNames.Main);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

#if UNITY_EDITOR
        private void BuildEditorDebugPanel()
        {
            GameObject canvasObject = new GameObject(
                "EditorAuthDebugCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            GameObject panel = AuthUiUtility.CreateRect("DebugPanel", canvasObject.transform);
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.15f, 0.1f, 0.05f, 0.9f);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-16f, 16f);
            rect.sizeDelta = new Vector2(360f, 120f);

            GameObject labelObject = AuthUiUtility.CreateRect("DebugLabel", panel.transform);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -8f);
            labelRect.sizeDelta = new Vector2(-16f, 22f);
            Text label = labelObject.AddComponent<Text>();
            label.font = AuthUiUtility.ResolveUiFont();
            label.fontSize = 12;
            label.color = new Color(1f, 0.85f, 0.4f, 1f);
            label.text = "EDITOR ONLY — paste JWT if deep link fails";

            GameObject fieldObject = AuthUiUtility.CreateRect("TokenField", panel.transform);
            RectTransform fieldRect = fieldObject.GetComponent<RectTransform>();
            fieldRect.anchorMin = new Vector2(0f, 1f);
            fieldRect.anchorMax = new Vector2(1f, 1f);
            fieldRect.pivot = new Vector2(0.5f, 1f);
            fieldRect.anchoredPosition = new Vector2(0f, -36f);
            fieldRect.sizeDelta = new Vector2(-16f, 28f);
            fieldObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            manualTokenField = fieldObject.AddComponent<InputField>();
            Text fieldText = AuthUiUtility.CreateText(fieldObject.transform, "Text", string.Empty, 12, TextAnchor.MiddleLeft);
            fieldText.supportRichText = false;
            manualTokenField.textComponent = fieldText;

            Button apply = AuthUiUtility.CreateButton(
                panel.transform,
                "ApplyJwtButton",
                "Apply JWT",
                new Vector2(0f, -28f),
                new Vector2(140f, 32f));
            RectTransform applyRect = apply.GetComponent<RectTransform>();
            applyRect.anchorMin = new Vector2(0.5f, 0f);
            applyRect.anchorMax = new Vector2(0.5f, 0f);
            applyRect.pivot = new Vector2(0.5f, 0f);
            applyRect.anchoredPosition = new Vector2(0f, 12f);
            apply.onClick.AddListener(() =>
            {
                Debug.Log("[LoginSceneController] Editor Apply JWT clicked");
                callbackHandler?.ApplyManualToken(manualTokenField != null ? manualTokenField.text : string.Empty);
            });
        }
#endif
    }
}
