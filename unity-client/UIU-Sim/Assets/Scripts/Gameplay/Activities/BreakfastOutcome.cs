namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>
    /// Neptune breakfast outcomes. Values match backend <c>BreakfastOutcome</c> enum names.
    /// </summary>
    public enum BreakfastOutcome
    {
        Rice,
        PorottaWait,
        SkipLine,
        SkipBreakfast
    }

    public static class BreakfastOutcomeApi
    {
        public static string ToApiValue(BreakfastOutcome outcome)
        {
            return outcome switch
            {
                BreakfastOutcome.Rice => "RICE",
                BreakfastOutcome.PorottaWait => "POROTTA_WAIT",
                BreakfastOutcome.SkipLine => "SKIP_LINE",
                BreakfastOutcome.SkipBreakfast => "SKIP_BREAKFAST",
                _ => outcome.ToString().ToUpperInvariant()
            };
        }

        public static bool TryParse(string raw, out BreakfastOutcome outcome)
        {
            switch ((raw ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "RICE":
                    outcome = BreakfastOutcome.Rice;
                    return true;
                case "POROTTA_WAIT":
                    outcome = BreakfastOutcome.PorottaWait;
                    return true;
                case "SKIP_LINE":
                    outcome = BreakfastOutcome.SkipLine;
                    return true;
                case "SKIP_BREAKFAST":
                    outcome = BreakfastOutcome.SkipBreakfast;
                    return true;
                default:
                    outcome = default;
                    return false;
            }
        }
    }
}
