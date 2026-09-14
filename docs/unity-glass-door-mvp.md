# University Glass Door MVP

A single glass leaf and fixed clear transom inspired by the supplied campus
corridor photo. Uses Unity cubes/cylinders and five generated URP Lit materials;
no textures, imported models, or runtime scripts are required.

## Assets and rebuilding

Unity project: `unity-client/UIU-Sim`.

Everything is under `Assets/Art/Doors/UniversityGlassDoor_MVP/`:

- `Editor/BuildUniversityGlassDoorMVP.cs`
- `Materials/MAT_Frame_Aluminum.mat`
- `Materials/MAT_Glass_Clear.mat`
- `Materials/MAT_Handle_Metal.mat`
- `Materials/MAT_Door_Stripe.mat`
- `Materials/MAT_Door_FrostedMarking.mat`
- `Prefabs/PF_UniversityGlassDoor_MVP.prefab`

All assets/folders include Unity-generated `.meta` files. In Unity, run
**Tools > UIU Simulator > Build University Glass Door MVP**. Missing folders,
materials, and the prefab are created automatically. Subsequent runs update the
same assets and preserve their references; generated material/geometry settings
are restored to the builder defaults. Duplicate the prefab/materials elsewhere
before making custom variants that should survive a rebuild.

The builder constructs geometry in a temporary preview scene. In interactive
Edit Mode it adds one Undo-supported example at the origin only when the active
scene is clean, is not a Prefab Stage, and contains no instance of this prefab.
It never saves that scene. Dirty scenes and batch-mode builds receive no example.

Batch entry point: `BuildUniversityGlassDoorMVP.BuildDoor`.

## Dimensions and wall placement

At root scale `(1, 1, 1)`, one Unity unit is one metre.

| Measurement | Metres |
| --- | --- |
| Overall closed prefab, including both pull handles (W × H × D) | **1.100 × 2.650 × 0.134** |
| Outer fixed frame (W × H × D) | **1.100 × 2.650 × 0.090** |
| Recommended wall cut (W × H) | **1.120 × 2.660** |
| Minimum flush wall cut (W × H) | 1.100 × 2.650 |
| Moving leaf including slim rails (W × H × D) | 1.020 × 2.100 × 0.020 |
| Fixed transom glass (W × H × D) | 1.030 × 0.465 × 0.010 |
| Frame face width / leaf rail face width | 0.035 / 0.014 |
| Closed leaf clearance at each side / top / floor | 0.005 / 0.005 / 0.010 |
| Each pull length / diameter | 0.500 / 0.022 |

The recommended cut allows 10 mm installation clearance at each side and above
the outer frame. Cut all the way through the existing wall, starting at floor
level. Centre the prefab across the cut and align its root Y with the floor;
there is no bottom threshold. The root is at `(0, 0, 0)` with identity rotation
and scale. Closed bounds are X `[-0.550, 0.550]`, Y `[0, 2.650]`, and Z
`[-0.067, 0.067]`. The front faces local `-Z`.

## Opening and collision

`DoorPivot` is at `(-0.510, 0, -0.010)`, on the front-left hinge edge.
`DoorLeaf` has a 10 mm local Z offset so its closed geometry remains centred in
the frame. Rotate **DoorPivot local Y from 0° to +90°** to open toward local `-Z`.
This is the intended swing; negative-Y or beyond-90° swings are not designed to
clear the fixed surround. The glass, rails, both handles, markings, and one
panel BoxCollider all move with the pivot.

The four fixed frame pieces and transom have BoxColliders. Decorative leaf
pieces have no additional colliders. There are six BoxColliders total, no
MeshColliders, and no Rigidbody or interaction behaviour. Add the interaction
script later if gameplay opening is needed.

## Materials

Glass uses nearly neutral `(0.97, 0.975, 0.98)` colour, alpha `0.07`, zero
metallic, and smoothness `0.94`. URP transparent blending, preserve-specular
keywords, separate RGB/alpha blend factors, RenderType, queue, and disabled
depth writes/shadow casting are configured together. Back-face culling on the
closed glass cubes leaves one visible surface from either viewing direction.

The restrained marking is a 100 mm high translucent white band, with a 14 mm
orange stripe at 1.05 m. Their outward surfaces are offset from the glass to
avoid z-fighting and are visible from either side. The main glass is not frosted.
Metal has a smooth charcoal finish; the silver pulls approximate brushed metal
through roughness alone. Reflections depend on the scene's lighting/probes.

## Validation

Validated on 2026-09-14 with Unity **6000.3.23f1**, in an isolated copy of the
project. Baseline and changed-project compilation both passed. The existing
`CanteenSceneSetup.cs:24` unused-variable warning remains; no new C# or shader
errors were found. Batch runs also reported an existing .NET SDK lookup message
at shutdown and still exited successfully.

- 487 automated checks passed: measured bounds, hierarchy/references, six
  BoxColliders, a 0–90° opening sweep in 1° increments, closed/open passage
  collision, stable prefab object IDs and material GUIDs, material-state repair
  on rebuild, and preview-scene cleanup with active-scene state preserved.
- URP renders of the front, back, and 90° open door were visually inspected.
  Background objects remain visible through the glass and the markings appear
  from both sides.
- 17 checks in an interactive Unity process passed: one example in a clean
  scene, Undo/Redo, no duplicate on rebuild in a clean scene containing an
  instance, and preservation of unsaved edits in a dirty scene.
- The copied prefab and materials match the validated Unity output. Existing
  door assets and the pre-existing scene/settings/material edits match the
  initial project snapshot.

Temporary evidence: `/tmp/uiu-glass-door-validation-result.txt`,
`/tmp/uiu-glass-door-build-validation.log`,
`/tmp/uiu-glass-door-render-validation.log`, and
`/tmp/uiu-glass-door-{front,back,open}.png`. Interactive results are in
`/tmp/uiu-glass-door-interactive-validation.txt`. The validation project and helpers
remain outside the repository and are not required to use or rebuild the door.
Gameplay interaction and a player build were outside this asset-only validation.

Manual scene work: drag in the prefab if an example was not added, place it at
floor level, and cut/remove the matching section of the ProBuilder wall. The
builder does not cut walls or add door interaction behaviour.
