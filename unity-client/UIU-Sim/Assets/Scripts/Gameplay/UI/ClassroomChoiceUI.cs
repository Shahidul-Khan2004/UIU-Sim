using System;
using TMPro;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.UI
{
    /// <summary>
    /// Pre-lecture classroom choice modal: Attend / Punch ID and Leave / Back.
    /// Built at runtime — no prefab required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClassroomChoiceUI : MonoBehaviour
    {
        public static ClassroomChoiceUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        private GameObject panelRoot;
        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI roomLabel;
        private Action onAttend;
        private Action onProxy;
        private Action onBack;
        private int openedFrame = -1;
        private bool isArmed;

        public static ClassroomChoiceUI EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            ClassroomChoiceUI existing = FindFirstObjectByType<ClassroomChoiceUI>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject host = new GameObject("ClassroomChoiceUI");
            return host.AddComponent<ClassroomChoiceUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildUi();
            Hide();
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

            if (!isArmed)
            {
                if (Time.frameCount > openedFrame)
                {
                    isArmed = true;
                }

                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                InvokeBack();
            }
        }

        public void Show(string title, string roomLine, Action onAttendClass, Action onPunchId, Action onBackChoice)
        {
            EnsureEventSystem();
            onAttend = onAttendClass;
            onProxy = onPunchId;
            onBack = onBackChoice;
            titleLabel.text = title;
            roomLabel.text = roomLine;
            panelRoot.SetActive(true);
            IsOpen = true;
            openedFrame = Time.frameCount;
            isArmed = false;
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            IsOpen = false;
            onAttend = null;
            onProxy = null;
            onBack = null;
        }

        private void InvokeAttend()
        {
            Action callback = onAttend;
            Hide();
            callback?.Invoke();
        }

        private void InvokeProxy()
        {
            Action callback = onProxy;
            Hide();
            callback?.Invoke();
        }

        private void InvokeBack()
        {
            Action callback = onBack;
            Hide();
            callback?.Invoke();
        }

        private void BuildUi()
        {
            GameObject canvasGo = new GameObject("ClassroomChoiceCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 215;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            panelRoot = new GameObject("ChoicePanel");
            panelRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 360f);

            Image bg = panelRoot.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.94f);

            titleLabel = CreateLabel(panelRoot.transform, "Title", "INTRODUCTION TO COMPUTER SCIENCE", 26f, UiTheme.BrightOrange, new Vector2(0f, 120f));
            roomLabel = CreateLabel(panelRoot.transform, "Room", "Room: —", 18f, UiTheme.White, new Vector2(0f, 70f));

            CreateButton(panelRoot.transform, "AttendButton", "ATTEND CLASS", new Vector2(0f, 10f), InvokeAttend);
            CreateButton(panelRoot.transform, "ProxyButton", "PUNCH ID AND LEAVE", new Vector2(0f, -55f), InvokeProxy);
            CreateButton(panelRoot.transform, "BackButton", "BACK", new Vector2(0f, -120f), InvokeBack);
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float size,
            Color color,
            Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(520f, 40f);
            rect.anchoredPosition = anchoredPos;
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            return label;
        }

        private static void CreateButton(Transform parent, string name, string label, Vector2 pos, Action onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 46f);
            rect.anchoredPosition = pos;
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
            colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
