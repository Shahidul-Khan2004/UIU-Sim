namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>ICS attendance outcomes. Values match backend <c>AttendIcsOutcome</c> names.</summary>
    public enum AttendIcsOutcome
    {
        Attending,
        Completed,
        LeftEarly,
        Proxy,
        Skipped
    }

    public static class AttendIcsOutcomeApi
    {
        public static string ToApiValue(AttendIcsOutcome outcome)
        {
            return outcome switch
            {
                AttendIcsOutcome.Attending => "ATTENDING",
                AttendIcsOutcome.Completed => "COMPLETED",
                AttendIcsOutcome.LeftEarly => "LEFT_EARLY",
                AttendIcsOutcome.Proxy => "PROXY",
                AttendIcsOutcome.Skipped => "SKIPPED",
                _ => outcome.ToString().ToUpperInvariant()
            };
        }

        public static bool TryParse(string raw, out AttendIcsOutcome outcome)
        {
            switch ((raw ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "ATTENDING":
                    outcome = AttendIcsOutcome.Attending;
                    return true;
                case "COMPLETED":
                    outcome = AttendIcsOutcome.Completed;
                    return true;
                case "LEFT_EARLY":
                    outcome = AttendIcsOutcome.LeftEarly;
                    return true;
                case "PROXY":
                    outcome = AttendIcsOutcome.Proxy;
                    return true;
                case "SKIPPED":
                    outcome = AttendIcsOutcome.Skipped;
                    return true;
                default:
                    outcome = default;
                    return false;
            }
        }

        public static string SummaryLabel(string outcomeRaw)
        {
            if (!TryParse(outcomeRaw, out AttendIcsOutcome outcome))
            {
                return "Introduction to Computer Science";
            }

            return outcome switch
            {
                AttendIcsOutcome.Completed => "Introduction to Computer Science",
                AttendIcsOutcome.LeftEarly => "Introduction to Computer Science (Left Early)",
                AttendIcsOutcome.Proxy => "Proxy — Punched ID and Left",
                AttendIcsOutcome.Skipped => "Introduction to Computer Science (Missed)",
                AttendIcsOutcome.Attending => "Introduction to Computer Science (In Progress)",
                _ => "Introduction to Computer Science"
            };
        }
    }
}
