UIU FLOOR MARKERS — 3D MESHES
================================

Assets:
FloorMarker_G.obj
FloorMarker_1.obj ... FloorMarker_10.obj
(each has a matching .mtl)

These are actual extruded 3D meshes, approximately 0.50 m tall and 0.045 m deep.

UNITY:
1. Put the OBJ + MTL files in Assets/Art/FloorMarkers/.
2. Drag a marker into the Scene.
3. Create a URP/Lit material named FloorMarker_Chrome.
4. Set Base Color to light silver, Metallic = 1, Smoothness = 0.9–1.0.
5. Assign the material to the marker's Mesh Renderer.
6. Rotate/place it on the wall.
7. Drag the configured object from Hierarchy into Assets to make a Prefab.
8. Duplicate the prefab wherever needed.

No image texture is required for the chrome metal. The shine comes from the Unity URP/Lit material plus scene lighting/reflections.

The font is a clean bold approximation of the reference, not an exact copy.
