using System.Collections.Generic;
using TMPro;
using UIU.Simulator.Building.Generation;
using UIU.Simulator.Gameplay.Admission;
using UIU.Simulator.Gameplay.Advisor;
using UIU.Simulator.Gameplay.Assessment;
using UIU.Simulator.Gameplay.Elevator;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Gameplay.IDCard;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UIU.Simulator.UI
{
    /// <summary>
    /// Floor plan HUD: a bottom-right corner map during gameplay, plus a fullscreen plan toggled with M.
    /// The floor comes from <see cref="FloorSceneLoader.CurrentFloorNumber"/>; floors above the third reuse
    /// the third-floor plan. The fullscreen map soft-pauses the local player only (no Time.timeScale change).
    /// Escape is routed here by <see cref="GameMenuManager"/>, which reads it earlier in the frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorMapHUD : MonoBehaviour
    {
        public static FloorMapHUD Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        public const string MapResourceFolder = "Maps";

        private const int CanvasSortOrder = 235;
        private const float PanelWidth = 1200f;
        private const float PanelHeight = 920f;

        // Above StatsHUD / FacultyHUD (50), below InteractionUI (100).
        private const int MiniMapSortOrder = 60;
        private const float MiniMapWidth = 320f;
        private const float MiniMapHeight = 340f;
        private const float GameplayLookupInterval = 0.5f;

        [Header("Appearance")]
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color panelColor = UiTheme.Black;

        [Header("Corner Map")]
        [SerializeField] private Color miniMapBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] private Vector2 miniMapScreenOffset = new Vector2(-24f, 24f);

        private GameObject overlayRoot;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI statusLabel;
        private RawImage mapImage;
        private AspectRatioFitter mapAspect;

        private GameObject miniMapRoot;
        private TextMeshProUGUI miniMapTitle;
        private RawImage miniMapImage;
        private AspectRatioFitter miniMapAspect;
        private int miniMapFloor = int.MinValue;

        private FloorSceneLoader cachedLoader;
        private PlayerMovement cachedPlayer;
        private float nextGameplayLookupTime;

        private readonly List<Behaviour> capturedControls = new List<Behaviour>();
        private readonly List<bool> capturedEnabled = new List<bool>();
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        public static FloorMapHUD EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            FloorMapHUD existing = FindFirstObjectByType<FloorMapHUD>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("FloorMapHUD");
            return host.AddComponent<FloorMapHUD>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBeforeFirstScene()
        {
            EnsureExists();
        }

        /// <summary>Closes whichever map instance is open. Used by GameMenuManager's Escape routing.</summary>
        public static void CloseActive()
        {
            FloorMapHUD map = Instance != null ? Instance : FindFirstObjectByType<FloorMapHUD>();
            if (map != null)
            {
                map.Close();
                return;
            }

            IsOpen = false;
        }

        /// <summary>
        /// Resource name (under Resources/Maps) of the plan for a floor.
        /// 0 → groundFloor, 1 → firstFloor, 2 → secondFloor, 3 and above → thirdFloor.
        /// </summary>
        public static string ResolveMapResourceName(int floorNumber)
        {
            if (floorNumber <= 0)
            {
                return "groundFloor";
            }

            switch (floorNumber)
            {
                case 1:
                    return "firstFloor";
                case 2:
                    return "secondFloor";
                default:
                    return "thirdFloor";
            }
        }

        public static string FormatFloorTitle(int floorNumber)
        {
            return floorNumber <= 0 ? "GROUND FLOOR" : $"FLOOR {floorNumber}";
        }

        /// <summary>True while any modal that owns player input (or the game menu) is showing.</summary>
        public static bool IsBlockingUiOpen()
        {
            return GameMenuManager.IsOpen
                || AcademicModal.BlocksGameplay
                || LibraryStudyUI.BlocksGameplay
                || LibraryStudyUI.IsOpen
                || AdmissionUI.IsOpen
                || AdvisorUI.IsOpen
                || ElevatorUI.IsOpen
                || IdCardUI.IsOpen
                || DailySummaryUI.IsOpen
                || DialogueUI.IsOpen
                || CanteenQueueUI.IsOpen
                || ClassroomChoiceUI.IsOpen
                || ClassroomLectureUI.IsOpen
                || ClassRoutineUI.IsOpen
                || FacultyPortalUI.IsOpen
                || FacultyClassroomChoiceUI.IsOpen
                || FacultyLectureUI.IsOpen
                || FacultyCoursesUI.IsOpen
                || FacultyMaterialsUI.IsOpen
                || FacultyStudentListUI.IsOpen;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                SafeDestroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            BuildUI();
            HideImmediate();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
            {
                if (IsOpen)
                {
                    Close();
                }

                Instance = null;
                IsOpen = false;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                Toggle();
            }

            RefreshMiniMap();
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            TryOpen();
        }

        /// <summary>
        /// Opens the map for the current floor. Refuses outside gameplay (no floor loader or player)
        /// and while another modal owns input.
        /// </summary>
        public bool TryOpen()
        {
            if (IsOpen || IsBlockingUiOpen())
            {
                return false;
            }

            FloorSceneLoader loader = FloorSceneLoader.Instance;
            if (loader == null || FindFirstObjectByType<PlayerMovement>() == null)
            {
                return false;
            }

            if (overlayRoot == null)
            {
                BuildUI();
            }

            ShowFloor(loader.CurrentFloorNumber);
            CaptureAndDisablePlayerControls();

            overlayRoot.SetActive(true);
            miniMapRoot.SetActive(false);
            IsOpen = true;
            return true;
        }

        /// <summary>
        /// Shows the corner map only during gameplay (floor loader and player present) while the fullscreen
        /// map is closed, and swaps its plan when the current floor changes.
        /// </summary>
        public void RefreshMiniMap()
        {
            if (miniMapRoot == null)
            {
                BuildUI();
            }

            bool inGameplay = TryResolveGameplay(out FloorSceneLoader loader);
            bool show = inGameplay && !IsOpen;
            if (miniMapRoot.activeSelf != show)
            {
                miniMapRoot.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            int floorNumber = loader.CurrentFloorNumber;
            if (floorNumber == miniMapFloor)
            {
                return;
            }

            miniMapFloor = floorNumber;
            miniMapTitle.text = FormatFloorTitle(floorNumber);
            ApplyMapTexture(miniMapImage, miniMapAspect, LoadMapTexture(floorNumber));
        }

        public void Close(bool restoreGameplayControls = true)
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            bool wasOpen = IsOpen;
            IsOpen = false;

            if (wasOpen && restoreGameplayControls)
            {
                RestorePlayerControls();
            }

            capturedControls.Clear();
            capturedEnabled.Clear();
        }

        /// <summary>EditMode test seam: texture currently shown in the map panel.</summary>
        public Texture DisplayedMapForTesting => mapImage != null ? mapImage.texture : null;

        /// <summary>EditMode test seam: floor title currently shown in the map panel.</summary>
        public string DisplayedTitleForTesting => titleLabel != null ? titleLabel.text : null;

        /// <summary>EditMode test seam: whether the bottom-right corner map is showing.</summary>
        public bool IsMiniMapVisibleForTesting => miniMapRoot != null && miniMapRoot.activeSelf;

        /// <summary>EditMode test seam: texture currently shown in the corner map.</summary>
        public Texture MiniMapTextureForTesting => miniMapImage != null ? miniMapImage.texture : null;

        /// <summary>EditMode test seam: floor title currently shown in the corner map.</summary>
        public string MiniMapTitleForTesting => miniMapTitle != null ? miniMapTitle.text : null;

        private void ShowFloor(int floorNumber)
        {
            titleLabel.text = FormatFloorTitle(floorNumber);

            Texture2D texture = LoadMapTexture(floorNumber);
            ApplyMapTexture(mapImage, mapAspect, texture);
            statusLabel.gameObject.SetActive(texture == null);
        }

        private Texture2D LoadMapTexture(int floorNumber)
        {
            string resourceName = ResolveMapResourceName(floorNumber);
            Texture2D texture = Resources.Load<Texture2D>(MapResourceFolder + "/" + resourceName);
            if (texture == null)
            {
                Debug.LogWarning($"[FloorMapHUD] Map texture 'Resources/{MapResourceFolder}/{resourceName}' not found.", this);
            }

            return texture;
        }

        private static void ApplyMapTexture(RawImage image, AspectRatioFitter aspect, Texture2D texture)
        {
            image.texture = texture;
            image.enabled = texture != null;
            if (texture != null)
            {
                aspect.aspectRatio = (float)texture.width / texture.height;
            }
        }

        private bool TryResolveGameplay(out FloorSceneLoader loader)
        {
            // Outside gameplay both lookups miss every time; throttle them so auth scenes don't search each frame.
            if ((cachedLoader == null || cachedPlayer == null)
                && (!Application.isPlaying || Time.unscaledTime >= nextGameplayLookupTime))
            {
                nextGameplayLookupTime = Time.unscaledTime + GameplayLookupInterval;
                if (cachedLoader == null)
                {
                    cachedLoader = FloorSceneLoader.Instance;
                }

                if (cachedPlayer == null)
                {
                    cachedPlayer = FindFirstObjectByType<PlayerMovement>();
                }
            }

            loader = cachedLoader;
            return cachedLoader != null && cachedPlayer != null;
        }

        private void CaptureAndDisablePlayerControls()
        {
            capturedControls.Clear();
            capturedEnabled.Clear();

            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;

            Capture(FindFirstObjectByType<PlayerMovement>());
            Capture(FindFirstObjectByType<FirstPersonLook>());
            Capture(FindFirstObjectByType<InteractionController>());
            Capture(FindFirstObjectByType<CameraFollow>());

            // Disabling look/follow frees the cursor; the map has nothing to click, so keep it hidden.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Capture(Behaviour control)
        {
            if (control == null)
            {
                return;
            }

            capturedControls.Add(control);
            capturedEnabled.Add(control.enabled);
            control.enabled = false;
        }

        private void RestorePlayerControls()
        {
            for (int i = 0; i < capturedControls.Count; i++)
            {
                Behaviour control = capturedControls[i];
                if (control == null)
                {
                    continue;
                }

                if (control is FirstPersonLook look)
                {
                    look.SuppressEscapeThisFrame();
                }

                control.enabled = capturedEnabled[i];
            }

            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                return;
            }

            // The captured player belonged to the unloaded scene; nothing to restore.
            Close(restoreGameplayControls: false);
            miniMapFloor = int.MinValue;
            nextGameplayLookupTime = 0f;
        }

        private void HideImmediate()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            if (miniMapRoot != null)
            {
                miniMapRoot.SetActive(false);
            }

            IsOpen = false;
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject("FloorMapCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            overlayRoot = new GameObject("FloorMapOverlay");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            StretchFull(overlayRoot.AddComponent<RectTransform>());
            Image overlay = overlayRoot.AddComponent<Image>();
            overlay.color = overlayColor;
            overlay.raycastTarget = false;

            GameObject panel = new GameObject("FloorMapPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = panelColor;
            panelImage.raycastTarget = false;

            titleLabel = CreateLabel(panel.transform, "FloorMapTitle", FormatFloorTitle(0), 28f, FontStyles.Bold, UiTheme.BrightOrange);
            RectTransform titleRect = titleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);
            titleRect.sizeDelta = new Vector2(-64f, 40f);

            GameObject mapArea = new GameObject("FloorMapArea");
            mapArea.transform.SetParent(panel.transform, false);
            RectTransform mapAreaRect = mapArea.AddComponent<RectTransform>();
            mapAreaRect.anchorMin = Vector2.zero;
            mapAreaRect.anchorMax = Vector2.one;
            mapAreaRect.offsetMin = new Vector2(32f, 64f);
            mapAreaRect.offsetMax = new Vector2(-32f, -84f);

            GameObject mapGo = new GameObject("FloorMapImage");
            mapGo.transform.SetParent(mapArea.transform, false);
            mapGo.AddComponent<RectTransform>();
            mapImage = mapGo.AddComponent<RawImage>();
            mapImage.raycastTarget = false;
            mapAspect = mapGo.AddComponent<AspectRatioFitter>();
            mapAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            mapAspect.aspectRatio = 1f;

            statusLabel = CreateLabel(mapArea.transform, "FloorMapStatus", "Map unavailable for this floor.", 20f, FontStyles.Bold, UiTheme.Red);
            StretchFull(statusLabel.rectTransform);
            statusLabel.gameObject.SetActive(false);

            TextMeshProUGUI hint = CreateLabel(panel.transform, "FloorMapHint", "Press M or Esc to close", 14f, FontStyles.Normal, UiTheme.Grey);
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 22f);
            hintRect.sizeDelta = new Vector2(-64f, 24f);

            BuildMiniMap();
        }

        private void BuildMiniMap()
        {
            GameObject canvasGo = new GameObject("FloorMiniMapCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = MiniMapSortOrder;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            miniMapRoot = new GameObject("FloorMiniMapPanel");
            miniMapRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = miniMapRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = miniMapScreenOffset;
            panelRect.sizeDelta = new Vector2(MiniMapWidth, MiniMapHeight);
            Image background = miniMapRoot.AddComponent<Image>();
            background.color = miniMapBackgroundColor;
            background.raycastTarget = false;

            miniMapTitle = CreateLabel(miniMapRoot.transform, "FloorMiniMapTitle", FormatFloorTitle(0), 14f, FontStyles.Bold, UiTheme.BrightOrange);
            miniMapTitle.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform titleRect = miniMapTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -8f);
            titleRect.sizeDelta = new Vector2(-24f, 22f);

            TextMeshProUGUI hint = CreateLabel(miniMapRoot.transform, "FloorMiniMapHint", "[M] Full map", 12f, FontStyles.Bold, UiTheme.Grey);
            hint.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -8f);
            hintRect.sizeDelta = new Vector2(-24f, 22f);

            GameObject mapArea = new GameObject("FloorMiniMapArea");
            mapArea.transform.SetParent(miniMapRoot.transform, false);
            RectTransform mapAreaRect = mapArea.AddComponent<RectTransform>();
            mapAreaRect.anchorMin = Vector2.zero;
            mapAreaRect.anchorMax = Vector2.one;
            mapAreaRect.offsetMin = new Vector2(12f, 12f);
            mapAreaRect.offsetMax = new Vector2(-12f, -38f);

            GameObject mapGo = new GameObject("FloorMiniMapImage");
            mapGo.transform.SetParent(mapArea.transform, false);
            mapGo.AddComponent<RectTransform>();
            miniMapImage = mapGo.AddComponent<RawImage>();
            miniMapImage.raycastTarget = false;
            miniMapAspect = mapGo.AddComponent<AspectRatioFitter>();
            miniMapAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            miniMapAspect.aspectRatio = 1f;

            miniMapRoot.SetActive(false);
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles style,
            Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SafeDestroy(Object obj)
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
