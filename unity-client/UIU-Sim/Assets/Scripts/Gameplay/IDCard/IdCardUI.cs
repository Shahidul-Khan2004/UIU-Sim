using TMPro;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.IDCard
{
    /// <summary>
    /// MVP view of the player's university ID card after admission.
    /// Data comes from <see cref="PlayerSaveState"/> (boolean issuance + profile fields).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IdCardUI : MonoBehaviour
    {
        public static IdCardUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private const int CanvasSortOrder = 250;

        private GameObject overlayRoot;
        private TextMeshProUGUI nameValue;
        private TextMeshProUGUI roleValue;
        private TextMeshProUGUI departmentValue;
        private TextMeshProUGUI universityIdValue;
        private TextMeshProUGUI statusLabel;

        public static IdCardUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            IdCardUI existing = FindFirstObjectByType<IdCardUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("IdCardUI");
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(host);
            }

            return host.AddComponent<IdCardUI>();
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
            }
        }

        public void Show()
        {
            if (overlayRoot == null)
            {
                BuildUI();
            }

            PopulateFromSaveState();
            overlayRoot.SetActive(true);
            IsOpen = true;
        }

        public void Hide()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            IsOpen = false;
        }

        private void PopulateFromSaveState()
        {
            PlayerSaveState saveState = PlayerSaveState.Instance != null
                ? PlayerSaveState.Instance
                : FindFirstObjectByType<PlayerSaveState>();

            if (saveState == null || !saveState.IsHydrated)
            {
                SetCardFields("—", "—", "—", "—");
                SetStatus("Loading ID card…", UiTheme.Grey);
                saveState ??= PlayerSaveState.EnsureExists();
                saveState.RefreshFromServer();
                return;
            }

            if (!saveState.HasSave || !saveState.IdCardIssued)
            {
                SetCardFields("—", "—", "—", "—");
                SetStatus("No ID card issued yet. Visit the receptionist.", UiTheme.Red);
                return;
            }

            SetCardFields(
                string.IsNullOrWhiteSpace(saveState.PlayerName) ? "—" : saveState.PlayerName,
                FormatRole(saveState.Role),
                string.IsNullOrWhiteSpace(saveState.Department) ? "—" : saveState.Department,
                string.IsNullOrWhiteSpace(saveState.UniversityId) ? "—" : saveState.UniversityId);
            SetStatus("ID Card", UiTheme.BrightOrange);
        }

        private static string FormatRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return "—";
            }

            if (role.Equals("STUDENT", System.StringComparison.OrdinalIgnoreCase))
            {
                return "Student";
            }

            if (role.Equals("FACULTY", System.StringComparison.OrdinalIgnoreCase))
            {
                return "Faculty";
            }

            return role;
        }

        private void SetCardFields(string name, string role, string department, string universityId)
        {
            if (nameValue != null) nameValue.text = name;
            if (roleValue != null) roleValue.text = role;
            if (departmentValue != null) departmentValue.text = department;
            if (universityIdValue != null) universityIdValue.text = universityId;
        }

        private void SetStatus(string message, Color color)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = message ?? string.Empty;
            statusLabel.color = color;
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject(
                "IdCardCanvas",
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

            overlayRoot = new GameObject("IdCardOverlay");
            overlayRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
            StretchFull(overlayRect);
            overlayRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            GameObject panel = new GameObject("IdCardPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(480f, 420f);
            panel.AddComponent<Image>().color = UiTheme.Black;

            // Orange accent bar at top
            GameObject accent = new GameObject("AccentBar");
            accent.transform.SetParent(panel.transform, false);
            RectTransform accentRect = accent.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(0f, 8f);
            accent.AddComponent<Image>().color = UiTheme.BrightOrange;

            statusLabel = CreateLabel(panel.transform, "Header", "ID Card", 26f, FontStyles.Bold, UiTheme.BrightOrange,
                new Vector2(0f, -28f), 36f);

            CreateLabel(panel.transform, "NameKey", "Name", 14f, FontStyles.Normal, UiTheme.Grey,
                new Vector2(0f, -78f), 22f);
            nameValue = CreateLabel(panel.transform, "NameValue", "—", 20f, FontStyles.Bold, UiTheme.White,
                new Vector2(0f, -104f), 28f);

            CreateLabel(panel.transform, "RoleKey", "Role", 14f, FontStyles.Normal, UiTheme.Grey,
                new Vector2(0f, -142f), 22f);
            roleValue = CreateLabel(panel.transform, "RoleValue", "—", 20f, FontStyles.Bold, UiTheme.White,
                new Vector2(0f, -168f), 28f);

            CreateLabel(panel.transform, "DeptKey", "Department", 14f, FontStyles.Normal, UiTheme.Grey,
                new Vector2(0f, -206f), 22f);
            departmentValue = CreateLabel(panel.transform, "DeptValue", "—", 20f, FontStyles.Bold, UiTheme.White,
                new Vector2(0f, -232f), 28f);

            CreateLabel(panel.transform, "IdKey", "University ID", 14f, FontStyles.Normal, UiTheme.Grey,
                new Vector2(0f, -270f), 22f);
            universityIdValue = CreateLabel(panel.transform, "IdValue", "—", 20f, FontStyles.Bold, UiTheme.White,
                new Vector2(0f, -296f), 28f);

            GameObject closeGo = new GameObject("Button_CloseIdCard");
            closeGo.transform.SetParent(panel.transform, false);
            RectTransform closeRect = closeGo.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 24f);
            closeRect.sizeDelta = new Vector2(160f, 40f);
            closeGo.AddComponent<Image>().color = UiTheme.BrightOrange;
            Button closeButton = closeGo.AddComponent<Button>();
            closeButton.onClick.AddListener(Hide);

            GameObject closeLabelGo = new GameObject("Label");
            closeLabelGo.transform.SetParent(closeGo.transform, false);
            RectTransform closeLabelRect = closeLabelGo.AddComponent<RectTransform>();
            StretchFull(closeLabelRect);
            TextMeshProUGUI closeLabel = closeLabelGo.AddComponent<TextMeshProUGUI>();
            closeLabel.text = "Close";
            closeLabel.fontSize = 18f;
            closeLabel.fontStyle = FontStyles.Bold;
            closeLabel.color = UiTheme.White;
            closeLabel.alignment = TextAlignmentOptions.Center;
            closeLabel.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles style,
            Color color,
            Vector2 anchoredPosition,
            float height)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-48f, height);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.richText = false;
            return label;
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
