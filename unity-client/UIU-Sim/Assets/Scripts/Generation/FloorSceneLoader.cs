using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Building.Generation
{
    /// <summary>
    /// Loads the initial floor additively while the persistent Main scene owns gameplay objects.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class FloorSceneLoader : MonoBehaviour
    {
        [SerializeField] private string initialFloorSceneName = "GroundFloor";

        public string InitialFloorSceneName => initialFloorSceneName;

        private void Start()
        {
            LoadInitialFloor();
        }

        public void LoadInitialFloor()
        {
            if (string.IsNullOrWhiteSpace(initialFloorSceneName))
            {
                Debug.LogError("FloorSceneLoader requires an initial floor scene name.", this);
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
                    $"Could not load floor scene '{initialFloorSceneName}' additively. " +
                    "Ensure it is included in Build Settings.\n" + exception.Message,
                    this);
            }
        }
    }
}
