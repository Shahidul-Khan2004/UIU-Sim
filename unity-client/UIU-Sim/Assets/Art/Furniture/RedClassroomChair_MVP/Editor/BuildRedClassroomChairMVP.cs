#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildRedClassroomChairMVP
{
    private const string RootFolder = "Assets/Art/Furniture/RedClassroomChair_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";

    private const string PrefabPath = RootFolder + "/PF_RedClassroomChair_MVP.prefab";
    private const string FabricMaterialPath = MaterialsFolder + "/M_Chair_RedFabric.mat";
    private const string MetalMaterialPath = MaterialsFolder + "/M_Chair_BlackMetal.mat";
    private const string FabricMeshPath = MeshesFolder + "/Chair_RedFabricCombined.asset";
    private const string MetalMeshPath = MeshesFolder + "/Chair_BlackMetalCombined.asset";

    [MenuItem("Tools/UIU Simulator/Assets/Build Red Classroom Chair MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);

        Material fabricMaterial = GetOrCreateMaterial(
            FabricMaterialPath,
            new Color(0.55f, 0.015f, 0.025f, 1f),
            metallic: 0.0f,
            smoothness: 0.18f);

        Material metalMaterial = GetOrCreateMaterial(
            MetalMaterialPath,
            new Color(0.015f, 0.017f, 0.020f, 1f),
            metallic: 0.22f,
            smoothness: 0.34f);

        GameObject root = new GameObject("PF_RedClassroomChair_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        GameObject tempFabric = new GameObject("__TEMP_FabricParts");
        GameObject tempMetal = new GameObject("__TEMP_MetalParts");
        tempFabric.transform.SetParent(root.transform, false);
        tempMetal.transform.SetParent(root.transform, false);

        try
        {
            BuildFabricParts(tempFabric.transform, fabricMaterial);
            BuildMetalParts(tempMetal.transform, metalMaterial);

            Mesh fabricMesh = CombineAndSave(tempFabric, root.transform, FabricMeshPath, "Chair_RedFabricCombined");
            Mesh metalMesh = CombineAndSave(tempMetal, root.transform, MetalMeshPath, "Chair_BlackMetalCombined");

            UnityEngine.Object.DestroyImmediate(tempFabric);
            UnityEngine.Object.DestroyImmediate(tempMetal);

            CreateFinalRenderer(root.transform, "Visual_RedFabric", fabricMesh, fabricMaterial);
            CreateFinalRenderer(root.transform, "Visual_BlackMetal", metalMesh, metalMaterial);

            BuildSimpleColliders(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success)
                throw new InvalidOperationException("Unity failed to save the red classroom chair prefab.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                $"Red classroom chair MVP built successfully.\n" +
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

    private static void BuildFabricParts(Transform parent, Material material)
    {
        // Seat cushion: thick, softly rounded and slightly wider at the front.
        CreateRoundedPart(
            parent, "Seat",
            width: 0.49f, depth: 0.455f, height: 0.075f,
            cornerRadius: 0.105f, bevel: 0.018f,
            localPosition: new Vector3(0f, 0.475f, 0.035f),
            localRotation: Quaternion.Euler(-2f, 0f, 0f),
            material: material);

        // Backrest: large upholstered rectangle, visibly taller than on the black tablet chair.
        CreateRoundedPart(
            parent, "Backrest",
            width: 0.485f, depth: 0.505f, height: 0.070f,
            cornerRadius: 0.090f, bevel: 0.018f,
            localPosition: new Vector3(0f, 0.790f, -0.225f),
            localRotation: Quaternion.Euler(-101f, 0f, 0f),
            material: material);
    }

    private static void BuildMetalParts(Transform parent, Material material)
    {
        const float tube = 0.0145f;

        // Front floor U-frame, matching the squared sled-like shape in the reference.
        Vector3 flFloor = new Vector3(-0.235f, 0.030f, 0.205f);
        Vector3 frFloor = new Vector3( 0.235f, 0.030f, 0.205f);
        Vector3 flTop   = new Vector3(-0.235f, 0.430f, 0.165f);
        Vector3 frTop   = new Vector3( 0.235f, 0.430f, 0.165f);

        CreateTube(parent, "FrontLeg_L", flFloor, flTop, tube, material);
        CreateTube(parent, "FrontLeg_R", frFloor, frTop, tube, material);
        CreateTube(parent, "FrontCrossbar", flFloor + new Vector3(0f, 0.060f, 0f), frFloor + new Vector3(0f, 0.060f, 0f), tube, material);

        // Rear legs.
        Vector3 rlTop   = new Vector3(-0.205f, 0.430f, -0.155f);
        Vector3 rrTop   = new Vector3( 0.205f, 0.430f, -0.155f);
        Vector3 rlFloor = new Vector3(-0.235f, 0.035f, -0.225f);
        Vector3 rrFloor = new Vector3( 0.235f, 0.035f, -0.225f);

        CreateTube(parent, "RearLeg_L", rlTop, rlFloor, tube, material);
        CreateTube(parent, "RearLeg_R", rrTop, rrFloor, tube, material);

        // Under-seat rectangular rails.
        CreateTube(parent, "SeatRail_L", new Vector3(-0.205f, 0.430f, -0.160f), new Vector3(-0.235f, 0.430f, 0.165f), tube, material);
        CreateTube(parent, "SeatRail_R", new Vector3( 0.205f, 0.430f, -0.160f), new Vector3( 0.235f, 0.430f, 0.165f), tube, material);
        CreateTube(parent, "SeatRail_Rear", rlTop, rrTop, tube, material);

        // Characteristic side/back supports continuing upward around the back cushion.
        CreateTube(parent, "BackPost_L", rlTop, new Vector3(-0.230f, 0.900f, -0.230f), tube, material);
        CreateTube(parent, "BackPost_R", rrTop, new Vector3( 0.230f, 0.900f, -0.230f), tube, material);
        CreateTube(parent, "BackTopBar", new Vector3(-0.230f, 0.900f, -0.230f), new Vector3(0.230f, 0.900f, -0.230f), tube, material);

        // Small lower stabilizer between the rear legs.
        CreateTube(parent, "RearCrossbar", new Vector3(-0.210f, 0.155f, -0.205f), new Vector3(0.210f, 0.155f, -0.205f), tube * 0.85f, material);
    }

    private static void BuildSimpleColliders(Transform root)
    {
        Transform collisionRoot = new GameObject("Collision").transform;
        collisionRoot.SetParent(root, false);

        CreateBoxCollider(
            collisionRoot, "SeatCollider",
            new Vector3(0f, 0.475f, 0.035f),
            new Vector3(0.49f, 0.085f, 0.455f),
            Quaternion.Euler(-2f, 0f, 0f));

        CreateBoxCollider(
            collisionRoot, "BackCollider",
            new Vector3(0f, 0.790f, -0.225f),
            new Vector3(0.485f, 0.070f, 0.505f),
            Quaternion.Euler(-101f, 0f, 0f));
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
            cornerSegments: 6);

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

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            triangles.Add(topCenter);
            triangles.Add(topInnerStart + i);
            triangles.Add(topInnerStart + next);
        }

        AddBand(triangles, topInnerStart, upperOuterStart, count);
        AddBand(triangles, upperOuterStart, lowerOuterStart, count);
        AddBand(triangles, lowerOuterStart, bottomInnerStart, count);

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
