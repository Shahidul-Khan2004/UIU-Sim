#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildCampusCafeBoothMVP
{
    private const string RootFolder = "Assets/Art/Environment/CampusCafeBooth_MVP";
    private const string MaterialsFolder = RootFolder + "/Materials";
    private const string MeshesFolder = RootFolder + "/Meshes";
    private const string PrefabPath = RootFolder + "/PF_CampusCafeBooth_MVP.prefab";
    private const int Wood = 0, Metal = 1, Cream = 2, Brown = 3, Gold = 4, Glass = 5;

    [MenuItem("Tools/UIU Simulator/Assets/Build Campus Cafe Booth MVP")]
    public static void Build()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(MeshesFolder);
        Material[] materials = {
            GetOrCreateMaterial(MaterialsFolder + "/M_Cafe_Wood.mat", new Color(0.43f, 0.22f, 0.09f), 0f, 0.35f),
            GetOrCreateMaterial(MaterialsFolder + "/M_Cafe_BlackMetal.mat", new Color(0.025f, 0.03f, 0.032f), 0.75f, 0.45f),
            GetOrCreateMaterial(MaterialsFolder + "/M_Cafe_Items.mat", new Color(0.89f, 0.83f, 0.68f), 0f, 0.3f),
            GetOrCreateMaterial(MaterialsFolder + "/M_Cafe_Items_Coffee.mat", new Color(0.19f, 0.075f, 0.025f), 0f, 0.3f),
            GetOrCreateMaterial(MaterialsFolder + "/M_Cafe_Items_Gold.mat", new Color(0.78f, 0.43f, 0.10f), 0f, 0.3f),
            GetOrCreateMaterial(MaterialsFolder + "/M_Cafe_Glass.mat", new Color(0.87f, 0.94f, 0.95f, 0.25f), 0f, 0.8f)
        };
        ConfigureGlass(materials[Glass]);
        Material[] opaque = { materials[Wood], materials[Metal], materials[Cream], materials[Brown], materials[Gold] };
        GameObject root = new GameObject("PF_CampusCafeBooth_MVP");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        var owned = new List<Mesh>();
        try
        {
            Transform main = new GameObject("__TEMP_Main").transform;
            Transform glazing = new GameObject("__TEMP_Glass").transform;
            main.SetParent(root.transform, false);
            glazing.SetParent(root.transform, false);
            BuildGeometry(main, glazing, materials, owned);
            Mesh mainMesh = CombineAndSave(main.gameObject, root.transform,
                MeshesFolder + "/CampusCafeBooth_Combined.asset", "CampusCafeBooth_Combined", opaque);
            Mesh glassMesh = CombineAndSave(glazing.gameObject, root.transform,
                MeshesFolder + "/CampusCafeBooth_Glass.asset", "CampusCafeBooth_Glass", new[] { materials[Glass] });
            UnityEngine.Object.DestroyImmediate(main.gameObject);
            UnityEngine.Object.DestroyImmediate(glazing.gameObject);
            CreateFinalRenderer(root.transform, "Visual_Main", mainMesh, opaque);
            CreateFinalRenderer(root.transform, "Visual_Glass", glassMesh, new[] { materials[Glass] }, glass: true);
            Transform collision = new GameObject("Collision").transform;
            collision.SetParent(root.transform, false);
            BoxCollider collider = collision.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.5f, 0f);
            collider.size = new Vector3(5f, 1f, 1.5f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success) throw new InvalidOperationException("Unity failed to save the campus cafe booth prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Campus cafe booth MVP built successfully.\nPrefab: {PrefabPath}\n" +
                "5 x 1.5 x 3 m; 1 m countertop; floor-center pivot; front +Z.\n" +
                "2 renderers, 6 material submeshes, 1 counter BoxCollider, no Light components.\n" +
                $"Triangles: {(mainMesh.triangles.Length + glassMesh.triangles.Length) / 3}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (Mesh mesh in owned) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    private static void BuildGeometry(Transform main, Transform glazing, Material[] materials, List<Mesh> owned)
    {
        Mesh cylinder = CreateLowPolyCylinder(); owned.Add(cylinder);
        Mesh rounded = CreateRoundedItem(); owned.Add(rounded);
        GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        template.SetActive(false);
        try
        {
            Mesh cube = template.GetComponent<MeshFilter>().sharedMesh;
            var groups = new List<CombineInstance>[materials.Length];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<CombineInstance>();
            // All parts below are matrix entries, never independent scene objects.
            Add(groups[Wood], cube, new Vector3(0f, 0.52f, 0.645f), new Vector3(4.8f, 0.84f, 0.08f));
            for (int side = -1; side <= 1; side += 2)
                Add(groups[Wood], cube, new Vector3(side * 2.36f, 0.52f, 0f), new Vector3(0.08f, 0.84f, 1.30f));
            Add(groups[Wood], cube, new Vector3(0f, 0.53f, -0.64f), new Vector3(4.64f, 0.82f, 0.06f));
            Add(groups[Cream], cube, new Vector3(0f, 0.97f, 0f), new Vector3(4.90f, 0.06f, 1.50f));
            Add(groups[Metal], cube, new Vector3(0f, 0.05f, 0f), new Vector3(4.80f, 0.10f, 1.32f));
            // Broad shallow grooves suggest vertical wood paneling at gameplay distance.
            for (int i = 1; i < 24; i++)
                Add(groups[Brown], cube, new Vector3(-2.4f + i * 0.20f, 0.52f, 0.686f), new Vector3(0.006f, 0.84f, 0.003f));
            for (int side = -1; side <= 1; side += 2)
            {
                for (int end = -1; end <= 1; end += 2)
                    Add(groups[Metal], cube, new Vector3(side * 2.44f, 1.5f, end * 0.69f), new Vector3(0.12f, 3f, 0.12f));
                Add(groups[Metal], cube, new Vector3(0f, 2.94f, side * 0.69f), new Vector3(4.76f, 0.12f, 0.12f));
                Add(groups[Metal], cube, new Vector3(side * 2.44f, 2.94f, 0f), new Vector3(0.12f, 0.12f, 1.26f));
            }
            Add(groups[Metal], cube, new Vector3(0f, 2.85f, 0.56f), new Vector3(4.70f, 0.035f, 0.045f));
            for (int i = 0; i < 6; i++)
            {
                Vector3 center = new Vector3(-2f + i * 0.80f, 2.69f, 0.56f);
                Quaternion tilt = Quaternion.Euler(15f, 0f, 0f);
                Add(groups[Metal], cube, new Vector3(center.x, 2.80f, center.z), new Vector3(0.04f, 0.07f, 0.04f));
                Add(groups[Metal], cylinder, center, new Vector3(0.06f, 0.18f, 0.06f), tilt);
                Add(groups[Cream], cylinder, center + tilt * Vector3.down * 0.091f,
                    new Vector3(0.050f, 0.004f, 0.050f), tilt);
            }
            BuildEquipment(groups, cube, cylinder);
            BuildDisplay(groups, cube, rounded);
            for (int i = 0; i < groups.Length; i++)
            {
                Mesh mesh = new Mesh { name = "__TEMP_CafeMaterial_" + i, indexFormat = IndexFormat.UInt16 };
                owned.Add(mesh);
                mesh.CombineMeshes(groups[i].ToArray(), true, true, false);
                mesh.RecalculateBounds();
                GameObject go = new GameObject("__TEMP_Material_" + i);
                go.transform.SetParent(i == Glass ? glazing : main, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = materials[i];
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(template); }
    }

    private static void BuildEquipment(List<CombineInstance>[] groups, Mesh cube, Mesh cylinder)
    {
        // Espresso machine silhouette with an open dispensing recess.
        Add(groups[Metal], cube, new Vector3(-1.55f, 1.035f, -0.04f), new Vector3(1.15f, 0.07f, 0.55f));
        Add(groups[Metal], cube, new Vector3(-1.55f, 1.29f, -0.235f), new Vector3(1.05f, 0.44f, 0.15f));
        Add(groups[Metal], cube, new Vector3(-1.55f, 1.385f, -0.04f), new Vector3(1.10f, 0.25f, 0.52f));
        Add(groups[Cream], cube, new Vector3(-1.55f, 1.40f, 0.224f), new Vector3(1.04f, 0.13f, 0.015f));
        for (int side = -1; side <= 1; side += 2)
        {
            Add(groups[Metal], cube, new Vector3(-1.55f + side * 0.48f, 1.17f, 0.09f), new Vector3(0.07f, 0.20f, 0.20f));
            Add(groups[Metal], cylinder, new Vector3(-1.55f + side * 0.23f, 1.23f, 0.12f), new Vector3(0.025f, 0.06f, 0.025f));
        }
        for (int stack = 0; stack < 3; stack++)
            for (int cup = 0; cup < 3; cup++)
                Add(groups[Cream], cylinder, new Vector3(-1.90f + stack * 0.22f, 1.55f + cup * 0.062f, -0.05f),
                    new Vector3(0.055f - cup * 0.003f, 0.078f, 0.055f - cup * 0.003f));
        for (int i = 0; i < 3; i++)
        {
            float x = -0.70f + i * 0.18f;
            Add(groups[i == 1 ? Gold : Brown], cylinder, new Vector3(x, 1.115f, 0.27f), new Vector3(0.053f, 0.23f, 0.053f));
            Add(groups[Brown], cylinder, new Vector3(x, 1.255f, 0.27f), new Vector3(0.020f, 0.05f, 0.020f));
            Add(groups[Metal], cube, new Vector3(x, 1.287f, 0.29f), new Vector3(0.075f, 0.016f, 0.022f));
            Add(groups[Cream], cube, new Vector3(x, 1.115f, 0.324f), new Vector3(0.055f, 0.115f, 0.005f));
        }
        Add(groups[Metal], cube, new Vector3(0.28f, 1.045f, 0.24f), new Vector3(0.56f, 0.09f, 0.40f));
        Add(groups[Metal], cube, new Vector3(0.28f, 1.155f, 0.17f), new Vector3(0.12f, 0.16f, 0.08f));
        Quaternion panelTilt = Quaternion.Euler(55f, 0f, 0f);
        Vector3 panel = new Vector3(0.28f, 1.26f, 0.20f);
        Add(groups[Metal], cube, panel, new Vector3(0.48f, 0.045f, 0.30f), panelTilt);
        Add(groups[Brown], cube, panel + panelTilt * Vector3.up * 0.025f,
            new Vector3(0.41f, 0.008f, 0.23f), panelTilt);
    }

    private static void BuildDisplay(List<CombineInstance>[] groups, Mesh cube, Mesh rounded)
    {
        Add(groups[Wood], cube, new Vector3(1.60f, 1.40f, -0.235f), new Vector3(1.44f, 0.80f, 0.03f));
        for (int side = -1; side <= 1; side += 2)
            for (int end = -1; end <= 1; end += 2)
                Add(groups[Metal], cube, new Vector3(1.60f + side * 0.70f, 1.40f, 0.18f + end * 0.40f),
                    new Vector3(0.04f, 0.80f, 0.04f));
        for (int level = 0; level < 3; level++)
            Add(groups[Metal], cube, new Vector3(1.60f, 1.025f + level * 0.375f, 0.18f), new Vector3(1.44f, 0.025f, 0.80f));
        Add(groups[Glass], cube, new Vector3(1.60f, 1.40f, 0.579f), new Vector3(1.36f, 0.725f, 0.006f));
        for (int side = -1; side <= 1; side += 2)
            Add(groups[Glass], cube, new Vector3(1.60f + side * 0.70f, 1.40f, 0.18f), new Vector3(0.006f, 0.725f, 0.76f));
        for (int level = 0; level < 2; level++)
            for (int item = 0; item < 5; item++)
                Add(groups[Gold], rounded, new Vector3(1.07f + item * 0.26f, 1.1175f + level * 0.375f, 0.20f),
                    new Vector3(0.22f, 0.16f, 0.28f));
    }

    private static void Add(List<CombineInstance> group, Mesh mesh, Vector3 position, Vector3 size, Quaternion? rotation = null)
    {
        group.Add(new CombineInstance { mesh = mesh,
            transform = Matrix4x4.TRS(position, rotation ?? Quaternion.identity, size) });
    }

    private static Mesh CreateRoundedItem()
    {
        // Low-poly sphere: 26 vertices and 48 triangles per rounded pastry.
        var vertices = new List<Vector3> { new Vector3(0f, -0.5f, 0f) };
        var triangles = new List<int>();
        for (int ring = 1; ring <= 3; ring++)
        {
            float latitude = -Mathf.PI * 0.5f + ring * Mathf.PI / 4f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                vertices.Add(new Vector3(0.5f * Mathf.Cos(latitude) * Mathf.Cos(a),
                    0.5f * Mathf.Sin(latitude), 0.5f * Mathf.Cos(latitude) * Mathf.Sin(a)));
            }
        }
        vertices.Add(new Vector3(0f, 0.5f, 0f));
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8;
            triangles.Add(0); triangles.Add(1 + i); triangles.Add(1 + next);
            for (int ring = 0; ring < 2; ring++)
            {
                int a = 1 + ring * 8 + i, b = 1 + ring * 8 + next;
                triangles.Add(a); triangles.Add(a + 8); triangles.Add(b + 8);
                triangles.Add(a); triangles.Add(b + 8); triangles.Add(b);
            }
            triangles.Add(25); triangles.Add(17 + next); triangles.Add(17 + i);
        }
        return FinishMesh("__TEMP_RoundedItem", vertices, triangles);
    }

    private static Mesh FinishMesh(string name, List<Vector3> vertices, List<int> triangles)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateLowPolyCylinder()
    {
        // Unit-height cylinder, radius 1, eight sides. Shared by cups, bottles and decorative spotlight housings.
        const int sides = 8;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            float b = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 loA = new Vector3(Mathf.Cos(a), -0.5f, Mathf.Sin(a));
            Vector3 loB = new Vector3(Mathf.Cos(b), -0.5f, Mathf.Sin(b));
            Vector3 hiA = loA + Vector3.up;
            Vector3 hiB = loB + Vector3.up;
            int n = vertices.Count;
            vertices.Add(loA); vertices.Add(hiA); vertices.Add(hiB); vertices.Add(loB);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
            n = vertices.Count;
            vertices.Add(new Vector3(0f, 0.5f, 0f)); vertices.Add(hiB); vertices.Add(hiA);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            n = vertices.Count;
            vertices.Add(new Vector3(0f, -0.5f, 0f)); vertices.Add(loA); vertices.Add(loB);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
        }
        Mesh mesh = new Mesh { name = "__TEMP_EightSidedCylinder" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void ConfigureGlass(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_BlendModePreserveSpecular", 1f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_AlphaToMask", 0f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_ZWriteControl", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ReceiveShadows", 1f);
        material.SetFloat("_QueueOffset", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetShaderPassEnabled("DepthOnly", false);
        material.SetShaderPassEnabled("DepthNormals", false);
        material.SetShaderPassEnabled("MotionVectors", false);
        EditorUtility.SetDirty(material);
    }

    private static void CreateFinalRenderer(Transform root, string name, Mesh mesh, Material[] materials, bool glass = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = glass ? ShadowCastingMode.Off : ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
    }

    private static Mesh CombineAndSave(GameObject tempRoot, Transform finalRoot,
        string assetPath, string meshName, Material[] materials)
    {
        MeshFilter[] filters = tempRoot.GetComponentsInChildren<MeshFilter>(true);
        var groupedMeshes = new List<Mesh>();
        Mesh combined = new Mesh { name = meshName, indexFormat = IndexFormat.UInt16 };
        try
        {
            var groups = new List<CombineInstance>();
            foreach (Material material in materials)
            {
                var parts = new List<CombineInstance>();
                foreach (MeshFilter filter in filters)
                    if (filter.sharedMesh != null && filter.GetComponent<MeshRenderer>().sharedMaterial == material)
                        parts.Add(new CombineInstance {
                            mesh = filter.sharedMesh,
                            transform = finalRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix
                        });
                Mesh group = new Mesh();
                groupedMeshes.Add(group);
                group.CombineMeshes(parts.ToArray(), true, true, false);
                groups.Add(new CombineInstance { mesh = group, transform = Matrix4x4.identity });
            }
            // One submesh per palette color, all in one saved mesh and renderer.
            combined.CombineMeshes(groups.ToArray(), false, true, false);
            combined.RecalculateBounds();
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                // Keep GUIDs stable so existing prefab instances retain their mesh references.
                EditorUtility.CopySerialized(combined, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(combined, assetPath);
            return combined;
        }
        finally
        {
            foreach (Mesh group in groupedMeshes)
                UnityEngine.Object.DestroyImmediate(group);
            if (!AssetDatabase.Contains(combined))
                UnityEngine.Object.DestroyImmediate(combined);
        }
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
            throw new InvalidOperationException("Could not find Universal Render Pipeline/Lit shader.");

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
