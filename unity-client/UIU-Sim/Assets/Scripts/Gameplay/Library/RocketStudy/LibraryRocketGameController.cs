using System;
using System.Collections.Generic;
using TMPro;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UIU.Simulator.Gameplay.Library.RocketStudy
{
    /// <summary>Scene-local overlay. The library modal retains campus control ownership throughout.</summary>
    [DisallowMultipleComponent]
    public sealed class LibraryRocketGameController : MonoBehaviour
    {
        [SerializeField] private RocketStudySettings settings = new RocketStudySettings();
        private readonly Dictionary<int, RectTransform> obstacleViews = new Dictionary<int, RectTransform>();
        private readonly List<int> expiredViews = new List<int>();
        private RocketStudySimulation simulation;
        private RectTransform playArea, rocket, obstacleLayer;
        private TextMeshProUGUI timerLabel, scoreLabel, readyLabel;
        private Action<LibraryStudyMinigameResult> onFinished;
        private int openedFrame;
        private bool inputReleased;

        public RocketStudySimulation Simulation => simulation;

        public void Open(Action<LibraryStudyMinigameResult> finished)
        {
            if (simulation != null) return;
            onFinished = finished;
            simulation = new RocketStudySimulation(settings, Environment.TickCount);
            simulation.Ended += OnEnded;
            BuildUi();
            Render();
            openedFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (simulation == null || Time.frameCount <= openedFrame) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                simulation.Cancel();
                return;
            }

            bool held = (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                || (Mouse.current != null && Mouse.current.leftButton.isPressed)
                || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed);
            // The click/submit which opened the modal must be released before beginning a run.
            if (!inputReleased)
            {
                inputReleased = !held;
                return;
            }
            if (simulation.State == RocketStudySimulation.RunState.Ready)
            {
                if (held)
                {
                    simulation.Begin();
                    readyLabel.gameObject.SetActive(false);
                }
                return;
            }
            simulation.Advance(Time.unscaledDeltaTime, held);
            Render();
        }

        private void OnEnded(LibraryStudyMinigameResult result)
        {
            Render();
            readyLabel.gameObject.SetActive(true);
            readyLabel.text = result.Completed
                ? $"Score: {result.Score}/100\nSaving study result…"
                : "Returning to the library…";
            Action<LibraryStudyMinigameResult> callback = onFinished;
            onFinished = null;
            callback?.Invoke(result);
        }

        private void OnDisable()
        {
            simulation?.Cancel();
        }

        private void OnDestroy()
        {
            if (simulation != null) simulation.Ended -= OnEnded;
        }

        private void Render()
        {
            if (rocket == null) return;
            rocket.anchoredPosition = new Vector2(RocketStudySimulation.RocketX, simulation.RocketY);
            timerLabel.text = $"Time: {simulation.Elapsed:0.0} / {simulation.Duration:0.0}";
            scoreLabel.text = $"Study Score: {simulation.Score} / 100";
            expiredViews.Clear();
            foreach (int id in obstacleViews.Keys) expiredViews.Add(id);
            foreach (RocketStudySimulation.Obstacle obstacle in simulation.Obstacles)
            {
                if (!obstacleViews.TryGetValue(obstacle.Id, out RectTransform pair))
                {
                    pair = CreateRect(obstacleLayer, "Study Distractions", Vector2.zero, Vector2.zero);
                    CreateBlock(pair, "Bottom book stack", obstacle.Bottom(simulation.GapSize));
                    CreateBlock(pair, "Top book stack", obstacle.Top(simulation.GapSize));
                    obstacleViews.Add(obstacle.Id, pair);
                }
                expiredViews.Remove(obstacle.Id);
                pair.anchoredPosition = new Vector2(obstacle.X, 0f);
            }
            foreach (int id in expiredViews)
            {
                RectTransform view = obstacleViews[id];
                // Remove from the visible hierarchy immediately; Destroy is deferred in Play Mode.
                view.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(view.gameObject);
                else DestroyImmediate(view.gameObject);
                obstacleViews.Remove(id);
            }
        }

        private void BuildUi()
        {
            RectTransform canvasRect = CreateRect(transform, "RocketStudyCanvas", Vector2.zero, Vector2.zero);
            Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 245;
            CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            // Reuse the campus EventSystem. The backdrop prevents click-through to underlying UI.
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            RectTransform backdrop = CreateRect(canvasRect, "Backdrop", Vector2.zero, Vector2.zero);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.03f, 0.04f, 1f);

            Label(canvasRect, "Title", "ROCKET STUDY", new Vector2(0f, 344f), new Vector2(1000f, 52f), 36f, UiTheme.BrightOrange);
            Label(canvasRect, "Subtitle", $"Stay focused for {simulation.Duration:0.#} seconds.",
                new Vector2(0f, 299f), new Vector2(1000f, 32f), 21f, UiTheme.Grey);
            playArea = CreateRect(canvasRect, "PlayArea", new Vector2(0f, -12f),
                new Vector2(RocketStudySimulation.Width, RocketStudySimulation.Height));
            playArea.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.08f, 0.10f, 1f);
            playArea.gameObject.AddComponent<RectMask2D>();
            obstacleLayer = CreateRect(playArea, "Obstacles", Vector2.zero, playArea.sizeDelta);
            rocket = CreateRect(playArea, "Rocket", Vector2.zero, simulation.RocketSize);
            rocket.gameObject.AddComponent<Image>().color = UiTheme.BrightOrange;
            RectTransform window = CreateRect(rocket, "Window", new Vector2(simulation.RocketSize.x * 0.2f, 0f),
                simulation.RocketSize * new Vector2(0.2f, 0.45f));
            window.gameObject.AddComponent<Image>().color = UiTheme.White;

            RectTransform hud = CreateRect(playArea, "Readout", new Vector2(0f, 251f), new Vector2(1000f, 58f));
            hud.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.03f, 0.04f, 0.90f);
            timerLabel = Label(hud, "Timer", "", new Vector2(-240f, 0f), new Vector2(440f, 44f), 22f, UiTheme.White);
            scoreLabel = Label(hud, "Score", "", new Vector2(240f, 0f), new Vector2(440f, 44f), 22f, UiTheme.BrightOrange);
            readyLabel = Label(playArea, "Status", "Press SPACE or CLICK to begin.\nHold to rise • Release to fall\nAvoid the study distractions.",
                new Vector2(50f, 20f), new Vector2(700f, 150f), 26f, UiTheme.White);
            Label(canvasRect, "Controls", "SPACE / CLICK / TOUCH — THRUST     •     ESC — CANCEL",
                new Vector2(0f, -334f), new Vector2(1100f, 46f), 21f, UiTheme.Grey);
        }

        private static void CreateBlock(Transform parent, string name, Rect bounds)
        {
            RectTransform block = CreateRect(parent, name, new Vector2(0f, bounds.center.y), bounds.size);
            block.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.30f, 0.34f, 1f);
            // A few built-in rectangles suggest stacked books without art or materials.
            for (float y = 32f; y < bounds.height; y += 32f)
            {
                RectTransform line = CreateRect(block, "Book edge", new Vector2(0f, -bounds.height / 2f + y),
                    new Vector2(bounds.width - 8f, 2f));
                line.gameObject.AddComponent<Image>().color = new Color(0.40f, 0.42f, 0.46f, 1f);
            }
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position,
            Vector2 size, float fontSize, Color color)
        {
            TextMeshProUGUI label = CreateRect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }
    }
}
