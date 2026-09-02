using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person look. Mouse X yaws the player body; Mouse Y pitches only the camera.
/// Reads the existing project-wide <c>Player/Look</c> Input System action (no new bindings).
/// </summary>
public class FirstPersonLook : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera child to pitch. Assign FirstPersonCamera on the Player prefab.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Look")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.15f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    private InputAction lookAction;
    private float pitch;

    private void Awake()
    {
        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
            }
        }

        // Reuse InputSystem_Actions → Player/Look (Pointer delta / gamepad right stick).
        lookAction = InputSystem.actions != null
            ? InputSystem.actions.FindAction("Player/Look")
            : null;

        if (lookAction == null)
        {
            Debug.LogWarning(
                "[FirstPersonLook] Player/Look action not found on the project-wide Input Actions asset.",
                this);
        }
    }

    private void OnEnable()
    {
        lookAction?.Enable();
        LockCursor();
    }

    private void OnDisable()
    {
        UnlockCursor();
    }

    private void Update()
    {
        HandleCursorToggle();

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        HandleLook();
    }

    private void HandleCursorToggle()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }
    }

    private void HandleLook()
    {
        if (lookAction == null)
        {
            return;
        }

        Vector2 lookDelta = lookAction.ReadValue<Vector2>();
        if (lookDelta.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // Horizontal look turns the whole player (and the camera child with it).
        transform.Rotate(0f, lookDelta.x * mouseSensitivity, 0f);

        if (cameraTransform == null)
        {
            return;
        }

        // Vertical look tilts only the camera, clamped so the view cannot flip.
        pitch = Mathf.Clamp(pitch - lookDelta.y * mouseSensitivity, minPitch, maxPitch);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isActiveAndEnabled)
        {
            LockCursor();
        }
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
