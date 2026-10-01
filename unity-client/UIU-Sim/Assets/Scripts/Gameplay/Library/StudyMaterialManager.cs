using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Library
{
    public readonly struct StudyMaterial
    {
        public StudyMaterial(string fileName, string displayName, string absolutePath)
        {
            FileName = fileName ?? string.Empty;
            DisplayName = displayName ?? fileName ?? string.Empty;
            AbsolutePath = absolutePath ?? string.Empty;
        }

        public string FileName { get; }
        public string DisplayName { get; }
        public string AbsolutePath { get; }
    }

    /// <summary>
    /// Local Unity-only PDF catalog under StreamingAssets/StudyMaterials.
    /// Opens files in the system browser. Nothing is uploaded or stored on the backend.
    /// </summary>
    public static class StudyMaterialManager
    {
        public const string ResourceFolder = "StudyMaterials";

        public static Func<StudyMaterial, bool> OpenOverrideForTesting { get; set; }
        public static Func<StudyMaterial[]> ListOverrideForTesting { get; set; }

        public static StudyMaterial[] ListMaterials()
        {
            if (ListOverrideForTesting != null)
            {
                return ListOverrideForTesting() ?? Array.Empty<StudyMaterial>();
            }

            string folder = ResolveFolder();
            bool exists = !string.IsNullOrEmpty(folder) && Directory.Exists(folder);
            Debug.Log($"[StudyMaterialManager] Study folder: {folder}");
            Debug.Log($"[StudyMaterialManager] Study folder exists: {exists}");
            if (!exists)
            {
                Debug.Log("[StudyMaterialManager] PDF count: 0");
                Debug.LogWarning($"[StudyMaterialManager] Study materials folder not found: {folder}");
                return Array.Empty<StudyMaterial>();
            }

            string[] files = Directory.GetFiles(folder, "*.pdf");
            var materials = new List<StudyMaterial>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileName = Path.GetFileName(path);
                materials.Add(new StudyMaterial(fileName, ToDisplayName(fileName), Path.GetFullPath(path)));
            }

            materials.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            Debug.Log($"[StudyMaterialManager] PDF count: {materials.Count}");
            return materials.ToArray();
        }

        public static bool Open(StudyMaterial material)
        {
            if (OpenOverrideForTesting != null)
            {
                return OpenOverrideForTesting(material);
            }

            if (string.IsNullOrWhiteSpace(material.AbsolutePath) || !File.Exists(material.AbsolutePath))
            {
                Debug.LogWarning($"[StudyMaterialManager] PDF not found: {material.AbsolutePath}");
                return false;
            }

            string uri = new Uri(material.AbsolutePath).AbsoluteUri;
            Application.OpenURL(uri);
            return true;
        }

        public static string ToDisplayName(string fileName)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
            stem = stem.Replace('_', ' ').Replace('-', ' ');
            while (stem.Contains("  "))
            {
                stem = stem.Replace("  ", " ");
            }

            return stem.Trim();
        }

        private static string ResolveFolder()
        {
            return Path.Combine(Application.streamingAssetsPath, ResourceFolder);
        }
    }
}
