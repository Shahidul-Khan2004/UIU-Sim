#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UIU.Simulator.Authentication;

namespace UIU.Simulator.Authentication.EditorTools
{
    /// <summary>
    /// Creates Bootstrap/Login scenes and wires Build Settings + Main auth guard.
    /// Menu: UIU Simulator → Setup Authentication Scenes
    /// </summary>
    public static class AuthSceneSetupMenu
    {
        private const string ScenesRoot = "Assets/Scenes";
        private const string AuthRoot = ScenesRoot + "/Auth";
        private const string BootstrapPath = AuthRoot + "/Bootstrap.unity";
        private const string LoginPath = AuthRoot + "/Login.unity";
        private const string MainPath = "Assets/Scenes/Main/UIU_Main.unity";

        [MenuItem("UIU Simulator/Setup Authentication Scenes")]
        public static void Setup()
        {
            EnsureFolder(ScenesRoot);
            EnsureFolder(AuthRoot);

            CreateControllerScene(BootstrapPath, "Bootstrap", typeof(AuthBootstrapController));
            CreateControllerScene(LoginPath, "Login", typeof(LoginSceneController));
            EnsureGameplayGuardOnMain();
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AuthSceneSetup] Bootstrap + Login scenes ready. Bootstrap is first in Build Settings.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        private static void CreateControllerScene(string assetPath, string sceneName, System.Type controllerType)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject(sceneName + "Root");
            root.AddComponent(controllerType);
            EditorSceneManager.SaveScene(scene, assetPath);
            Debug.Log($"[AuthSceneSetup] Wrote {assetPath}");
        }

        private static void EnsureGameplayGuardOnMain()
        {
            Scene main = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
            GameObject managers = GameObject.Find("Game Managers");
            if (managers == null)
            {
                managers = new GameObject("Game Managers");
            }

            if (managers.GetComponent<AuthGameplayGuard>() == null)
            {
                managers.AddComponent<AuthGameplayGuard>();
                EditorSceneManager.MarkSceneDirty(main);
                EditorSceneManager.SaveScene(main);
                Debug.Log("[AuthSceneSetup] Added AuthGameplayGuard to Game Managers.");
            }
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(LoginPath, true),
                new EditorBuildSettingsScene(MainPath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path == BootstrapPath || existing.path == LoginPath || existing.path == MainPath)
                {
                    continue;
                }

                scenes.Add(existing);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
