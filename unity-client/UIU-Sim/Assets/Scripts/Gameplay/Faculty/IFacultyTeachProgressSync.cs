using System;

namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Faculty classroom scan and lecture mutations. Rewards apply only after server confirmation.
    /// </summary>
    public interface IFacultyTeachProgressSync
    {
        bool IsHydrated { get; }
        bool IsMutationInFlight { get; }

        void RequestScan(string courseId, Action onSuccess, Action onFailure);

        void RequestCompleteLecture(Action onSuccess, Action onFailure);

        void RequestLeaveLecture(Action onSuccess, Action onFailure);
    }
}
