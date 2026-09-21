using UnityEngine;

namespace UIU.Simulator.Core
{
    /// <summary>
    /// Persistent application-level manager. Owns session-wide systems that must survive
    /// scene changes (Bootstrap → Login → Main, plus additive floor loads).
    ///
    /// This is the only place that should request the application to quit. Other scripts
    /// should call <see cref="Quit"/> instead of using Application.Quit directly.
    /// Escape is owned by the in-game <c>GameMenuManager</c>; it does not quit.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

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
    }
}
