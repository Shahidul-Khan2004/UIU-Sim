using System.Collections.Generic;
using UnityEngine;

namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// Current-day conversation memory keyed by NPC ID + save + trimester day. Not a friendship system:
    /// every entry is dropped as soon as the day changes. The backend activity row remains the
    /// authority for whether Aura was granted; this only avoids repeat trees and redundant requests.
    /// </summary>
    public static class StudentConversationMemory
    {
        public sealed class Entry
        {
            public readonly HashSet<string> SeenTreeIds = new HashSet<string>();
            public bool ConversationCompleted;
            public bool RewardClaimed;
            public bool RewardRequestInFlight;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        private static string currentDayScope;

        public static Entry For(string npcId, StudentDialogueContext context)
        {
            string scope = context != null
                ? $"{context.UniversityId}|{context.Semester}|{context.Day}"
                : string.Empty;
            if (scope != currentDayScope)
            {
                Entries.Clear();
                currentDayScope = scope;
            }

            string key = npcId ?? string.Empty;
            if (!Entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry();
                Entries[key] = entry;
            }

            return entry;
        }

        public static void Clear()
        {
            Entries.Clear();
            currentDayScope = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeEnter()
        {
            Clear();
        }
    }
}
