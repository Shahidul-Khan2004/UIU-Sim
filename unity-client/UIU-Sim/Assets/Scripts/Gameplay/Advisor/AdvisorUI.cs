using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Advisor
{
    /// <summary>
    /// Screen-space modal UI panel for interacting with the AI Academic Advisor.
    /// Built entirely at runtime at sorting order 220 (renders above CanteenQueueUI 210, DialogueUI 200, InteractionUI 100).
    /// Manages client-side runtime conversation history, input locks, quick actions, and state transitions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdvisorUI : MonoBehaviour
    {
        public static AdvisorUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        [Header("Appearance Colors")]
        [SerializeField] private Color panelBackground = new Color(0.06f, 0.07f, 0.09f, 0.95f);
        [SerializeField] private Color headerColor = new Color(1f, 0.82f, 0.35f, 1f); // warm amber
        [SerializeField] private Color advisorTextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color playerTextColor = new Color(0.75f, 0.88f, 1f, 1f);
        [SerializeField] private Color statusTextColor = new Color(0.70f, 0.70f, 0.70f, 1f);
        [SerializeField] private Color buttonNormalColor = new Color(0.18f, 0.20f, 0.24f, 1f);
        [SerializeField] private Color buttonHighlightedColor = new Color(0.28f, 0.32f, 0.38f, 1f);
        [SerializeField] private Color buttonPressedColor = new Color(0.12f, 0.14f, 0.16f, 1f);
        [SerializeField] private Color quickActionButtonColor = new Color(0.14f, 0.18f, 0.22f, 1f);

        private AdvisorClient advisorClient;
        private PlayerMovement cachedPlayerMovement;
        private bool wasMovementEnabled = true;

        // Cursor state backup
        private CursorLockMode previousLockMode;
        private bool previousCursorVisible;

        // UI GameObjects & Components
        private GameObject panelRoot;
        private Transform messageContainer;
        private ScrollRect scrollRect;
        private TMP_InputField inputField;
        private Button sendButton;
        private TextMeshProUGUI statusLabel;
        private readonly List<Button> quickActionButtons = new List<Button>();

        // Conversation runtime state
        private readonly List<AdvisorClient.AdvisorHistoryMessageDto> history = new List<AdvisorClient.AdvisorHistoryMessageDto>();
        private bool hasReceivedInitialGreeting;
        private bool isRequestInFlight;

        // Constants
        public const int MaxHistoryCount = 8;
        public const int MaxMessageLength = 500;

        public const string QuickAction1Prompt = "What should I focus on to improve my academic performance?";
        public const string QuickAction2Prompt = "How can I improve my Academic Reputation and overall standing at university?";
        public const string QuickAction3Prompt = "Can you help me think about my career direction and what kinds of interests or skills I should explore?";
        public const string QuickAction4Prompt = "How can I make better choices and balance academics with university life?";

        public static AdvisorUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameObject host = new GameObject("AdvisorUI_Host");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }
            host.AddComponent<AdvisorClient>();
            return host.AddComponent<AdvisorUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            advisorClient = GetComponent<AdvisorClient>();
            if (advisorClient == null)
            {
                advisorClient = gameObject.AddComponent<AdvisorClient>();
            }

            BuildUI();
            panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            // Close on Escape key
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

            if (panelRoot == null)
            {
                BuildUI();
            }

            IsOpen = true;
            panelRoot.SetActive(true);

            // Lock FPS movement so typing does not move character
            cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            if (cachedPlayerMovement != null)
            {
                wasMovementEnabled = cachedPlayerMovement.enabled;
                cachedPlayerMovement.enabled = false;
            }

            // Unlock and show cursor
            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureEventSystem();

            if (!hasReceivedInitialGreeting && !isRequestInFlight)
            {
                RequestInitialGreeting();
            }
            else
            {
                SetInputInteractable(!isRequestInFlight);
            }
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            // Restore movement
            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.enabled = wasMovementEnabled;
            }

            // Restore cursor
            Cursor.lockState = previousLockMode;
            Cursor.visible = previousCursorVisible;

            Debug.Log("[AdvisorUI] Closed advisor panel and restored player controls.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsOpen = false;
            }
        }

        private void EnsureAdvisorClient()
        {
            if (advisorClient == null)
            {
                advisorClient = GetComponent<AdvisorClient>();
                if (advisorClient == null)
                {
                    advisorClient = gameObject.AddComponent<AdvisorClient>();
                }
            }
        }

        private void RequestInitialGreeting()
        {
            EnsureAdvisorClient();
            SetThinkingState(true, "Advisor is reviewing your situation...");
            SetInputInteractable(false);

            AdvisorClient.AdvisorChatRequestDto request = new AdvisorClient.AdvisorChatRequestDto("", history, true);

            advisorClient.SendChat(
                request,
                onSuccess: response =>
                {
                    SetThinkingState(false, null);
                    hasReceivedInitialGreeting = true;
                    AppendAdvisorMessage(response.reply);
                    RecordHistory("advisor", response.reply);
                    SetInputInteractable(true);
                },
                onError: (error, code) =>
                {
                    SetThinkingState(false, null);
                    string userError = ClassifyErrorMessage(code, error);
                    AppendSystemNotice(userError);
                    SetInputInteractable(true);
                    Debug.LogWarning($"[AdvisorUI] Initial greeting failed: {error} (HTTP {code})");
                }
            );
        }

        public void SendUserMessage(string text)
        {
            if (isRequestInFlight)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string trimmed = text.Trim();
            if (trimmed.Length > MaxMessageLength)
            {
                trimmed = trimmed.Substring(0, MaxMessageLength);
            }

            AppendUserMessage(trimmed);
            RecordHistory("user", trimmed);

            if (inputField != null)
            {
                inputField.text = string.Empty;
            }

            SetThinkingState(true, "Advisor is thinking...");
            SetInputInteractable(false);

            AdvisorClient.AdvisorChatRequestDto request = new AdvisorClient.AdvisorChatRequestDto(trimmed, history, false);

            EnsureAdvisorClient();
            advisorClient.SendChat(
                request,
                onSuccess: response =>
                {
                    SetThinkingState(false, null);
                    AppendAdvisorMessage(response.reply);
                    RecordHistory("advisor", response.reply);
                    SetInputInteractable(true);
                },
                onError: (error, code) =>
                {
                    SetThinkingState(false, null);
                    string userError = ClassifyErrorMessage(code, error);
                    AppendSystemNotice(userError);
                    SetInputInteractable(true);
                    Debug.LogWarning($"[AdvisorUI] Chat message failed: {error} (HTTP {code})");
                }
            );
        }

        private void RecordHistory(string role, string content)
        {
            history.Add(new AdvisorClient.AdvisorHistoryMessageDto(role, content));
            if (history.Count > MaxHistoryCount)
            {
                history.RemoveAt(0);
            }
        }

        private void SetThinkingState(bool thinking, string statusText)
        {
            isRequestInFlight = thinking;
            if (statusLabel != null)
            {
                statusLabel.gameObject.SetActive(thinking);
                statusLabel.text = statusText ?? string.Empty;
            }
        }

        private void SetInputInteractable(bool interactable)
        {
            if (sendButton != null) sendButton.interactable = interactable;
            if (inputField != null) inputField.interactable = interactable;
            foreach (Button btn in quickActionButtons)
            {
                if (btn != null) btn.interactable = interactable;
            }
        }

        private void AppendUserMessage(string message)
        {
            CreateMessageBubble("You", message, playerTextColor, TextAlignmentOptions.TopRight);
        }

        private void AppendAdvisorMessage(string message)
        {
            CreateMessageBubble("Advisor", message, advisorTextColor, TextAlignmentOptions.TopLeft);
        }

        private void AppendSystemNotice(string notice)
        {
            CreateMessageBubble("System", notice, new Color(1f, 0.45f, 0.45f, 1f), TextAlignmentOptions.TopLeft);
        }

        private void CreateMessageBubble(string sender, string message, Color textColor, TextAlignmentOptions alignment)
        {
            if (messageContainer == null) return;

            GameObject bubbleGo = new GameObject("Bubble_" + sender);
            bubbleGo.transform.SetParent(messageContainer, false);

            VerticalLayoutGroup vlg = bubbleGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 8, 8);
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            Image bg = bubbleGo.AddComponent<Image>();
            bg.color = sender == "You" ? new Color(0.12f, 0.16f, 0.22f, 0.85f) : new Color(0.09f, 0.11f, 0.14f, 0.85f);

            // Sender label
            GameObject labelGo = new GameObject("SenderLabel");
            labelGo.transform.SetParent(bubbleGo.transform, false);
            TextMeshProUGUI senderTmp = labelGo.AddComponent<TextMeshProUGUI>();
            senderTmp.fontSize = 13f;
            senderTmp.fontStyle = FontStyles.Bold;
            senderTmp.color = sender == "Advisor" ? headerColor : new Color(0.6f, 0.8f, 1f, 1f);
            senderTmp.text = sender;
            senderTmp.richText = false; // Prevent rich text injection

            // Content text
            GameObject textGo = new GameObject("MessageText");
            textGo.transform.SetParent(bubbleGo.transform, false);
            TextMeshProUGUI textTmp = textGo.AddComponent<TextMeshProUGUI>();
            textTmp.fontSize = 15f;
            textTmp.color = textColor;
            textTmp.text = message;
            textTmp.richText = false; // Prevent rich text injection
            textTmp.textWrappingMode = TextWrappingModes.Normal;

            LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleGo.GetComponent<RectTransform>());
            StartCoroutine(ScrollToBottomRoutine());
        }

        private IEnumerator ScrollToBottomRoutine()
        {
            yield return null; // Wait one frame for UI layout update
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private static string ClassifyErrorMessage(long code, string rawError)
        {
            if (code == 429)
            {
                return "The advisor is busy right now. Please try again shortly.";
            }
            if (code == 401)
            {
                return "Your session has expired. Please sign in again.";
            }
            return "Advisor is unavailable right now. Please try again.";
        }

        // ── UI Construction ───────────────────────────────────────────────

        private void BuildUI()
        {
            // Canvas
            GameObject canvasGo = new GameObject("AdvisorCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220; // Above CanteenQueueUI (210) & DialogueUI (200), below SystemNotificationUI (250)

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel Root
            panelRoot = new GameObject("AdvisorPanel");
            panelRoot.transform.SetParent(canvasGo.transform, false);

            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 660f);
            panelRect.anchoredPosition = Vector2.zero;

            panelRoot.AddComponent<Image>().color = panelBackground;

            VerticalLayoutGroup panelVLG = panelRoot.AddComponent<VerticalLayoutGroup>();
            panelVLG.padding = new RectOffset(24, 24, 20, 20);
            panelVLG.spacing = 10f;
            panelVLG.childAlignment = TextAnchor.UpperCenter;
            panelVLG.childControlWidth = true;
            panelVLG.childControlHeight = false;
            panelVLG.childForceExpandWidth = true;
            panelVLG.childForceExpandHeight = false;

            // ── Header ──
            BuildHeader(panelRoot.transform);

            // ── Divider ──
            CreateDivider(panelRoot.transform);

            // ── Conversation Scroll View ──
            BuildConversationArea(panelRoot.transform);

            // ── Status Line ──
            GameObject statusGo = new GameObject("StatusLabel");
            statusGo.transform.SetParent(panelRoot.transform, false);
            statusLabel = statusGo.AddComponent<TextMeshProUGUI>();
            statusLabel.fontSize = 13f;
            statusLabel.fontStyle = FontStyles.Italic;
            statusLabel.color = statusTextColor;
            statusLabel.alignment = TextAlignmentOptions.MidlineLeft;
            statusLabel.richText = false;
            statusGo.SetActive(false);

            // ── Quick Actions ──
            BuildQuickActions(panelRoot.transform);

            // ── Input Area ──
            BuildInputArea(panelRoot.transform);
        }

        private void BuildHeader(Transform parent)
        {
            GameObject headerGo = new GameObject("Header");
            headerGo.transform.SetParent(parent, false);

            RectTransform headerRect = headerGo.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 48f);

            HorizontalLayoutGroup hlg = headerGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 10f;

            // Title block
            GameObject titleBlock = new GameObject("TitleBlock");
            titleBlock.transform.SetParent(headerGo.transform, false);
            VerticalLayoutGroup vlg = titleBlock.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            LayoutElement titleLE = titleBlock.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1f;

            GameObject titleTextGo = new GameObject("Title");
            titleTextGo.transform.SetParent(titleBlock.transform, false);
            TextMeshProUGUI titleTmp = titleTextGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "AI Academic Advisor";
            titleTmp.fontSize = 20f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = headerColor;
            titleTmp.richText = false;

            GameObject subtitleTextGo = new GameObject("Subtitle");
            subtitleTextGo.transform.SetParent(titleBlock.transform, false);
            TextMeshProUGUI subTmp = subtitleTextGo.AddComponent<TextMeshProUGUI>();
            subTmp.text = "Academic & Career Guidance";
            subTmp.fontSize = 13f;
            subTmp.color = statusTextColor;
            subTmp.richText = false;

            // Close button
            GameObject closeBtnGo = new GameObject("CloseButton");
            closeBtnGo.transform.SetParent(headerGo.transform, false);

            LayoutElement closeLE = closeBtnGo.AddComponent<LayoutElement>();
            closeLE.preferredWidth = 36f;
            closeLE.preferredHeight = 36f;

            Image closeImg = closeBtnGo.AddComponent<Image>();
            closeImg.color = buttonNormalColor;

            Button closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            ApplyButtonColors(closeBtn);
            closeBtn.onClick.AddListener(Hide);

            GameObject closeTextGo = new GameObject("CloseIcon");
            closeTextGo.transform.SetParent(closeBtnGo.transform, false);
            RectTransform iconRect = closeTextGo.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            TextMeshProUGUI iconTmp = closeTextGo.AddComponent<TextMeshProUGUI>();
            iconTmp.text = "X";
            iconTmp.fontSize = 16f;
            iconTmp.fontStyle = FontStyles.Bold;
            iconTmp.color = Color.white;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.richText = false;
        }

        private void BuildConversationArea(Transform parent)
        {
            GameObject scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(parent, false);

            LayoutElement scrollLE = scrollGo.AddComponent<LayoutElement>();
            scrollLE.preferredHeight = 360f;
            scrollLE.flexibleWidth = 1f;

            Image scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0.04f, 0.05f, 0.07f, 0.8f);

            scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Viewport
            GameObject viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform vpRect = viewportGo.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;

            viewportGo.AddComponent<RectMask2D>();
            scrollRect.viewport = vpRect;

            // Content
            GameObject contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            RectTransform contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup contentVLG = contentGo.AddComponent<VerticalLayoutGroup>();
            contentVLG.padding = new RectOffset(14, 14, 12, 12);
            contentVLG.spacing = 8f;
            contentVLG.childControlWidth = true;
            contentVLG.childControlHeight = true;
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;

            ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.content = contentRect;
            messageContainer = contentGo.transform;
        }

        private void BuildQuickActions(Transform parent)
        {
            GameObject qaGo = new GameObject("QuickActionsGrid");
            qaGo.transform.SetParent(parent, false);

            GridLayoutGroup glg = qaGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(346f, 36f);
            glg.spacing = new Vector2(10f, 6f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            LayoutElement qaLE = qaGo.AddComponent<LayoutElement>();
            qaLE.preferredHeight = 82f;
            qaLE.flexibleWidth = 1f;

            AddQuickActionButton(qaGo.transform, "1. Improve Academically", QuickAction1Prompt);
            AddQuickActionButton(qaGo.transform, "2. Improve Reputation", QuickAction2Prompt);
            AddQuickActionButton(qaGo.transform, "3. Career Advice", QuickAction3Prompt);
            AddQuickActionButton(qaGo.transform, "4. University Life", QuickAction4Prompt);
        }

        private void AddQuickActionButton(Transform parent, string label, string prompt)
        {
            GameObject btnGo = new GameObject("QA_" + label);
            btnGo.transform.SetParent(parent, false);

            Image img = btnGo.AddComponent<Image>();
            img.color = quickActionButtonColor;

            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            ApplyButtonColors(btn);

            btn.onClick.AddListener(() =>
            {
                SendUserMessage(prompt);
            });

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(btnGo.transform, false);
            RectTransform tr = textGo.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10f, 0);
            tr.offsetMax = new Vector2(-10f, 0);

            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.richText = false;

            quickActionButtons.Add(btn);
        }

        private void BuildInputArea(Transform parent)
        {
            GameObject inputRow = new GameObject("InputRow");
            inputRow.transform.SetParent(parent, false);

            LayoutElement rowLE = inputRow.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 44f;
            rowLE.flexibleWidth = 1f;

            HorizontalLayoutGroup hlg = inputRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // Input Field Root
            GameObject ifGo = new GameObject("InputField");
            ifGo.transform.SetParent(inputRow.transform, false);

            LayoutElement ifLE = ifGo.AddComponent<LayoutElement>();
            ifLE.flexibleWidth = 1f;

            Image ifBg = ifGo.AddComponent<Image>();
            ifBg.color = new Color(0.12f, 0.14f, 0.18f, 1f);

            inputField = ifGo.AddComponent<TMP_InputField>();
            inputField.characterLimit = MaxMessageLength;
            inputField.richText = false;

            // Text Area
            GameObject textAreaGo = new GameObject("Text Area");
            textAreaGo.transform.SetParent(ifGo.transform, false);
            RectTransform areaRect = textAreaGo.AddComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(12f, 4f);
            areaRect.offsetMax = new Vector2(-12f, -4f);
            textAreaGo.AddComponent<RectMask2D>();

            // Placeholder
            GameObject placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textAreaGo.transform, false);
            RectTransform phRect = placeholderGo.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI phTmp = placeholderGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = "Ask your advisor a question (academics, study habits, career)...";
            phTmp.fontSize = 14f;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            phTmp.richText = false;
            inputField.placeholder = phTmp;

            // Text
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(textAreaGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI textTmp = textGo.AddComponent<TextMeshProUGUI>();
            textTmp.fontSize = 14f;
            textTmp.color = Color.white;
            textTmp.alignment = TextAlignmentOptions.MidlineLeft;
            textTmp.richText = false;
            inputField.textComponent = textTmp;

            inputField.onSubmit.AddListener(text =>
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    SendUserMessage(text);
                }
            });

            // Send Button
            GameObject sendBtnGo = new GameObject("SendButton");
            sendBtnGo.transform.SetParent(inputRow.transform, false);

            LayoutElement sendLE = sendBtnGo.AddComponent<LayoutElement>();
            sendLE.preferredWidth = 90f;

            Image sendImg = sendBtnGo.AddComponent<Image>();
            sendImg.color = buttonNormalColor;

            sendButton = sendBtnGo.AddComponent<Button>();
            sendButton.targetGraphic = sendImg;
            ApplyButtonColors(sendButton);

            sendButton.onClick.AddListener(() =>
            {
                if (inputField != null && !string.IsNullOrWhiteSpace(inputField.text))
                {
                    SendUserMessage(inputField.text);
                }
            });

            GameObject sendTextGo = new GameObject("SendLabel");
            sendTextGo.transform.SetParent(sendBtnGo.transform, false);
            RectTransform stRect = sendTextGo.AddComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;

            TextMeshProUGUI sendTmp = sendTextGo.AddComponent<TextMeshProUGUI>();
            sendTmp.text = "Send";
            sendTmp.fontSize = 14f;
            sendTmp.fontStyle = FontStyles.Bold;
            sendTmp.color = Color.white;
            sendTmp.alignment = TextAlignmentOptions.Center;
            sendTmp.richText = false;
        }

        private void CreateDivider(Transform parent)
        {
            GameObject go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 1f;
            le.flexibleWidth = 1f;
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        }

        private void ApplyButtonColors(Button btn)
        {
            ColorBlock colors = btn.colors;
            colors.normalColor = buttonNormalColor;
            colors.highlightedColor = buttonHighlightedColor;
            colors.pressedColor = buttonPressedColor;
            colors.selectedColor = buttonHighlightedColor;
            colors.fadeDuration = 0.1f;
            btn.colors = colors;
        }

        private static void EnsureEventSystem()
        {
            EventSystem existingES = FindFirstObjectByType<EventSystem>();
            if (existingES == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                InputSystemUIInputModule module = esGo.AddComponent<InputSystemUIInputModule>();
                if (UnityEngine.InputSystem.InputSystem.actions != null)
                {
                    module.actionsAsset = UnityEngine.InputSystem.InputSystem.actions;
                }
                return;
            }

#pragma warning disable CS0618
            StandaloneInputModule legacy = existingES.GetComponent<StandaloneInputModule>();
#pragma warning restore CS0618
            if (legacy != null)
            {
                Destroy(legacy);
            }

            if (existingES.GetComponent<InputSystemUIInputModule>() == null)
            {
                InputSystemUIInputModule module = existingES.gameObject.AddComponent<InputSystemUIInputModule>();
                if (UnityEngine.InputSystem.InputSystem.actions != null)
                {
                    module.actionsAsset = UnityEngine.InputSystem.InputSystem.actions;
                }
            }
        }
    }
}
