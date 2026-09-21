using System;
using UIU.Simulator.Gameplay.Activities;

namespace UIU.Simulator.Gameplay.Player
{
    /// <summary>
    /// ICS lecture session mutations. Rewards apply only after server confirmation —
    /// never via a separate Aura/Reputation PATCH.
    /// </summary>
    public interface IAttendIcsProgressSync
    {
        bool IsHydrated { get; }
        bool IsMutationInFlight { get; }

        void RequestAttendIcsStart(Action<AttendIcsSessionResult> onSuccess, Action onFailure);

        void RequestAttendIcsPause(Action<AttendIcsSessionResult> onSuccess, Action onFailure);

        void RequestAttendIcsResume(Action<AttendIcsSessionResult> onSuccess, Action onFailure);

        void RequestAttendIcsMilestone(
            int milestoneSeconds,
            Action<AttendIcsSessionResult> onSuccess,
            Action onFailure);

        void RequestAttendIcsLeaveEarly(Action<AttendIcsSessionResult> onSuccess, Action onFailure);

        void RequestAttendIcsProxy(Action<AttendIcsSessionResult> onSuccess, Action onFailure);
    }
}
