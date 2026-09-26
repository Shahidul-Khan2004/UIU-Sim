using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// Fills player-data tokens in dialogue lines so NPCs never have to ask what the game already knows.
    /// <list type="bullet">
    ///   <item><c>{name}</c> — player's first name</item>
    ///   <item><c>{department}</c> — department code (CSE / BBA)</item>
    ///   <item><c>{departmentName}</c> — full department name</item>
    ///   <item><c>{trimester}</c> — ordinal trimester word (first, second, ...)</item>
    ///   <item><c>{day}</c> — current trimester day number</item>
    ///   <item><c>{course}</c> — the tree's required course, else a current course, else a generic phrase</item>
    ///   <item><c>{honorific}</c> — how students address faculty (database setting, default "sir")</item>
    /// </list>
    /// </summary>
    public static class StudentDialogueText
    {
        private static readonly Regex TokenPattern = new Regex(@"\{([A-Za-z]+)\}", RegexOptions.Compiled);

        private static readonly HashSet<string> KnownTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "name", "department", "departmentName", "trimester", "day", "course", "honorific"
        };

        private static readonly string[] Ordinals =
        {
            "zeroth", "first", "second", "third", "fourth", "fifth", "sixth",
            "seventh", "eighth", "ninth", "tenth", "eleventh", "twelfth"
        };

        public static string Resolve(string text, StudentDialogueContext context, StudentDialogueTree tree, string honorific)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
            {
                return text ?? string.Empty;
            }

            return TokenPattern.Replace(text, match =>
            {
                switch (match.Groups[1].Value)
                {
                    case "name": return FirstName(context);
                    case "department": return string.IsNullOrEmpty(context?.Department) ? "your department" : context.Department;
                    case "departmentName": return DepartmentName(context?.Department);
                    case "trimester": return Ordinal(context?.Semester ?? 1);
                    case "day": return (context?.Day ?? 1).ToString();
                    case "course": return CourseName(context, tree);
                    case "honorific": return string.IsNullOrWhiteSpace(honorific) ? "sir" : honorific.Trim();
                    default: return match.Value;
                }
            });
        }

        public static void CollectUnknownTokens(string text, string where, List<string> errors)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (Match match in TokenPattern.Matches(text))
            {
                if (!KnownTokens.Contains(match.Groups[1].Value))
                {
                    errors.Add($"{where}: unknown token '{match.Value}'.");
                }
            }
        }

        public static string DepartmentName(string departmentCode)
        {
            switch ((departmentCode ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "CSE": return "Computer Science and Engineering";
                case "BBA": return "Business Administration";
                case "": return "your department";
                default: return departmentCode.Trim();
            }
        }

        public static string Ordinal(int value)
        {
            return value >= 0 && value < Ordinals.Length ? Ordinals[value] : value + "th";
        }

        private static string FirstName(StudentDialogueContext context)
        {
            string full = context?.PlayerName?.Trim();
            if (string.IsNullOrEmpty(full))
            {
                return "friend";
            }

            int space = full.IndexOf(' ');
            return space > 0 ? full.Substring(0, space) : full;
        }

        private static string CourseName(StudentDialogueContext context, StudentDialogueTree tree)
        {
            string required = tree != null ? tree.Requirements.requiredCourseId : null;
            if (!string.IsNullOrWhiteSpace(required))
            {
                return context?.CourseName(required) ?? required.Trim();
            }

            if (context != null && context.Courses.Count > 0)
            {
                return context.Courses[0].Name;
            }

            return context?.Department == "BBA" ? "the business courses" : "the core courses";
        }
    }
}
