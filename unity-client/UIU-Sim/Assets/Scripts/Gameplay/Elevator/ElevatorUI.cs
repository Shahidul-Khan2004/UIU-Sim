using System;
using System.Collections.Generic;
using TMPro;
using UIU.Simulator.Building.Generation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Elevator
{
    /// <summary>
    /// Screen-space runtime modal UI for elevator floor selection.
    /// Excludes the current floor. Supports both mouse click and New Input System keyboard shortcuts
    /// (1..9 -> Floors 1..9, 0 -> Floor 10, G -> Ground Floor, Esc -> Close).
    /// Disables player movement and camera look while open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevatorUI : MonoBehaviour
    {
        public static ElevatorUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        [Header("Appearance")]
        [SerializeField] private Color panelBackground = new Color(0.06f, 0.07f, 0.09f, 0.95f);
        [SerializeField] private Color headerColor = new Color(1f, 0.82f, 0.35f, 1f); // warm amber
        [SerializeField] private Color subtitleColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private Color buttonTextColor = Color.white;
        [SerializeField] private Color buttonNormalColor = new Color(0.18f, 0.20f, 0.24f, 1f);
        [SerializeField] private Color buttonHighlightedColor = new Color(0.28f, 0.32f, 0.38f, 1f);
        [SerializeField] private Color buttonPressedColor = new Color(0.12f, 0.14f, 0.16f, 1f);
        [SerializeField] private Color buttonDisabledColor = new Color(0.10f, 0.10f, 0.12f, 0.5f);

        private GameObject panelRoot;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI subtitleLabel;
        private TextMeshProUGUI statusLabel;
        private Transform buttonsContainer;
        private LayoutElement buttonsContainerLE;
        private GridLayoutGroup buttonsGrid;
        private Button closeButton;

        private readonly List<GameObject> activeButtons = new List<GameObject>();
        private readonly List<Button> floorButtonsList = new List<Button>();

        // Travel state
        private int currentElevatorId = 1;
        private int currentFloor = 0;
        private bool isTravelling;

        // Player input locks
        private PlayerMovement cachedPlayerMovement;
        private FirstPersonLook cachedFirstPersonLook;

        // Layout constants (Two-column clean MVP layout)
        private const float PanelWidth = 520f;
        private const float PanelHeight = 670f;
        private const float ButtonWidth = 220f;
        private const float ButtonHeight = 50f;
        private const float ButtonSpacingX = 14f;
        private const float ButtonSpacingY = 10f;
        private const float CloseButtonHeight = 48f;

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

        public static ElevatorUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            ElevatorUI existing = FindFirstObjectByType<ElevatorUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("ElevatorUI");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }
            return host.AddComponent<ElevatorUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                SafeDestroy(gameObject);
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

            HandleKeyboardInput();
        }

        /// <summary>
        /// Displays the floor selection modal for the given elevator and current floor.
        /// </summary>
        public void Show(int elevatorId, int currentFloorNumber)
        {
            if (isTravelling)
            {
                return;
            }

            currentElevatorId = elevatorId;
            currentFloor = currentFloorNumber;

            if (panelRoot == null)
            {
                BuildUI();
            }

            UpdateHeader();
            RebuildFloorButtons();

            isTravelling = false;
            SetStatusText(string.Empty);

            panelRoot.SetActive(true);
            IsOpen = true;

            // Lock FPS movement and look
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

            // Unlock and show cursor for the modal
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureEventSystem();
        }

        /// <summary>
        /// Closes the elevator floor modal and restores player movement, look, and locked cursor.
        /// Shared authoritative exit path for Escape close, UI close button, successful travel, and aborted/failed travel.
        /// </summary>
        public void CloseAndRestoreInput()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            IsOpen = false;
            isTravelling = false;

            // Re-resolve and restore PlayerMovement
            if (cachedPlayerMovement == null)
            {
                cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
            }
            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.enabled = true;
            }

            // Re-resolve and restore FirstPersonLook
            if (cachedFirstPersonLook == null)
            {
                cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
            }
            if (cachedFirstPersonLook != null)
            {
                cachedFirstPersonLook.SuppressEscapeThisFrame();
                cachedFirstPersonLook.enabled = true;
            }

            // Restore cursor for first-person gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Alias for CloseAndRestoreInput to maintain backwards compatibility.
        /// </summary>
        public void Hide()
        {
            CloseAndRestoreInput();
        }

        /// <summary>
        /// Selects a destination floor and starts elevator travel.
        /// </summary>
        public void SelectFloor(int targetFloor)
        {
            if (!IsOpen || isTravelling)
            {
                return;
            }

            if (targetFloor == currentFloor)
            {
                return;
            }

            if (targetFloor < FloorSceneLoader.MinFloorNumber || targetFloor > FloorSceneLoader.MaxFloorNumber)
            {
                return;
            }

            isTravelling = true;
            SetButtonsInteractable(false);
            SetStatusText("Travelling...");

            ElevatorTravelController.Instance.TravelToFloor(
                currentElevatorId,
                currentFloor,
                targetFloor,
                onComplete: () =>
                {
                    isTravelling = false;
                    CloseAndRestoreInput();
                },
                onError: _ =>
                {
                    isTravelling = false;
                    CloseAndRestoreInput();
                });
        }

        private void HandleKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (!isTravelling)
                {
                    CloseAndRestoreInput();
                }
                return;
            }

            if (isTravelling)
            {
                return;
            }

            // G -> Ground Floor (Floor 0)
            if (keyboard.gKey.wasPressedThisFrame)
            {
                if (currentFloor != 0)
                {
                    SelectFloor(0);
                }
                return;
            }

            // 1..9 -> Floors 1..9
            for (int i = 0; i < 9; i++)
            {
                int floor = i + 1;
                Key digitKey = DigitKeys[i];
                Key numpadKey = NumpadKeys[i];

                bool pressed = (keyboard[digitKey] != null && keyboard[digitKey].wasPressedThisFrame)
                            || (keyboard[numpadKey] != null && keyboard[numpadKey].wasPressedThisFrame);

                if (pressed)
                {
                    if (floor != currentFloor)
                    {
                        SelectFloor(floor);
                    }
                    return;
                }
            }

            // 0 -> Floor 10
            bool zeroPressed = (keyboard.digit0Key != null && keyboard.digit0Key.wasPressedThisFrame)
                            || (keyboard.numpad0Key != null && keyboard.numpad0Key.wasPressedThisFrame);

            if (zeroPressed)
            {
                if (currentFloor != 10)
                {
                    SelectFloor(10);
                }
            }
        }

        private void UpdateHeader()
        {
            if (titleLabel != null)
            {
                titleLabel.text = $"ELEVATOR {currentElevatorId}";
            }

            if (subtitleLabel != null)
            {
                string floorName = currentFloor == 0 ? "Ground Floor" : $"Floor {currentFloor}";
                subtitleLabel.text = $"Current: {floorName}\nSelect Destination";
            }
        }

        private void SetStatusText(string text)
        {
            if (statusLabel != null)
            {
                statusLabel.text = text;
                statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            for (int i = 0; i < floorButtonsList.Count; i++)
            {
                if (floorButtonsList[i] != null)
                {
                    floorButtonsList[i].interactable = interactable;
                }
            }

            if (closeButton != null)
            {
                closeButton.interactable = interactable;
            }
        }

        private void RebuildFloorButtons()
        {
            for (int i = 0; i < activeButtons.Count; i++)
            {
                SafeDestroy(activeButtons[i]);
            }
            activeButtons.Clear();
            floorButtonsList.Clear();

            FloorSceneLoader loader = FloorSceneLoader.Instance;

            int validFloorsCount = 0;
            for (int f = FloorSceneLoader.MinFloorNumber; f <= FloorSceneLoader.MaxFloorNumber; f++)
            {
                if (f != currentFloor)
                {
                    validFloorsCount++;
                }
            }

            int rowCount = Mathf.Max(1, Mathf.CeilToInt(validFloorsCount / 2f));
            if (buttonsGrid != null)
            {
                buttonsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                buttonsGrid.constraintCount = 2;
                buttonsGrid.startAxis = GridLayoutGroup.Axis.Vertical;
                buttonsGrid.cellSize = new Vector2(ButtonWidth, ButtonHeight);
                buttonsGrid.spacing = new Vector2(ButtonSpacingX, ButtonSpacingY);
            }

            float containerHeight = rowCount * ButtonHeight + Mathf.Max(0, rowCount - 1) * ButtonSpacingY;
            float containerWidth = ButtonWidth * 2f + ButtonSpacingX;

            if (buttonsContainerLE != null)
            {
                buttonsContainerLE.minWidth = containerWidth;
                buttonsContainerLE.preferredWidth = containerWidth;
                buttonsContainerLE.minHeight = containerHeight;
                buttonsContainerLE.preferredHeight = containerHeight;
                buttonsContainerLE.flexibleWidth = 0f;
                buttonsContainerLE.flexibleHeight = 0f;
            }

            if (buttonsContainer is RectTransform containerRt)
            {
                containerRt.sizeDelta = new Vector2(containerWidth, containerHeight);
            }

            // Render destinations in numeric order: Ground Floor (0), then Floors 1..10
            for (int f = FloorSceneLoader.MinFloorNumber; f <= FloorSceneLoader.MaxFloorNumber; f++)
            {
                if (f == currentFloor)
                {
                    continue; // Exclude current floor
                }

                int floorNumber = f;
                string label = GetFloorButtonLabel(floorNumber);
                bool canLoad = loader == null || loader.CanLoadFloorScene(floorNumber);

                GameObject btnGo = CreateFloorButton(label, floorNumber, canLoad);
                activeButtons.Add(btnGo);
            }
        }

        private static string GetFloorButtonLabel(int floor)
        {
            if (floor == 0)
            {
                return "Ground Floor [G]";
            }

            if (floor == 10)
            {
                return "Floor 10 [0]";
            }

            return $"Floor {floor} [{floor}]";
        }

        private GameObject CreateFloorButton(string label, int floorNumber, bool isAvailable)
        {
            GameObject btnGo = new GameObject("Button_Floor_" + floorNumber);
            btnGo.transform.SetParent(buttonsContainer, false);

            LayoutElement le = btnGo.AddComponent<LayoutElement>();
            le.minWidth = ButtonWidth;
            le.preferredWidth = ButtonWidth;
            le.minHeight = ButtonHeight;
            le.preferredHeight = ButtonHeight;

            Image btnImage = btnGo.AddComponent<Image>();
            btnImage.color = isAvailable ? buttonNormalColor : buttonDisabledColor;

            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImage;
            ApplyButtonColors(btn);
            btn.interactable = isAvailable;

            if (isAvailable)
            {
                btn.onClick.AddListener(() => SelectFloor(floorNumber));
            }

            floorButtonsList.Add(btn);

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(btnGo.transform, false);

            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = isAvailable ? label : $"{label} (Unavailable)";
            tmp.fontSize = 15f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = isAvailable ? buttonTextColor : new Color(0.6f, 0.6f, 0.6f, 0.6f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            return btnGo;
        }

        private void ApplyButtonColors(Button btn)
        {
            ColorBlock colors = btn.colors;
            colors.normalColor = buttonNormalColor;
            colors.highlightedColor = buttonHighlightedColor;
            colors.pressedColor = buttonPressedColor;
            colors.disabledColor = buttonDisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
        }

        // ── Procedural UI Construction ────────────────────────────────────

        private void BuildUI()
        {
            // Canvas
            GameObject canvasGo = new GameObject("ElevatorCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 215; // Above CanteenQueueUI (210) & DialogueUI (200), below AdvisorUI (220)

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel Root (fixed MVP size: 520 x 670 px)
            panelRoot = new GameObject("ElevatorPanel");
            panelRoot.transform.SetParent(canvasGo.transform, false);

            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelRect.anchoredPosition = Vector2.zero;

            panelRoot.AddComponent<Image>().color = panelBackground;

            float gridWidth = ButtonWidth * 2f + ButtonSpacingX;
            float gridHeight = 5 * ButtonHeight + 4 * ButtonSpacingY;
            float horizontalPadding = Mathf.Max(0f, (PanelWidth - gridWidth) * 0.5f);

            VerticalLayoutGroup panelVLG = panelRoot.AddComponent<VerticalLayoutGroup>();
            panelVLG.padding = new RectOffset(Mathf.RoundToInt(horizontalPadding), Mathf.RoundToInt(horizontalPadding), 36, 36);
            panelVLG.spacing = 20f;
            panelVLG.childAlignment = TextAnchor.MiddleCenter;
            panelVLG.childControlWidth = true;
            panelVLG.childControlHeight = true;
            panelVLG.childForceExpandWidth = true;
            panelVLG.childForceExpandHeight = false;

            // Header Container (Title + Subtitle)
            GameObject headerGo = new GameObject("Header");
            headerGo.transform.SetParent(panelRoot.transform, false);

            LayoutElement headerLE = headerGo.AddComponent<LayoutElement>();
            headerLE.minHeight = 76f;
            headerLE.preferredHeight = 76f;
            headerLE.flexibleHeight = 0f;
            headerLE.minWidth = gridWidth;
            headerLE.preferredWidth = gridWidth;
            headerLE.flexibleWidth = 1f;

            VerticalLayoutGroup headerVLG = headerGo.AddComponent<VerticalLayoutGroup>();
            headerVLG.padding = new RectOffset(0, 0, 0, 0);
            headerVLG.spacing = 4f;
            headerVLG.childAlignment = TextAnchor.UpperCenter;
            headerVLG.childControlWidth = true;
            headerVLG.childControlHeight = true;
            headerVLG.childForceExpandWidth = true;
            headerVLG.childForceExpandHeight = false;

            // Title
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerGo.transform, false);

            LayoutElement titleLE = titleGo.AddComponent<LayoutElement>();
            titleLE.minHeight = 28f;
            titleLE.preferredHeight = 28f;
            titleLE.flexibleHeight = 0f;

            titleLabel = titleGo.AddComponent<TextMeshProUGUI>();
            titleLabel.text = "ELEVATOR";
            titleLabel.fontSize = 22f;
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.color = headerColor;
            titleLabel.alignment = TextAlignmentOptions.Center;
            titleLabel.richText = false;

            // Subtitle
            GameObject subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(headerGo.transform, false);

            LayoutElement subLE = subGo.AddComponent<LayoutElement>();
            subLE.minHeight = 44f;
            subLE.preferredHeight = 44f;
            subLE.flexibleHeight = 0f;

            subtitleLabel = subGo.AddComponent<TextMeshProUGUI>();
            subtitleLabel.text = "Select Destination Floor";
            subtitleLabel.fontSize = 14f;
            subtitleLabel.lineSpacing = 4f;
            subtitleLabel.color = subtitleColor;
            subtitleLabel.alignment = TextAlignmentOptions.Center;
            subtitleLabel.richText = false;

            // Status label (e.g. "Travelling...")
            GameObject statusGo = new GameObject("StatusLabel");
            statusGo.transform.SetParent(panelRoot.transform, false);

            LayoutElement statusLE = statusGo.AddComponent<LayoutElement>();
            statusLE.minHeight = 24f;
            statusLE.preferredHeight = 24f;
            statusLE.flexibleHeight = 0f;

            statusLabel = statusGo.AddComponent<TextMeshProUGUI>();
            statusLabel.text = "";
            statusLabel.fontSize = 15f;
            statusLabel.fontStyle = FontStyles.Bold;
            statusLabel.color = new Color(1f, 0.78f, 0.28f, 1f);
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.richText = false;
            statusGo.SetActive(false);

            // Floor Buttons Container (Two-column vertical grid: 2 cols x 5 rows)
            GameObject containerGo = new GameObject("ButtonsContainer");
            containerGo.transform.SetParent(panelRoot.transform, false);

            RectTransform containerRt = containerGo.AddComponent<RectTransform>();
            containerRt.sizeDelta = new Vector2(gridWidth, gridHeight);

            buttonsContainerLE = containerGo.AddComponent<LayoutElement>();
            buttonsContainerLE.minWidth = gridWidth;
            buttonsContainerLE.preferredWidth = gridWidth;
            buttonsContainerLE.minHeight = gridHeight;
            buttonsContainerLE.preferredHeight = gridHeight;
            buttonsContainerLE.flexibleWidth = 0f;
            buttonsContainerLE.flexibleHeight = 0f;

            buttonsGrid = containerGo.AddComponent<GridLayoutGroup>();
            buttonsGrid.cellSize = new Vector2(ButtonWidth, ButtonHeight);
            buttonsGrid.spacing = new Vector2(ButtonSpacingX, ButtonSpacingY);
            buttonsGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            buttonsGrid.startAxis = GridLayoutGroup.Axis.Vertical;
            buttonsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            buttonsGrid.constraintCount = 2;
            buttonsGrid.childAlignment = TextAnchor.MiddleCenter;

            buttonsContainer = containerGo.transform;

            // Close Button
            GameObject closeBtnGo = new GameObject("CloseButton");
            closeBtnGo.transform.SetParent(panelRoot.transform, false);

            RectTransform closeRt = closeBtnGo.AddComponent<RectTransform>();
            closeRt.sizeDelta = new Vector2(gridWidth, CloseButtonHeight);

            LayoutElement closeLE = closeBtnGo.AddComponent<LayoutElement>();
            closeLE.minHeight = CloseButtonHeight;
            closeLE.preferredHeight = CloseButtonHeight;
            closeLE.flexibleHeight = 0f;
            closeLE.minWidth = gridWidth;
            closeLE.preferredWidth = gridWidth;
            closeLE.flexibleWidth = 1f;

            Image closeImg = closeBtnGo.AddComponent<Image>();
            closeImg.color = new Color(0.14f, 0.15f, 0.18f, 1f);

            closeButton = closeBtnGo.AddComponent<Button>();
            closeButton.targetGraphic = closeImg;
            ApplyButtonColors(closeButton);
            closeButton.onClick.AddListener(CloseAndRestoreInput);

            GameObject closeTextGo = new GameObject("CloseLabel");
            closeTextGo.transform.SetParent(closeBtnGo.transform, false);
            RectTransform ctRect = closeTextGo.AddComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = Vector2.zero;
            ctRect.offsetMax = Vector2.zero;

            TextMeshProUGUI closeTmp = closeTextGo.AddComponent<TextMeshProUGUI>();
            closeTmp.text = "[Esc] Close";
            closeTmp.fontSize = 15f;
            closeTmp.fontStyle = FontStyles.Bold;
            closeTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.richText = false;
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
                return;
            }

#pragma warning disable CS0618
            StandaloneInputModule legacy = existingES.GetComponent<StandaloneInputModule>();
#pragma warning restore CS0618
            if (legacy != null)
            {
                SafeDestroy(legacy);
            }

            if (existingES.GetComponent<InputSystemUIInputModule>() == null)
            {
                InputSystemUIInputModule module = existingES.gameObject.AddComponent<InputSystemUIInputModule>();
                if (InputSystem.actions != null)
                {
                    module.actionsAsset = InputSystem.actions;
                }
            }
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
