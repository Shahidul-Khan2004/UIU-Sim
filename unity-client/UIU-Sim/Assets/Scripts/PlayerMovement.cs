using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// MVP first-person CharacterController movement: walk, sprint, jump with coyote time
/// and jump buffering. Yaw comes from <see cref="FirstPersonLook"/>; this script only moves.
/// Modal UIs disable this component; elevator travel may disable the CharacterController.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 7.5f;
    [SerializeField, Min(0f)] private float acceleration = 18f;
    [SerializeField, Min(0f)] private float deceleration = 22f;

    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.4f;
    [SerializeField] private float gravity = -22f;
    [SerializeField] private float groundedVerticalVelocity = -2f;

    [Header("Jump Forgiveness")]
    [SerializeField, Min(0f)] private float coyoteTime = 0.10f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;

    private CharacterController characterController;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private bool grounded;
    private bool sprinting;

    /// <summary>True when the controller reported stable ground contact last move.</summary>
    public bool IsGrounded => grounded;

    /// <summary>True while sprint input is held and there is move input.</summary>
    public bool IsSprinting => sprinting;

    /// <summary>Current combined horizontal + vertical velocity.</summary>
    public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        // Fresh enable after a modal: do not carry stale horizontal motion.
        ResetTransientMovementState();
    }

    private void OnDisable()
    {
        ResetTransientMovementState();
    }

    private void Update()
    {
        if (characterController == null || !characterController.enabled)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            Tick(Time.deltaTime, Vector2.zero, jumpPressedThisFrame: false, sprintHeld: false);
            return;
        }

        Vector2 moveInput = ReadMovementInput(keyboard);
        bool jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        bool sprintHeld = keyboard.leftShiftKey.isPressed;
        Tick(Time.deltaTime, moveInput, jumpPressed, sprintHeld);
    }

    /// <summary>
    /// One simulation step with explicit input. Called from Update; EditMode tests invoke via reflection.
    /// </summary>
    private void Tick(float deltaTime, Vector2 moveInput, bool jumpPressedThisFrame, bool sprintHeld)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        if (characterController == null || !characterController.enabled)
        {
            return;
        }

        UpdateJumpTimers(deltaTime, jumpPressedThisFrame);
        UpdateHorizontalVelocity(deltaTime, moveInput, sprintHeld);
        ApplyGravity(deltaTime);
        // Jump after gravity so launch velocity is exact for jumpHeight (not reduced same frame).
        TryConsumeJump();

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = characterController.Move(velocity * deltaTime);
        ResolveCollisions(flags);
    }

    private void UpdateJumpTimers(float deltaTime, bool jumpPressedThisFrame)
    {
        if (jumpPressedThisFrame)
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= deltaTime;
            if (jumpBufferCounter < 0f)
            {
                jumpBufferCounter = 0f;
            }
        }

        if (grounded)
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= deltaTime;
            if (coyoteCounter < 0f)
            {
                coyoteCounter = 0f;
            }
        }
    }

    private void UpdateHorizontalVelocity(float deltaTime, Vector2 moveInput, bool sprintHeld)
    {
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        bool hasMoveInput = moveInput.sqrMagnitude > 0.0001f;
        sprinting = sprintHeld && hasMoveInput;

        Vector3 targetHorizontal = Vector3.zero;
        if (hasMoveInput)
        {
            Vector3 wishDir = transform.right * moveInput.x + transform.forward * moveInput.y;
            wishDir.y = 0f;
            if (wishDir.sqrMagnitude > 0.0001f)
            {
                wishDir.Normalize();
            }

            float targetSpeed = sprinting ? sprintSpeed : walkSpeed;
            targetHorizontal = wishDir * targetSpeed;
        }

        float rate = hasMoveInput ? acceleration : deceleration;
        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetHorizontal,
            rate * deltaTime);
    }

    private void TryConsumeJump()
    {
        if (jumpBufferCounter <= 0f || coyoteCounter <= 0f)
        {
            return;
        }

        // Physically consistent launch for the configured apex height under constant gravity.
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        grounded = false;
    }

    private void ApplyGravity(float deltaTime)
    {
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedVerticalVelocity;
            return;
        }

        verticalVelocity += gravity * deltaTime;
    }

    private void ResolveCollisions(CollisionFlags flags)
    {
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        // isGrounded reflects the Move we just performed; Below is the explicit floor hit flag.
        bool hitBelow = (flags & CollisionFlags.Below) != 0 || characterController.isGrounded;

        // While rising (jump), ignore floor contact so one press cannot double-jump on the same ledge.
        if (hitBelow && verticalVelocity <= 0f)
        {
            grounded = true;
            verticalVelocity = groundedVerticalVelocity;
        }
        else if (!hitBelow)
        {
            grounded = false;
        }
    }

    private void ResetTransientMovementState()
    {
        horizontalVelocity = Vector3.zero;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        sprinting = false;
    }

    private static Vector2 ReadMovementInput(Keyboard keyboard)
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.wKey.isPressed)
        {
            vertical += 1f;
        }

        return new Vector2(horizontal, vertical);
    }
}
