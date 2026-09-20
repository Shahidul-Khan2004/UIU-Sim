using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggles between the player's first-person and third-person cameras with the P key.
/// Does not own movement or look; those stay on <see cref="PlayerMovement"/> and
/// <see cref="FirstPersonLook"/>. Attach to the Player root and assign both camera
/// GameObjects in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraSwitcher : MonoBehaviour
{
    /// <summary>Which view is currently driving the player's render camera.</summary>
    public enum CameraMode
    {
        FirstPerson,
        ThirdPerson
    }

    [Header("Cameras")]
    [Tooltip("Child camera used for first-person view (FirstPersonCamera).")]
    [SerializeField] private GameObject firstPersonCamera;

    [Tooltip("Child camera placed behind and above the player (ThirdPersonCamera).")]
    [SerializeField] private GameObject thirdPersonCamera;

    [Header("Mode")]
    [Tooltip("Starting view when the player spawns. First person matches the existing MVP.")]
    [SerializeField] private CameraMode startingMode = CameraMode.FirstPerson;

    private CameraMode currentMode;

    /// <summary>Active camera mode after the last switch or startup apply.</summary>
    public CameraMode CurrentMode => currentMode;

    private void Awake()
    {
        // Prefer explicit Inspector refs; fall back to child names if left empty.
        if (firstPersonCamera == null || thirdPersonCamera == null)
        {
            TryResolveCamerasFromChildren();
        }

        if (firstPersonCamera == null || thirdPersonCamera == null)
        {
            Debug.LogWarning(
                "[CameraSwitcher] Assign FirstPersonCamera and ThirdPersonCamera in the Inspector.",
                this);
        }
    }

    private void Start()
    {
        // Apply once at startup so only one camera renders (and one AudioListener is active).
        ApplyMode(startingMode);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // New Input System only — never UnityEngine.Input.
        if (keyboard.pKey.wasPressedThisFrame)
        {
            ToggleMode();
        }
    }

    /// <summary>Switches to the other camera mode.</summary>
    public void ToggleMode()
    {
        CameraMode next = currentMode == CameraMode.FirstPerson
            ? CameraMode.ThirdPerson
            : CameraMode.FirstPerson;
        ApplyMode(next);
    }

    /// <summary>
    /// Enables exactly one camera GameObject and disables the other.
    /// First person: FirstPersonCamera on, ThirdPersonCamera off.
    /// Third person: opposite.
    /// </summary>
    public void ApplyMode(CameraMode mode)
    {
        currentMode = mode;

        bool useFirstPerson = mode == CameraMode.FirstPerson;
        SetCameraActive(firstPersonCamera, useFirstPerson);
        SetCameraActive(thirdPersonCamera, !useFirstPerson);
    }

    private void TryResolveCamerasFromChildren()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == transform)
            {
                continue;
            }

            if (firstPersonCamera == null && child.name == "FirstPersonCamera")
            {
                firstPersonCamera = child.gameObject;
            }
            else if (thirdPersonCamera == null && child.name == "ThirdPersonCamera")
            {
                thirdPersonCamera = child.gameObject;
            }
        }
    }

    /// <summary>
    /// Activates or deactivates a camera child. Uses SetActive so the Camera and
    /// AudioListener on that object stay in sync (only one listener at a time).
    /// </summary>
    private static void SetCameraActive(GameObject cameraObject, bool active)
    {
        if (cameraObject == null)
        {
            return;
        }

        if (cameraObject.activeSelf != active)
        {
            cameraObject.SetActive(active);
        }

        // Keep the Camera component enabled whenever its GameObject is active.
        Camera cam = cameraObject.GetComponent<Camera>();
        if (cam != null)
        {
            cam.enabled = active;
        }
    }
}
