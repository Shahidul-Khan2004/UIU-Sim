using System;

namespace UIU.Simulator.Gameplay.Player
{
    /// <summary>
    /// Library Study start/complete. Reputation is applied only from the server response.
    /// </summary>
    public interface ILibraryStudyProgressSync
    {
        void RequestLibraryStudyStart(Action<UIU.Simulator.Gameplay.Activities.LibraryStudySessionResult> onSuccess, Action onFailure);

        void RequestLibraryStudyComplete(
            int score,
            Action<UIU.Simulator.Gameplay.Activities.LibraryStudySessionResult> onSuccess,
            Action onFailure);
    }
}
