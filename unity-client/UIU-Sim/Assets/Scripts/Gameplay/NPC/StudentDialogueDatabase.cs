using System.Collections.Generic;
using UnityEngine;

namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// Pool of student NPC conversations plus shared dialogue settings.
    /// The default instance lives at <c>Resources/StudentDialogue/StudentDialogueDatabase</c> so NPCs
    /// work without per-object wiring; any NPC may override it in the Inspector.
    /// </summary>
    [CreateAssetMenu(
        fileName = "StudentDialogueDatabase",
        menuName = "UIU Simulator/Dialogue/Student Dialogue Database")]
    public sealed class StudentDialogueDatabase : ScriptableObject
    {
        public const string DefaultResourcePath = "StudentDialogue/StudentDialogueDatabase";

        [SerializeField] private List<StudentDialogueTree> trees = new List<StudentDialogueTree>();

        [Header("Shared Lines")]
        [Tooltip("How student NPCs address a faculty player ({honorific} token).")]
        [SerializeField] private string facultyHonorific = "sir";

        [Tooltip("Close-button line on goodbye nodes when a tree does not set its own.")]
        [SerializeField] private string defaultClosingPlayerLine = "See you around!";

        [Tooltip("Extra choice on every non-final node that ends the conversation early (no Aura).")]
        [SerializeField] private string leaveChoiceLine = "(Leave) Sorry, I have to go.";

        [Tooltip("Interaction response when no conversation is valid for the player right now.")]
        [SerializeField] private string noConversationLine = "Hey! Catch you later, I'm heading somewhere.";

        private static StudentDialogueDatabase cachedDefault;

        public IReadOnlyList<StudentDialogueTree> Trees => trees;
        public string FacultyHonorific => facultyHonorific;
        public string DefaultClosingPlayerLine => defaultClosingPlayerLine;
        public string LeaveChoiceLine => leaveChoiceLine;
        public string NoConversationLine => noConversationLine;

        public static StudentDialogueDatabase LoadDefault()
        {
            if (cachedDefault == null)
            {
                cachedDefault = Resources.Load<StudentDialogueDatabase>(DefaultResourcePath);
            }

            return cachedDefault;
        }

        public List<StudentDialogueTree> GetValidTrees(StudentNpcType npcType, StudentDialogueContext context)
        {
            var valid = new List<StudentDialogueTree>();
            if (trees == null)
            {
                return valid;
            }

            foreach (StudentDialogueTree tree in trees)
            {
                if (tree != null && tree.StartNode != null && tree.IsValidFor(npcType, context))
                {
                    valid.Add(tree);
                }
            }

            return valid;
        }

        /// <summary>
        /// Randomly picks a valid tree, preferring ones not yet used by this NPC today.
        /// Falls back to any valid tree once the unused pool is exhausted.
        /// </summary>
        public StudentDialogueTree SelectTree(
            StudentNpcType npcType,
            StudentDialogueContext context,
            ICollection<string> usedTreeIds,
            System.Random random)
        {
            List<StudentDialogueTree> valid = GetValidTrees(npcType, context);
            if (valid.Count == 0)
            {
                return null;
            }

            List<StudentDialogueTree> fresh = valid;
            if (usedTreeIds != null && usedTreeIds.Count > 0)
            {
                fresh = valid.FindAll(tree => !usedTreeIds.Contains(tree.TreeId));
                if (fresh.Count == 0)
                {
                    fresh = valid;
                }
            }

            return fresh[(random ?? new System.Random()).Next(fresh.Count)];
        }

        /// <summary>Test / tooling seam.</summary>
        public void SetTreesForTesting(List<StudentDialogueTree> newTrees)
        {
            trees = newTrees ?? new List<StudentDialogueTree>();
        }
    }
}
