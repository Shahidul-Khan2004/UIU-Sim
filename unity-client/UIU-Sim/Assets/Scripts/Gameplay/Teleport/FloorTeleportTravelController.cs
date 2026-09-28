using System.Collections;
using UIU.Simulator.Building.Generation;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Gameplay.Teleport
{
    /// <summary>
    /// Loads the destination floor, finds the matching <see cref="FloorTeleportDestination"/>,
    /// teleports the existing player, then unloads the floor they left.
    /// Lives outside floor scenes so unloading the source floor does not cancel travel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorTeleportTravelController : MonoBehaviour
    {
        private static FloorTeleportTravelController instance;

        public static FloorTeleportTravelController EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            FloorTeleportTravelController existing = FindFirstObjectByType<FloorTeleportTravelController>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            GameObject go = new GameObject("FloorTeleportTravelController");
            instance = go.AddComponent<FloorTeleportTravelController>();
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
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }

                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void TravelTo(int linkId, int currentFloor, int targetFloor)
        {
            if (isTravelling)
            {
                Debug.LogWarning("[FloorTeleportTravelController] Travel request ignored: travel is already active.", this);
                return;
            }

            StartCoroutine(TravelRoutine(linkId, currentFloor, targetFloor));
        }

        private IEnumerator TravelRoutine(int linkId, int currentFloor, int targetFloor)
        {
            isTravelling = true;

            FloorSceneLoader loader = FloorSceneLoader.Instance;
            if (loader == null)
            {
                Debug.LogError("[FloorTeleportTravelController] FloorSceneLoader instance could not be found.", this);
                SystemNotificationUI.Show("Floor loader is not available.");
                isTravelling = false;
                yield break;
            }

            if (!loader.CanLoadFloorScene(targetFloor))
            {
                Debug.LogError($"[FloorTeleportTravelController] Floor {targetFloor} scene is not configured or cannot be loaded.", this);
                SystemNotificationUI.Show($"Floor {targetFloor} is currently unavailable.");
                isTravelling = false;
                yield break;
            }

            bool wasDestinationAlreadyLoaded = loader.IsFloorLoaded(targetFloor);
            Scene destinationScene = default;
            bool loadFailed = false;

            yield return loader.EnsureFloorLoadedRoutine(
                targetFloor,
                (loadedScene, wasAlreadyLoaded) =>
                {
                    destinationScene = loadedScene;
                    wasDestinationAlreadyLoaded = wasAlreadyLoaded;
                },
                error =>
                {
                    Debug.LogError(error, this);
                    loadFailed = true;
                });

            if (loadFailed || !destinationScene.IsValid() || !destinationScene.isLoaded)
            {
                SystemNotificationUI.Show("Failed to load destination floor.");
                isTravelling = false;
                yield break;
            }

            FloorTeleportDestination arrival = FindDestinationInScene(destinationScene, linkId, targetFloor);
            if (arrival == null)
            {
                Debug.LogError(
                    $"[FloorTeleportTravelController] Missing FloorTeleportDestination with link {linkId} on floor {targetFloor} in scene '{destinationScene.name}'.",
                    this);

                if (!wasDestinationAlreadyLoaded)
                {
                    yield return loader.UnloadFloorRoutine(targetFloor);
                }

                SystemNotificationUI.Show("Teleport destination is not configured yet.");
                isTravelling = false;
                yield break;
            }

            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                Debug.LogError("[FloorTeleportTravelController] Active PlayerMovement was not found.", this);

                if (!wasDestinationAlreadyLoaded)
                {
                    yield return loader.UnloadFloorRoutine(targetFloor);
                }

                isTravelling = false;
                yield break;
            }

            loader.CurrentFloorNumber = targetFloor;
            IEnumerator unloadSource = currentFloor != targetFloor ? loader.UnloadFloorRoutine(currentFloor) : null;
            yield return ArriveRoutine(player, arrival.transform.position, arrival.transform.rotation, unloadSource);

            Debug.Log($"[FloorTeleportTravelController] Floor {currentFloor} -> Floor {targetFloor}, landed at {player.transform.position} (marker '{arrival.name}' at {arrival.transform.position}).", arrival);
            isTravelling = false;
        }

        /// <summary>
        /// Moves the player so their feet rest on <paramref name="feetPosition"/>.
        /// Collision stays off until <paramref name="unloadSource"/> finishes, because floors share world space
        /// and the old floor's colliders would push the capsule under the new one.
        /// </summary>
        public static IEnumerator ArriveRoutine(PlayerMovement player, Vector3 feetPosition, Quaternion facing, IEnumerator unloadSource)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            FirstPersonLook look = player.GetComponent<FirstPersonLook>();
            Vector3 standingPosition = ResolveStandingPosition(feetPosition, controller);

            bool movementEnabled = player.enabled;
            player.enabled = false;
            player.PrepareForTeleport();

            if (controller != null)
            {
                controller.enabled = false;
            }

            if (unloadSource != null)
            {
                yield return unloadSource;
            }

            standingPosition = ClearSpawnPosition(standingPosition, controller);
            CommitPose(player, controller, look, standingPosition, facing);

            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForFixedUpdate();
                if (player == null)
                {
                    yield break;
                }

                if (player.transform.position.y < standingPosition.y - 0.35f)
                {
                    player.PrepareForTeleport();
                    CommitPose(player, controller, look, standingPosition, facing);
                }
            }

            player.PrepareForTeleport();
            player.enabled = movementEnabled;
        }

        /// <summary>
        /// Returns the player-root position that puts the capsule just above <paramref name="feetPosition"/>.
        /// CharacterController height and center are scaled by the transform, so the Player prefab's
        /// y scale of 1.2 puts the feet 1.08 below the root, not 0.9.
        /// </summary>
        public static Vector3 ResolveStandingPosition(Vector3 feetPosition, CharacterController controller)
        {
            if (controller == null)
            {
                return feetPosition;
            }

            float scaleY = Mathf.Abs(controller.transform.lossyScale.y);
            float feetBelowRoot = (controller.height * 0.5f - controller.center.y) * scaleY;
            return feetPosition + Vector3.up * (feetBelowRoot + controller.skinWidth);
        }

        /// <summary>
        /// Keeps a spawn that is already above a floor at that height.
        /// If the pose is underneath a slab, with no floor below it, lifts the capsule onto that slab.
        /// Sideways nudges escape a wall without dropping through the floor.
        /// </summary>
        public static Vector3 ClearSpawnPosition(Vector3 desired, CharacterController controller)
        {
            if (controller == null)
            {
                return desired;
            }

            Physics.SyncTransforms();

            float scaleY = Mathf.Abs(controller.transform.lossyScale.y);
            float feetBelowCenter = (controller.height * 0.5f - controller.center.y) * scaleY;
            bool floorBelow = Physics.Raycast(
                desired,
                Vector3.down,
                8f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            if (!floorBelow
                && Physics.Raycast(
                    desired,
                    Vector3.up,
                    out RaycastHit ceiling,
                    controller.height * scaleY,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)
                && ceiling.normal.y < -0.5f
                && ceiling.collider.bounds.size.y < 1.25f)
            {
                desired.y = ceiling.collider.bounds.max.y + feetBelowCenter + controller.skinWidth;
            }

            if (!CapsuleOverlaps(desired, controller))
            {
                return desired;
            }

            Vector3[] directions =
            {
                Vector3.forward, Vector3.back, Vector3.right, Vector3.left,
                (Vector3.forward + Vector3.right).normalized,
                (Vector3.forward + Vector3.left).normalized,
                (Vector3.back + Vector3.right).normalized,
                (Vector3.back + Vector3.left).normalized
            };

            for (float distance = 0.35f; distance <= 1.75f; distance += 0.35f)
            {
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector3 candidate = desired + directions[i] * distance;
                    if (!CapsuleOverlaps(candidate, controller))
                    {
                        return candidate;
                    }
                }
            }

            return desired;
        }

        private static bool CapsuleOverlaps(Vector3 origin, CharacterController controller)
        {
            Vector3 scale = controller.transform.lossyScale;
            float scaleY = Mathf.Abs(scale.y);
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float half = Mathf.Max(controller.height * scaleY * 0.5f - radius, 0.05f);
            Vector3 center = origin + Vector3.Scale(controller.center, scale);
            Vector3 bottom = center - Vector3.up * half;
            Vector3 top = center + Vector3.up * half;
            return Physics.CheckCapsule(
                bottom,
                top,
                Mathf.Max(0.05f, radius * 0.9f),
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// CharacterController restores its previous position when re-enabled.
        /// Set the pose while it is off, then again after it turns on.
        /// </summary>
        private static void CommitPose(
            PlayerMovement player,
            CharacterController controller,
            FirstPersonLook look,
            Vector3 position,
            Quaternion facing)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }

            ApplyPose(player, look, position, facing);

            if (controller != null)
            {
                controller.enabled = true;
                if ((player.transform.position - position).sqrMagnitude > 0.01f)
                {
                    controller.enabled = false;
                    ApplyPose(player, look, position, facing);
                    controller.enabled = true;
                }
            }
        }

        private static void ApplyPose(PlayerMovement player, FirstPersonLook look, Vector3 position, Quaternion facing)
        {
            player.transform.position = position;

            if (look != null)
            {
                look.SetFacingRotation(facing);
            }
            else
            {
                Vector3 euler = facing.eulerAngles;
                player.transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
            }

            Physics.SyncTransforms();
        }

        /// <summary>
        /// Searches only objects in <paramref name="destinationScene"/> for a destination
        /// whose link and floor match.
        /// </summary>
        public static FloorTeleportDestination FindDestinationInScene(Scene destinationScene, int linkId, int targetFloor)
        {
            if (!destinationScene.IsValid() || !destinationScene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = destinationScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                FloorTeleportDestination[] points = roots[i].GetComponentsInChildren<FloorTeleportDestination>(false);
                for (int j = 0; j < points.Length; j++)
                {
                    FloorTeleportDestination point = points[j];
                    if (point.LinkId == linkId && point.FloorNumber == targetFloor)
                    {
                        return point;
                    }
                }
            }

            return null;
        }
    }
}
