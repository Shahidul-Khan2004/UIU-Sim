# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `unity-client/UIU-Sim`
- Last analyzed: 2026-08-31
- Last analyzed commit: `833f863b70a74089a956e9bd636d14724dab403f`
- Status: MVP client with a persistent Main scene, separate floor scenes, a data-driven Editor generator, and a generated playable GroundFloor blockout.

## Confirmed Environment

- Unity version: 6000.3.23f1 (Unity 6.3), revision `09d2ecc7fb28`
- Render pipeline: Universal Render Pipeline (URP)
- Input system: Input System package only (`activeInputHandler: 1`)
- Requested target: Windows PC; the checked-out project is currently being edited from Linux.
- Scale/orientation contract: 1 unit = 1 metre; +Z north, +X east; main entrance faces south.

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP is active through the PC pipeline asset | Confirmed | `Packages/manifest.json`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/QualitySettings.asset` |
| Input | Input System 1.20.0 is declared and first-party scripts use `UnityEngine.InputSystem` | Confirmed | `Packages/manifest.json`, `Assets/Scripts/PlayerMovement.cs`, `Assets/Scripts/CameraFollow.cs` |
| Testing | Unity Test Framework with first-party EditMode and PlayMode building tests | Confirmed | `Packages/manifest.json`, `Assets/Tests/` |
| Networking | No networking implementation is present | Confirmed | package configuration and first-party code search |
| Package state | Manifest and lock-file versions differ for several built-in packages, including URP and Test Framework | Confirmed | `Packages/manifest.json`, `Packages/packages-lock.json` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/Scripts` | First-party runtime scripts | Confirmed | player/camera scripts |
| `Assets/Scripts/Data` | ScriptableObject floor schema | Confirmed | `FloorLayoutData.cs` |
| `Assets/Scripts/Generation` | Runtime metadata, geometry calculations, and additive scene loading | Confirmed | generation assembly |
| `Assets/Scripts/Editor` | Editor-only building tooling | Confirmed | `UIUBuildingGenerator.cs` |
| `Assets/Scenes/Main` | Persistent player/camera/manager scene | Confirmed | uncommitted `UIU_Main.unity` |
| `Assets/Scenes/Floors` | Independently owned floor scenes | Confirmed | `GroundFloor.unity` |
| `Assets/Prefabs` | Reusable runtime prefabs | Confirmed | `Player.prefab` |
| `Assets/Materials`, `Assets/Settings` | Blockout materials and URP assets | Confirmed | asset inventory |
| `Assets/Data/Floors` | Per-floor authoritative layout assets | Confirmed | `GroundFloorLayout.asset` |
| `Assets/Tests` | EditMode architecture checks and PlayMode startup smoke test | Confirmed | test assemblies |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `Assembly-CSharp` | Prototype player and camera runtime code | UnityEngine, Input System | No first-party runtime asmdef existed at analysis time |
| `UIU.Simulator.Building.Data` | Floor layout ScriptableObject schema | UnityEngine | Runtime-safe data boundary |
| `UIU.Simulator.Building.Generation` | Geometry calculations, generation metadata, and floor scene loading | Data assembly | Runtime-safe; no UnityEditor reference |
| `UIU.Simulator.Building.Editor` | Editor window and primitive floor generation | Data and Generation assemblies, UnityEditor | Editor-only |
| `UIU.Simulator.Building.EditorTests` | Data, hierarchy, collision, Main scene, and build-scene checks | Data and Generation assemblies | Editor-only test assembly |
| `UIU.Simulator.Building.PlayModeTests` | Main-to-GroundFloor runtime smoke test | Generation assembly | Test assembly |

## Scenes And Startup Flow

- Build scenes: `UIU_Main`, `GroundFloor`, then `Floor01` through `Floor10`, all enabled.
- Startup scene: `Assets/Scenes/Main/UIU_Main.unity`.
- Floor scenes: `Assets/Scenes/Floors/GroundFloor.unity` plus independent placeholders through `Floor10.unity`.
- Scene loading flow: `FloorSceneLoader` loads GroundFloor additively from Main.
- Existing worktree note: `MainUniversity.unity` is being replaced by `UIU_Main.unity`; preserve this user-owned reorganization.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Gameplay | Small MonoBehaviour-based prototype | Confirmed | `PlayerMovement.cs`, `CameraFollow.cs` |
| Input | Direct Input System device polling | Confirmed | player/camera scripts |
| Building generation | Editor-only primitive generation from selected floor data | Confirmed | `Assets/Scripts/Editor/UIUBuildingGenerator.cs` |
| Floor data | Per-floor ScriptableObject assets are authoritative; generated scene objects are committed output | Confirmed | data schema, GroundFloor asset, generated scene metadata |
| Scene ownership | Main owns player/camera/managers; each floor scene owns only that floor | Confirmed | Main and Floors scenes |

## Coding Conventions

- Namespace style: legacy player scripts use the global namespace; building assemblies use `UIU.Simulator.Building.*`.
- Serialized fields: `[SerializeField] private`, with headers, tooltips, and range attributes.
- Lifecycle: movement in `Update`, camera follow in `LateUpdate`.
- Comments/docs: XML summaries plus comments for non-obvious behavior.

## Testing And Validation

- EditMode tests: 5 building/data/scene tests passed on 2026-08-31.
- PlayMode tests: 1 Main startup/additive GroundFloor smoke test passed on 2026-08-31.
- Player build: Linux x86_64 fallback build passed on 2026-08-31; Windows build was unavailable.
- CI/build validation: no Unity CI workflow found.
- Local editor: Unity 6000.3.23f1 executable is available at `/home/shahidul/Unity/Hub/Editor/6000.3.23f1/Editor/Unity`.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Repository and serialized asset inspection | available | local workspace |
| Unity batch-mode compile/test/Linux build | available | matching local Unity executable and completed validation |
| Connected Unity MCP/editor inspection | unavailable | no Unity MCP tools exposed in this session |
| Interactive Play Mode and Console inspection | unavailable | no connected Editor process/tooling |
| Runtime screenshot/visual inspection | unverified | depends on batch rendering support |

## Important Constraints

- Do not change building orientation or metre scale.
- Keep persistent gameplay objects in Main and floor-owned geometry in separate floor scenes.
- Do not change package dependencies or shared URP/lighting settings for the building MVP.
- Preserve visible `.meta` files and force-text serialization for Git collaboration.
- Treat the current scene rename/reorganization as user-owned work.

## Unknowns And Confidence

- The supplied evacuation-map image is not present in the workspace; the requested coordinates are the authoritative blockout source.
- Windows build support is unavailable in the installed editor; only `LinuxStandaloneSupport` is installed.
- The package manifest/lock discrepancy may be normalized by Unity during package resolution; do not edit either file as part of this feature.

## Source Files Inspected

- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/ProjectSettings.asset`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/QualitySettings.asset`
- `ProjectSettings/EditorSettings.asset`
- `ProjectSettings/VersionControlSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Assets/Scripts/PlayerMovement.cs`
- `Assets/Scripts/CameraFollow.cs`
- `Assets/Scripts/Data/FloorLayoutData.cs`
- `Assets/Scripts/Generation/FloorSceneLoader.cs`
- `Assets/Scripts/Generation/FloorGeometry.cs`
- `Assets/Scripts/Editor/UIUBuildingGenerator.cs`
- `Assets/Prefabs/Player.prefab`
- `Assets/Data/Floors/GroundFloorLayout.asset`
- Main/Floor scenes and `ProjectSettings/EditorBuildSettings.asset`
- first-party building tests and their result artifacts under `/tmp`
- repository README and files under `docs/`

<!-- unity-onboarding:generated:end -->
