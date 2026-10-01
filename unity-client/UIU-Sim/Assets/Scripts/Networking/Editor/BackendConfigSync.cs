using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UIU.Simulator.Networking;

namespace UIU.Simulator.Networking.Editor
{
    /// <summary>
    /// Editor/build-only: syncs public UIU_* keys from backend/.env into the
    /// runtime BackendConfig ScriptableObject. Never reads .env in standalone players.
    /// </summary>
    [InitializeOnLoad]
    public static class BackendConfigSync
    {
        private const string AssetPath = "Assets/Resources/BackendConfig.asset";
        private const string MenuPath = "UIU Simulator/Sync Backend Config From .env";

        static BackendConfigSync()
        {
            EditorApplication.delayCall += () => SyncFromEnv(logSuccess: false, showDialog: false);
        }

        [MenuItem(MenuPath)]
        public static void SyncFromMenu()
        {
            SyncFromEnv(logSuccess: true, showDialog: true);
        }

        public static bool SyncFromEnv(bool logSuccess, bool showDialog)
        {
            if (!TryFindEnvFile(out string envPath))
            {
                string message =
                    "Could not find backend/.env. Expected at <repo>/backend/.env " +
                    "(copy from backend/.env.example if needed).";
                Debug.LogWarning($"[BackendConfigSync] {message}");
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Backend Config Sync", message, "OK");
                }

                return false;
            }

            Dictionary<string, string> values = ParseEnvFile(envPath);

            values.TryGetValue(BackendConfig.EnvModeKey, out string modeRaw);
            values.TryGetValue(BackendConfig.EnvLocalUrlKey, out string localUrl);
            values.TryGetValue(BackendConfig.EnvRemoteUrlKey, out string remoteUrl);

            if (string.IsNullOrWhiteSpace(modeRaw))
            {
                modeRaw = "LOCAL";
            }

            if (!BackendConfig.TryParseEnvironment(modeRaw, out BackendEnvironment mode))
            {
                string message =
                    $"Invalid {BackendConfig.EnvModeKey}='{modeRaw}'. Use LOCAL or REMOTE.";
                Debug.LogError($"[BackendConfigSync] {message}");
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Backend Config Sync", message, "OK");
                }

                return false;
            }

            if (string.IsNullOrWhiteSpace(localUrl))
            {
                localUrl = "http://localhost:8080";
            }

            if (string.IsNullOrWhiteSpace(remoteUrl))
            {
                remoteUrl = BackendConfig.RemotePlaceholder;
            }

            BackendConfig config = LoadOrCreateAsset();
            config.ApplyFromEnv(mode, localUrl, remoteUrl);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            if (logSuccess)
            {
                Debug.Log(
                    $"[BackendConfigSync] Synced from {envPath}: " +
                    $"mode={mode}, local={BackendConfig.NormalizeBaseUrl(localUrl)}, " +
                    $"remote={BackendConfig.NormalizeBaseUrl(remoteUrl)}");
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Backend Config Sync",
                    $"Synced BackendConfig from backend/.env\n\n" +
                    $"Mode: {mode}\n" +
                    $"Local: {BackendConfig.NormalizeBaseUrl(localUrl)}\n" +
                    $"Remote: {BackendConfig.NormalizeBaseUrl(remoteUrl)}",
                    "OK");
            }

            return true;
        }

        public static string ValidateForBuild()
        {
            SyncFromEnv(logSuccess: false, showDialog: false);

            BackendConfig config = AssetDatabase.LoadAssetAtPath<BackendConfig>(AssetPath);
            if (config == null)
            {
                return
                    "BackendConfig asset missing. Run menu: UIU Simulator → Sync Backend Config From .env";
            }

            if (!config.TryResolveBaseUrl(out _, out string error))
            {
                return error;
            }

            return null;
        }

        private static BackendConfig LoadOrCreateAsset()
        {
            BackendConfig existing = AssetDatabase.LoadAssetAtPath<BackendConfig>(AssetPath);
            if (existing != null)
            {
                return existing;
            }

            string directory = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            BackendConfig created = ScriptableObject.CreateInstance<BackendConfig>();
            AssetDatabase.CreateAsset(created, AssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        private static bool TryFindEnvFile(out string envPath)
        {
            envPath = null;
            string dataPath = Application.dataPath;
            DirectoryInfo dir = new DirectoryInfo(dataPath);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "backend", ".env");
                if (File.Exists(candidate))
                {
                    envPath = candidate;
                    return true;
                }

                dir = dir.Parent;
            }

            return false;
        }

        private static Dictionary<string, string> ParseEnvFile(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string line = raw.Trim();
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("export ", StringComparison.Ordinal))
                {
                    line = line.Substring("export ".Length).Trim();
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = Unquote(line.Substring(eq + 1).Trim());
                if (key.Length == 0)
                {
                    continue;
                }

                // Last occurrence wins (matches duplicate keys in .env).
                result[key] = value;
            }

            return result;
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2)
            {
                char first = value[0];
                char last = value[value.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    return value.Substring(1, value.Length - 2);
                }
            }

            return value;
        }
    }

    /// <summary>
    /// Fails the Unity build when REMOTE mode is selected with a missing/invalid URL.
    /// Prevents accidentally shipping a localhost or placeholder friend build.
    /// </summary>
    public sealed class BackendConfigBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string error = BackendConfigSync.ValidateForBuild();
            if (!string.IsNullOrEmpty(error))
            {
                throw new BuildFailedException($"[BackendConfig] Build blocked: {error}");
            }

            BackendConfig config = Resources.Load<BackendConfig>(BackendConfig.DefaultResourcePath);
            if (config != null && config.TryResolveBaseUrl(out string url, out _))
            {
                Debug.Log($"[BackendConfig] Building with Environment={config.Environment}, Base URL={url}");
            }
        }
    }
}
