namespace UIU.Simulator.Gameplay.Player
{
    /// <summary>
    /// Contract for requesting persistent stat changes and inspecting hydration state.
    /// Used by gameplay systems (Canteen, Receptionist) and test mocks.
    /// </summary>
    public interface IPlayerProgressSync
    {
        bool IsHydrated { get; }
        bool IsMutationInFlight { get; }
        void RequestStatDelta(int auraDelta, int academicReputationDelta);
    }
}
