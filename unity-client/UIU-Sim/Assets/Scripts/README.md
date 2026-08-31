# UIU Simulator Runtime Scripts

## `PlayerMovement.cs`

Moves the persistent Main-scene player with Unity's `CharacterController` component.

- `W`, `A`, `S`, and `D` move relative to the camera's horizontal direction.
- `Space` jumps when the character is grounded.
- Gravity is applied every frame.
- The character turns smoothly toward its movement direction.

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
3. Click the Game view if needed, then use WASD, mouse, and Space.

The scripts use the installed Unity Input System package directly. Active Input Handling is already set to **Input System Package (New)**.

See `docs/unity-building-workflow.md` for building generation and team workflow details.
