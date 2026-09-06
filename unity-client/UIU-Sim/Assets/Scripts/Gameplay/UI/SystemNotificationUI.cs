using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.UI
{
    /// <summary>
    /// Minimal, reusable screen-space notification component for system alerts:
    /// sync errors, Advisor backend errors, TTS failures, attendance sync issues.
    /// Built dynamically at runtime — no prefab or scene setup required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SystemNotificationUI : MonoBehaviour
    {
        private static SystemNotificationUI instance;

        public static SystemNotificationUI Instance
        {
            get
            {
                if (instance == null)
                {
                    EnsureExists();
                }
                return instance;
            }
        }

        [Header("Appearance")]
        [SerializeField] private float fontSize = 20f;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color backgroundColor = new Color(0.12f, 0.05f, 0.05f, 0.92f);
        [SerializeField] private Color accentBarColor = new Color(0.95f, 0.3f, 0.3f, 1f);

        [Header("Timing")]
        [SerializeField] private float defaultDuration = 3.5f;
        [SerializeField] private float fadeDuration = 0.35f;

        private GameObject rootPanel;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI messageLabel;
        private Coroutine activeRoutine;

        public static SystemNotificationUI EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            SystemNotificationUI existing = FindFirstObjectByType<SystemNotificationUI>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            GameObject go = new GameObject("SystemNotificationUI");
            instance = go.AddComponent<SystemNotificationUI>();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
            return instance;
        }

        public static void Show(string message, float duration = 0f)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            EnsureExists().DisplayMessage(message, duration);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
                return;
            }

            instance = this;
            BuildUI();
            HideImmediate();
        }

        public void DisplayMessage(string message, float duration = 0f)
        {
            if (messageLabel != null)
            {
                messageLabel.text = message;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            float holdTime = duration > 0f ? duration : defaultDuration;
            activeRoutine = StartCoroutine(ShowRoutine(message, holdTime));
        }

        private IEnumerator ShowRoutine(string message, float holdDuration)
        {
            if (messageLabel != null)
            {
                messageLabel.text = message;
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }

            // Fade in
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                }
                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            yield return new WaitForSecondsRealtime(holdDuration);

            // Fade out
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
                }
                yield return null;
            }

            HideImmediate();
            activeRoutine = null;
        }

        private void HideImmediate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
        }

        private void BuildUI()
        {
            // Canvas
            GameObject canvasGo = new GameObject("NotificationCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250; // Above DialogueUI (200) and InteractionUI (100)

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel Root (Top-Center)
            rootPanel = new GameObject("NotificationPanel");
            rootPanel.transform.SetParent(canvasGo.transform, false);

            RectTransform panelRect = rootPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -40f);
            panelRect.sizeDelta = new Vector2(700f, 64f);

            Image panelBg = rootPanel.AddComponent<Image>();
            panelBg.color = backgroundColor;
            panelBg.raycastTarget = false;

            canvasGroup = rootPanel.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Accent Bar on left edge
            GameObject accentGo = new GameObject("AccentBar");
            accentGo.transform.SetParent(rootPanel.transform, false);
            RectTransform accentRect = accentGo.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.sizeDelta = new Vector2(6f, 0f);
            accentRect.anchoredPosition = Vector2.zero;

            Image accentImg = accentGo.AddComponent<Image>();
            accentImg.color = accentBarColor;
            accentImg.raycastTarget = false;

            // Message Text
            GameObject textGo = new GameObject("NotificationText");
            textGo.transform.SetParent(rootPanel.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(24f, 8f);
            textRect.offsetMax = new Vector2(-24f, -8f);

            messageLabel = textGo.AddComponent<TextMeshProUGUI>();
            messageLabel.fontSize = fontSize;
            messageLabel.color = textColor;
            messageLabel.fontStyle = FontStyles.Bold;
            messageLabel.alignment = TextAlignmentOptions.Center;
            messageLabel.textWrappingMode = TextWrappingModes.Normal;
            messageLabel.raycastTarget = false;
        }
    }
}
