# UIU Procedural Building Workflow

## Scope and coordinate contract

This system is the architectural MVP for UIU Simulator. It uses Unity primitives, simple URP materials, and BoxColliders; it intentionally has no furniture, decorative art, NPCs, or functional elevators.

- 1 Unity unit = 1 metre.
- +Z is north, -Z is south, +X is east, and -X is west.
- Main entrance door centre is `(0, 0, 0)`.
- The GroundFloor footprint extends north from `z = 0` to `z = 90` and from `x = -50` to `x = 50`.
- The Main-scene player starts at `(0, 0, -8)`, facing north.
- Player capsule: 1.8 m high, 0.3 m radius; camera pivot/eye height: 1.6 m.
- `FloorLayoutData.baseElevation` raises a whole floor without changing its local X/Z connection coordinates.

Do not rotate the building or offset individual floor scenes. On every floor, retain these connection IDs and X/Z positions:

| ID | Type | Position |
| --- | --- | --- |
| `Elevator_A` | Elevator | `(15, 0, 15)` |
| `Elevator_B` | Elevator | `(0, 0, 45)` |
| `Stair_SW` | Stair | `(-40, 0, 20)` |
| `Stair_SE` | Stair | `(40, 0, 20)` |
| `Stair_NW` | Stair | `(-40, 0, 70)` |
| `Stair_NE` | Stair | `(40, 0, 70)` |

## Folder structure

```text
Assets/
├── Data/
│   └── Floors/
│       └── GroundFloorLayout.asset
├── Materials/
│   └── UIUBlockout/
├── Prefabs/
│   └── Player.prefab
├── Scenes/
│   ├── Main/
│   │   └── UIU_Main.unity
│   └── Floors/
│       ├── GroundFloor.unity
│       └── Floor01.unity ... Floor10.unity
├── Scripts/
│   ├── Data/
│   │   └── FloorLayoutData.cs
│   ├── Generation/
│   │   ├── FloorGeometry.cs
│   │   ├── FloorGenerationMetadata.cs
│   │   └── FloorSceneLoader.cs
│   └── Editor/
│       └── UIUBuildingGenerator.cs
└── Tests/
    ├── Editor/
    └── PlayMode/
```

The three building code folders have explicit assembly definitions. `UnityEditor` code remains isolated in the Editor assembly and is not included in player builds.

## Data model

Create a layout through **Assets > Create > UIU Simulator > Building > Floor Layout**.

`FloorLayoutData` stores:

- floor name, base elevation, ceiling height, wall thickness, slab thickness, and footprint;
- entrance position, opening width, and exterior approach size;
- named open zones;
- named rooms, including one doorway side, offset, and width per room;
- elevators and stairs with stable connection IDs, positions, and sizes.

Positions are area centres. The position Y is local to the floor's `baseElevation`; room/zone sizes are full X/Y/Z dimensions in metres. Room doors are openings, not functional door objects.

## GroundFloor data asset

The committed asset is `Assets/Data/Floors/GroundFloorLayout.asset`. To recreate it manually:

1. Create a Floor Layout asset at `Assets/Data/Floors/GroundFloorLayout.asset`.
2. Set floor name `Ground Floor`, base elevation `0`, ceiling height `3.5`, wall thickness `0.25`, slab thickness `0.2`, and footprint `(100, 90)`.
3. Set entrance position `(0, 0, 0)`, entrance width `4`, and approach size `(20, 12)`.
4. Add the two zones and seven rooms from the table below.
5. Add the six vertical connections from the coordinate-contract table above. The GroundFloor asset uses elevator size `(3, 3.5, 3)` and stair size `(4, 3.5, 7)`.

| Kind | Name | Position | Size | Door |
| --- | --- | --- | --- | --- |
| Zone | Entrance Lobby | `(0, 0, 5)` | `(20, 3.5, 12)` | — |
| Zone | Central Hall | `(0, 0, 25)` | `(45, 3.5, 25)` | — |
| Room | Shaheed Irfan Library G002 | `(0, 0, 55)` | `(35, 3.5, 25)` | South, 2.4 m |
| Room | Book Shop | `(-25, 0, 20)` | `(15, 3.5, 12)` | East, 1.8 m |
| Room | Guardian Lounge | `(-40, 0, 35)` | `(12, 3.5, 10)` | East, 1.8 m |
| Room | Canteen | `(-35, 0, 65)` | `(20, 3.5, 20)` | East, 2.0 m |
| Room | Admission Office | `(35, 0, 20)` | `(18, 3.5, 12)` | West, 1.8 m |
| Room | Finance Office | `(35, 0, 40)` | `(20, 3.5, 15)` | West, 1.8 m |
| Room | Medical Center | `(35, 0, 65)` | `(18, 3.5, 12)` | West, 1.8 m |

## Generate or regenerate GroundFloor

1. Open `Assets/Scenes/Floors/GroundFloor.unity` as the active scene.
2. Select `Assets/Data/Floors/GroundFloorLayout.asset`.
3. Open **Tools > UIU Simulator > Building Generator**.
4. Assign the layout asset if it was not picked automatically.
5. Choose **Generate Floor** or **Regenerate Floor**.
6. Inspect the `Generated` hierarchy, then save only `GroundFloor.unity`.

Generation replaces only the active scene's root object named `Generated`. It does not touch cameras, managers, other floor scenes, packages, or global rendering/lighting settings. **Clear Generated Objects** is Undo-aware and removes only that generated root.

Generated hierarchy:

```text
Generated/
├── Floors
├── Walls
├── Rooms
├── Doors
├── Stairs
└── Elevators
```

## Main scene and play setup

`Assets/Scenes/Main/UIU_Main.unity` owns the Player prefab instance, Main Camera, local directional light/volume, and `Game Managers/FloorSceneLoader`. At startup, the loader opens `GroundFloor` additively. The floor scene contains no player, camera, or manager.

The build scene order is already configured with `UIU_Main` first, followed by GroundFloor and Floor01–Floor10. Open `UIU_Main` and press Play. Controls use the new Input System directly:

- WASD: move relative to camera yaw.
- Mouse: look.
- Space: jump.

## Create Floor01 (or any later floor)

1. Work only in the assigned `Assets/Scenes/Floors/Floor01.unity` scene and a new `Assets/Data/Floors/Floor01Layout.asset`.
2. Create the asset with **Assets > Create > UIU Simulator > Building > Floor Layout**.
3. Set a unique floor name and its base elevation. With the current 3.5 m storey height, Floor01 starts at `3.5`; later floors can use the agreed architectural floor-to-floor height.
4. Enter that floor's rooms and zones in the shared coordinate system.
5. Copy all elevator/stair IDs, X positions, and Z positions exactly from GroundFloor. Keep their position Y at `0` so `baseElevation` provides vertical placement.
6. Open Floor01, select Floor01Layout, run the Building Generator, inspect, and save.
7. Do not add the player, cameras, game managers, or scene loading logic to a floor scene.

## Git workflow for parallel floors

- Use one branch per floor or focused generator change, for example `feature/floor-01-blockout`.
- Assign one owner to each `.unity` scene and its matching layout `.asset` during active work.
- A floor developer should normally commit only their floor scene, layout asset, and associated `.meta` files.
- Always commit Unity `.meta` files; never commit `Library`, `Temp`, `Logs`, `UserSettings`, or build output.
- Pull/rebase before opening a shared scene. Do not regenerate another developer's floor to resolve a merge conflict.
- Coordinate changes to generator scripts, Main scene, Player prefab, blockout materials, and `ProjectSettings/EditorBuildSettings.asset` before editing them.
- Keep Unity serialization set to Force Text and Version Control set to Visible Meta Files (already configured).
- Review scene diffs for unexpected lighting, navigation, or global-setting changes before merging.

The GroundFloor scene is generated output but remains committed so the project is immediately playable. Its ScriptableObject asset is the authoritative layout source.
