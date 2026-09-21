namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>
    /// Receptionist ID card outcomes. Values match backend <c>GetIdCardOutcome</c> enum names.
    /// </summary>
    public enum GetIdCardOutcome
    {
        Completed
    }

    public static class GetIdCardOutcomeApi
    {
        public static string ToApiValue(GetIdCardOutcome outcome)
        {
            return outcome switch
            {
                GetIdCardOutcome.Completed => "COMPLETED",
                _ => outcome.ToString().ToUpperInvariant()
            };
        }

        public static bool TryParse(string raw, out GetIdCardOutcome outcome)
        {
            switch ((raw ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "COMPLETED":
                    outcome = GetIdCardOutcome.Completed;
                    return true;
                default:
                    outcome = default;
                    return false;
            }
        }
    }
}
