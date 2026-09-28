using UIU.Simulator.Building.Generation;
using UIU.Simulator.Gameplay.Elevator;
using UnityEngine;
using UnityEngine.Serialization;

namespace UIU.Simulator.Gameplay.Teleport
{
    /// <summary>
    /// Attach to any object with a collider. One object can go up, down, or both.
    /// When both directions are available, the player chooses. Each direction
    /// lands on the <see cref="FloorTeleportDestination"/> with the matching link id.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorTeleportInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("Prompt shown while looking at this object. Ignored when both directions are available; that prompt is Use Stairs.")]
        [SerializeField] private string interactionPrompt = "Go Up";

        [Header("Up")]
        [SerializeField] private bool goesUp = true;

        [Tooltip("Floor to load when going up. 0 is Ground Floor, 1 is Floor 01, up to 10.")]
        [FormerlySerializedAs("targetFloor")]
        [SerializeField, Range(0, 10)] private int upFloor = 1;

        [Tooltip("Must match the Link Id on the Floor Teleport Destination where the player appears after going up.")]
        [FormerlySerializedAs("linkId")]
        [SerializeField, Min(1)] private int upLinkId = 1;

        [Header("Down")]
        [SerializeField] private bool goesDown;

        [Tooltip("Floor to load when going down. 0 is Ground Floor, 1 is Floor 01, up to 10.")]
        [SerializeField, Range(0, 10)] private int downFloor;

        [Tooltip("Must match the Link Id on the Floor Teleport Destination where the player appears after going down.")]
        [SerializeField, Min(1)] private int downLinkId = 1;

        public bool GoesUp => goesUp;
        public bool GoesDown => goesDown;
        public int UpFloor => upFloor;
        public int UpLinkId => upLinkId;
        public int DownFloor => downFloor;
        public int DownLinkId => downLinkId;

        public string InteractionPrompt
        {
            get
            {
                if (goesUp && goesDown)
                {
                    return "Use Stairs";
                }

                if (!string.IsNullOrWhiteSpace(interactionPrompt))
                {
                    return interactionPrompt;
                }

                return goesDown ? "Go Down" : "Go Up";
            }
        }

        public void Initialize(int floor, int id, string prompt = "Go Up")
        {
            goesUp = true;
            goesDown = false;
            upFloor = Mathf.Clamp(floor, FloorSceneLoader.MinFloorNumber, FloorSceneLoader.MaxFloorNumber);
            upLinkId = Mathf.Max(1, id);
            if (!string.IsNullOrEmpty(prompt))
            {
                interactionPrompt = prompt;
            }
        }

        public void InitializeBoth(int upTargetFloor, int upLink, int downTargetFloor, int downLink)
        {
            goesUp = true;
            goesDown = true;
            upFloor = Mathf.Clamp(upTargetFloor, FloorSceneLoader.MinFloorNumber, FloorSceneLoader.MaxFloorNumber);
            downFloor = Mathf.Clamp(downTargetFloor, FloorSceneLoader.MinFloorNumber, FloorSceneLoader.MaxFloorNumber);
            upLinkId = Mathf.Max(1, upLink);
            downLinkId = Mathf.Max(1, downLink);
            interactionPrompt = "Use Stairs";
        }

        private void OnValidate()
        {
            upFloor = Mathf.Clamp(upFloor, FloorSceneLoader.MinFloorNumber, FloorSceneLoader.MaxFloorNumber);
            downFloor = Mathf.Clamp(downFloor, FloorSceneLoader.MinFloorNumber, FloorSceneLoader.MaxFloorNumber);
            upLinkId = Mathf.Max(1, upLinkId);
            downLinkId = Mathf.Max(1, downLinkId);
            if (string.IsNullOrWhiteSpace(interactionPrompt))
            {
                interactionPrompt = goesUp && goesDown ? "Use Stairs" : goesDown ? "Go Down" : "Go Up";
            }
        }

        public string Interact()
        {
            if (FloorTeleportChoiceUI.IsOpen)
            {
                return null;
            }

            FloorSceneLoader loader = FloorSceneLoader.Instance;
            if (loader == null)
            {
                Debug.LogError("[FloorTeleportInteractable] FloorSceneLoader was not found.", this);
                return "Floor loader unavailable.";
            }

            int currentFloor = loader.CurrentFloorNumber;
            ResolveAvailableDirections(
                currentFloor,
                goesUp,
                upFloor,
                goesDown,
                downFloor,
                out bool upAvailable,
                out bool downAvailable);

            if (!upAvailable && !downAvailable)
            {
                return "There is no floor in that direction.";
            }

            ElevatorTravelController elevator = FindFirstObjectByType<ElevatorTravelController>();
            if (elevator != null && elevator.IsTravelling)
            {
                return "The lift is moving.";
            }

            FloorTeleportTravelController travel = FloorTeleportTravelController.EnsureExists();
            if (travel.IsTravelling)
            {
                return "Already on the way.";
            }

            if (upAvailable && downAvailable)
            {
                FloorTeleportChoiceUI.EnsureExists().Show(
                    () => travel.TravelTo(upLinkId, currentFloor, upFloor),
                    () => travel.TravelTo(downLinkId, currentFloor, downFloor));
                return null;
            }

            if (upAvailable)
            {
                travel.TravelTo(upLinkId, currentFloor, upFloor);
            }
            else
            {
                travel.TravelTo(downLinkId, currentFloor, downFloor);
            }

            return null;
        }

        /// <summary>
        /// A direction is available when it is enabled and its floor is inside the building
        /// and is not the floor the player is already on. If both point at the same floor, only up is offered.
        /// </summary>
        public static void ResolveAvailableDirections(
            int currentFloor,
            bool goesUp,
            int upFloor,
            bool goesDown,
            int downFloor,
            out bool upAvailable,
            out bool downAvailable)
        {
            upAvailable = goesUp && IsOtherFloor(currentFloor, upFloor);
            downAvailable = goesDown && IsOtherFloor(currentFloor, downFloor);
            if (upAvailable && downAvailable && upFloor == downFloor)
            {
                downAvailable = false;
            }
        }

        private static bool IsOtherFloor(int currentFloor, int targetFloor)
        {
            return targetFloor != currentFloor
                && targetFloor >= FloorSceneLoader.MinFloorNumber
                && targetFloor <= FloorSceneLoader.MaxFloorNumber;
        }
    }
}
