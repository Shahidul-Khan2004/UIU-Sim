using System.Collections;
using UIU.Simulator.Building.Generation;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instantiates the player into the persistent Main scene after the additive floor environment is ready.
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
