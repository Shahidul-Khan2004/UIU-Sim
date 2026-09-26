using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.NPC;
using UIU.Simulator.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UIU.Simulator.Gameplay.Tests
{
    internal static class StudentDialogueTestData
    {
        public static StudentDialogueTree BuildTree(
            string id,
            StudentNpcType type,
            DialoguePlayerRole role = DialoguePlayerRole.Student,
            DialogueDepartment department = DialogueDepartment.Any)
        {
            var nodes = new List<StudentDialogueNode>
            {
                Question(id, "q1", "auto"),
                new StudentDialogueNode
                {
                    id = "auto",
                    npcLine = $"{id} auto question",
                    choices = new List<StudentDialogueChoice>
                    {
                        new StudentDialogueChoice
                        {
                            playerLine = "Yeah, I'm in {department}, my {trimester} trimester.",
                            npcReaction = $"{id} auto reaction",
                            nextNodeId = "q2"
                        }
                    }
                },
                Question(id, "q2", "q3"),
                Question(id, "q3", "q4"),
                Question(id, "q4", "end"),
                new StudentDialogueNode { id = "end", npcLine = $"Goodbye from {id}, {{honorific}}." }
            };

            StudentDialogueTree tree = ScriptableObject.CreateInstance<StudentDialogueTree>();
            tree.Configure(
                id,
                type,
                new StudentDialogueRequirements { playerRole = role, department = department, semester = 0 },
                "q1",
                nodes,
                "Bye!");
            return tree;
        }

        private static StudentDialogueNode Question(string treeId, string nodeId, string next)
        {
            var node = new StudentDialogueNode { id = nodeId, npcLine = $"{treeId} {nodeId} line" };
            for (int i = 1; i <= 3; i++)
            {
                node.choices.Add(new StudentDialogueChoice
                {
                    playerLine = $"{nodeId} option {i}",
                    npcReaction = $"{treeId} {nodeId} reaction {i}",
                    nextNodeId = next
                });
            }

            return node;
        }

        public static StudentDialogueDatabase BuildDatabase(params StudentDialogueTree[] trees)
        {
            StudentDialogueDatabase database = ScriptableObject.CreateInstance<StudentDialogueDatabase>();
            database.SetTreesForTesting(trees.ToList());
            return database;
        }

        public static StudentDialogueContext Context(
            string role = "STUDENT",
            string department = "CSE",
            int day = 1,
            bool assessmentDay = false)
        {
            var courses = department == "CSE"
                ? new[]
                {
                    new StudentDialogueContext.CourseInfo("ICS", "Introduction to Computer Science"),
                    new StudentDialogueContext.CourseInfo("ENGLISH", "English"),
                    new StudentDialogueContext.CourseInfo("DM", "Discrete Mathematics")
                }
                : Array.Empty<StudentDialogueContext.CourseInfo>();
            return new StudentDialogueContext(role, department, 1, day, role == "STUDENT" ? courses : null, assessmentDay, "Nadia Rahman", "011221001");
        }
    }

    /// <summary>Content and selection rules for the shipped student dialogue database.</summary>
    [TestFixture]
    public sealed class StudentDialogueContentTests
    {
        private StudentDialogueDatabase database;

        [SetUp]
        public void SetUp()
        {
            database = Resources.Load<StudentDialogueDatabase>(StudentDialogueDatabase.DefaultResourcePath);
            Assert.That(database, Is.Not.Null, "Default StudentDialogueDatabase must exist in Resources.");
        }

        [Test]
        public void EveryShippedTree_PassesStructuralValidation_WithFourPlusExchanges()
        {
            var errors = new List<string>();
            foreach (StudentDialogueTree tree in database.Trees)
            {
                Assert.That(tree, Is.Not.Null, "Database contains a missing tree reference.");
                tree.Validate(errors);
            }

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Assert.That(database.Trees.Select(t => t.TreeId).Distinct().Count(), Is.EqualTo(database.Trees.Count), "Tree ids must be unique.");
        }

        [Test]
        public void Day1Pools_HaveAtLeastTenStudentTreesPerNpcType()
        {
            foreach (StudentNpcType type in new[] { StudentNpcType.Batchmate, StudentNpcType.Senior })
            {
                var union = new HashSet<string>();
                foreach (string dept in new[] { "CSE", "BBA" })
                {
                    List<StudentDialogueTree> valid = database.GetValidTrees(type, StudentDialogueTestData.Context(department: dept));
                    Assert.That(valid.Count, Is.GreaterThanOrEqualTo(9), $"{type} pool for {dept} is too small.");
                    union.UnionWith(valid.Select(t => t.TreeId));
                }

                Assert.That(union.Count, Is.GreaterThanOrEqualTo(10), $"{type} Day 1 pool must have at least 10 trees.");
            }
        }

        [Test]
        public void FacultyDialogue_IsSeparateFromStudentDialogue()
        {
            foreach (StudentNpcType type in new[] { StudentNpcType.Batchmate, StudentNpcType.Senior })
            {
                List<StudentDialogueTree> faculty = database.GetValidTrees(type, StudentDialogueTestData.Context(role: "FACULTY"));
                List<StudentDialogueTree> student = database.GetValidTrees(type, StudentDialogueTestData.Context());

                Assert.That(faculty, Is.Not.Empty, $"{type} needs faculty conversations.");
                Assert.That(faculty.All(t => t.Requirements.playerRole == DialoguePlayerRole.Faculty), Is.True);
                Assert.That(faculty.Select(t => t.TreeId).Intersect(student.Select(t => t.TreeId)), Is.Empty);
                Assert.That(
                    faculty.SelectMany(t => t.Nodes).Any(n => n.npcLine.Contains("{honorific}")),
                    Is.True,
                    "Faculty conversations should address the player respectfully.");
            }
        }

        [Test]
        public void DepartmentAwareSelection_CseGetsIcs_BbaGetsBusinessCourses()
        {
            var cse = database.GetValidTrees(StudentNpcType.Batchmate, StudentDialogueTestData.Context(department: "CSE")).Select(t => t.TreeId).ToList();
            var bba = database.GetValidTrees(StudentNpcType.Batchmate, StudentDialogueTestData.Context(department: "BBA")).Select(t => t.TreeId).ToList();

            Assert.That(cse, Does.Contain("batchmate_ics_discussion"));
            Assert.That(cse, Does.Not.Contain("batchmate_bba_courses"));
            Assert.That(bba, Does.Contain("batchmate_bba_courses"));
            Assert.That(bba, Does.Not.Contain("batchmate_ics_discussion"));

            var random = new System.Random(7);
            for (int i = 0; i < 200; i++)
            {
                StudentDialogueTree picked = database.SelectTree(StudentNpcType.Batchmate, StudentDialogueTestData.Context(department: "BBA"), null, random);
                Assert.That(picked.TreeId, Is.Not.EqualTo("batchmate_ics_discussion"));
            }
        }

        [Test]
        public void DayAndAssessmentFilters_ExcludeInvalidConversations()
        {
            var day1 = database.GetValidTrees(StudentNpcType.Batchmate, StudentDialogueTestData.Context(day: 1)).Select(t => t.TreeId).ToList();
            var day4 = database.GetValidTrees(StudentNpcType.Batchmate, StudentDialogueTestData.Context(day: 4)).Select(t => t.TreeId).ToList();
            Assert.That(day1, Does.Contain("batchmate_first_meeting"));
            Assert.That(day4, Does.Not.Contain("batchmate_first_meeting"));

            var quizDay = database.GetValidTrees(StudentNpcType.Senior, StudentDialogueTestData.Context(day: 2, assessmentDay: true)).Select(t => t.TreeId).ToList();
            var classDay = database.GetValidTrees(StudentNpcType.Senior, StudentDialogueTestData.Context(day: 4)).Select(t => t.TreeId).ToList();
            Assert.That(quizDay, Does.Not.Contain("senior_preparing_quizzes"));
            Assert.That(classDay, Does.Contain("senior_preparing_quizzes"));
        }

        [Test]
        public void Tokens_FillPlayerDataInsteadOfAsking()
        {
            StudentDialogueContext cse = StudentDialogueTestData.Context();
            StudentDialogueTree ics = database.Trees.First(t => t.TreeId == "batchmate_ics_discussion");

            Assert.That(
                StudentDialogueText.Resolve("Yeah, I'm in my {trimester} trimester.", cse, null, "sir"),
                Is.EqualTo("Yeah, I'm in my first trimester."));
            Assert.That(
                StudentDialogueText.Resolve("How are you finding {course}?", cse, ics, "sir"),
                Is.EqualTo("How are you finding Introduction to Computer Science?"));
            Assert.That(
                StudentDialogueText.Resolve("{department} / {departmentName} / {name}", StudentDialogueTestData.Context(department: "BBA"), null, "sir"),
                Is.EqualTo("BBA / Business Administration / Nadia"));
            Assert.That(
                StudentDialogueText.Resolve("Good morning, {honorific}.", StudentDialogueTestData.Context(role: "FACULTY"), null, "sir"),
                Is.EqualTo("Good morning, sir."));
        }

        [Test]
        public void Validation_RejectsShortConversations()
        {
            StudentDialogueTree tree = ScriptableObject.CreateInstance<StudentDialogueTree>();
            tree.Configure("too_short", StudentNpcType.Batchmate, null, "q1", new List<StudentDialogueNode>
            {
                new StudentDialogueNode
                {
                    id = "q1",
                    npcLine = "Hi?",
                    choices = new List<StudentDialogueChoice>
                    {
                        new StudentDialogueChoice { playerLine = "Hi.", npcReaction = "Cool.", nextNodeId = "end" }
                    }
                },
                new StudentDialogueNode { id = "end", npcLine = "Bye." }
            });

            var errors = new List<string>();
            Assert.That(tree.Validate(errors), Is.False);
            Assert.That(errors.Any(e => e.Contains("exchanges")), Is.True);
            Object.DestroyImmediate(tree);
        }
    }

    /// <summary>
    /// NPC integration: E-interaction starts DialogueUI conversations, choices advance by mouse or 1/2/3,
    /// and completion requests the server-authoritative NPC_TALK activity (+2 Aura, once per NPC per day).
    /// </summary>
    [TestFixture]
    public sealed class StudentNpcConversationTests
    {
        private GameObject playerObject;
        private PlayerSaveState saveState;
        private PlayerStats playerStats;
        private DailyActivityState dailyActivityState;
        private FakeNpcRewardSync sync;
        private readonly List<Object> created = new List<Object>();
        private readonly List<string> lineHistory = new List<string>();

        private sealed class FakeNpcRewardSync : IActivityProgressSync
        {
            private readonly PlayerStats stats;
            private readonly HashSet<string> resolvedToday = new HashSet<string>();

            public FakeNpcRewardSync(PlayerStats stats)
            {
                this.stats = stats;
            }

            public bool IsHydrated => true;
            public bool IsMutationInFlight => false;
            public int RequestCount { get; private set; }
            public string LastActivityId { get; private set; }
            public string LastOutcome { get; private set; }

            /// <summary>Mirrors the backend clearing current-day activity rows on day advance.</summary>
            public void StartNewDay() => resolvedToday.Clear();

            public void RequestActivityResolve(
                string activityId,
                string outcome,
                Action<ActivityResolveResult> onSuccess,
                Action onFailure)
            {
                RequestCount++;
                LastActivityId = activityId;
                LastOutcome = outcome;

                // Mirrors NpcConversationDefinition: first completion per NPC per day = +2 Aura.
                bool alreadyResolved = !resolvedToday.Add(activityId);
                int delta = alreadyResolved ? 0 : 2;
                float aura = stats.Aura + delta;
                stats.ApplyServerState(
                    aura,
                    stats.AcademicReputation,
                    alreadyResolved ? StatUpdateSource.InitialHydration : StatUpdateSource.GameplayMutation);
                onSuccess?.Invoke(new ActivityResolveResult(
                    new ActivityRecord(activityId, ActivityStatus.Completed, outcome, delta, 0, 1),
                    alreadyResolved,
                    aura,
                    stats.AcademicReputation));
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Walking a conversation re-shows DialogueUI, which requires play mode (runtime Destroy).
            yield return new EnterPlayMode();
            if (PlayerSaveState.Instance != null)
            {
                Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            }

            StudentConversationMemory.Clear();
            lineHistory.Clear();

            playerObject = new GameObject("NpcTestPlayer");
            playerStats = playerObject.AddComponent<PlayerStats>();
            dailyActivityState = playerObject.AddComponent<DailyActivityState>();
            playerObject.AddComponent<DialogueUI>();
            saveState = playerObject.AddComponent<PlayerSaveState>();
            saveState.enabled = false; // No live hydration/network requests in this fixture.
            SetPlayer("STUDENT", "CSE", day: 1);
            playerStats.ApplyServerState(50f, 50f, StatUpdateSource.InitialHydration);

            sync = new FakeNpcRewardSync(playerStats);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (DialogueUI.Instance != null && DialogueUI.IsOpen)
            {
                DialogueUI.Instance.Hide();
            }

            foreach (Object obj in created)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            created.Clear();
            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }

            StudentConversationMemory.Clear();
            yield return new ExitPlayMode();
        }

        // ── Starting ──────────────────────────────────────────────────────

        [Test]
        public void Interact_StartsDialogueThroughExistingDialogueUI()
        {
            var npc = CreateNpc<SeniorStudentNPC>("SENIOR_TEST", Db(StudentDialogueTestData.BuildTree("senior_a", StudentNpcType.Senior)));

            string response = npc.Interact();

            Assert.That(response, Is.Null, "Conversations use DialogueUI, not the interaction response line.");
            Assert.That(DialogueUI.IsOpen, Is.True);
            Assert.That(SpeakerLabel(), Is.EqualTo("Senior Student"));
            Assert.That(CurrentLine(), Is.EqualTo("senior_a q1 line"));
            Assert.That(ChoiceCount(), Is.EqualTo(4), "Three responses plus the leave option.");
            Assert.That(npc.InteractionPrompt, Is.EqualTo("Talk to Senior Student"));
            Assert.That(npc.ActivityId, Is.EqualTo("NPC_TALK_SENIOR_TEST"));
        }

        [Test]
        public void Interact_WhileDialogueOpen_DoesNotRestartConversation()
        {
            var npc = CreateNpc<BatchmateStudentNPC>("MATE_OPEN", Db(StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate)));
            npc.Interact();
            StudentConversationRunner first = npc.ActiveConversation;

            Assert.That(npc.Interact(), Is.Null);
            Assert.That(npc.ActiveConversation, Is.SameAs(first));
        }

        [Test]
        public void NpcOnModelWithoutCollider_AddsInteractionCollider()
        {
            var model = new GameObject("StudentModel01");
            created.Add(model);
            var body = new GameObject("Body");
            body.transform.SetParent(model.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            body.AddComponent<MeshRenderer>();

            model.AddComponent<BatchmateStudentNPC>();

            Assert.That(model.GetComponentInChildren<Collider>(), Is.Not.Null);
        }

        // ── Choices ───────────────────────────────────────────────────────

        [Test]
        public void MouseChoice_ShowsReactionThenNextLine()
        {
            var npc = CreateNpc<BatchmateStudentNPC>("MATE_CHOICE", Db(StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate)));
            npc.Interact();

            DialogueUI.Instance.SelectChoice(1);

            Assert.That(npc.ActiveConversation.ExchangesCompleted, Is.EqualTo(1));
            Assert.That(npc.ActiveConversation.CurrentNode.id, Is.EqualTo("auto"));
            Assert.That(CurrentLine(), Is.EqualTo("mate_a q1 reaction 2\n\nmate_a auto question"));
        }

        [Test]
        public void AutomaticResponse_IsFilledFromPlayerData()
        {
            var npc = CreateNpc<BatchmateStudentNPC>("MATE_AUTO", Db(StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate)));
            npc.Interact();
            DialogueUI.Instance.SelectChoice(0);

            Assert.That(ChoiceLabels()[0], Is.EqualTo("[1] Yeah, I'm in CSE, my first trimester."));
            Assert.That(ChoiceCount(), Is.EqualTo(2), "Automatic response plus the leave option.");
        }

        [Test]
        public void KeyboardNumbers123_SelectMatchingChoices()
        {
            var npc = CreateNpc<BatchmateStudentNPC>("MATE_KEYS", Db(StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate)));
            npc.Interact();

            PressKey(Key.Digit2);
            Assert.That(CurrentLine(), Does.StartWith("mate_a q1 reaction 2"));

            PressKey(Key.Digit1); // automatic response
            Assert.That(npc.ActiveConversation.CurrentNode.id, Is.EqualTo("q2"));

            PressKey(Key.Digit3);
            Assert.That(CurrentLine(), Does.StartWith("mate_a q2 reaction 3"));

            PressKey(Key.Digit1);
            Assert.That(CurrentLine(), Does.StartWith("mate_a q3 reaction 1"));
            Assert.That(npc.ActiveConversation.ExchangesCompleted, Is.EqualTo(4));
        }

        // ── Role / department ─────────────────────────────────────────────

        [Test]
        public void FacultyPlayer_GetsFacultyDialogue_AndNoAura()
        {
            SetPlayer("FACULTY", "CSE", day: 1);
            var npc = CreateNpc<SeniorStudentNPC>(
                "SENIOR_FACULTY",
                Db(
                    StudentDialogueTestData.BuildTree("student_only", StudentNpcType.Senior),
                    StudentDialogueTestData.BuildTree("faculty_only", StudentNpcType.Senior, DialoguePlayerRole.Faculty)));

            npc.Interact();
            Assert.That(npc.ActiveConversation.Tree.TreeId, Is.EqualTo("faculty_only"));

            CompleteConversation(npc);

            Assert.That(lineHistory.Last(), Does.Contain("Goodbye from faculty_only, sir."));
            Assert.That(sync.RequestCount, Is.Zero, "Faculty conversations never request Aura.");
            Assert.That(playerStats.Aura, Is.EqualTo(50f));
        }

        [Test]
        public void StudentPlayer_NeverGetsFacultyDialogue()
        {
            var npc = CreateNpc<SeniorStudentNPC>(
                "SENIOR_STUDENT",
                Db(
                    StudentDialogueTestData.BuildTree("student_only", StudentNpcType.Senior),
                    StudentDialogueTestData.BuildTree("faculty_only", StudentNpcType.Senior, DialoguePlayerRole.Faculty)));

            for (int i = 0; i < 3; i++)
            {
                npc.Interact();
                Assert.That(npc.ActiveConversation.Tree.TreeId, Is.EqualTo("student_only"));
                CompleteConversation(npc);
            }
        }

        [Test]
        public void DepartmentAwareSelection_UsesPlayerDepartment()
        {
            SetPlayer("STUDENT", "BBA", day: 1);
            var npc = CreateNpc<BatchmateStudentNPC>(
                "MATE_DEPT",
                Db(
                    StudentDialogueTestData.BuildTree("cse_tree", StudentNpcType.Batchmate, department: DialogueDepartment.CSE),
                    StudentDialogueTestData.BuildTree("bba_tree", StudentNpcType.Batchmate, department: DialogueDepartment.BBA)));

            npc.Interact();

            Assert.That(npc.ActiveConversation.Tree.TreeId, Is.EqualTo("bba_tree"));
        }

        [Test]
        public void NpcType_OnlyUsesItsOwnPool()
        {
            StudentDialogueDatabase db = Db(
                StudentDialogueTestData.BuildTree("senior_tree", StudentNpcType.Senior),
                StudentDialogueTestData.BuildTree("mate_tree", StudentNpcType.Batchmate));
            var senior = CreateNpc<SeniorStudentNPC>("SENIOR_POOL", db);

            senior.Interact();

            Assert.That(senior.ActiveConversation.Tree.TreeId, Is.EqualTo("senior_tree"));
        }

        [Test]
        public void ShippedDatabase_SameNpcSameDay_DoesNotRepeatConversation()
        {
            var npc = CreateNpc<BatchmateStudentNPC>("MATE_REAL_DB", null);
            int poolSize = StudentDialogueDatabase.LoadDefault()
                .GetValidTrees(StudentNpcType.Batchmate, StudentDialogueContext.FromGameState(saveState, dailyActivityState))
                .Count;
            Assert.That(poolSize, Is.GreaterThanOrEqualTo(9));

            var seen = new HashSet<string>();
            for (int i = 0; i < poolSize; i++)
            {
                npc.Interact();
                Assert.That(seen.Add(npc.ActiveConversation.Tree.TreeId), Is.True, "Repeated a conversation before exhausting the pool.");
                Assert.That(npc.ActiveConversation.Tree.Requirements.playerRole, Is.EqualTo(DialoguePlayerRole.Student));
                CompleteConversation(npc);
            }

            Assert.That(sync.RequestCount, Is.EqualTo(1));
            Assert.That(playerStats.Aura, Is.EqualTo(52f));
        }

        // ── Aura reward ───────────────────────────────────────────────────

        [Test]
        public void CompletingConversation_Requests_NpcTalkActivity_AndAwardsPlus2Aura()
        {
            var npc = CreateNpc<BatchmateStudentNPC>("MATE_REWARD", Db(StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate)));
            npc.Interact();

            // Rewards only after the goodbye node's close button.
            for (int i = 0; i < 5; i++)
            {
                DialogueUI.Instance.SelectChoice(0);
            }

            Assert.That(npc.ActiveConversation.CurrentNode.IsEnd, Is.True);
            Assert.That(CurrentLine(), Does.EndWith("Goodbye from mate_a, sir."));
            Assert.That(sync.RequestCount, Is.Zero, "No reward before the conversation closes.");

            DialogueUI.Instance.SelectChoice(0);

            Assert.That(npc.ActiveConversation.State, Is.EqualTo(StudentConversationRunner.RunState.Completed));
            Assert.That(DialogueUI.IsOpen, Is.False, "Control returns to gameplay.");
            Assert.That(sync.RequestCount, Is.EqualTo(1));
            Assert.That(sync.LastActivityId, Is.EqualTo("NPC_TALK_MATE_REWARD"));
            Assert.That(sync.LastOutcome, Is.EqualTo("COMPLETED"));
            Assert.That(playerStats.Aura, Is.EqualTo(52f));
        }

        [Test]
        public void RepeatingSameNpcSameDay_DoesNotRewardTwice()
        {
            var npc = CreateNpc<BatchmateStudentNPC>(
                "MATE_REPEAT",
                Db(
                    StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate),
                    StudentDialogueTestData.BuildTree("mate_b", StudentNpcType.Batchmate)));

            npc.Interact();
            CompleteConversation(npc);
            npc.Interact();
            CompleteConversation(npc);

            Assert.That(sync.RequestCount, Is.EqualTo(1));
            Assert.That(playerStats.Aura, Is.EqualTo(52f));

            // Even if local memory is lost (e.g. restart), the server row keeps the reward at once per day.
            StudentConversationMemory.Clear();
            npc.Interact();
            CompleteConversation(npc);

            Assert.That(sync.RequestCount, Is.EqualTo(2));
            Assert.That(playerStats.Aura, Is.EqualTo(52f));
        }

        [Test]
        public void DifferentNpcsSameDay_EachRewardOnce()
        {
            StudentDialogueDatabase db = Db(
                StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate),
                StudentDialogueTestData.BuildTree("senior_a", StudentNpcType.Senior));
            var mate = CreateNpc<BatchmateStudentNPC>("MATE_ONE", db);
            var senior = CreateNpc<SeniorStudentNPC>("SENIOR_ONE", db);

            mate.Interact();
            CompleteConversation(mate);
            senior.Interact();
            CompleteConversation(senior);

            Assert.That(playerStats.Aura, Is.EqualTo(54f));
        }

        [Test]
        public void NewDay_ResetsRewardAvailability()
        {
            var npc = CreateNpc<SeniorStudentNPC>("SENIOR_DAYS", Db(StudentDialogueTestData.BuildTree("senior_a", StudentNpcType.Senior)));
            npc.Interact();
            CompleteConversation(npc);
            Assert.That(playerStats.Aura, Is.EqualTo(52f));

            saveState.SetDayProgressForTesting(1, 2);
            sync.StartNewDay();

            npc.Interact();
            CompleteConversation(npc);

            Assert.That(sync.RequestCount, Is.EqualTo(2));
            Assert.That(playerStats.Aura, Is.EqualTo(54f));
        }

        [Test]
        public void LeavingBeforeCompletion_GivesNoAura()
        {
            var npc = CreateNpc<BatchmateStudentNPC>("MATE_LEAVE", Db(StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate)));
            npc.Interact();
            DialogueUI.Instance.SelectChoice(0);
            DialogueUI.Instance.SelectChoice(0);

            Assert.That(ChoiceLabels().Last(), Does.Contain("(Leave)"));
            DialogueUI.Instance.SelectChoice(ChoiceCount() - 1);

            Assert.That(npc.ActiveConversation.State, Is.EqualTo(StudentConversationRunner.RunState.Abandoned));
            Assert.That(DialogueUI.IsOpen, Is.False);
            Assert.That(sync.RequestCount, Is.Zero);
            Assert.That(playerStats.Aura, Is.EqualTo(50f));
        }

        [Test]
        public void DialogueClosedExternally_CountsAsIncomplete()
        {
            var npc = CreateNpc<BatchmateStudentNPC>("MATE_CLOSED", Db(StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate)));
            npc.Interact();
            StudentConversationRunner first = npc.ActiveConversation;
            DialogueUI.Instance.Hide();

            npc.Interact();

            Assert.That(first.State, Is.EqualTo(StudentConversationRunner.RunState.Abandoned));
            Assert.That(sync.RequestCount, Is.Zero);
            Assert.That(playerStats.Aura, Is.EqualTo(50f));
        }

        [Test]
        public void NoActiveUniversityDay_OnlyNods()
        {
            saveState.SetStateForTesting(hasSaveValue: false, idCardIssuedValue: false);
            var npc = CreateNpc<BatchmateStudentNPC>("MATE_VISITOR", Db(StudentDialogueTestData.BuildTree("mate_a", StudentNpcType.Batchmate)));

            string response = npc.Interact();

            Assert.That(response, Does.Contain("nod"));
            Assert.That(DialogueUI.IsOpen, Is.False);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void SetPlayer(string role, string department, int day)
        {
            saveState.SetIdentityForTesting(role, department);
            saveState.SetDayProgressForTesting(1, day);
        }

        private StudentDialogueDatabase Db(params StudentDialogueTree[] trees)
        {
            created.AddRange(trees);
            StudentDialogueDatabase db = StudentDialogueTestData.BuildDatabase(trees);
            created.Add(db);
            return db;
        }

        private T CreateNpc<T>(string npcId, StudentDialogueDatabase database) where T : StudentNPCBase
        {
            var go = new GameObject(npcId);
            created.Add(go);
            go.AddComponent<BoxCollider>();
            T npc = go.AddComponent<T>();
            npc.SetNpcIdForTesting(npcId);
            npc.SetProgressSyncForTesting(sync);
            npc.SetRandomSeedForTesting(12345);
            if (database != null)
            {
                npc.SetDatabaseForTesting(database);
            }

            return npc;
        }

        private void CompleteConversation(StudentNPCBase npc)
        {
            StudentConversationRunner runner = npc.ActiveConversation;
            for (int guard = 0; guard < 20 && runner.State == StudentConversationRunner.RunState.Running; guard++)
            {
                lineHistory.Add(CurrentLine());
                DialogueUI.Instance.SelectChoice(0);
            }

            Assert.That(runner.State, Is.EqualTo(StudentConversationRunner.RunState.Completed));
        }

        private static string CurrentLine() => Label("dialogueLabel");
        private static string SpeakerLabel() => Label("speakerLabel");

        private static string Label(string field)
        {
            FieldInfo info = typeof(DialogueUI).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            return ((TextMeshProUGUI)info.GetValue(DialogueUI.Instance)).text;
        }

        private static int ChoiceCount() => ChoiceLabels().Count;

        private static List<string> ChoiceLabels()
        {
            FieldInfo info = typeof(DialogueUI).GetField("activeButtons", BindingFlags.Instance | BindingFlags.NonPublic);
            var buttons = (List<GameObject>)info.GetValue(DialogueUI.Instance);
            return buttons.Select(b => b.GetComponentInChildren<TextMeshProUGUI>().text).ToList();
        }

        private static void PressKey(Key key)
        {
            // Same synthetic-input approach as LibraryStudyTests: no focused Game view in batch mode.
            InputSettings settings = InputSystem.settings;
            var previousBackground = settings.backgroundBehavior;
            var previousEditorInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                _ = keyboard[key].wasPressedThisFrame;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                InputSystem.Update();
                keyboard.MakeCurrent();
                Assert.That(keyboard[key].wasPressedThisFrame, Is.True, $"Synthetic {key} was not delivered.");
                typeof(DialogueUI).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(DialogueUI.Instance, null);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior = previousBackground;
                settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
        }
    }
}
