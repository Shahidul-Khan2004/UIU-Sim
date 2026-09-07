using System;
using System.Collections;
using UIU.Simulator.Building.Generation;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Gameplay.Elevator
{
    /// <summary>
    /// Central authority coordinating elevator travel between floors.
    /// Manages additive scene loading via FloorSceneLoader, destination-scene-aware marker resolution,
    /// safe CharacterController teleportation, and clean source-floor unloading.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevatorTravelController : MonoBehaviour
    {
        private static ElevatorTravelController instance;

        public static ElevatorTravelController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = EnsureExists();
                }
                return instance;
            }
        }

        public static ElevatorTravelController EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            ElevatorTravelController existing = FindFirstObjectByType<ElevatorTravelController>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            GameObject go = new GameObject("ElevatorTravelController");
            instance = go.AddComponent<ElevatorTravelController>();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
            return instance;
        }

        private bool isTravelling;
        public bool IsTravelling => isTravelling;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                SafeDestroy(gameObject);
                return;
            }

            instance = this;
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Initiates travel from source elevator door to destination floor.
        /// </summary>
        public void TravelToFloor(
            int elevatorId,
            int currentFloor,
            int targetFloor,
            Action onComplete,
            Action<string> onError)
        {
            if (isTravelling)
            {
                Debug.LogWarning("[ElevatorTravelController] Travel request ignored: travel is already active.", this);
                return;
            }

            StartCoroutine(TravelRoutine(elevatorId, currentFloor, targetFloor, onComplete, onError));
        }

        private IEnumerator TravelRoutine(
            int elevatorId,
            int currentFloor,
            int targetFloor,
            Action onComplete,
            Action<string> onError)
        {
            isTravelling = true;

            FloorSceneLoader loader = FloorSceneLoader.Instance;
            if (loader == null)
            {
                const string error = "[ElevatorTravelController] FloorSceneLoader instance could not be found.";
                Debug.LogError(error, this);
                SystemNotificationUI.Show("Floor loader is not available.");
                isTravelling = false;
                onError?.Invoke(error);
                yield break;
            }

            // Validate destination floor configuration and streamability
            if (!loader.CanLoadFloorScene(targetFloor))
            {
                string error = $"[ElevatorTravelController] Floor {targetFloor} scene is not configured or cannot be loaded.";
                Debug.LogError(error, this);
                SystemNotificationUI.Show($"Floor {targetFloor} is currently unavailable.");
                isTravelling = false;
                onError?.Invoke(error);
                yield break;
            }

            // Track whether destination was already loaded before this travel operation
            bool wasDestinationAlreadyLoaded = loader.IsFloorLoaded(targetFloor);

            // Ensure destination floor is loaded additively (keeps UIU_Main active)
            Scene destinationScene = default;
            bool loadFailed = false;
            string loadErrorMessage = null;

            yield return loader.EnsureFloorLoadedRoutine(
                targetFloor,
                (loadedScene, wasAlreadyLoaded) =>
                {
                    destinationScene = loadedScene;
                    wasDestinationAlreadyLoaded = wasAlreadyLoaded;
                },
                err =>
                {
                    loadFailed = true;
                    loadErrorMessage = err;
                });

            if (loadFailed || !destinationScene.IsValid() || !destinationScene.isLoaded)
            {
                string error = loadErrorMessage ?? $"Failed to load destination floor {targetFloor}.";
                Debug.LogError($"[ElevatorTravelController] {error}", this);
                SystemNotificationUI.Show("Failed to load destination floor.");
                isTravelling = false;
                onError?.Invoke(error);
                yield break;
            }

            // Find arrival point exclusively in the destination scene
            ElevatorArrivalPoint arrivalPoint = FindArrivalPointInScene(destinationScene, elevatorId, targetFloor);
            if (arrivalPoint == null)
            {
                string configError = $"[ElevatorTravelController] Missing ElevatorArrivalPoint for Elevator {elevatorId} on floor {targetFloor} in scene '{destinationScene.name}'.";
                Debug.LogError(configError, this);

                // If destination was newly loaded by this travel, unload it so overlapping geometry doesn't persist
                if (!wasDestinationAlreadyLoaded)
                {
                    yield return loader.UnloadFloorRoutine(targetFloor);
                }

                SystemNotificationUI.Show("Elevator destination is not configured yet.");
                isTravelling = false;
                onError?.Invoke(configError);
                yield break;
            }

            // Resolve player components safely
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                const string error = "[ElevatorTravelController] Active PlayerMovement was not found in scene.";
                Debug.LogError(error, this);

                // If destination was newly loaded by this travel, unload it
                if (!wasDestinationAlreadyLoaded)
                {
                    yield return loader.UnloadFloorRoutine(targetFloor);
                }

                isTravelling = false;
                onError?.Invoke(error);
                yield break;
            }

            Transform playerTransform = player.transform;
            CharacterController controller = player.GetComponent<CharacterController>();
            FirstPersonLook look = player.GetComponent<FirstPersonLook>();

            // Safe teleport: disable CharacterController, move, sync look, re-enable
            if (controller != null)
            {
                controller.enabled = false;
            }

            playerTransform.position = arrivalPoint.transform.position;

            if (look != null)
            {
                look.SetFacingRotation(arrivalPoint.transform.rotation);
            }
            else
            {
                Vector3 euler = arrivalPoint.transform.rotation.eulerAngles;
                playerTransform.rotation = Quaternion.Euler(0f, euler.y, 0f);
            }

            if (controller != null)
            {
                controller.enabled = true;
            }

            // Update current floor tracking
            loader.CurrentFloorNumber = targetFloor;

            // Safe source floor unload: because floor scenes occupy overlapping world space (y: 0..3),
            // safely unload the source floor now that the destination is loaded and player has arrived.
            if (currentFloor != targetFloor)
            {
                yield return loader.UnloadFloorRoutine(currentFloor);
            }

            isTravelling = false;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Searches only root objects and children belonging to destinationScene for the matching ElevatorArrivalPoint.
        /// Guaranteed not to match arrival points in other additively loaded scenes.
        /// </summary>
        public static ElevatorArrivalPoint FindArrivalPointInScene(Scene destinationScene, int elevatorId, int targetFloor)
        {
            if (!destinationScene.IsValid() || !destinationScene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = destinationScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                ElevatorArrivalPoint[] points = roots[i].GetComponentsInChildren<ElevatorArrivalPoint>(true);
                for (int j = 0; j < points.Length; j++)
                {
                    ElevatorArrivalPoint point = points[j];
                    if (point.ElevatorId == elevatorId && point.FloorNumber == targetFloor)
                    {
                        return point;
                    }
                }
            }

            return null;
        }
    }
}
