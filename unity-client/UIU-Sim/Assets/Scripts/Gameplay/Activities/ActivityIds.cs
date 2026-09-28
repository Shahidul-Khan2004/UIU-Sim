namespace UIU.Simulator.Gameplay.Activities
{
    /// <summary>Stable activity identifiers. Definitions live in Unity configuration.</summary>
    public static class ActivityIds
    {
        public const string Breakfast = "BREAKFAST";
        public const string GetIdCard = "GET_ID_CARD";
        public const string AttendIcs = "ATTEND_ICS";
        public const string AttendEnglish = "ATTEND_ENGLISH";
        public const string AttendDm = "ATTEND_DM";
        public const string LibraryStudy = "LIBRARY_STUDY";
        public const string LibrarySelfStudy = "LIBRARY_SELF_STUDY";

        /// <summary>Per-NPC daily conversation activity: <c>NPC_TALK_&lt;NPC ID&gt;</c>.</summary>
        public const string NpcConversationPrefix = "NPC_TALK_";

        public static bool IsClassroomActivity(string activityId)
        {
            return activityId == AttendIcs || activityId == AttendEnglish || activityId == AttendDm;
        }

        public static bool IsNpcConversation(string activityId)
        {
            return activityId != null && activityId.StartsWith(NpcConversationPrefix, System.StringComparison.Ordinal);
        }
    }
}
