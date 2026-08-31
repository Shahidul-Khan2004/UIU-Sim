using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves a player using a CharacterController and the Unity Input System.
/// Movement is relative to the assigned camera's horizontal facing direction.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera used to determine the direction of WASD movement.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float rotationSmoothTime = 0.1f;

    [Header("Jumping and Gravity")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController characterController;
    private float verticalVelocity;
    private float rotationVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        // Use the scene's main camera by default, while still allowing an explicit assignment.
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        HandleMovement();
        HandleGravityAndJump();
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null || cameraTransform == null)
        {
            return;
        }

        // Read WASD as a two-dimensional input vector: X is left/right and Y is forward/back.
        Vector2 input = ReadMovementInput();
        Vector3 inputDirection = new Vector3(input.x, 0f, input.y).normalized;

        if (inputDirection.sqrMagnitude < 0.01f)
        {
            return;
        }

        // Ignore the camera's vertical tilt so movement remains level on the ground.
        float cameraYaw = cameraTransform.eulerAngles.y;
        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraYaw;

        // Turn smoothly toward the movement direction for third-person character movement.
        float smoothedAngle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref rotationVelocity,
            rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        characterController.Move(moveDirection.normalized * moveSpeed * Time.deltaTime);
    }

    private void HandleGravityAndJump()
    {
        // Keep the controller pressed to the ground instead of accumulating downward speed.
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        // Space jumps only while grounded. The formula reaches the selected jump height under gravity.
        if (characterController.isGrounded && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
        characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private static Vector2 ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed) horizontal += 1f;
        if (keyboard.sKey.isPressed) vertical -= 1f;
        if (keyboard.wKey.isPressed) vertical += 1f;

        return new Vector2(horizontal, vertical);
    }
}
