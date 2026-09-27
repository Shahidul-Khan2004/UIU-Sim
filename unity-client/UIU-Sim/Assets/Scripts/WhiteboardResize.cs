using UnityEngine;

// Attach this to the root of the Whiteboard. It builds the board surface,
// frame, and marker tray as child cubes, and keeps them sized correctly
// whenever you change Width / Height in the Inspector.
[ExecuteAlways]
[DisallowMultipleComponent]
public class WhiteboardResize : MonoBehaviour
{
    [Header("Board Size (meters)")]
    public float width = 1.5f;
    public float height = 1.0f;
    [Range(0.01f, 0.2f)] public float thickness = 0.03f;

    [Header("Frame")]
    public bool showFrame = true;
    [Range(0.01f, 0.15f)] public float frameThickness = 0.04f;
    [Range(0.01f, 0.15f)] public float frameDepth = 0.05f;

    [Header("Marker Tray")]
    public bool showTray = true;
    [Range(0.02f, 0.15f)] public float trayDepth = 0.06f;
    [Range(0.02f, 0.1f)] public float trayHeight = 0.03f;

    [Header("Parts (auto-created — leave empty)")]
    public Transform boardSurface;
    public Transform frameTop;
    public Transform frameBottom;
    public Transform frameLeft;
    public Transform frameRight;
    public Transform tray;

    void OnValidate() => Rebuild();
    void OnEnable() => Rebuild();

    public void Rebuild()
    {
        if (this == null) return;

        EnsurePart(ref boardSurface, "Board Surface");
        EnsurePart(ref frameTop, "Frame Top");
        EnsurePart(ref frameBottom, "Frame Bottom");
        EnsurePart(ref frameLeft, "Frame Left");
        EnsurePart(ref frameRight, "Frame Right");
        EnsurePart(ref tray, "Marker Tray");

        boardSurface.localPosition = Vector3.zero;
        boardSurface.localRotation = Quaternion.identity;
        boardSurface.localScale = new Vector3(width, height, thickness);

        frameTop.gameObject.SetActive(showFrame);
        frameBottom.gameObject.SetActive(showFrame);
        frameLeft.gameObject.SetActive(showFrame);
        frameRight.gameObject.SetActive(showFrame);

        if (showFrame)
        {
            float frameZ = -(thickness * 0.5f + frameDepth * 0.5f) - 0.001f;

            Vector3 hScale = new Vector3(width + frameThickness * 2f, frameThickness, frameDepth);
            frameTop.localScale = hScale;
            frameTop.localPosition = new Vector3(0f, height * 0.5f + frameThickness * 0.5f, frameZ);

            frameBottom.localScale = hScale;
            frameBottom.localPosition = new Vector3(0f, -(height * 0.5f + frameThickness * 0.5f), frameZ);

            Vector3 vScale = new Vector3(frameThickness, height, frameDepth);
            frameLeft.localScale = vScale;
            frameLeft.localPosition = new Vector3(-(width * 0.5f + frameThickness * 0.5f), 0f, frameZ);

            frameRight.localScale = vScale;
            frameRight.localPosition = new Vector3(width * 0.5f + frameThickness * 0.5f, 0f, frameZ);
        }

        tray.gameObject.SetActive(showTray);
        if (showTray)
        {
            tray.localScale = new Vector3(width * 0.9f, trayHeight, trayDepth);
            tray.localPosition = new Vector3(
                0f,
                -(height * 0.5f) - frameThickness - trayHeight * 0.5f,
                thickness * 0.5f + trayDepth * 0.3f);
        }
    }

    void EnsurePart(ref Transform part, string partName)
    {
        if (part != null) return;

        Transform existing = transform.Find(partName);
        if (existing != null)
        {
            part = existing;
            return;
        }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = partName;
        go.transform.SetParent(transform, false);
        part = go.transform;
    }
}
