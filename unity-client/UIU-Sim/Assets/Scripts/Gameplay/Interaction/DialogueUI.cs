using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Minimal screen-space dialogue panel for NPC interactions.
/// Built entirely at runtime — no prefab or scene setup required.
/// Add this to the Player prefab root alongside <see cref="InteractionUI"/>.
/// <para>
/// Sorting order 200 ensures it renders above the existing InteractionUI canvas (100).
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueUI : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────

    /// <summary>The active DialogueUI instance in the scene.</summary>
    public static DialogueUI Instance { get; private set; }

    /// <summary>
    /// True while the dialogue panel is visible.
    /// Read by <see cref="InteractionController"/> to block re-interaction while a dialogue is open.
    /// </summary>
    public static bool IsOpen { get; private set; }

    // ── Serialized ────────────────────────────────────────────────────

    [Header("Panel Appearance")]
    [SerializeField] private Color panelBackground  = new Color(0.05f, 0.05f, 0.05f, 0.92f);

    [Header("Text Colors")]
    [SerializeField] private Color speakerColor  = new Color(1f, 0.82f, 0.35f, 1f);  // warm amber
    [SerializeField] private Color dialogueColor = Color.white;
    [SerializeField] private Color buttonTextColor = Color.white;

    [Header("Button Appearance")]
    [SerializeField] private Color buttonNormalColor      = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color buttonHighlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
    [SerializeField] private Color buttonPressedColor     = new Color(0.08f, 0.08f, 0.08f, 1f);

    [Header("Font Sizes")]
    [SerializeField] private float speakerFontSize  = 18f;
    [SerializeField] private float dialogueFontSize = 22f;
    [SerializeField] private float buttonFontSize   = 20f;

    // ── Runtime state ─────────────────────────────────────────────────

    private GameObject             panelRoot;
    private TextMeshProUGUI        speakerLabel;
    private TextMeshProUGUI        dialogueLabel;
    private Transform              choiceContainer;
    private LayoutElement          choiceContainerLE;
    private readonly List<GameObject> activeButtons = new List<GameObject>();
    private Choice[]               currentChoices;

    private static readonly Key[] DigitKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private static readonly Key[] NumpadKeys =
    {
        Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5,
        Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
    };

    // Saved cursor state, restored when dialogue closes.
    private CursorLockMode previousLockMode;
    private bool           previousCursorVisible;

    // Layout constants — keep in sync with BuildUI padding / spacing.
    private const float ButtonHeight  = 48f;
    private const float ButtonSpacing = 8f;

    // ── Choice ────────────────────────────────────────────────────────

    /// <summary>
    /// A single dialogue option consisting of a display label and an action
    /// invoked when the player clicks it.
    /// </summary>
    public readonly struct Choice
    {
        /// <summary>Text displayed on the button.</summary>
        public readonly string Label;

        /// <summary>Callback invoked after the dialogue closes.</summary>
        public readonly Action OnSelected;

        public Choice(string label, Action onSelected)
        {
            Label      = label;
            OnSelected = onSelected;
        }
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Opens the dialogue panel.
    /// </summary>
    /// <param name="speakerName">NPC name shown in the header.</param>
    /// <param name="line">NPC's spoken line.</param>
    /// <param name="choices">Available choices. Each choice closes the panel and fires its callback.</param>
    public void Show(string speakerName, string line, Choice[] choices)
    {
        speakerLabel.text  = speakerName;
        dialogueLabel.text = line;
        currentChoices     = choices;

        RebuildChoiceButtons(choices);

        panelRoot.SetActive(true);
        IsOpen = true;

        // Unlock cursor so the player can click the buttons.
        previousLockMode    = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        EnsureEventSystem();

        Debug.Log($"[DialogueUI] Opened — speaker='{speakerName}', choices={choices.Length}");
    }

    /// <summary>
    /// Closes the dialogue panel and restores the cursor to its previous state.
    /// Called automatically when a choice is selected; may also be called externally.
    /// </summary>
    public void Hide()
    {
        panelRoot.SetActive(false);
        IsOpen = false;
        currentChoices = null;

        Cursor.lockState = previousLockMode;
        Cursor.visible   = previousCursorVisible;

        Debug.Log("[DialogueUI] Closed.");
    }

    /// <summary>
    /// Selects the choice at <paramref name="index"/>, closing the dialogue and firing its callback.
    /// Shared execution path for both UI button clicks and number key shortcuts.
    /// </summary>
    public void SelectChoice(int index)
    {
        if (!IsOpen || currentChoices == null || index < 0 || index >= currentChoices.Length)
        {
            return;
        }

        Action callback = currentChoices[index].OnSelected;
        Hide();
        callback?.Invoke();
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
        panelRoot.SetActive(false); // hidden until Show() is called
    }

    private void Update()
    {
        if (!IsOpen || currentChoices == null || currentChoices.Length == 0)
        {
            return;
        }

        HandleKeyboardInput();
    }

    private void HandleKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        int max = Mathf.Min(currentChoices.Length, 9);
        for (int i = 0; i < max; i++)
        {
            Key digitKey = DigitKeys[i];
            Key numpadKey = NumpadKeys[i];

            bool pressed = (keyboard[digitKey] != null && keyboard[digitKey].wasPressedThisFrame)
                        || (keyboard[numpadKey] != null && keyboard[numpadKey].wasPressedThisFrame);

            if (pressed)
            {
                SelectChoice(i);
                break;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsOpen   = false;
        }
    }

    // ── UI Construction ───────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas ──

        GameObject canvasGo = new GameObject("DialogueCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // Above InteractionUI (100)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // ── Panel ──

        panelRoot = new GameObject("DialoguePanel");
        panelRoot.transform.SetParent(canvasGo.transform, false);

        RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin       = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax       = new Vector2(0.5f, 0.5f);
        panelRect.pivot           = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta       = new Vector2(700f, 0f);
        panelRect.anchoredPosition = Vector2.zero;

        panelRoot.AddComponent<Image>().color = panelBackground;

        VerticalLayoutGroup panelVLG = panelRoot.AddComponent<VerticalLayoutGroup>();
        panelVLG.padding              = new RectOffset(30, 30, 24, 24);
        panelVLG.spacing              = 12f;
        panelVLG.childAlignment       = TextAnchor.UpperLeft;
        panelVLG.childControlWidth    = true;
        panelVLG.childControlHeight   = true;
        panelVLG.childForceExpandWidth  = true;
        panelVLG.childForceExpandHeight = false;

        ContentSizeFitter panelFitter = panelRoot.AddComponent<ContentSizeFitter>();
        panelFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ── Speaker label ──

        speakerLabel = CreateTextElement(
            panelRoot.transform, "SpeakerLabel",
            preferredHeight: 28f,
            fontSize: speakerFontSize,
            color: speakerColor,
            fontStyle: FontStyles.Bold,
            alignment: TextAlignmentOptions.Left,
            wordWrap: false);

        // ── Thin divider line ──

        CreateDivider(panelRoot.transform);

        // ── Dialogue text ──

        dialogueLabel = CreateTextElement(
            panelRoot.transform, "DialogueLabel",
            preferredHeight: 90f,
            fontSize: dialogueFontSize,
            color: dialogueColor,
            fontStyle: FontStyles.Italic,
            alignment: TextAlignmentOptions.Left,
            wordWrap: true);

        // ── Spacer ──

        CreateSpacer(panelRoot.transform, 6f);

        // ── Choice container ──

        GameObject containerGo = new GameObject("ChoiceContainer");
        containerGo.transform.SetParent(panelRoot.transform, false);
        containerGo.AddComponent<RectTransform>();

        choiceContainerLE = containerGo.AddComponent<LayoutElement>();
        choiceContainerLE.preferredHeight = 0f;
        choiceContainerLE.flexibleWidth   = 1f;

        VerticalLayoutGroup containerVLG = containerGo.AddComponent<VerticalLayoutGroup>();
        containerVLG.spacing              = ButtonSpacing;
        containerVLG.childAlignment       = TextAnchor.UpperLeft;
        containerVLG.childControlWidth    = true;
        containerVLG.childControlHeight   = true;
        containerVLG.childForceExpandWidth  = true;
        containerVLG.childForceExpandHeight = false;

        choiceContainer = containerGo.transform;
    }

    // ── Choice buttons ────────────────────────────────────────────────

    private void RebuildChoiceButtons(Choice[] choices)
    {
        // Destroy previous buttons.
        foreach (GameObject btn in activeButtons)
        {
            Destroy(btn);
        }
        activeButtons.Clear();

        // Reserve the correct height in the parent layout before instantiating buttons,
        // so ContentSizeFitter on the panel sizes correctly on the same frame.
        float containerHeight = choices.Length * ButtonHeight
                              + Mathf.Max(0, choices.Length - 1) * ButtonSpacing;
        choiceContainerLE.preferredHeight = containerHeight;

        // Build buttons.
        for (int i = 0; i < choices.Length; i++)
        {
            Choice choice = choices[i];
            int    number = i + 1;
            int    choiceIndex = i;
            GameObject btnGo = CreateChoiceButton($"[{number}] {choice.Label}", choiceIndex);
            activeButtons.Add(btnGo);
        }
    }

    private GameObject CreateChoiceButton(string label, int index)
    {
        // ── Root ──

        GameObject btnGo = new GameObject("ChoiceButton");
        btnGo.transform.SetParent(choiceContainer, false);

        LayoutElement le = btnGo.AddComponent<LayoutElement>();
        le.preferredHeight = ButtonHeight;
        le.flexibleWidth   = 1f;

        Image btnImage = btnGo.AddComponent<Image>();
        btnImage.color = buttonNormalColor;

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImage;

        ColorBlock colors         = btn.colors;
        colors.normalColor        = buttonNormalColor;
        colors.highlightedColor   = buttonHighlightedColor;
        colors.pressedColor       = buttonPressedColor;
        colors.selectedColor      = buttonHighlightedColor;
        colors.colorMultiplier    = 1f;
        colors.fadeDuration       = 0.1f;
        btn.colors = colors;

        // ── Label ──

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(btnGo.transform, false);

        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 0f);
        textRect.offsetMax = new Vector2(-18f, 0f);

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text               = label;
        tmp.fontSize           = buttonFontSize;
        tmp.color              = buttonTextColor;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        tmp.textWrappingMode   = TextWrappingModes.NoWrap;

        // ── Click ──

        btn.onClick.AddListener(() =>
        {
            SelectChoice(index);
        });

        return btnGo;
    }

    // ── Layout helpers ────────────────────────────────────────────────

    private TextMeshProUGUI CreateTextElement(
        Transform parent,
        string    name,
        float     preferredHeight,
        float     fontSize,
        Color     color,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        bool      wordWrap)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.flexibleWidth   = 1f;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.fontStyle          = fontStyle;
        tmp.alignment          = alignment;
        tmp.textWrappingMode   = wordWrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        tmp.overflowMode       = TextOverflowModes.Overflow;

        return tmp;
    }

    private void CreateDivider(Transform parent)
    {
        GameObject go = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1f;
        le.flexibleWidth   = 1f;

        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
    }

    private void CreateSpacer(Transform parent, float height)
    {
        GameObject go = new GameObject("Spacer");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.flexibleWidth   = 1f;
    }

    // ── Event System ──────────────────────────────────────────────────

    /// <summary>
    /// Ensures an <see cref="EventSystem"/> exists and is driven by
    /// <see cref="InputSystemUIInputModule"/> (new Input System).
    /// <para>
    /// Three cases are handled:
    /// <list type="number">
    ///   <item>No EventSystem at all → create one with InputSystemUIInputModule.</item>
    ///   <item>EventSystem exists with a legacy StandaloneInputModule → remove the legacy
    ///         module and add InputSystemUIInputModule.</item>
    ///   <item>EventSystem already has InputSystemUIInputModule → nothing to do.</item>
    /// </list>
    /// </para>
    /// </summary>
    private static void EnsureEventSystem()
    {
        EventSystem existingES = FindFirstObjectByType<EventSystem>();

        if (existingES == null)
        {
            // ── Case 1: No EventSystem at all — create a fresh one ────────
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            InputSystemUIInputModule module = esGo.AddComponent<InputSystemUIInputModule>();
            AssignActionsAsset(module);

            Debug.Log("[DialogueUI] Created EventSystem with InputSystemUIInputModule at runtime. " +
                      "Add one to the scene permanently to suppress this log.");
            return;
        }

        // ── Case 2: EventSystem exists — check for legacy StandaloneInputModule ──
#pragma warning disable CS0618 // StandaloneInputModule is referenced intentionally to detect and remove it.
        StandaloneInputModule legacy = existingES.GetComponent<StandaloneInputModule>();
#pragma warning restore CS0618
        if (legacy != null)
        {
            Debug.LogWarning(
                "[DialogueUI] Found a StandaloneInputModule on the EventSystem. " +
                "This project uses the new Input System. Removing StandaloneInputModule " +
                "and adding InputSystemUIInputModule.",
                existingES);

            Destroy(legacy);
        }

        // ── Case 3 (also falls through from Case 2): ensure the correct module exists ─
        if (existingES.GetComponent<InputSystemUIInputModule>() == null)
        {
            InputSystemUIInputModule module = existingES.gameObject.AddComponent<InputSystemUIInputModule>();
            AssignActionsAsset(module);

            Debug.Log("[DialogueUI] Added InputSystemUIInputModule to the existing EventSystem.", existingES);
        }
    }

    /// <summary>
    /// Wires the project-wide Input System actions asset to <paramref name="module"/> so the
    /// UI action map (Navigate, Submit, Cancel, Point, Click, etc.) is active.
    /// Uses the same asset reference pattern as <see cref="GameManager"/> and
    /// <see cref="InteractionController"/> — <c>InputSystem.actions</c> is the project-wide
    /// singleton set in Project Settings → Input System → Default Actions.
    /// </summary>
    private static void AssignActionsAsset(InputSystemUIInputModule module)
    {
        UnityEngine.InputSystem.InputActionAsset asset = UnityEngine.InputSystem.InputSystem.actions;

        if (asset == null)
        {
            Debug.LogWarning(
                "[DialogueUI] InputSystem.actions is null — no project-wide Input Actions asset found. " +
                "UI module will use its own default bindings. " +
                "Set a Default Actions asset in Project Settings → Input System.",
                module);
            return;
        }

        module.actionsAsset = asset;
        Debug.Log($"[DialogueUI] Assigned actions asset '{asset.name}' to InputSystemUIInputModule.", module);
    }
}
