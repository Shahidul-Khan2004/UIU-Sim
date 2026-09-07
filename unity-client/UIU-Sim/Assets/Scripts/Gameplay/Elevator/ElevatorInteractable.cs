using UnityEngine;

namespace UIU.Simulator.Gameplay.Elevator
{
    /// <summary>
    /// Interactable surface attached to elevator doors.
    /// Exposes elevator identity and opens the floor selection modal when interacted with.
    /// Scene loading, destination resolution, and teleportation are delegated to ElevatorTravelController and FloorSceneLoader.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevatorInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("The elevator ID (1..6). Must match across all floors for the same physical elevator shaft.")]
        [SerializeField, Range(1, 6)] private int elevatorId = 1;

        [Tooltip("The current floor number (0..10) where this door is located.")]
        [SerializeField, Range(0, 10)] private int currentFloor = 0;

        [Tooltip("Prompt displayed to the player when looking at this elevator door.")]
        [SerializeField] private string interactionPrompt = "Use Elevator";

        public int ElevatorId => elevatorId;
        public int CurrentFloor => currentFloor;
        public string InteractionPrompt => interactionPrompt;

        public void Initialize(int id, int floor, string prompt = "Use Elevator")
        {
            elevatorId = Mathf.Clamp(id, 1, 6);
            currentFloor = Mathf.Clamp(floor, 0, 10);
            if (!string.IsNullOrEmpty(prompt))
            {
                interactionPrompt = prompt;
            }
        }

        private void OnValidate()
        {
            elevatorId = Mathf.Clamp(elevatorId, 1, 6);
            currentFloor = Mathf.Clamp(currentFloor, 0, 10);
            if (string.IsNullOrWhiteSpace(interactionPrompt))
            {
                interactionPrompt = "Use Elevator";
            }
        }

        public string Interact()
        {
            ElevatorUI ui = ElevatorUI.EnsureExists();
            if (ui != null)
            {
                ui.Show(elevatorId, currentFloor);
            }
            else
            {
                Debug.LogError("[ElevatorInteractable] ElevatorUI could not be instantiated or found.", this);
            }

            return null;
        }
    }
}
