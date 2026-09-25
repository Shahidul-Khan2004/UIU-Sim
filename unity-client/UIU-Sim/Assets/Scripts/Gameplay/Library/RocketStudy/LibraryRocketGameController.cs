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
        public const string BrainSpritePath = "Assets/Art/Minigames/RocketStudy/brain.png";
        private const float BrainVisualSize = 88f;
        [SerializeField] private RocketStudySettings settings = new RocketStudySettings();
        [SerializeField] private Sprite brainSprite;
        private readonly Dictionary<int, RectTransform> obstacleViews = new Dictionary<int, RectTransform>();
        private readonly List<int> expiredViews = new List<int>();
        private RocketStudySimulation simulation;
        private RectTransform playArea, rocket, obstacleLayer;
        private TextMeshProUGUI timerLabel, scoreLabel, readyLabel;
        private Action<LibraryStudyMinigameResult> onFinished;
        private int openedFrame;
        private bool inputReleased;
        private static readonly Color[] BookColors =
        {
            new Color(0.76f, 0.35f, 0.16f),
            new Color(0.22f, 0.48f, 0.47f),
            new Color(0.48f, 0.34f, 0.46f),
            new Color(0.33f, 0.43f, 0.59f)
        };

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
                    CreateBookStack(pair, "Bottom book stack", obstacle.Bottom(simulation.GapSize), false, obstacle.Id);
                    CreateBookStack(pair, "Top book stack", obstacle.Top(simulation.GapSize), true, obstacle.Id);
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
                new Vector2(RocketStudySimulation.Width,
                    RocketStudySimulation.PlayAreaTop - RocketStudySimulation.PlayAreaBottom));
            playArea.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.08f, 0.10f, 1f);
            playArea.gameObject.AddComponent<RectMask2D>();
            obstacleLayer = CreateRect(playArea, "Obstacles", Vector2.zero, playArea.sizeDelta);
            rocket = CreateRect(playArea, "Rocket", Vector2.zero, simulation.RocketSize);
            CreateBrainVisual(rocket);

            // Keep the readout outside the simulation area: the old overlay obscured its top 58 units.
            float readoutY = playArea.anchoredPosition.y + RocketStudySimulation.PlayAreaBottom - 8f - 21f;
            RectTransform hud = CreateRect(canvasRect, "Readout", new Vector2(0f, readoutY),
                new Vector2(RocketStudySimulation.Width, 42f));
            hud.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.03f, 0.04f, 0.90f);
            timerLabel = Label(hud, "Timer", "", new Vector2(-240f, 0f), new Vector2(440f, 44f), 22f, UiTheme.White);
            scoreLabel = Label(hud, "Score", "", new Vector2(240f, 0f), new Vector2(440f, 44f), 22f, UiTheme.BrightOrange);
            readyLabel = Label(playArea, "Status", "Press SPACE or CLICK to begin.\nHold to rise • Release to fall\nAvoid the study distractions.",
                new Vector2(50f, 20f), new Vector2(700f, 150f), 26f, UiTheme.White);
            Label(canvasRect, "Controls", "SPACE / CLICK / TOUCH — THRUST     •     ESC — CANCEL",
                new Vector2(0f, readoutY - 21f - 8f - 16f), new Vector2(1100f, 32f), 21f, UiTheme.Grey);
        }

        private void CreateBrainVisual(RectTransform playerRoot)
        {
            Sprite sprite = brainSprite;
#if UNITY_EDITOR
            if (sprite == null)
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BrainSpritePath);
#endif
            if (sprite == null)
            {
                Debug.LogWarning("[RocketStudy] Brain sprite is not assigned.");
                return;
            }

            RectTransform visual = CreateRect(playerRoot, "Brain", Vector2.zero,
                new Vector2(BrainVisualSize, BrainVisualSize));
            Image image = visual.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void CreateBookStack(Transform parent, string name, Rect bounds, bool top, int id)
        {
            // X belongs to the moving pair; Y is already in playfield coordinates. No visual offset.
            RectTransform stack = CreateRect(parent, name, new Vector2(0f, bounds.center.y), bounds.size);
            int count = Mathf.CeilToInt(bounds.height / 24f);
            if (count == 0) return;
            // A separate seed keeps decorative variation out of the gameplay random sequence.
            var random = new System.Random(unchecked(id * 397) ^ (top ? 7919 : 104729));
            var thicknesses = new int[count];
            int total = 0;
            for (int i = 0; i < count; i++) total += thicknesses[i] = random.Next(18, 31);
            int consumed = 0;
            for (int i = 0; i < count; i++)
            {
                float start = (float)consumed / total;
                consumed += thicknesses[i];
                float end = (float)consumed / total;
                // Fill the full height, including the last book, with no thin leftover strip or overhang.
                RectTransform book = CreateRect(stack, "Book", Vector2.zero, Vector2.zero);
                book.anchorMin = new Vector2(0f, top ? 1f - end : start);
                book.anchorMax = new Vector2(1f, top ? 1f - start : end);
                // At most two units of empty space per side; gap-facing and boundary books span the envelope.
                bool fullWidth = i == 0 || i == count - 1;
                book.offsetMin = new Vector2(fullWidth ? 0f : random.Next(0, 3), 0f);
                book.offsetMax = new Vector2(fullWidth ? 0f : -random.Next(0, 3), 0f);
                book.gameObject.AddComponent<Image>().color = BookColors[random.Next(BookColors.Length)];

                RectTransform pages = CreateRect(book, "Pages", Vector2.zero, Vector2.zero);
                pages.anchorMin = new Vector2(0f, 0.22f);
                pages.anchorMax = new Vector2(1f, 0.78f);
                pages.offsetMin = new Vector2(5f, 0f);
                pages.offsetMax = new Vector2(-5f, 0f);
                pages.gameObject.AddComponent<Image>().color = new Color(0.82f, 0.79f, 0.68f);
                RectTransform spine = CreateRect(book, "Spine", Vector2.zero, Vector2.zero);
                float side = i % 2 == 0 ? 0.08f : 0.86f;
                spine.anchorMin = new Vector2(side, 0.12f);
                spine.anchorMax = new Vector2(side + 0.06f, 0.88f);
                spine.offsetMin = spine.offsetMax = Vector2.zero;
                spine.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.19f, 0.8f);
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
