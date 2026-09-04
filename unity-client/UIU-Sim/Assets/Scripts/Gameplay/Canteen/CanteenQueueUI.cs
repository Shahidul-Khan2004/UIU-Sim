using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Modal screen-space waiting UI for the Canteen Porotta queue.
/// Built entirely at runtime — no prefab or scene setup required.
/// Attach to the Player prefab root alongside <see cref="DialogueUI"/> and <see cref="StatsHUD"/>.
/// <para>
/// Canvas sorting order 210 renders this above DialogueUI (200) and InteractionUI (100).
/// Displays a smooth progress bar (no numeric countdown) and two modal choices:
/// [1] Skip Breakfast and [2] Skip the Line.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class CanteenQueueUI : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────

    /// <summary>The active CanteenQueueUI instance in the scene.</summary>
    public static CanteenQueueUI Instance { get; private set; }

    /// <summary>
    /// True while the queue waiting UI is open.
    /// Used by <see cref="InteractionController"/> to block world interactions.
    /// </summary>
    public static bool IsOpen { get; private set; }

    // ── Serialized Appearance ─────────────────────────────────────────

    [Header("Panel Appearance")]
    [SerializeField] private Color panelBackground = new Color(0.05f, 0.05f, 0.05f, 0.94f);

    [Header("Text Colors")]
    [SerializeField] private Color headerColor = new Color(1f, 0.82f, 0.35f, 1f); // warm amber
    [SerializeField] private Color statusColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color buttonTextColor = Color.white;

    [Header("Progress Bar Colors")]
    [SerializeField] private Color progressBarBackground = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color progressBarFillColor = new Color(1f, 0.65f, 0.15f, 1f); // warm orange/gold

    [Header("Button Appearance")]
    [SerializeField] private Color buttonNormalColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color buttonHighlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
    [SerializeField] private Color buttonPressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);

    [Header("Queue UI Text")]
    [Tooltip("Header/title text displayed at the top of the queue panel.")]
    [SerializeField] private string queueTitle = "Waiting for Porotta";

    [Tooltip("Waiting notice or worker remark shown under the title.")]
    [SerializeField, TextArea] private string waitingHelpText = "\"Porotta? That'll take around 10 minutes.\"";

    [Tooltip("Label for the skip breakfast choice button.")]
    [SerializeField] private string skipBreakfastButtonLabel = "[1] Skip Breakfast";

    [Tooltip("Label for the skip line choice button.")]
    [SerializeField] private string skipLineButtonLabel = "[2] Skip the Line";

    // ── Runtime State ─────────────────────────────────────────────────

    private GameObject panelRoot;
    private TextMeshProUGUI headerLabel;
    private TextMeshProUGUI statusLabel;
    private RectTransform fillRect;
    private Button skipBreakfastButton;
    private Button skipLineButton;
    private TextMeshProUGUI skipBreakfastLabelText;
    private TextMeshProUGUI skipLineLabelText;
    private Action onSkipBreakfastCallback;
    private Action onSkipLineCallback;

    // Input-arming guard against same-frame input bleed (keyboard & mouse)
    private int openedFrame = -1;
    private bool isArmed;

    private static readonly Key[] SkipBreakfastKeys = { Key.Digit1, Key.Numpad1 };
    private static readonly Key[] SkipLineKeys = { Key.Digit2, Key.Numpad2 };

    // Layout constants
    private const float PanelWidth = 620f;
    private const float ProgressBarHeight = 22f;
    private const float ButtonHeight = 46f;
    private const float ButtonSpacing = 8f;

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Opens the modal queue waiting UI, resets progress to 0, and disarms input
    /// on the opening frame to prevent same-frame input bleed.
    /// </summary>
    /// <param name="onSkipBreakfast">Callback when Skip Breakfast is chosen.</param>
    /// <param name="onSkipLine">Callback when Skip the Line is chosen.</param>
    /// <param name="statusNoticeOverride">Optional custom status/wait message. If null/empty, uses <see cref="waitingHelpText"/>.</param>
    public void Show(Action onSkipBreakfast, Action onSkipLine, string statusNoticeOverride = null)
    {
        onSkipBreakfastCallback = onSkipBreakfast;
        onSkipLineCallback = onSkipLine;

        // Arming guard: record opening frame and temporarily disarm input & UI buttons
        openedFrame = Time.frameCount;
        isArmed = false;

        if (skipBreakfastButton != null)
        {
            skipBreakfastButton.interactable = false;
        }

        if (skipLineButton != null)
        {
            skipLineButton.interactable = false;
        }

        // Apply Inspector-configurable text labels
        if (headerLabel != null)
        {
            headerLabel.text = queueTitle;
        }

        if (statusLabel != null)
        {
            statusLabel.text = !string.IsNullOrEmpty(statusNoticeOverride) ? statusNoticeOverride : waitingHelpText;
        }

        if (skipBreakfastLabelText != null)
        {
            skipBreakfastLabelText.text = skipBreakfastButtonLabel;
        }

        if (skipLineLabelText != null)
        {
            skipLineLabelText.text = skipLineButtonLabel;
        }

        SetProgress(0f);
        panelRoot.SetActive(true);
        IsOpen = true;

        EnsureEventSystem();

        Debug.Log($"[CanteenQueueUI] Queue waiting UI opened on frame {openedFrame}. Input disarmed for opening frame.");
    }

    /// <summary>
    /// Hides the modal queue waiting UI and resets input-arming state.
    /// </summary>
    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        IsOpen = false;
        openedFrame = -1;
        isArmed = false;
        onSkipBreakfastCallback = null;
        onSkipLineCallback = null;

        Debug.Log("[CanteenQueueUI] Queue waiting UI closed.");
    }

    /// <summary>
    /// Updates the fill amount of the progress bar from [0.0, 1.0].
    /// Does not display numeric text.
    /// </summary>
    public void SetProgress(float normalizedProgress)
    {
        if (fillRect == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(normalizedProgress);
        fillRect.anchorMax = new Vector2(clamped, 1f);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        BuildUI();
        panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // Do not process choice input on or before the opening frame to prevent input bleed
        if (Time.frameCount <= openedFrame)
        {
            return;
        }

        if (!isArmed)
        {
            ArmInput();
        }

        HandleKeyboardInput();
    }

    private void ArmInput()
    {
        isArmed = true;

        if (skipBreakfastButton != null)
        {
            skipBreakfastButton.interactable = true;
        }

        if (skipLineButton != null)
        {
            skipLineButton.interactable = true;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsOpen = false;
        }
    }

    // ── Input Handling ────────────────────────────────────────────────

    private void HandleKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // [1] Skip Breakfast
        for (int i = 0; i < SkipBreakfastKeys.Length; i++)
        {
            Key key = SkipBreakfastKeys[i];
            if (keyboard[key] != null && keyboard[key].wasPressedThisFrame)
            {
                TriggerSkipBreakfast();
                return;
            }
        }

        // [2] Skip the Line
        for (int i = 0; i < SkipLineKeys.Length; i++)
        {
            Key key = SkipLineKeys[i];
            if (keyboard[key] != null && keyboard[key].wasPressedThisFrame)
            {
                TriggerSkipLine();
                return;
            }
        }
    }

    private void TriggerSkipBreakfast()
    {
        // Guard against any input bleed (keyboard shortcut or mouse click on opening frame)
        if (!IsOpen || !isArmed || Time.frameCount <= openedFrame)
        {
            return;
        }

        Action callback = onSkipBreakfastCallback;
        callback?.Invoke();
    }

    private void TriggerSkipLine()
    {
        // Guard against any input bleed (keyboard shortcut or mouse click on opening frame)
        if (!IsOpen || !isArmed || Time.frameCount <= openedFrame)
        {
            return;
        }

        Action callback = onSkipLineCallback;
        callback?.Invoke();
    }

    // ── UI Construction ───────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas ──
        GameObject canvasGo = new GameObject("CanteenQueueCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210; // Above DialogueUI (200)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // ── Panel (Centered) ──
        panelRoot = new GameObject("QueuePanel");
        panelRoot.transform.SetParent(canvasGo.transform, false);

        RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, 0f);
        panelRect.anchoredPosition = Vector2.zero;

        panelRoot.AddComponent<Image>().color = panelBackground;

        VerticalLayoutGroup panelVLG = panelRoot.AddComponent<VerticalLayoutGroup>();
        panelVLG.padding = new RectOffset(32, 32, 28, 28);
        panelVLG.spacing = 14f;
        panelVLG.childAlignment = TextAnchor.UpperLeft;
        panelVLG.childControlWidth = true;
        panelVLG.childControlHeight = true;
        panelVLG.childForceExpandWidth = true;
        panelVLG.childForceExpandHeight = false;

        ContentSizeFitter panelFitter = panelRoot.AddComponent<ContentSizeFitter>();
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ── Header: "Waiting for Porotta" ──
        headerLabel = CreateTextElement(
            panelRoot.transform, "HeaderLabel",
            preferredHeight: 30f,
            fontSize: 22f,
            color: headerColor,
            fontStyle: FontStyles.Bold,
            alignment: TextAlignmentOptions.Left);

        headerLabel.text = queueTitle;

        // ── Status Text: Worker's 10 minute notice ──
        statusLabel = CreateTextElement(
            panelRoot.transform, "StatusLabel",
            preferredHeight: 24f,
            fontSize: 16f,
            color: statusColor,
            fontStyle: FontStyles.Italic,
            alignment: TextAlignmentOptions.Left);

        statusLabel.text = waitingHelpText;

        // ── Divider ──
        CreateDivider(panelRoot.transform);

        // ── Progress Bar ──
        BuildProgressBar(panelRoot.transform);

        // ── Spacer ──
        CreateSpacer(panelRoot.transform, 6f);

        // ── Buttons ──
        skipBreakfastButton = CreateChoiceButton(panelRoot.transform, skipBreakfastButtonLabel, TriggerSkipBreakfast, out skipBreakfastLabelText);
        skipLineButton = CreateChoiceButton(panelRoot.transform, skipLineButtonLabel, TriggerSkipLine, out skipLineLabelText);
    }

    private void BuildProgressBar(Transform parent)
    {
        // Container
        GameObject barBg = new GameObject("ProgressBarBackground");
        barBg.transform.SetParent(parent, false);

        LayoutElement barLE = barBg.AddComponent<LayoutElement>();
        barLE.preferredHeight = ProgressBarHeight;
        barLE.flexibleWidth = 1f;

        Image bgImage = barBg.AddComponent<Image>();
        bgImage.color = progressBarBackground;

        // Mask / Fill container
        GameObject fillGo = new GameObject("ProgressBarFill");
        fillGo.transform.SetParent(barBg.transform, false);

        fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f); // Width controlled by anchorMax.x
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillGo.AddComponent<Image>();
        fillImage.color = progressBarFillColor;
    }

    private Button CreateChoiceButton(Transform parent, string label, Action onClick, out TextMeshProUGUI outText)
    {
        GameObject btnGo = new GameObject("QueueChoiceButton");
        btnGo.transform.SetParent(parent, false);

        LayoutElement le = btnGo.AddComponent<LayoutElement>();
        le.preferredHeight = ButtonHeight;
        le.flexibleWidth = 1f;

        Image btnImage = btnGo.AddComponent<Image>();
        btnImage.color = buttonNormalColor;

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        ColorBlock colors = btn.colors;
        colors.normalColor = buttonNormalColor;
        colors.highlightedColor = buttonHighlightedColor;
        colors.pressedColor = buttonPressedColor;
        colors.selectedColor = buttonHighlightedColor;
        colors.disabledColor = buttonNormalColor; // Prevent 1-frame visual flash while disarmed
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        btn.colors = colors;

        // Label
        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(btnGo.transform, false);

        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 0f);
        textRect.offsetMax = new Vector2(-18f, 0f);

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.color = buttonTextColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        outText = tmp;

        btn.onClick.AddListener(() => onClick?.Invoke());
        return btn;
    }

    private TextMeshProUGUI CreateTextElement(
        Transform parent,
        string name,
        float preferredHeight,
        float fontSize,
        Color color,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.flexibleWidth = 1f;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = fontStyle;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;

        return tmp;
    }

    private void CreateDivider(Transform parent)
    {
        GameObject go = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1f;
        le.flexibleWidth = 1f;

        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
    }

    private void CreateSpacer(Transform parent, float height)
    {
        GameObject go = new GameObject("Spacer");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.flexibleWidth = 1f;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existingES = FindFirstObjectByType<EventSystem>();
        if (existingES == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            InputSystemUIInputModule module = esGo.AddComponent<InputSystemUIInputModule>();
            if (InputSystem.actions != null)
            {
                module.actionsAsset = InputSystem.actions;
            }
        }
    }
}
