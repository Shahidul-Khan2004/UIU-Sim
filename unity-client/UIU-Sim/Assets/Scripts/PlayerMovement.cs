using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves a player using a CharacterController and the Unity Input System.
/// Movement is relative to the player transform's forward and right vectors.
/// Rotation is handled entirely by FirstPersonLook.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    [Header("Jumping and Gravity")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController characterController;
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleMovement();
        HandleGravityAndJump();
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        // Read WASD as a two-dimensional input vector: X is left/right and Y is forward/back.
        Vector2 input = ReadMovementInput();

        if (input.sqrMagnitude < 0.01f)
        {
            return;
        }

        // Build the move direction relative to the player's own facing.
        // FirstPersonLook already yaws the player transform, so forward/right are correct.
        Vector3 moveDirection = (transform.right * input.x + transform.forward * input.y).normalized;
        characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
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
