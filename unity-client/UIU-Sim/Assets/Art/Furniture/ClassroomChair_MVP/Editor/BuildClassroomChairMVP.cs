#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildClassroomChairMVP
{
    private const string RootFolder = "Assets/Art/Furniture/ClassroomChair_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";

    private const string PrefabPath = RootFolder + "/PF_ClassroomChair_MVP.prefab";
    private const string BlackMaterialPath = MaterialsFolder + "/M_Chair_BlackPlastic.mat";
    private const string MetalMaterialPath = MaterialsFolder + "/M_Chair_Chrome.mat";
    private const string BlackMeshPath = MeshesFolder + "/Chair_BlackCombined.asset";
    private const string MetalMeshPath = MeshesFolder + "/Chair_MetalCombined.asset";

    [MenuItem("Tools/UIU Simulator/Assets/Build Classroom Chair MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);

        Material blackMaterial = GetOrCreateMaterial(
            BlackMaterialPath,
            new Color(0.018f, 0.020f, 0.022f, 1f),
            metallic: 0.02f,
            smoothness: 0.42f);

        Material metalMaterial = GetOrCreateMaterial(
            MetalMaterialPath,
            new Color(0.62f, 0.64f, 0.66f, 1f),
            metallic: 0.88f,
            smoothness: 0.72f);

        GameObject root = new GameObject("PF_ClassroomChair_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        GameObject tempBlack = new GameObject("__TEMP_BlackParts");
        GameObject tempMetal = new GameObject("__TEMP_MetalParts");
        tempBlack.transform.SetParent(root.transform, false);
        tempMetal.transform.SetParent(root.transform, false);

        try
        {
            BuildBlackParts(tempBlack.transform, blackMaterial);
            BuildMetalParts(tempMetal.transform, metalMaterial);

            Mesh blackMesh = CombineAndSave(tempBlack, root.transform, BlackMeshPath, "Chair_BlackCombined");
            Mesh metalMesh = CombineAndSave(tempMetal, root.transform, MetalMeshPath, "Chair_MetalCombined");

            UnityEngine.Object.DestroyImmediate(tempBlack);
            UnityEngine.Object.DestroyImmediate(tempMetal);

            CreateFinalRenderer(root.transform, "Visual_Black", blackMesh, blackMaterial);
            CreateFinalRenderer(root.transform, "Visual_Metal", metalMesh, metalMaterial);

            BuildSimpleColliders(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the classroom chair prefab.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                $"Classroom chair MVP built successfully.\n" +
                $"Prefab: {PrefabPath}\n" +
                $"Pivot: floor center\n" +
                $"Renderers per chair: 2\n" +
                $"Materials: shared + GPU instancing enabled");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
        finally
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildBlackParts(Transform parent, Material material)
    {
        // Seat: broad, slightly rounded black plastic cushion.
        CreateRoundedPart(
            parent, "Seat",
            width: 0.455f, depth: 0.425f, height: 0.050f,
            cornerRadius: 0.075f, bevel: 0.012f,
            localPosition: new Vector3(0f, 0.465f, 0.005f),
            localRotation: Quaternion.Euler(-2f, 0f, 0f),
            material: material);

        // Backrest: compact black panel with a slight backwards lean.
        CreateRoundedPart(
            parent, "Backrest",
            width: 0.425f, depth: 0.315f, height: 0.040f,
            cornerRadius: 0.065f, bevel: 0.010f,
            localPosition: new Vector3(0f, 0.735f, -0.205f),
            localRotation: Quaternion.Euler(-98f, 0f, 0f),
            material: material);

        // Main writing tablet, modeled from the close-up reference.
        CreateRoundedPart(
            parent, "WritingTablet",
            width: 0.455f, depth: 0.315f, height: 0.026f,
            cornerRadius: 0.105f, bevel: 0.007f,
            localPosition: new Vector3(0.105f, 0.695f, 0.105f),
            localRotation: Quaternion.Euler(1.5f, -2f, 0f),
            material: material);

        // Small opposite-side arm pad.
        CreateRoundedPart(
            parent, "LeftArmPad",
            width: 0.075f, depth: 0.205f, height: 0.028f,
            cornerRadius: 0.030f, bevel: 0.006f,
            localPosition: new Vector3(-0.235f, 0.635f, 0.020f),
            localRotation: Quaternion.identity,
            material: material);

        // Rubber feet.
        CreateSphere(parent, "Foot_FL", new Vector3(-0.205f, 0.030f, 0.215f), new Vector3(0.034f, 0.020f, 0.034f), material);
        CreateSphere(parent, "Foot_FR", new Vector3( 0.205f, 0.030f, 0.215f), new Vector3(0.034f, 0.020f, 0.034f), material);
        CreateSphere(parent, "Foot_RL", new Vector3(-0.205f, 0.030f,-0.215f), new Vector3(0.034f, 0.020f, 0.034f), material);
        CreateSphere(parent, "Foot_RR", new Vector3( 0.205f, 0.030f,-0.215f), new Vector3(0.034f, 0.020f, 0.034f), material);
    }

    private static void BuildMetalParts(Transform parent, Material material)
    {
        const float tube = 0.0125f;

        // Four lightly splayed tubular legs.
        CreateTube(parent, "Leg_FL", new Vector3(-0.170f, 0.445f, 0.155f), new Vector3(-0.205f, 0.045f, 0.215f), tube, material);
        CreateTube(parent, "Leg_FR", new Vector3( 0.170f, 0.445f, 0.155f), new Vector3( 0.205f, 0.045f, 0.215f), tube, material);
        CreateTube(parent, "Leg_RL", new Vector3(-0.170f, 0.445f,-0.155f), new Vector3(-0.205f, 0.045f,-0.215f), tube, material);
        CreateTube(parent, "Leg_RR", new Vector3( 0.170f, 0.445f,-0.155f), new Vector3( 0.205f, 0.045f,-0.215f), tube, material);

        // Under-seat frame rails.
        CreateTube(parent, "Rail_Left",  new Vector3(-0.175f, 0.425f,-0.155f), new Vector3(-0.175f, 0.425f, 0.155f), tube, material);
        CreateTube(parent, "Rail_Right", new Vector3( 0.175f, 0.425f,-0.155f), new Vector3( 0.175f, 0.425f, 0.155f), tube, material);
        CreateTube(parent, "Rail_Rear",  new Vector3(-0.175f, 0.425f,-0.155f), new Vector3( 0.175f, 0.425f,-0.155f), tube, material);

        // Back supports.
        CreateTube(parent, "BackSupport_L", new Vector3(-0.175f, 0.425f,-0.155f), new Vector3(-0.185f, 0.805f,-0.220f), tube, material);
        CreateTube(parent, "BackSupport_R", new Vector3( 0.175f, 0.425f,-0.155f), new Vector3( 0.185f, 0.805f,-0.220f), tube, material);

        // Right-side tablet support with a bent-tube silhouette.
        CreateTube(parent, "TabletSupport_Rise", new Vector3(0.178f, 0.430f, 0.060f), new Vector3(0.255f, 0.665f, 0.075f), tube, material);
        CreateTube(parent, "TabletSupport_Top",  new Vector3(0.255f, 0.665f, 0.075f), new Vector3(0.245f, 0.680f, 0.205f), tube, material);
        CreateTube(parent, "TabletSupport_Inner",new Vector3(0.245f, 0.680f, 0.205f), new Vector3(0.130f, 0.680f, 0.205f), tube, material);

        // Left arm support.
        CreateTube(parent, "LeftArmSupport", new Vector3(-0.178f, 0.430f, 0.030f), new Vector3(-0.235f, 0.615f, 0.020f), tube, material);
        CreateTube(parent, "LeftArmTop",     new Vector3(-0.235f, 0.615f,-0.065f), new Vector3(-0.235f, 0.615f, 0.105f), tube, material);
    }

    private static void BuildSimpleColliders(Transform root)
    {
        Transform collisionRoot = new GameObject("Collision").transform;
        collisionRoot.SetParent(root, false);

        CreateBoxCollider(
            collisionRoot, "SeatCollider",
            new Vector3(0f, 0.465f, 0.005f),
            new Vector3(0.455f, 0.060f, 0.425f),
            Quaternion.Euler(-2f, 0f, 0f));

        CreateBoxCollider(
            collisionRoot, "BackCollider",
            new Vector3(0f, 0.735f, -0.205f),
            new Vector3(0.425f, 0.040f, 0.315f),
            Quaternion.Euler(-98f, 0f, 0f));

        CreateBoxCollider(
            collisionRoot, "TabletCollider",
            new Vector3(0.105f, 0.695f, 0.105f),
            new Vector3(0.455f, 0.030f, 0.315f),
            Quaternion.Euler(1.5f, -2f, 0f));
    }

    private static void CreateFinalRenderer(Transform root, string name, Mesh mesh, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
    }

    private static Mesh CombineAndSave(GameObject tempRoot, Transform finalRoot, string assetPath, string meshName)
    {
        MeshFilter[] filters = tempRoot.GetComponentsInChildren<MeshFilter>(true);
        var combine = new List<CombineInstance>(filters.Length);

        Matrix4x4 worldToRoot = finalRoot.worldToLocalMatrix;

        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                continue;

            combine.Add(new CombineInstance
            {
                mesh = filter.sharedMesh,
                transform = worldToRoot * filter.transform.localToWorldMatrix
            });
        }

        Mesh combined = new Mesh
        {
            name = meshName,
            indexFormat = IndexFormat.UInt32
        };
        combined.CombineMeshes(combine.ToArray(), true, true, false);
        combined.RecalculateBounds();

        if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        AssetDatabase.CreateAsset(combined, assetPath);
        return AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
    }

    private static void CreateRoundedPart(
        Transform parent,
        string name,
        float width,
        float depth,
        float height,
        float cornerRadius,
        float bevel,
        Vector3 localPosition,
        Quaternion localRotation,
        Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateRoundedBeveledPrism(
            name + "_Mesh",
            width, depth, height,
            cornerRadius, bevel,
            cornerSegments: 5);

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
    }

    private static Mesh CreateRoundedBeveledPrism(
        string name,
        float width,
        float depth,
        float height,
        float radius,
        float bevel,
        int cornerSegments)
    {
        float halfHeight = height * 0.5f;
        bevel = Mathf.Clamp(bevel, 0.001f, Mathf.Min(halfHeight * 0.85f, Mathf.Min(width, depth) * 0.12f));
        radius = Mathf.Clamp(radius, 0.001f, Mathf.Min(width, depth) * 0.49f);

        Vector2[] inner = RoundedRectPoints(
            Mathf.Max(0.01f, width - bevel * 2f),
            Mathf.Max(0.01f, depth - bevel * 2f),
            Mathf.Max(0.002f, radius - bevel),
            cornerSegments);

        Vector2[] outer = RoundedRectPoints(width, depth, radius, cornerSegments);

        int count = outer.Length;
        var vertices = new List<Vector3>(count * 4 + 2);
        var triangles = new List<int>(count * 6 * 3);

        int topInnerStart = vertices.Count;
        AddRing(vertices, inner, halfHeight);

        int upperOuterStart = vertices.Count;
        AddRing(vertices, outer, halfHeight - bevel);

        int lowerOuterStart = vertices.Count;
        AddRing(vertices, outer, -halfHeight + bevel);

        int bottomInnerStart = vertices.Count;
        AddRing(vertices, inner, -halfHeight);

        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, halfHeight, 0f));

        int bottomCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -halfHeight, 0f));

        // Top cap.
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            triangles.Add(topCenter);
            triangles.Add(topInnerStart + i);
            triangles.Add(topInnerStart + next);
        }

        // Three side/bevel bands.
        AddBand(triangles, topInnerStart, upperOuterStart, count);
        AddBand(triangles, upperOuterStart, lowerOuterStart, count);
        AddBand(triangles, lowerOuterStart, bottomInnerStart, count);

        // Bottom cap.
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            triangles.Add(bottomCenter);
            triangles.Add(bottomInnerStart + next);
            triangles.Add(bottomInnerStart + i);
        }

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector2[] RoundedRectPoints(float width, float depth, float radius, int cornerSegments)
    {
        float hx = width * 0.5f;
        float hz = depth * 0.5f;
        radius = Mathf.Clamp(radius, 0.001f, Mathf.Min(hx, hz));

        var points = new List<Vector2>(4 * (cornerSegments + 1));

        AddArc(points, new Vector2( hx - radius,  hz - radius),  90f,   0f, cornerSegments, radius);
        AddArc(points, new Vector2( hx - radius, -hz + radius),   0f, -90f, cornerSegments, radius);
        AddArc(points, new Vector2(-hx + radius, -hz + radius), -90f,-180f, cornerSegments, radius);
        AddArc(points, new Vector2(-hx + radius,  hz - radius), 180f,  90f, cornerSegments, radius);

        return points.ToArray();
    }

    private static void AddArc(List<Vector2> points, Vector2 center, float startDegrees, float endDegrees, int segments, float radius)
    {
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private static void AddRing(List<Vector3> vertices, Vector2[] points, float y)
    {
        foreach (Vector2 p in points)
            vertices.Add(new Vector3(p.x, y, p.y));
    }

    private static void AddBand(List<int> triangles, int upperStart, int lowerStart, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;

            int a = upperStart + i;
            int b = upperStart + next;
            int c = lowerStart + next;
            int d = lowerStart + i;

            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);

            triangles.Add(a);
            triangles.Add(d);
            triangles.Add(c);
        }
    }

    private static void CreateTube(
        Transform parent,
        string name,
        Vector3 start,
        Vector3 end,
        float radius,
        Material material)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.0001f)
            return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (start + end) * 0.5f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateSphere(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateBoxCollider(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 size,
        Quaternion localRotation)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;

        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.center = Vector3.zero;
        collider.size = size;
    }

    private static Material GetOrCreateMaterial(
        string path,
        Color baseColor,
        float metallic,
        float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            throw new InvalidOperationException("Could not find URP/Lit or Standard shader.");

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", baseColor);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string normalized = path.Replace("\\", "/");
        int slash = normalized.LastIndexOf('/');
        if (slash <= 0)
            return;

        string parent = normalized.Substring(0, slash);
        string folderName = normalized.Substring(slash + 1);

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(normalized))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
