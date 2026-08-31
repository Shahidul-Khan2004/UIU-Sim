using UnityEngine;
using UnityEngine.InputSystem;

namespace UIU.Simulator.Core
{
    /// <summary>
    /// Persistent application-level manager. Owns session-wide systems that must survive
    /// scene changes (Bootstrap → Login → Main, plus additive floor loads).
    ///
    /// This is the only place that should request the application to quit. Other scripts
    /// should call <see cref="Quit"/> instead of using Application.Quit directly.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        private const string SystemMapName = "System";
        private const string ExitActionName = "Exit";

        public static GameManager Instance { get; private set; }

        [Header("Input")]
        [Tooltip("Project Input Action asset. Leave empty to use the Input System project-wide asset.")]
        [SerializeField] private InputActionAsset inputActions;

        private InputActionMap systemMap;
        private InputAction exitAction;
        private InputAction fallbackExitAction;
        private bool isQuitting;

        /// <summary>
        /// Creates the manager if none exists. Safe to call from any scene.
        /// </summary>
        public static GameManager EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameManager existing = FindFirstObjectByType<GameManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject root = new GameObject("GameManager");
            return root.AddComponent<GameManager>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBeforeFirstScene()
        {
            EnsureExists();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            BindExitAction();
        }

        private void OnDisable()
        {
            UnbindExitAction();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Stops Play Mode in the Editor; closes the application in a player build.
        /// </summary>
        public void Quit()
        {
            if (isQuitting)
            {
                return;
            }

            isQuitting = true;
            Debug.Log("[GameManager] Exit requested.");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private void BindExitAction()
        {
            InputActionAsset asset = inputActions != null ? inputActions : InputSystem.actions;
            if (asset != null)
            {
                systemMap = asset.FindActionMap(SystemMapName);
                exitAction = systemMap != null
                    ? systemMap.FindAction(ExitActionName)
                    : asset.FindAction($"{SystemMapName}/{ExitActionName}");
            }

            if (exitAction != null)
            {
                exitAction.performed += OnExitPerformed;
                systemMap?.Enable();
                if (!exitAction.enabled)
                {
                    exitAction.Enable();
                }

                return;
            }

            // Asset missing or System/Exit not imported yet — still honor Escape.
            fallbackExitAction = new InputAction(
                ExitActionName,
                InputActionType.Button,
                "<Keyboard>/escape");
            fallbackExitAction.performed += OnExitPerformed;
            fallbackExitAction.Enable();
            Debug.LogWarning("[GameManager] System/Exit action not found. Using a runtime Escape binding.");
        }

        private void UnbindExitAction()
        {
            if (exitAction != null)
            {
                exitAction.performed -= OnExitPerformed;
                exitAction = null;
            }

            systemMap = null;

            if (fallbackExitAction != null)
            {
                fallbackExitAction.performed -= OnExitPerformed;
                fallbackExitAction.Disable();
                fallbackExitAction.Dispose();
                fallbackExitAction = null;
            }
        }

        private void OnExitPerformed(InputAction.CallbackContext context)
        {
            Quit();
        }
    }
}
