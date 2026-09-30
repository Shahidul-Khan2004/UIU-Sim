namespace UIU.Simulator.Gameplay.Faculty
{
    /// <summary>
    /// Semester 1 faculty day labels and exam-day gating.
    /// Days 2, 3, 5, and 6 require Prepare Questions; days 1 and 4 are normal teaching.
    /// </summary>
    public static class FacultyDaySchedule
    {
        public const int MaxDayPerSemester = 6;

        public static bool IsExamDay(int semester, int day)
        {
            if (semester != 1)
            {
                return false;
            }

            return day == 2 || day == 3 || day == 5 || day == 6;
        }

        public static string GetDayLabel(int semester, int day)
        {
            if (semester != 1)
            {
                return string.Empty;
            }

            return day switch
            {
                2 or 5 => "Exam Day",
                3 => "Midterm Exam Day",
                6 => "Final Exam Day",
                _ => string.Empty
            };
        }

        public static string BuildPrepareQuestionsNextActivity(int semester, int day)
        {
            string label = GetDayLabel(semester, day);
            if (string.IsNullOrEmpty(label))
            {
                return "Next Activity:\nUse your office computer";
            }

            return $"Next Activity:\n{label} — use your office computer";
        }
    }
}
