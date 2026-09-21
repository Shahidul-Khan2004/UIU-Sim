using System;
using System.Collections;
using UIU.Simulator.Building.Generation;
using UIU.Simulator.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instantiates the player into the persistent Main scene after the additive floor environment is ready.
/// Also relocates the existing persistent player to the primary outdoor spawn on Next Day.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Camera")]
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Spawn")]
    [Tooltip("Used when the loaded floor has no PlayerSpawnPoint.")]
    [SerializeField] private Vector3 fallbackPosition = new Vector3(0f, 1f, 15f);

    private GameObject spawnedPlayer;

    private IEnumerator Start()
    {
        // Ensure admission / ID-card save cache is fresh for this gameplay session.
        PlayerSaveState.EnsureExists().RefreshFromServer();

        string floorSceneName = ResolveFloorSceneName();
        Scene floorScene = SceneManager.GetSceneByName(floorSceneName);
        float elapsed = 0f;
        const float timeoutSeconds = 10f;

        while (!IsLoaded(floorScene) && elapsed < timeoutSeconds)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            floorScene = SceneManager.GetSceneByName(floorSceneName);
        }

        if (!IsLoaded(floorScene))
        {
            Debug.LogError(
                $"PlayerSpawner: floor scene '{floorSceneName}' did not load in time.",
                this);
            yield break;
        }

        SpawnPlayer(floorScene);
    }

    /// <summary>
    /// Moves the existing player to the primary outdoor <see cref="PlayerSpawnPoint"/>
    /// (GroundFloor). Does not instantiate a second player or touch save/stats/ID ownership.
    /// Ensures GroundFloor is loaded, teleports, then unloads other gameplay floors via
    /// <see cref="FloorSceneLoader"/> (same ownership rules as elevator/stair travel).
    /// </summary>
    public IEnumerator RespawnExistingPlayerAtPrimarySpawnRoutine(
        Action onComplete = null,
        Action<string> onError = null)
    {
        FloorSceneLoader loader = FloorSceneLoader.Instance != null
            ? FloorSceneLoader.Instance
            : FindFirstObjectByType<FloorSceneLoader>();

        Scene groundScene = default;
        bool loadFailed = false;
        string loadError = null;

        if (loader != null)
        {
            yield return loader.EnsureFloorLoadedRoutine(
                0,
                (scene, _) => groundScene = scene,
                err =>
                {
                    loadFailed = true;
                    loadError = err;
                });
        }
        else
        {
            string sceneName = ResolveFloorSceneName();
            groundScene = SceneManager.GetSceneByName(sceneName);
            if (!IsLoaded(groundScene))
            {
                loadFailed = true;
                loadError = $"Floor scene '{sceneName}' is not loaded and FloorSceneLoader is missing.";
            }
        }

        if (loadFailed || !IsLoaded(groundScene))
        {
            string error = loadError ?? "Could not load the primary spawn floor.";
            Debug.LogError($"[PlayerSpawner] Respawn failed: {error}", this);
            onError?.Invoke(error);
            yield break;
        }

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player == null)
        {
            const string error = "No active player found to respawn.";
            Debug.LogError($"[PlayerSpawner] {error}", this);
            onError?.Invoke(error);
            yield break;
        }

        spawnedPlayer = player.gameObject;

        // Player must not remain owned by a floor scene that is about to unload.
        EnsurePlayerInPersistentScene(player.gameObject);

        PlayerSpawnPoint spawnPoint = FindSpawnPoint(groundScene);
        Vector3 position;
        Quaternion rotation;
        if (spawnPoint != null)
        {
            position = spawnPoint.transform.position;
            rotation = spawnPoint.transform.rotation;
        }
        else
        {
            position = fallbackPosition;
            rotation = Quaternion.identity;
            Debug.LogWarning(
                $"[PlayerSpawner] No PlayerSpawnPoint in '{groundScene.name}'. Using fallback {fallbackPosition}.",
                this);
        }

        TeleportPlayer(player, position, rotation);
        AssignCameraFollow(player.transform);

        if (loader != null)
        {
            loader.CurrentFloorNumber = 0;

            bool unloadFailed = false;
            string unloadError = null;
            yield return loader.UnloadOtherGameplayFloorsRoutine(
                keepFloorNumber: 0,
                onComplete: null,
                onError: err =>
                {
                    unloadFailed = true;
                    unloadError = err;
                });

            if (unloadFailed)
            {
                string error = unloadError
                    ?? "Day advanced and player respawned, but previous floor scenes could not be unloaded.";
                Debug.LogError($"[PlayerSpawner] {error}", this);
                onError?.Invoke(error);
                yield break;
            }
        }

        Debug.Log($"[PlayerSpawner] Respawned existing player at primary spawn in '{groundScene.name}'.");
        onComplete?.Invoke();
    }

    private void EnsurePlayerInPersistentScene(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }

        Scene persistentScene = gameObject.scene;
        if (!persistentScene.IsValid() || !persistentScene.isLoaded)
        {
            return;
        }

        if (playerObject.scene == persistentScene)
        {
            return;
        }

        SceneManager.MoveGameObjectToScene(playerObject, persistentScene);
    }

    /// <summary>EditMode/test seam: teleport without scene loading.</summary>
    public static bool TryTeleportPlayerToSpawnPoint(PlayerMovement player, PlayerSpawnPoint spawnPoint)
    {
        if (player == null || spawnPoint == null)
        {
            return false;
        }

        TeleportPlayer(player, spawnPoint.transform.position, spawnPoint.transform.rotation);
        return true;
    }

    private static void TeleportPlayer(PlayerMovement player, Vector3 position, Quaternion rotation)
    {
        Transform playerTransform = player.transform;
        CharacterController controller = player.GetComponent<CharacterController>();
        FirstPersonLook look = player.GetComponent<FirstPersonLook>();
        bool controllerWasEnabled = controller != null && controller.enabled;

        if (controller != null)
        {
            controller.enabled = false;
        }

        playerTransform.position = position;

        if (look != null)
        {
            look.SetFacingRotation(rotation);
        }
        else
        {
            Vector3 euler = rotation.eulerAngles;
            playerTransform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }

        if (controller != null)
        {
            controller.enabled = controllerWasEnabled;
        }
    }

    private void SpawnPlayer(Scene floorScene)
    {
        if (spawnedPlayer != null)
        {
            return;
        }

        PlayerMovement existingPlayer = FindFirstObjectByType<PlayerMovement>();
        if (existingPlayer != null)
        {
            spawnedPlayer = existingPlayer.gameObject;
            AssignCameraFollow(spawnedPlayer.transform);
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: player prefab is not assigned.", this);
            return;
        }

        PlayerSpawnPoint spawnPoint = FindSpawnPoint(floorScene);
        Vector3 position;
        Quaternion rotation;

        if (spawnPoint != null)
        {
            position = spawnPoint.transform.position;
            rotation = spawnPoint.transform.rotation;
        }
        else
        {
            position = fallbackPosition;
            rotation = Quaternion.identity;
            Debug.LogWarning(
                $"PlayerSpawner: no PlayerSpawnPoint in '{floorScene.name}'. Using fallback {fallbackPosition}.",
                this);
        }

        spawnedPlayer = Instantiate(playerPrefab, position, rotation);
        spawnedPlayer.name = playerPrefab.name;
        SceneManager.MoveGameObjectToScene(spawnedPlayer, gameObject.scene);
        AssignCameraFollow(spawnedPlayer.transform);
    }

    private void AssignCameraFollow(Transform playerTransform)
    {
        CameraFollow follow = cameraFollow;
        if (follow == null && Camera.main != null)
        {
            follow = Camera.main.GetComponent<CameraFollow>();
        }

        if (follow == null)
        {
            follow = FindFirstObjectByType<CameraFollow>();
        }

        if (follow == null)
        {
            Debug.LogWarning("PlayerSpawner: no CameraFollow found to assign.", this);
            return;
        }

        follow.SetTarget(playerTransform);
        cameraFollow = follow;
    }

    private string ResolveFloorSceneName()
    {
        FloorSceneLoader loader = GetComponent<FloorSceneLoader>();
        if (loader != null && !string.IsNullOrWhiteSpace(loader.InitialFloorSceneName))
        {
            return loader.InitialFloorSceneName;
        }

        return "GroundFloor";
    }

    private static PlayerSpawnPoint FindSpawnPoint(Scene floorScene)
    {
        GameObject[] roots = floorScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            PlayerSpawnPoint spawnPoint = roots[i].GetComponentInChildren<PlayerSpawnPoint>(true);
            if (spawnPoint != null)
            {
                return spawnPoint;
            }
        }

        return FindFirstObjectByType<PlayerSpawnPoint>();
    }

    private static bool IsLoaded(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded;
    }
}
