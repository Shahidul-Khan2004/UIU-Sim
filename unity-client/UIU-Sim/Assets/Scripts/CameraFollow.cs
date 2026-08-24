using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Provides a smooth, mouse-controlled third-person camera that orbits a player target.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Position")]
    [Tooltip("How far behind the player the camera sits.")]
    [SerializeField, Min(0f)] private float distance = 5f;
    [Tooltip("Height of the camera pivot above the player's position.")]
    [SerializeField] private float height = 2f;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.12f;

    [Header("Mouse Look")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.15f;
    [SerializeField, Range(-89f, 0f)] private float minimumPitch = -35f;
    [SerializeField, Range(0f, 89f)] private float maximumPitch = 70f;

    private float yaw;
    private float pitch = 15f;
    private Vector3 followVelocity;

    private void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }

        // Lock the cursor during play so mouse movement controls the camera naturally.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        ReadMouseLook();

        // Rotate an offset behind the target, then ease the camera to that desired position.
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * height;
        Vector3 desiredPosition = pivot - cameraRotation * Vector3.forward * distance;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            followSmoothTime);

        // Always look at the raised pivot so the player stays in view.
        transform.rotation = cameraRotation;
    }

    private void ReadMouseLook()
    {
        if (Mouse.current == null)
        {
            return;
        }

        // Mouse delta is supplied by the Unity Input System each frame.
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, minimumPitch, maximumPitch);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnApplicationQuit()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
