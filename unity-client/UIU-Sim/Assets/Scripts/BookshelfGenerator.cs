using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Drop this on an empty GameObject. It builds a full bookshelf (frame + shelves + books)
// procedurally, so no external meshes or textures are needed. Change any field in the
// Inspector and it rebuilds automatically. Once you like the result, drag the GameObject
// from the Hierarchy into your Project window to save it as a reusable prefab.
[ExecuteAlways]
[DisallowMultipleComponent]
public class BookshelfGenerator : MonoBehaviour
{
    [Header("Overall Size (meters)")]
    [Min(0.3f)] public float width = 2.0f;
    [Min(0.3f)] public float height = 2.2f;
    [Min(0.1f)] public float depth = 0.35f;

    [Header("Shelf Structure")]
    [Range(1, 12)] public int shelfCount = 5;
    public float shelfThickness = 0.03f;
    public float sideThickness = 0.03f;
    public bool hasBackPanel = true;
    public float backPanelThickness = 0.01f;

    [Header("Books")]
    [Tooltip("Change this to get a different random arrangement without changing size.")]
    public int randomSeed = 12345;
    [Range(0f, 1f)] public float shelfFillAmount = 0.9f;
    public float bookMinThickness = 0.018f;
    public float bookMaxThickness = 0.045f;
    [Range(0.2f, 1f)] public float bookMinHeightFactor = 0.65f;
    [Range(0.2f, 1f)] public float bookMaxHeightFactor = 0.98f;
    [Range(0.3f, 1f)] public float bookDepthFactor = 0.85f;
    [Range(0f, 0.3f)] public float leaningBookChance = 0.06f;
    [Range(0f, 0.3f)] public float gapChance = 0.04f;

    [Header("Colors")]
    public Color frameColor = new Color(0.42f, 0.27f, 0.15f);
    public Color[] bookColorPalette = new Color[]
    {
        new Color(0.80f,0.15f,0.15f), new Color(0.15f,0.35f,0.75f),
        new Color(0.95f,0.75f,0.10f), new Color(0.20f,0.55f,0.25f),
        new Color(0.55f,0.20f,0.65f), new Color(0.90f,0.45f,0.10f),
        new Color(0.10f,0.60f,0.60f), new Color(0.85f,0.85f,0.90f),
        new Color(0.20f,0.20f,0.25f), new Color(0.90f,0.55f,0.65f)
    };

    [HideInInspector] public float[] shelfYPositions;

    Transform generatedRoot;
    Material sharedFrameMaterial;
    Material sharedBookMaterial;
    const string GENERATED_NAME = "__Generated";

    void OnEnable()
    {
        // If this instance already has baked geometry (e.g. it came from a saved
        // prefab), leave it alone. Otherwise build it for the first time.
        if (transform.Find(GENERATED_NAME) == null)
            Generate();
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        // Deferred because Unity doesn't allow creating/destroying objects
        // synchronously from inside OnValidate.
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            Generate();
        };
#endif
    }

    [ContextMenu("Regenerate Bookshelf")]
    public void Generate()
    {
        width = Mathf.Max(0.3f, width);
        height = Mathf.Max(0.3f, height);
        depth = Mathf.Max(0.1f, depth);
        shelfCount = Mathf.Max(1, shelfCount);

        ClearGenerated();
        EnsureMaterials();

        GameObject rootGO = new GameObject(GENERATED_NAME);
        rootGO.transform.SetParent(transform, false);
        generatedRoot = rootGO.transform;

        BuildFrame();
        BuildBooks();
    }

    void ClearGenerated()
    {
        var existing = transform.Find(GENERATED_NAME);
        if (existing != null) SafeDestroy(existing.gameObject);
    }

    void EnsureMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");
        if (lit == null) lit = Shader.Find("Diffuse");

        sharedFrameMaterial = new Material(lit);
        SetColor(sharedFrameMaterial, frameColor);

        sharedBookMaterial = new Material(lit);
    }

    void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    Transform MakeCube(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        SafeDestroy(go.GetComponent<Collider>()); // per-book colliders would be wasteful
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    void BuildFrame()
    {
        Transform frameParent = new GameObject("Frame").transform;
        frameParent.SetParent(generatedRoot, false);

        float sideY = height / 2f;
        MakeCube("Side_Left", frameParent,
            new Vector3(-width / 2f + sideThickness / 2f, sideY, depth / 2f),
            new Vector3(sideThickness, height, depth), sharedFrameMaterial);
        MakeCube("Side_Right", frameParent,
            new Vector3(width / 2f - sideThickness / 2f, sideY, depth / 2f),
            new Vector3(sideThickness, height, depth), sharedFrameMaterial);

        shelfYPositions = new float[shelfCount + 1];
        for (int i = 0; i <= shelfCount; i++)
        {
            float y = Mathf.Clamp((height / shelfCount) * i, shelfThickness / 2f, height - shelfThickness / 2f);
            shelfYPositions[i] = y;
            MakeCube($"Board_{i}", frameParent,
                new Vector3(0, y, depth / 2f),
                new Vector3(width - 2 * sideThickness, shelfThickness, depth), sharedFrameMaterial);
        }

        if (hasBackPanel)
        {
            MakeCube("Back", frameParent,
                new Vector3(0, height / 2f, depth - backPanelThickness / 2f),
                new Vector3(width, height, backPanelThickness), sharedFrameMaterial);
        }

        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, height / 2f, depth / 2f);
        col.size = new Vector3(width, height, depth);
    }

    void BuildBooks()
    {
        Random.InitState(randomSeed);
        Transform booksParent = new GameObject("Books").transform;
        booksParent.SetParent(generatedRoot, false);

        float innerLeft = -width / 2f + sideThickness + 0.01f;
        float innerRight = width / 2f - sideThickness - 0.01f;
        float usableWidth = innerRight - innerLeft;

        for (int shelf = 0; shelf < shelfCount; shelf++)
        {
            float yBottom = shelfYPositions[shelf] + shelfThickness / 2f;
            float yTopLimit = shelfYPositions[shelf + 1] - shelfThickness / 2f;
            float compartmentHeight = yTopLimit - yBottom;
            if (compartmentHeight <= 0.02f) continue;

            Transform shelfGroup = new GameObject($"Shelf_{shelf + 1}_Books").transform;
            shelfGroup.SetParent(booksParent, false);

            float x = innerLeft;
            float targetFilled = usableWidth * Mathf.Clamp01(shelfFillAmount);
            float filled = 0f;
            int bookIndex = 0;

            while (filled < targetFilled && x < innerRight - 0.005f)
            {
                if (Random.value < gapChance)
                {
                    float gap = Random.Range(0.01f, 0.04f);
                    x += gap; filled += gap;
                    continue;
                }

                float thickness = Random.Range(bookMinThickness, bookMaxThickness);
                if (x + thickness > innerRight) thickness = innerRight - x;
                if (thickness <= 0.005f) break;

                float bh = compartmentHeight * Random.Range(bookMinHeightFactor, bookMaxHeightFactor);
                float bd = depth * bookDepthFactor;
                Color c = VaryColor(bookColorPalette[Random.Range(0, bookColorPalette.Length)], 0.08f);
                bool leaning = Random.value < leaningBookChance;
                float zCenter = bd / 2f + 0.015f; // flush toward the front edge, like real spines

                Transform bookT = MakeCube($"Book_{shelf + 1}_{bookIndex}", shelfGroup,
                    new Vector3(x + thickness / 2f, yBottom + bh / 2f, zCenter),
                    new Vector3(thickness, bh, bd), sharedBookMaterial);

                MeshRenderer rend = bookT.GetComponent<MeshRenderer>();
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                rend.SetPropertyBlock(mpb);

                if (leaning)
                {
                    float tilt = Random.Range(8f, 22f) * (Random.value < 0.5f ? 1f : -1f);
                    bookT.localRotation = Quaternion.Euler(0, 0, tilt);
                    bookT.localPosition += new Vector3(0, 0.002f, 0);
                }

                x += thickness + Random.Range(0f, 0.004f);
                filled += thickness;
                bookIndex++;
            }
        }
    }

    Color VaryColor(Color c, float amount)
    {
        float r = Mathf.Clamp01(c.r + Random.Range(-amount, amount));
        float g = Mathf.Clamp01(c.g + Random.Range(-amount, amount));
        float b = Mathf.Clamp01(c.b + Random.Range(-amount, amount));
        return new Color(r, g, b, 1f);
    }

    void SafeDestroy(Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { DestroyImmediate(obj); return; }
#endif
        Destroy(obj);
    }
}
