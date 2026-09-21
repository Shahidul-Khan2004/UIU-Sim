using UnityEngine;
using UIU.Simulator.Building.Generation;

namespace UIU.Simulator.Gameplay.Stairs
{
    public sealed class StairInteractable : MonoBehaviour, IInteractable
    {
        [Header("Staircase Settings")]

        [SerializeField]
        private int staircaseId = 1;

        [SerializeField]
        private bool goUpstairs = true;

        // Text shown by the existing interaction system.
        public string InteractionPrompt =>
            goUpstairs ? "Go Upstairs" : "Go Downstairs";

        // Called by the existing InteractionController
        // when the player presses the interaction key.
        public string Interact()
        {
            FloorSceneLoader loader = FloorSceneLoader.Instance;

            if (loader == null)
            {
                Debug.LogError("FloorSceneLoader not found!");
                return "Floor loader unavailable.";
            }

            // Find the player's current floor.
            int currentFloor = loader.CurrentFloorNumber;

            // Upstairs = +1, Downstairs = -1.
            int targetFloor = currentFloor +
                              (goUpstairs ? 1 : -1);

            // Prevent travelling outside the building.
            if (targetFloor < FloorSceneLoader.MinFloorNumber ||
                targetFloor > FloorSceneLoader.MaxFloorNumber)
            {
                return "There is no floor in that direction.";
            }

            // Find our staircase travel controller.
            StairTravelController travelController =
                FindFirstObjectByType<StairTravelController>();

            if (travelController == null)
            {
                Debug.LogError(
                    "StairTravelController not found!"
                );

                return "Stair travel system unavailable.";
            }

            // Start travelling to the adjacent floor.
            travelController.Travel(staircaseId, targetFloor);

            return null;
        }
    }
}