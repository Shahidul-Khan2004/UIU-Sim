using UnityEngine;

namespace UIU.Simulator.Gameplay.Teleport
{
    /// <summary>
        /// Marker placed on the floor the player should arrive on.
        /// The transform sits at their feet. Forward is the direction they face.
    /// Match <see cref="LinkId"/> to a <see cref="FloorTeleportInteractable"/> on the floor they leave from.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class FloorTeleportDestination : MonoBehaviour
    {
        [Tooltip("Shared id. Must match the Floor Teleport Interactable that sends the player here.")]
        [SerializeField, Min(1)] private int linkId = 1;

        [Tooltip("Floor this marker belongs to. 0 is Ground Floor, 1 is Floor 01, up to 10. Filled from the scene name when the scene is a floor scene.")]
        [SerializeField, Range(0, 10)] private int floorNumber = 0;

        public int LinkId => linkId;
        public int FloorNumber => floorNumber;

        public void Initialize(int id, int floor)
        {
            linkId = Mathf.Max(1, id);
            floorNumber = Mathf.Clamp(floor, 0, 10);
        }

        private void OnValidate()
        {
            linkId = Mathf.Max(1, linkId);
            if (TryParseFloorSceneName(gameObject.scene.name, out int parsedFloor))
            {
                floorNumber = parsedFloor;
            }
            else
            {
                floorNumber = Mathf.Clamp(floorNumber, 0, 10);
            }
        }

        /// <summary>
        /// GroundFloor -> 0, Floor01..Floor10 -> 1..10.
        /// </summary>
        public static bool TryParseFloorSceneName(string sceneName, out int floorNumber)
        {
            floorNumber = 0;
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            if (sceneName == "GroundFloor")
            {
                return true;
            }

            if (sceneName.Length == 7
                && sceneName.StartsWith("Floor")
                && int.TryParse(sceneName.Substring(5, 2), out int parsed)
                && parsed >= 1
                && parsed <= 10)
            {
                floorNumber = parsed;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.55f, 0.15f, 0.85f);
            Vector3 center = transform.position + Vector3.up * 0.9f;
            Gizmos.DrawWireCube(center, new Vector3(0.6f, 1.8f, 0.6f));

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
