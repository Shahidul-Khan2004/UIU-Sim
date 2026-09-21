using System;

namespace UIU.Simulator.Gameplay.Player
{
    /// <summary>
    /// Contract for requesting persistent activity resolution and inspecting hydration state.
    /// Breakfast Aura mutations must go through this path — never via a separate stat delta.
    /// </summary>
    public interface IActivityProgressSync
    {
        bool IsHydrated { get; }
        bool IsMutationInFlight { get; }

        void RequestActivityResolve(
            string activityId,
            string outcome,
            Action<UIU.Simulator.Gameplay.Activities.ActivityResolveResult> onSuccess,
            Action onFailure);
    }
}
