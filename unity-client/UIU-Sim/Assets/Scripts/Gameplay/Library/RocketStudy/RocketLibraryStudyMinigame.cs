using System;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Gameplay.Library.RocketStudy
{
    /// <summary>Owns only the additive Rocket scene, never the campus or its player controls.</summary>
    [DisallowMultipleComponent]
    public sealed class RocketLibraryStudyMinigame : MonoBehaviour, ILibraryStudyMinigame, ILibraryStudyMinigameSession
    {
        public const string ScenePath = "Assets/Scenes/Minigames/LibraryRocketStudy.unity";
        [SerializeField] private string scenePath = ScenePath;
        private static RocketLibraryStudyMinigame owner;
        private Action<LibraryStudyMinigameResult> resultCallback;
        private Action closedCallbacks;
        private AsyncOperation loadOperation, unloadOperation;
        private Scene loadedScene;
        private bool hasSession, closeRequested;

        public bool IsAvailable => isActiveAndEnabled && !hasSession && owner == null
            && !string.IsNullOrWhiteSpace(scenePath)
            && !SceneManager.GetSceneByPath(scenePath).isLoaded
            && Application.CanStreamedLevelBeLoaded(scenePath);

        public void Launch(Action<LibraryStudyMinigameResult> onFinished)
        {
            if (!IsAvailable)
            {
                onFinished?.Invoke(LibraryStudyMinigameResult.Cancelled());
                return;
            }
            owner = this;
            hasSession = true;
            closeRequested = false;
            resultCallback = onFinished;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                if (loadOperation == null) throw new InvalidOperationException("Scene load did not start.");
                // Async callbacks keep cleanup alive even if the station is disabled during loading.
                loadOperation.completed += OnLoaded;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RocketStudy] Cannot load minigame: {exception.Message}");
                SystemNotificationUI.Show("Rocket Study could not open. Please try again.");
                Deliver(LibraryStudyMinigameResult.Cancelled());
                Close(null);
            }
        }

        private void OnLoaded(AsyncOperation operation)
        {
            loadOperation = null;
            loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (closeRequested || this == null || !isActiveAndEnabled)
            {
                Close(null);
                return;
            }
            LibraryRocketGameController controller = null;
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                foreach (GameObject root in loadedScene.GetRootGameObjects())
                {
                    controller = root.GetComponentInChildren<LibraryRocketGameController>();
                    if (controller != null) break;
                }
            }
            if (controller == null)
            {
                SystemNotificationUI.Show("Rocket Study scene is missing its controller.");
                Deliver(LibraryStudyMinigameResult.Cancelled());
                Close(null);
                return;
            }
            // Do not SetActiveScene: all campus services retain their original scene ownership.
            controller.Open(Deliver);
        }

        private void Deliver(LibraryStudyMinigameResult result)
        {
            Action<LibraryStudyMinigameResult> callback = resultCallback;
            resultCallback = null;
            callback?.Invoke(result);
        }

        public void Close(Action onClosed)
        {
            if (!hasSession)
            {
                onClosed?.Invoke();
                return;
            }
            closedCallbacks += onClosed;
            closeRequested = true;
            if (loadOperation != null || unloadOperation != null) return;
            resultCallback = null;
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                unloadOperation = SceneManager.UnloadSceneAsync(loadedScene);
                if (unloadOperation != null)
                {
                    unloadOperation.completed += _ => FinishClose();
                    return;
                }
            }
            FinishClose();
        }

        private void FinishClose()
        {
            hasSession = false;
            unloadOperation = null;
            loadedScene = default;
            if (owner == this) owner = null;
            Action callbacks = closedCallbacks;
            closedCallbacks = null;
            callbacks?.Invoke();
        }

        private void OnDisable()
        {
            if (!hasSession) return;
            Deliver(LibraryStudyMinigameResult.Cancelled());
            Close(null);
        }
    }
}
