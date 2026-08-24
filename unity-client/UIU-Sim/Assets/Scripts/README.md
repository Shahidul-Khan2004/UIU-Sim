# Player Prototype Scripts

## `PlayerMovement.cs`

Moves a player GameObject with Unity's `CharacterController` component.

- `W`, `A`, `S`, and `D` move relative to the third-person camera's horizontal direction.
- `Space` jumps when the character is grounded.
- Gravity is applied every frame.
- The character turns smoothly toward its movement direction.

### Attach it

1. Create an empty GameObject named `Player` and place it above the ground.
2. Add **Character Controller** in the Inspector, then add **Player Movement**.
3. Assign the scene's Main Camera to **Camera Transform**. This is optional if the camera uses the `MainCamera` tag before play starts.
4. Adjust the Character Controller's height, radius, and centre to fit any future player model.

## `CameraFollow.cs`

Creates a smooth third-person camera that follows and rotates around a target.

- Mouse movement rotates the camera.
- **Distance** and **Height** control the camera offset from the player.
- **Follow Smooth Time** controls the follow responsiveness.

### Attach it

1. Select the scene's Main Camera.
2. Add **Camera Follow**.
3. Drag the `Player` GameObject from the Hierarchy into the **Target** field.
4. Start with Distance `5` and Height `2`, then tune them in the Inspector as needed.

## Scene checklist

1. Add a ground object with a collider, such as **GameObject > 3D Object > Plane**.
2. Place the `Player` above that ground object.
3. Ensure the camera is tagged `MainCamera` if relying on Player Movement's automatic camera lookup.
4. Enter Play mode. Click the Game view if needed; then use WASD, mouse, and Space.

The scripts use the installed Unity Input System package directly. In **Edit > Project Settings > Player > Active Input Handling**, select **Input System Package (New)** or **Both**.
