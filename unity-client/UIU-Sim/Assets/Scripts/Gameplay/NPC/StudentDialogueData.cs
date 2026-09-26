using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>Which campus student NPC component may use a dialogue tree.</summary>
    public enum StudentNpcType
    {
        Batchmate = 0,
        Senior = 1
    }

    public enum DialoguePlayerRole
    {
        Student = 0,
        Faculty = 1
    }

    public enum DialogueDepartment
    {
        Any = 0,
        CSE = 1,
        BBA = 2
    }

    public enum DialogueAssessmentDayRule
    {
        Any = 0,
        NotAssessmentDay = 1,
        AssessmentDayOnly = 2
    }

    /// <summary>
    /// Player-state filters a tree must pass before an NPC may pick it.
    /// </summary>
    [Serializable]
    public sealed class StudentDialogueRequirements
    {
        public DialoguePlayerRole playerRole = DialoguePlayerRole.Student;
        public DialogueDepartment department = DialogueDepartment.Any;

        [Tooltip("Trimester this tree belongs to. 0 = any trimester.")]
        [Min(0)] public int semester = 1;

        [Tooltip("First trimester day (inclusive) on which this tree may appear.")]
        [Min(1)] public int minDay = 1;

        [Tooltip("Last trimester day (inclusive). 0 = no upper bound.")]
        [Min(0)] public int maxDay;

        [Tooltip("Course id the player must currently be enrolled in (e.g. ICS). Empty = none. Also drives the {course} token.")]
        public string requiredCourseId = string.Empty;

        public DialogueAssessmentDayRule assessmentDay = DialogueAssessmentDayRule.Any;
    }

    /// <summary>
    /// One player response. A node with a single choice is an automatic response:
    /// the line is filled from player data via tokens instead of asking the player.
    /// </summary>
    [Serializable]
    public sealed class StudentDialogueChoice
    {
        [TextArea(1, 3)] public string playerLine = string.Empty;

        [Tooltip("NPC reaction shown before the next node's line.")]
        [TextArea(1, 3)] public string npcReaction = string.Empty;

        public string nextNodeId = string.Empty;
    }

    /// <summary>
    /// NPC line plus player choices. A node with no choices is the goodbye node that ends the conversation.
    /// </summary>
    [Serializable]
    public sealed class StudentDialogueNode
    {
        public string id = string.Empty;
        [TextArea(2, 5)] public string npcLine = string.Empty;
        public List<StudentDialogueChoice> choices = new List<StudentDialogueChoice>();

        public bool IsEnd => choices == null || choices.Count == 0;
    }
}
