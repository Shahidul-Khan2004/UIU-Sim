using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// Walks one <see cref="StudentDialogueTree"/> through the shared <see cref="DialogueUI"/>.
    /// Each player choice shows the NPC reaction followed by the next node's line. The goodbye node
    /// gets a single close button; selecting it is the only path to <see cref="RunState.Completed"/>.
    /// </summary>
    public sealed class StudentConversationRunner
    {
        public enum RunState
        {
            NotStarted,
            Running,
            Completed,
            Abandoned
        }

        private readonly DialogueUI dialogueUI;
        private readonly StudentDialogueTree tree;
        private readonly StudentDialogueContext context;
        private readonly string speakerName;
        private readonly string honorific;
        private readonly string closingPlayerLine;
        private readonly string leaveChoiceLine;
        private readonly Action onCompleted;
        private readonly Action onAbandoned;

        public StudentConversationRunner(
            DialogueUI dialogueUI,
            StudentDialogueTree tree,
            StudentDialogueContext context,
            string speakerName,
            StudentDialogueDatabase settings,
            Action onCompleted,
            Action onAbandoned)
        {
            this.dialogueUI = dialogueUI;
            this.tree = tree;
            this.context = context;
            this.speakerName = speakerName;
            this.onCompleted = onCompleted;
            this.onAbandoned = onAbandoned;

            honorific = settings != null ? settings.FacultyHonorific : "sir";
            leaveChoiceLine = settings != null ? settings.LeaveChoiceLine : null;
            closingPlayerLine = !string.IsNullOrWhiteSpace(tree.ClosingPlayerLine)
                ? tree.ClosingPlayerLine
                : settings != null && !string.IsNullOrWhiteSpace(settings.DefaultClosingPlayerLine)
                    ? settings.DefaultClosingPlayerLine
                    : "See you around!";
        }

        public RunState State { get; private set; } = RunState.NotStarted;
        public StudentDialogueTree Tree => tree;
        public StudentDialogueNode CurrentNode { get; private set; }
        public int ExchangesCompleted { get; private set; }

        public bool Start()
        {
            if (State != RunState.NotStarted || dialogueUI == null || tree == null || tree.StartNode == null)
            {
                return false;
            }

            State = RunState.Running;
            ShowNode(tree.StartNode, null);
            return true;
        }

        /// <summary>Ends the conversation without completion (no reward).</summary>
        public void Abandon()
        {
            if (State != RunState.Running)
            {
                return;
            }

            State = RunState.Abandoned;
            if (DialogueUI.IsOpen && dialogueUI != null)
            {
                dialogueUI.Hide();
            }

            Debug.Log($"[StudentConversation] '{tree.TreeId}' with {speakerName} left before completion.");
            onAbandoned?.Invoke();
        }

        private void ShowNode(StudentDialogueNode node, string reactionPrefix)
        {
            CurrentNode = node;

            string line = Resolve(node.npcLine);
            if (!string.IsNullOrWhiteSpace(reactionPrefix))
            {
                line = reactionPrefix + "\n\n" + line;
            }

            var choices = new List<DialogueUI.Choice>();
            if (node.IsEnd)
            {
                choices.Add(new DialogueUI.Choice(Resolve(closingPlayerLine), Complete));
            }
            else
            {
                foreach (StudentDialogueChoice choice in node.choices)
                {
                    StudentDialogueChoice captured = choice;
                    choices.Add(new DialogueUI.Choice(Resolve(captured.playerLine), () => OnChoiceSelected(captured)));
                }

                if (!string.IsNullOrWhiteSpace(leaveChoiceLine))
                {
                    choices.Add(new DialogueUI.Choice(Resolve(leaveChoiceLine), Abandon));
                }
            }

            dialogueUI.Show(speakerName, line, choices.ToArray());
        }

        private void OnChoiceSelected(StudentDialogueChoice choice)
        {
            if (State != RunState.Running)
            {
                return;
            }

            StudentDialogueNode next = tree.GetNode(choice.nextNodeId);
            if (next == null)
            {
                Debug.LogError($"[StudentConversation] '{tree.TreeId}' references missing node '{choice.nextNodeId}'.");
                Abandon();
                return;
            }

            ExchangesCompleted++;
            ShowNode(next, Resolve(choice.npcReaction));
        }

        private void Complete()
        {
            if (State != RunState.Running)
            {
                return;
            }

            State = RunState.Completed;
            Debug.Log($"[StudentConversation] '{tree.TreeId}' with {speakerName} completed after {ExchangesCompleted} exchanges.");
            onCompleted?.Invoke();
        }

        private string Resolve(string text)
        {
            return StudentDialogueText.Resolve(text, context, tree, honorific);
        }
    }
}
