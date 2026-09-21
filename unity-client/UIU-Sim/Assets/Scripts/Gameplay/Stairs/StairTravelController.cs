using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UIU.Simulator.Building.Generation;
using UIU.Simulator.Gameplay.Elevator;

namespace UIU.Simulator.Gameplay.Stairs
{
    public sealed class StairTravelController : MonoBehaviour
    {
        private bool isTravelling = false;

        public void Travel(int staircaseId, int targetFloor)
        {
            // Prevent multiple staircase travel requests.
            if (isTravelling)
                return;

            // Avoid starting staircase travel during elevator travel.
            ElevatorTravelController elevator =
                FindFirstObjectByType<ElevatorTravelController>();

            if (elevator != null && elevator.IsTravelling)
            {
                Debug.LogWarning("Elevator travel is active.");
                return;
            }

            StartCoroutine(TravelRoutine(staircaseId, targetFloor));
        }

        private IEnumerator TravelRoutine(
            int staircaseId,
            int targetFloor)
        {
            isTravelling = true;

            FloorSceneLoader loader = FloorSceneLoader.Instance;

            if (loader == null)
            {
                Debug.LogError("FloorSceneLoader not found!");
                isTravelling = false;
                yield break;
            }

            int currentFloor = loader.CurrentFloorNumber;

            // Stairs only connect adjacent floors.
            if (Mathf.Abs(targetFloor - currentFloor) != 1)
            {
                Debug.LogError("Stairs must connect adjacent floors.");
                isTravelling = false;
                yield break;
            }

            if (!loader.CanLoadFloorScene(targetFloor))
            {
                Debug.LogError("Destination floor is unavailable.");
                isTravelling = false;
                yield break;
            }

            // Remember whether destination was already loaded.
            bool alreadyLoaded = loader.IsFloorLoaded(targetFloor);

            Scene destinationScene = default;
            bool loadFailed = false;

            // Load destination using the existing floor loader.
            yield return loader.EnsureFloorLoadedRoutine(
                targetFloor,
                (scene, wasAlreadyLoaded) =>
                {
                    destinationScene = scene;
                    alreadyLoaded = wasAlreadyLoaded;
                },
                error =>
                {
                    Debug.LogError(error);
                    loadFailed = true;
                }
            );

            if (loadFailed ||
                !destinationScene.IsValid() ||
                !destinationScene.isLoaded)
            {
                isTravelling = false;
                yield break;
            }

            // Search ONLY inside the destination scene.
            StairArrivalPoint arrival = null;

            foreach (GameObject root
                     in destinationScene.GetRootGameObjects())
            {
                StairArrivalPoint[] points =
                    root.GetComponentsInChildren<StairArrivalPoint>(true);

                foreach (StairArrivalPoint point in points)
                {
                    if (point.StaircaseId == staircaseId &&
                        point.ArrivingFromFloor == currentFloor)
                    {
                        arrival = point;
                        break;
                    }
                }

                if (arrival != null)
                    break;
            }

            // Do not unload the current floor if no arrival exists.
            if (arrival == null)
            {
                Debug.LogError(
                    "Stair arrival point not found on destination floor!"
                );

                if (!alreadyLoaded)
                    yield return loader.UnloadFloorRoutine(targetFloor);

                isTravelling = false;
                yield break;
            }

            // Find the SAME existing player.
            PlayerMovement player =
                FindFirstObjectByType<PlayerMovement>();

            if (player == null)
            {
                Debug.LogError("Player not found!");

                if (!alreadyLoaded)
                    yield return loader.UnloadFloorRoutine(targetFloor);

                isTravelling = false;
                yield break;
            }

            // Safely teleport the CharacterController.
            CharacterController controller =
                player.GetComponent<CharacterController>();

            FirstPersonLook look =
                player.GetComponent<FirstPersonLook>();

            if (controller != null)
                controller.enabled = false;

            player.transform.position = arrival.transform.position;

            if (look != null)
            {
                look.SetFacingRotation(arrival.transform.rotation);
            }
            else
            {
                float y = arrival.transform.eulerAngles.y;

                player.transform.rotation =
                    Quaternion.Euler(0f, y, 0f);
            }

            if (controller != null)
                controller.enabled = true;

            // Update the current floor.
            loader.CurrentFloorNumber = targetFloor;

            // Remove the previous floor.
            yield return loader.UnloadFloorRoutine(currentFloor);

            Debug.Log(
                "Stair travel successful: Floor " +
                currentFloor + " -> Floor " + targetFloor
            );

            isTravelling = false;
        }
    }
}