using System;
using UIU.Simulator.Gameplay.Activities;

namespace UIU.Simulator.Gameplay.Player
{
    /// <summary>
    /// Library Self Study start/pause/resume/milestone. Reputation applies only from the server.
    /// </summary>
    public interface ILibrarySelfStudyProgressSync
    {
        void RequestLibrarySelfStudyStart(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure);

        void RequestLibrarySelfStudyPause(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure);

        void RequestLibrarySelfStudyAbandon(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure);

        void RequestLibrarySelfStudyResume(Action<LibrarySelfStudySessionResult> onSuccess, Action onFailure);

        void RequestLibrarySelfStudyMilestone(
            int milestoneSeconds,
            Action<LibrarySelfStudySessionResult> onSuccess,
            Action onFailure);
    }
}
