namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>Presentation of Semester 1 lecture days; backend validates every action.
    /// Assessment days and definitions come from the authoritative Report Card.</summary>
    public static class SemesterSchedule
    {
        public static bool IsNormalClassDay(int semester, int day) => semester == 1 && (day == 1 || day == 4);
        public static bool IsBetaEnd(int semester, int day) => semester == 1 && day == 6;
        public static string DayHeader(string assessmentType) => assessmentType switch
        {
            "QUIZ_1" or "QUIZ_2" => "QUIZ DAY · TODAY",
            "MIDTERM" => "MIDTERM DAY · TODAY",
            "FINAL" => "FINAL DAY · TODAY",
            _ => "TODAY"
        };
    }
}
