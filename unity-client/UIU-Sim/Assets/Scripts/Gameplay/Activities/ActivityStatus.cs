namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>Lifecycle status for a current-day activity.</summary>
    public enum ActivityStatus
    {
        Pending,
        Completed,
        Missed,
        /// <summary>Mid-lecture ICS session (server IN_PROGRESS).</summary>
        InProgress
    }
}
