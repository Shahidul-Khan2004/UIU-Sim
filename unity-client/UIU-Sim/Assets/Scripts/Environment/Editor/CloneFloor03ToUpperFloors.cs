using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UIU.Simulator.Gameplay.Advisor;
using UIU.Simulator.Gameplay.Elevator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using EditorUtility = UnityEditor.EditorUtility;

namespace UIU.Simulator.Environment.Editor
{
    /// <summary>
    /// Editor-only utility that clones the completed Floor03 hierarchy into the
    /// existing Floor04–Floor10 scene assets (preserving destination asset identity),
    /// patches elevator floor metadata, and excludes Floor03-only Advisor gameplay.
    /// </summary>
    public static class CloneFloor03ToUpperFloors
    {
        const string MenuPath = "Tools/UIU Simulator/Clone Floor03 To Floors 04-10";

        const string SourceScenePath = "Assets/Scenes/Floors/Floor03.unity";

        static readonly int[] DestinationFloorNumbers = { 4, 5, 6, 7, 8, 9, 10 };

        [MenuItem(MenuPath, priority = 100)]
        public static void CloneFromMenu()
        {
            try
            {
                RunClone();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog(
                    "Clone Floor03 Failed",
                    "The clone operation failed.\n\n" + ex.Message,
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void RunClone()
        {
            if (!File.Exists(GetAbsolutePath(SourceScenePath)))
            {
                EditorUtility.DisplayDialog(
                    "Clone Floor03",
                    $"Source scene was not found:\n{SourceScenePath}",
                    "OK");
                return;
            }

            if (!TryEnsureSourceSaved())
                return;

            string gitStatus = RunGitStatus();
            Debug.Log($"[CloneFloor03] git status before clone:\n{gitStatus}");

            List<string> destinationPaths = new List<string>(DestinationFloorNumbers.Length);
            List<string> nonEmptyDestinations = new List<string>();

            foreach (int floorNumber in DestinationFloorNumbers)
            {
                string path = GetDestinationScenePath(floorNumber);
                if (!File.Exists(GetAbsolutePath(path)))
                {
                    EditorUtility.DisplayDialog(
                        "Clone Floor03",
                        $"Destination scene asset is missing (will not create a new identity):\n{path}",
                        "OK");
                    return;
                }

                destinationPaths.Add(path);

                if (!IsDestinationSceneEmpty(path, out int rootCount))
                {
                    nonEmptyDestinations.Add($"{Path.GetFileNameWithoutExtension(path)} ({rootCount} root objects)");
                }
            }

            if (nonEmptyDestinations.Count > 0)
            {
                bool proceedAnyway = EditorUtility.DisplayDialog(
                    "Clone Floor03 — Destinations Not Empty",
                    "These destination scenes are not empty:\n\n" +
                    string.Join("\n", nonEmptyDestinations) +
                    "\n\nOnly empty Floor04–Floor10 scenes should be replaced for this MVP.\n" +
                    "Continue and overwrite them anyway?",
                    "Overwrite",
                    "Abort");
                if (!proceedAnyway)
                    return;
            }

            string confirmMessage =
                "Replace the hierarchy in these existing scene assets with a full copy of Floor03?\n\n" +
                string.Join("\n", destinationPaths) +
                "\n\nThis preserves each destination scene asset/path (no delete/recreate).\n" +
                "Floor03-only Academic Advisor (Capsule) will be excluded.\n" +
                "ElevatorInteractable.CurrentFloor and ElevatorArrivalPoint.FloorNumber will be patched per floor.\n" +
                "ElevatorId values will not be changed.";

            string preflight = BuildSourcePreflightReport();
            confirmMessage += "\n\n" + preflight;

            if (!EditorUtility.DisplayDialog("Clone Floor03 To Floors 04-10", confirmMessage, "Clone", "Cancel"))
                return;

            string previouslyActivePath = SceneManager.GetActiveScene().path;

            // Ensure a non-destination scene is active before closing/overwriting destinations.
            EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            CloseOpenDestinationScenes(destinationPaths);

            var summary = new StringBuilder();
            summary.AppendLine("Clone Floor03 → Floors 04–10 complete.");
            summary.AppendLine();
            summary.AppendLine(preflight);
            summary.AppendLine();

            for (int i = 0; i < DestinationFloorNumbers.Length; i++)
            {
                int floorNumber = DestinationFloorNumbers[i];
                string destPath = destinationPaths[i];

                EditorUtility.DisplayProgressBar(
                    "Clone Floor03 To Floors 04-10",
                    $"Cloning into Floor{floorNumber:D2}…",
                    (float)i / DestinationFloorNumbers.Length);

                CloneIntoDestination(destPath, floorNumber, summary);
            }

            EditorUtility.ClearProgressBar();

            string restorePath = previouslyActivePath;
            if (string.IsNullOrEmpty(restorePath) || !File.Exists(GetAbsolutePath(restorePath)))
                restorePath = SourceScenePath;

            EditorSceneManager.OpenScene(restorePath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string result = summary.ToString();
            Debug.Log("[CloneFloor03]\n" + result);
            EditorUtility.DisplayDialog("Clone Floor03 Complete", result, "OK");
        }

        static string BuildSourcePreflightReport()
        {
            // Lightweight YAML inspection so confirmation shows exactly what will be patched/excluded.
            string text = File.ReadAllText(GetAbsolutePath(SourceScenePath));
            int interactables = CountOccurrences(text, "UIU.Simulator.Gameplay.Elevator.ElevatorInteractable");
            int arrivals = CountOccurrences(text, "UIU.Simulator.Gameplay.Elevator.ElevatorArrivalPoint");
            int advisors = CountOccurrences(text, "UIU.Simulator.Gameplay.Advisor.AcademicAdvisorInteractable");
            int doors = CountOccurrences(text, "UIU.Simulator.Gameplay.Doors.DoorTeleportInteractable");

            return
                "Floor03 preflight:\n" +
                $"- ElevatorInteractable (patch currentFloor): {interactables}\n" +
                $"- ElevatorArrivalPoint (patch floorNumber): {arrivals}\n" +
                $"- AcademicAdvisorInteractable (exclude object): {advisors}\n" +
                $"- DoorTeleportInteractable (copy as-is, no floor fields): {doors}\n" +
                "- No other scene MonoBehaviours with serialized current-floor / floor-number metadata were found.";
        }

        static int CountOccurrences(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        static void CloseOpenDestinationScenes(List<string> destinationPaths)
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene open = SceneManager.GetSceneAt(i);
                if (!open.IsValid() || !open.isLoaded)
                    continue;

                if (!destinationPaths.Contains(open.path))
                    continue;

                if (open.isDirty)
                {
                    throw new InvalidOperationException(
                        $"Destination scene '{open.path}' has unsaved changes. Save or discard it before cloning.");
                }

                EditorSceneManager.CloseScene(open, true);
            }
        }

        static void CloneIntoDestination(string destPath, int floorNumber, StringBuilder summary)
        {
            // Overwrite destination .unity file contents while leaving destination .meta
            // (and therefore asset GUID / Build Settings identity) untouched.
            File.Copy(GetAbsolutePath(SourceScenePath), GetAbsolutePath(destPath), overwrite: true);
            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

            Scene destScene = EditorSceneManager.OpenScene(destPath, OpenSceneMode.Single);

            int excludedAdvisorCount = ExcludeFloor03OnlyGameplay(destScene);
            int interactablePatched = 0;
            int arrivalPatched = 0;
            PatchFloorSpecificData(destScene, floorNumber, out interactablePatched, out arrivalPatched);

            bool saved = EditorSceneManager.SaveScene(destScene);
            if (!saved)
                throw new InvalidOperationException($"Failed to save scene '{destPath}'.");

            summary.AppendLine(
                $"Floor{floorNumber:D2}: excluded Advisor={excludedAdvisorCount}, " +
                $"ElevatorInteractable patched={interactablePatched}, " +
                $"ElevatorArrivalPoint patched={arrivalPatched}");
        }

        static int ExcludeFloor03OnlyGameplay(Scene scene)
        {
            int removed = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                AcademicAdvisorInteractable[] advisors =
                    roots[i].GetComponentsInChildren<AcademicAdvisorInteractable>(true);
                for (int j = 0; j < advisors.Length; j++)
                {
                    AcademicAdvisorInteractable advisor = advisors[j];
                    if (advisor == null)
                        continue;

                    GameObject target = advisor.gameObject;
                    string path = GetTransformPath(target.transform);
                    Debug.Log(
                        $"[CloneFloor03] Excluding Floor03-only Advisor gameplay object '{path}' " +
                        $"from scene '{scene.path}'.",
                        target);
                    Undo.DestroyObjectImmediate(target);
                    removed++;
                }
            }

            return removed;
        }

        static void PatchFloorSpecificData(
            Scene scene,
            int floorNumber,
            out int interactablePatched,
            out int arrivalPatched)
        {
            interactablePatched = 0;
            arrivalPatched = 0;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                ElevatorInteractable[] interactables =
                    roots[r].GetComponentsInChildren<ElevatorInteractable>(true);
                for (int i = 0; i < interactables.Length; i++)
                {
                    ElevatorInteractable interactable = interactables[i];
                    SerializedObject so = new SerializedObject(interactable);
                    SerializedProperty currentFloor = so.FindProperty("currentFloor");
                    if (currentFloor == null)
                    {
                        Debug.LogWarning(
                            $"[CloneFloor03] ElevatorInteractable on '{GetTransformPath(interactable.transform)}' " +
                            "has no serialized currentFloor field.",
                            interactable);
                        continue;
                    }

                    // Do not touch elevatorId.
                    currentFloor.intValue = floorNumber;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(interactable);
                    interactablePatched++;
                }

                ElevatorArrivalPoint[] arrivals =
                    roots[r].GetComponentsInChildren<ElevatorArrivalPoint>(true);
                for (int i = 0; i < arrivals.Length; i++)
                {
                    ElevatorArrivalPoint arrival = arrivals[i];
                    SerializedObject so = new SerializedObject(arrival);
                    SerializedProperty floorNumberProp = so.FindProperty("floorNumber");
                    if (floorNumberProp == null)
                    {
                        Debug.LogWarning(
                            $"[CloneFloor03] ElevatorArrivalPoint on '{GetTransformPath(arrival.transform)}' " +
                            "has no serialized floorNumber field.",
                            arrival);
                        continue;
                    }

                    // Do not touch elevatorId.
                    floorNumberProp.intValue = floorNumber;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(arrival);
                    arrivalPatched++;
                }
            }
        }

        static bool TryEnsureSourceSaved()
        {
            Scene sourceScene = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                if (open.path == SourceScenePath)
                {
                    sourceScene = open;
                    break;
                }
            }

            if (sourceScene.IsValid() && sourceScene.isDirty)
            {
                bool saveAndContinue = EditorUtility.DisplayDialog(
                    "Clone Floor03",
                    "Floor03 has unsaved changes in the Editor.\n\n" +
                    "Save Floor03 before cloning?",
                    "Save and Continue",
                    "Abort");

                if (!saveAndContinue)
                    return false;

                if (!EditorSceneManager.SaveScene(sourceScene))
                {
                    EditorUtility.DisplayDialog(
                        "Clone Floor03",
                        "Failed to save Floor03. Aborting.",
                        "OK");
                    return false;
                }
            }

            // Re-check after optional save.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                if (open.path == SourceScenePath && open.isDirty)
                {
                    EditorUtility.DisplayDialog(
                        "Clone Floor03",
                        "Floor03 still has unsaved changes. Aborting.",
                        "OK");
                    return false;
                }
            }

            return true;
        }

        static bool IsDestinationSceneEmpty(string scenePath, out int rootCount)
        {
            rootCount = 0;

            // Prefer a lightweight YAML check so we do not thrash scene loading during safety scans.
            string absolute = GetAbsolutePath(scenePath);
            string text = File.ReadAllText(absolute);
            if (text.Contains("m_Roots: []"))
            {
                rootCount = 0;
                return true;
            }

            Scene previouslyActive = SceneManager.GetActiveScene();
            string previousPath = previouslyActive.path;

            Scene opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            rootCount = opened.rootCount;
            bool empty = rootCount == 0;
            EditorSceneManager.CloseScene(opened, true);

            if (!string.IsNullOrEmpty(previousPath))
            {
                Scene restore = SceneManager.GetSceneByPath(previousPath);
                if (restore.IsValid() && restore.isLoaded)
                    SceneManager.SetActiveScene(restore);
            }

            return empty;
        }

        static string RunGitStatus()
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
                // Repo root is three levels up from Assets when Assets lives at
                // unity-client/UIU-Sim/Assets. Fall back to searching upward for .git.
                projectRoot = FindGitRoot(Application.dataPath) ?? projectRoot;

                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "status --short -- \"unity-client/UIU-Sim/Assets/Scenes/Floors/\"",
                    WorkingDirectory = projectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                        return "(git status unavailable)";

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit(10000);
                    if (!string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(output))
                        return error.Trim();
                    return string.IsNullOrWhiteSpace(output) ? "(clean for Floors/)" : output.Trim();
                }
            }
            catch (Exception ex)
            {
                return "(git status failed: " + ex.Message + ")";
            }
        }

        static string FindGitRoot(string startPath)
        {
            DirectoryInfo dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return null;
        }

        static string GetDestinationScenePath(int floorNumber)
        {
            return $"Assets/Scenes/Floors/Floor{floorNumber:D2}.unity";
        }

        static string GetAbsolutePath(string assetPath)
        {
            string relative = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? assetPath.Substring("Assets/".Length)
                : assetPath;
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        }

        static string GetTransformPath(Transform transform)
        {
            var parts = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
