using UnityEngine;

namespace UIU.Simulator.Gameplay.Elevator
{
    /// <summary>
    /// Manual arrival marker placed outside elevator doors on each floor.
    /// Stores the elevator ID and floor number. The transform position and rotation
    /// represent where the player will appear and which direction they face.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class ElevatorArrivalPoint : MonoBehaviour
    {
        [Tooltip("The elevator ID (1..6) corresponding to this physical elevator door.")]
        [SerializeField, Range(1, 6)] private int elevatorId = 1;

        [Tooltip("The floor number (0..10) where this arrival marker is located.")]
        [SerializeField, Range(0, 10)] private int floorNumber = 0;

        public int ElevatorId => elevatorId;
        public int FloorNumber => floorNumber;

        public void Initialize(int id, int floor)
        {
            elevatorId = Mathf.Clamp(id, 1, 6);
            floorNumber = Mathf.Clamp(floor, 0, 10);
        }

        private void OnValidate()
        {
            elevatorId = Mathf.Clamp(elevatorId, 1, 6);
            floorNumber = Mathf.Clamp(floorNumber, 0, 10);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.3f, 0.75f);
            Vector3 center = transform.position + Vector3.up * 1f; // player center height approx
            Gizmos.DrawWireCube(center, new Vector3(0.6f, 1.8f, 0.6f));

            // Facing direction arrow
            Gizmos.color = Color.cyan;
            Vector3 forwardStart = transform.position + Vector3.up * 1.2f;
            Vector3 forwardEnd = forwardStart + transform.forward * 1.0f;
            Gizmos.DrawLine(forwardStart, forwardEnd);
            Gizmos.DrawLine(forwardEnd, forwardEnd - transform.forward * 0.25f + transform.right * 0.15f);
            Gizmos.DrawLine(forwardEnd, forwardEnd - transform.forward * 0.25f - transform.right * 0.15f);
        }
#endif
    }
}
