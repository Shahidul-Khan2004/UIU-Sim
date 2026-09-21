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

        /// <summary>
        /// HUD objective title. Terminal failure outcomes stay on the same row as the pending title.
        /// </summary>
        public static string HudTitle(string baseTitle, string outcomeRaw)
        {
            string title = string.IsNullOrWhiteSpace(baseTitle) ? "Attend Class" : baseTitle.Trim();
            if (!TryParse(outcomeRaw, out AttendIcsOutcome outcome))
            {
                return title;
            }

            return outcome switch
            {
                AttendIcsOutcome.LeftEarly => title + " — Left Early",
                AttendIcsOutcome.Skipped => title + " — Missed",
                _ => title
            };
        }

        public static string SummaryLabel(string outcomeRaw)
        {
            return SummaryLabel(outcomeRaw, "Introduction to Computer Science");
        }

        public static string SummaryLabel(string outcomeRaw, string courseName)
        {
            string name = string.IsNullOrWhiteSpace(courseName)
                ? "Class"
                : courseName.Trim();
            if (!TryParse(outcomeRaw, out AttendIcsOutcome outcome))
            {
                return name;
            }

            return outcome switch
            {
                AttendIcsOutcome.Completed => name,
                AttendIcsOutcome.LeftEarly => name + " (Left Early)",
                AttendIcsOutcome.Proxy => "Proxy — Punched ID and Left",
                AttendIcsOutcome.Skipped => name + " (Missed)",
                AttendIcsOutcome.Attending => name + " (In Progress)",
                _ => name
            };
        }
    }
}
