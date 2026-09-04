using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a minimal screen-space interaction prompt at runtime.
/// No prefab or scene setup required — just add this alongside <see cref="InteractionController"/>.
/// <para>
/// Shows "[E] Interact" at the bottom-center when looking at an interactable,
/// and a response message at the center of the screen after interacting.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InteractionController))]
public sealed class InteractionUI : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private float fontSize = 24f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private string keyHint = "E";

    [Header("Response")]
    [SerializeField] private float responseFontSize = 22f;
    [SerializeField] private Color responseColor = new Color(1f, 0.92f, 0.55f, 1f); // Warm yellow

    [Header("Layout")]
    [Tooltip("Vertical offset from bottom of screen (0 = bottom edge, 0.5 = center).")]
    [SerializeField, Range(0f, 0.5f)] private float verticalOffset = 0.15f;

    private InteractionController interactionController;

    // Prompt UI
    private GameObject promptRoot;
    private TextMeshProUGUI promptText;

    // Response UI
    private GameObject responseRoot;
    private TextMeshProUGUI responseText;
    private CanvasGroup responseCanvasGroup;
    private string shownResponse;

    private void Awake()
    {
        interactionController = GetComponent<InteractionController>();
        BuildUI();
        HidePrompt();
        HideResponse();
    }

    private void LateUpdate()
    {
        // Response: show after interaction, auto-hides when controller clears it.
        string response = interactionController.LastResponse;
        bool hasResponse = !string.IsNullOrEmpty(response);

        if (hasResponse)
        {
            ShowResponse(response);
            HidePrompt(); // Hide prompt while response is visible.
        }
        else
        {
            HideResponse();

            // Prompt: show only when looking at an interactable and no response is active.
            if (interactionController.HasTarget)
            {
                ShowPrompt(interactionController.CurrentTarget.InteractionPrompt);
            }
            else
            {
                HidePrompt();
            }
        }
    }

    // ── UI Construction ────────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas — screen-space overlay, renders on top of everything.
        GameObject canvasGo = new GameObject("InteractionCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        BuildPromptPanel(canvasGo.transform);
        BuildResponsePanel(canvasGo.transform);
    }

    private void BuildPromptPanel(Transform parent)
    {
        // Prompt container — anchored to bottom-center.
        promptRoot = new GameObject("PromptPanel");
        promptRoot.transform.SetParent(parent, false);

        RectTransform panelRect = promptRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, verticalOffset);
        panelRect.anchorMax = new Vector2(0.5f, verticalOffset);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;

        // Background image.
        Image bg = promptRoot.AddComponent<Image>();
        bg.color = backgroundColor;

        // Horizontal layout to auto-size around text.
        HorizontalLayoutGroup layout = promptRoot.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 10, 10);
        layout.childAlignment = TextAnchor.MiddleCenter;

        ContentSizeFitter fitter = promptRoot.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Text element.
        GameObject textGo = new GameObject("PromptText");
        textGo.transform.SetParent(promptRoot.transform, false);

        promptText = textGo.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = fontSize;
        promptText.color = textColor;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void BuildResponsePanel(Transform parent)
    {
        // Response container — anchored to center of screen.
        responseRoot = new GameObject("ResponsePanel");
        responseRoot.transform.SetParent(parent, false);

        RectTransform panelRect = responseRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.4f);
        panelRect.anchorMax = new Vector2(0.5f, 0.4f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        // Background.
        Image bg = responseRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        // Layout.
        HorizontalLayoutGroup layout = responseRoot.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 12, 12);
        layout.childAlignment = TextAnchor.MiddleCenter;

        ContentSizeFitter fitter = responseRoot.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // CanvasGroup for future fade-out if desired.
        responseCanvasGroup = responseRoot.AddComponent<CanvasGroup>();

        // Text element.
        GameObject textGo = new GameObject("ResponseText");
        textGo.transform.SetParent(responseRoot.transform, false);

        responseText = textGo.AddComponent<TextMeshProUGUI>();
        responseText.fontSize = responseFontSize;
        responseText.color = responseColor;
        responseText.alignment = TextAlignmentOptions.Center;
        responseText.textWrappingMode = TextWrappingModes.NoWrap;
        responseText.fontStyle = FontStyles.Italic;
    }

    // ── Show / Hide ────────────────────────────────────────────────────

    private void ShowPrompt(string prompt)
    {
        promptText.text = $"[{keyHint}]  {prompt}";

        if (!promptRoot.activeSelf)
        {
            promptRoot.SetActive(true);
        }
    }

    private void HidePrompt()
    {
        if (promptRoot != null && promptRoot.activeSelf)
        {
            promptRoot.SetActive(false);
        }
    }

    private void ShowResponse(string response)
    {
        if (shownResponse != response)
        {
            responseText.text = response;
            shownResponse = response;
        }

        if (!responseRoot.activeSelf)
        {
            responseRoot.SetActive(true);
        }
    }

    private void HideResponse()
    {
        if (responseRoot != null && responseRoot.activeSelf)
        {
            responseRoot.SetActive(false);
            shownResponse = null;
        }
    }
}
