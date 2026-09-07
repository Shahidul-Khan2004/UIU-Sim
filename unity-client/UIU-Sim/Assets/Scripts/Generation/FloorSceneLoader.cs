using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Building.Generation
{
    /// <summary>
    /// Sole authoritative manager for additive floor scene loading, unloading, and floor-to-scene name mapping.
    /// The persistent UIU_Main scene owns gameplay objects and remains the active scene at all times.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class FloorSceneLoader : MonoBehaviour
    {
        private static FloorSceneLoader instance;

        public static FloorSceneLoader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<FloorSceneLoader>();
                }
                return instance;
            }
        }

        public const int MinFloorNumber = 0;
        public const int MaxFloorNumber = 10;

        [SerializeField] private string initialFloorSceneName = "GroundFloor";
        [SerializeField] private int currentFloorNumber = 0;

        public string InitialFloorSceneName => initialFloorSceneName;

        public int CurrentFloorNumber
        {
            get => currentFloorNumber;
            set => currentFloorNumber = value;
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            LoadInitialFloor();
        }

        /// <summary>
        /// Maps a numeric floor (0..10) to its standard scene name.
        /// 0 -> "GroundFloor", 1..10 -> "Floor01".."Floor10".
        /// </summary>
        public static string GetDefaultFloorSceneName(int floorNumber)
        {
            if (floorNumber == 0)
            {
                return "GroundFloor";
            }

            if (floorNumber >= 1 && floorNumber <= MaxFloorNumber)
            {
                return $"Floor{floorNumber:D2}";
            }

            return null;
        }

        /// <summary>
        /// Central authoritative source of truth mapping floor number to scene name.
        /// </summary>
        public bool TryGetFloorSceneName(int floorNumber, out string sceneName)
        {
            sceneName = GetDefaultFloorSceneName(floorNumber);
            return !string.IsNullOrEmpty(sceneName);
        }

        /// <summary>
        /// Returns the scene name for the given floor number, or null if invalid.
        /// </summary>
        public string GetFloorSceneName(int floorNumber)
        {
            TryGetFloorSceneName(floorNumber, out string sceneName);
            return sceneName;
        }

        /// <summary>
        /// Validates that a floor number is within supported range (0..10) and its scene can be loaded in Unity.
        /// Checks both memory and Application.CanStreamedLevelBeLoaded.
        /// </summary>
        public bool CanLoadFloorScene(int floorNumber)
        {
            if (!TryGetFloorSceneName(floorNumber, out string sceneName) || string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            Scene existingScene = SceneManager.GetSceneByName(sceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                return true;
            }

            return Application.CanStreamedLevelBeLoaded(sceneName);
        }

        /// <summary>
        /// Checks if a floor scene is currently loaded in memory.
        /// </summary>
        public bool IsFloorLoaded(int floorNumber)
        {
            if (!TryGetFloorSceneName(floorNumber, out string sceneName))
            {
                return false;
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        /// <summary>
        /// Loads the initial configured floor scene additively if not already loaded.
        /// </summary>
        public void LoadInitialFloor()
        {
            if (string.IsNullOrWhiteSpace(initialFloorSceneName))
            {
                Debug.LogError("[FloorSceneLoader] FloorSceneLoader requires an initial floor scene name.", this);
                return;
            }

            Scene existingScene = SceneManager.GetSceneByName(initialFloorSceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                return;
            }

            try
            {
                SceneManager.LoadScene(initialFloorSceneName, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[FloorSceneLoader] Could not load floor scene '{initialFloorSceneName}' additively. " +
                    "Ensure it is included in Build Settings.\n" + exception.Message,
                    this);
            }
        }

        /// <summary>
        /// Ensures destination floor scene is loaded additively without changing the active scene.
        /// Reports whether the scene was already loaded before this call (true) or newly loaded (false).
        /// UIU_Main remains the persistent active scene.
        /// </summary>
        public IEnumerator EnsureFloorLoadedRoutine(int floorNumber, Action<Scene, bool> onComplete, Action<string> onError = null)
        {
            if (!TryGetFloorSceneName(floorNumber, out string sceneName) || string.IsNullOrWhiteSpace(sceneName))
            {
                string error = $"[FloorSceneLoader] Invalid floor number: {floorNumber}. Supported range is {MinFloorNumber}..{MaxFloorNumber}.";
                Debug.LogError(error, this);
                onError?.Invoke(error);
                yield break;
            }

            Scene existingScene = SceneManager.GetSceneByName(sceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                onComplete?.Invoke(existingScene, true); // was already loaded
                yield break;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                string error = $"[FloorSceneLoader] Scene '{sceneName}' for floor {floorNumber} cannot be loaded. Ensure it is in Build Settings.";
                Debug.LogError(error, this);
                onError?.Invoke(error);
                yield break;
            }

            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (loadOp == null)
            {
                string error = $"[FloorSceneLoader] SceneManager.LoadSceneAsync returned null for scene '{sceneName}'.";
                Debug.LogError(error, this);
                onError?.Invoke(error);
                yield break;
            }

            while (!loadOp.isDone)
            {
                yield return null;
            }

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                string error = $"[FloorSceneLoader] Scene '{sceneName}' was loaded but could not be retrieved from SceneManager.";
                Debug.LogError(error, this);
                onError?.Invoke(error);
                yield break;
            }

            // CRITICAL: UIU_Main remains the persistent active scene. Do NOT call SetActiveScene(loadedScene).
            onComplete?.Invoke(loadedScene, false); // newly loaded
        }

        /// <summary>
        /// Backwards-compatible overload for EnsureFloorLoadedRoutine without wasAlreadyLoaded parameter.
        /// </summary>
        public IEnumerator EnsureFloorLoadedRoutine(int floorNumber, Action<Scene> onComplete, Action<string> onError = null)
        {
            return EnsureFloorLoadedRoutine(floorNumber, (scene, _) => onComplete?.Invoke(scene), onError);
        }

        /// <summary>
        /// Safely unloads a floor scene additively. Protects the active scene (UIU_Main) from being unloaded.
        /// </summary>
        public IEnumerator UnloadFloorRoutine(int floorNumber, Action onComplete = null, Action<string> onError = null)
        {
            if (!TryGetFloorSceneName(floorNumber, out string sceneName) || string.IsNullOrWhiteSpace(sceneName))
            {
                onComplete?.Invoke();
                yield break;
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                onComplete?.Invoke();
                yield break;
            }

            // Never unload the active persistent scene
            if (scene == SceneManager.GetActiveScene())
            {
                string warn = $"[FloorSceneLoader] Refusing to unload active scene '{sceneName}'.";
                Debug.LogWarning(warn, this);
                onComplete?.Invoke();
                yield break;
            }

            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(scene);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone)
                {
                    yield return null;
                }
            }

            onComplete?.Invoke();
        }
    }
}
