using System;

namespace UIU.Simulator.Gameplay.IDCard
{
    /// <summary>
    /// University-assigned Faculty identity values for this milestone.
    /// Not collected at admission and not persisted yet.
    /// </summary>
    public static class FacultyIdentity
    {
        public const string Role = "FACULTY";
        public const string Designation = "Lecturer";
        public const string OfficeRoom = "335";

        public static bool Matches(string role)
        {
            return !string.IsNullOrWhiteSpace(role)
                && role.Equals(Role, StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatOffice()
        {
            return $"Room {OfficeRoom}";
        }
    }
}
