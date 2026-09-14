# UIU Simulator Runtime Scripts

## `PlayerMovement.cs`

MVP first-person movement on Unity's `CharacterController` (kept; not Rigidbody).

- `W` / `A` / `S` / `D` move relative to player yaw (`transform.forward` / `transform.right` from `FirstPersonLook`).
- Hold **Left Shift** to sprint; release to walk.
- **Space** jumps with coyote time + jump buffer (reliable grounded jumps).
- One `CharacterController.Move` per frame; explicit gravity and grounded stick velocity.
- Modal UIs disable this component; elevator travel may disable the `CharacterController` temporarily.

Tunable in the Inspector: walk/sprint speeds, accel/decel, jump height, gravity, coyote/buffer times.

### Attach it

The configured `Assets/Prefabs/Player.prefab` is 1.8 m high with a 0.3 m radius and is already wired in `UIU_Main.unity`.

## `CameraFollow.cs`

Creates a smooth mouse-look camera that follows and rotates around a target. The Main scene uses a near-first-person distance and a 1.6 m eye pivot.

- Mouse movement rotates the camera.
- **Distance** and **Height** control the camera offset from the player.
- **Follow Smooth Time** controls the follow responsiveness.

### Attach it

The Main Camera is already configured in `UIU_Main.unity`.

## Scene checklist

1. Open `Assets/Scenes/Main/UIU_Main.unity`.
2. Enter Play mode; `GroundFloor` loads additively.
3. Click the Game view if needed, then use WASD, mouse, Shift (sprint), and Space.

The scripts use the installed Unity Input System package directly. Active Input Handling is already set to **Input System Package (New)**.

See `docs/unity-building-workflow.md` for building generation and team workflow details.
