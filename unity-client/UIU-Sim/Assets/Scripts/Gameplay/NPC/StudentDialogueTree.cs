using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// One data-driven campus conversation. Add the asset to a <see cref="StudentDialogueDatabase"/>
    /// to make it available to <see cref="SeniorStudentNPC"/> / <see cref="BatchmateStudentNPC"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "StudentDialogueTree",
        menuName = "UIU Simulator/Dialogue/Student Dialogue Tree")]
    public sealed class StudentDialogueTree : ScriptableObject
    {
        public const int MaxChoicesPerNode = 3;
        public const int MinExchanges = 4;
        public const int MinOpinionExchanges = 3;

        [Tooltip("Stable unique id, used for per-NPC same-day repeat avoidance.")]
        [SerializeField] private string treeId = string.Empty;

        [Tooltip("Designer note describing the conversation topic.")]
        [SerializeField] private string topic = string.Empty;

        [SerializeField] private StudentNpcType npcType = StudentNpcType.Batchmate;
        [SerializeField] private StudentDialogueRequirements requirements = new StudentDialogueRequirements();
        [SerializeField] private string startNodeId = "start";

        [Tooltip("Player line on the goodbye node's close button. Empty = database default.")]
        [SerializeField] private string closingPlayerLine = string.Empty;

        [SerializeField] private List<StudentDialogueNode> nodes = new List<StudentDialogueNode>();

        public string TreeId => string.IsNullOrWhiteSpace(treeId) ? name : treeId.Trim();
        public string Topic => topic;
        public StudentNpcType NpcType => npcType;
        public StudentDialogueRequirements Requirements => requirements ??= new StudentDialogueRequirements();
        public string StartNodeId => startNodeId;
        public string ClosingPlayerLine => closingPlayerLine;
        public IReadOnlyList<StudentDialogueNode> Nodes => nodes;

        public StudentDialogueNode GetNode(string nodeId)
        {
            if (nodes == null || string.IsNullOrEmpty(nodeId))
            {
                return null;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].id == nodeId)
                {
                    return nodes[i];
                }
            }

            return null;
        }

        public StudentDialogueNode StartNode => GetNode(startNodeId);

        /// <summary>True when this tree may be used by <paramref name="type"/> for the given player state.</summary>
        public bool IsValidFor(StudentNpcType type, StudentDialogueContext context)
        {
            if (context == null || type != npcType)
            {
                return false;
            }

            StudentDialogueRequirements req = Requirements;

            DialoguePlayerRole? role = context.PlayerRole;
            if (role == null || role.Value != req.playerRole)
            {
                return false;
            }

            if (req.department != DialogueDepartment.Any
                && !string.Equals(context.Department, req.department.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (req.semester > 0 && context.Semester != req.semester)
            {
                return false;
            }

            if (context.Day < Mathf.Max(1, req.minDay) || (req.maxDay > 0 && context.Day > req.maxDay))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(req.requiredCourseId) && !context.HasCourse(req.requiredCourseId))
            {
                return false;
            }

            switch (req.assessmentDay)
            {
                case DialogueAssessmentDayRule.NotAssessmentDay when context.IsAssessmentDay:
                case DialogueAssessmentDayRule.AssessmentDayOnly when !context.IsAssessmentDay:
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Structural checks: node graph is acyclic, every path ends on a goodbye node, and every
        /// path has at least <see cref="MinExchanges"/> exchanges (NPC line → player response → NPC reaction).
        /// </summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            string label = $"Tree '{TreeId}'";

            if (nodes == null || nodes.Count == 0)
            {
                errors.Add($"{label}: has no nodes.");
                return false;
            }

            var ids = new HashSet<string>();
            foreach (StudentDialogueNode node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id))
                {
                    errors.Add($"{label}: node with empty id.");
                    continue;
                }

                if (!ids.Add(node.id))
                {
                    errors.Add($"{label}: duplicate node id '{node.id}'.");
                }

                if (string.IsNullOrWhiteSpace(node.npcLine))
                {
                    errors.Add($"{label}: node '{node.id}' has an empty NPC line.");
                }

                StudentDialogueText.CollectUnknownTokens(node.npcLine, $"{label} node '{node.id}'", errors);

                int choiceCount = node.choices?.Count ?? 0;
                if (choiceCount > MaxChoicesPerNode)
                {
                    errors.Add($"{label}: node '{node.id}' has {choiceCount} choices (max {MaxChoicesPerNode}).");
                }

                for (int c = 0; c < choiceCount; c++)
                {
                    StudentDialogueChoice choice = node.choices[c];
                    string where = $"{label} node '{node.id}' choice {c + 1}";
                    if (choice == null || string.IsNullOrWhiteSpace(choice.playerLine))
                    {
                        errors.Add($"{where}: empty player line.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(choice.npcReaction))
                    {
                        errors.Add($"{where}: empty NPC reaction.");
                    }

                    if (GetNode(choice.nextNodeId) == null)
                    {
                        errors.Add($"{where}: next node '{choice.nextNodeId}' does not exist.");
                    }

                    StudentDialogueText.CollectUnknownTokens(choice.playerLine, where, errors);
                    StudentDialogueText.CollectUnknownTokens(choice.npcReaction, where, errors);
                }
            }

            StudentDialogueText.CollectUnknownTokens(closingPlayerLine, $"{label} closing line", errors);

            if (StartNode == null)
            {
                errors.Add($"{label}: start node '{startNodeId}' does not exist.");
            }

            if (errors.Count > before)
            {
                return false;
            }

            var visiting = new HashSet<string>();
            if (!MeasurePaths(StartNode, visiting, out int minExchanges, out int minOpinion))
            {
                errors.Add($"{label}: node graph contains a cycle.");
                return false;
            }

            if (minExchanges < MinExchanges)
            {
                errors.Add($"{label}: shortest path has {minExchanges} exchanges (min {MinExchanges}).");
            }

            if (minOpinion < MinOpinionExchanges)
            {
                errors.Add($"{label}: shortest path has {minOpinion} multi-choice exchanges (min {MinOpinionExchanges}).");
            }

            return errors.Count == before;
        }

        /// <summary>Shortest exchange counts over every path from <paramref name="node"/> to an end node.</summary>
        private bool MeasurePaths(StudentDialogueNode node, HashSet<string> visiting, out int minExchanges, out int minOpinion)
        {
            minExchanges = 0;
            minOpinion = 0;
            if (node.IsEnd)
            {
                return true;
            }

            if (!visiting.Add(node.id))
            {
                return false;
            }

            minExchanges = int.MaxValue;
            minOpinion = int.MaxValue;
            foreach (StudentDialogueChoice choice in node.choices)
            {
                if (!MeasurePaths(GetNode(choice.nextNodeId), visiting, out int childExchanges, out int childOpinion))
                {
                    return false;
                }

                minExchanges = Mathf.Min(minExchanges, childExchanges + 1);
                minOpinion = Mathf.Min(minOpinion, childOpinion + (node.choices.Count > 1 ? 1 : 0));
            }

            visiting.Remove(node.id);
            return true;
        }

        /// <summary>Test / tooling seam for building trees in memory.</summary>
        public void Configure(
            string id,
            StudentNpcType type,
            StudentDialogueRequirements treeRequirements,
            string startId,
            List<StudentDialogueNode> treeNodes,
            string closingLine = "")
        {
            treeId = id;
            npcType = type;
            requirements = treeRequirements ?? new StudentDialogueRequirements();
            startNodeId = startId;
            nodes = treeNodes ?? new List<StudentDialogueNode>();
            closingPlayerLine = closingLine;
        }
    }
}
