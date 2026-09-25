using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Assessment;
using UIU.Simulator.Gameplay.Classroom;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace UIU.Simulator.Gameplay.Editor.Tests
{
    public sealed class AssessmentTests
    {
        private GameObject player;
        private PlayerSaveState save;
        private DailyActivityState daily;
        private PlayerMovement movement;
        private FirstPersonLook look;
        private InteractionController interaction;
        private FakeSync sync;
        private IcsClassroomLocationConfig[] locations;
        [SetUp] public void Setup()
        {
            if (PlayerSaveState.Instance != null) Object.DestroyImmediate(PlayerSaveState.Instance.gameObject);
            player = new GameObject("AssessmentTestPlayer"); player.AddComponent<CharacterController>();
            player.AddComponent<PlayerStats>(); movement = player.AddComponent<PlayerMovement>();
            look = player.AddComponent<FirstPersonLook>(); interaction = player.AddComponent<InteractionController>();
            var camera = new GameObject("Camera"); camera.transform.SetParent(player.transform); camera.AddComponent<Camera>();
            save = player.AddComponent<PlayerSaveState>(); save.SetStateForTesting(true,true);
            save.SetDayProgressForTesting(1,2); save.SetIdentityForTesting("STUDENT","CSE");
            daily = player.AddComponent<DailyActivityState>();
            locations = new[] {Location("ICS","427",4),Location("ENGLISH","702",7),Location("DM","423",4)};
            daily.SetLocationConfigForTesting(locations[0]); daily.SetAdditionalLocationConfigsForTesting(new[]{locations[1],locations[2]});
            sync = new FakeSync(); daily.ApplyReportCard(sync.Card);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        [TearDown] public void Cleanup()
        {
            if (AssessmentUI.Instance != null) { AssessmentUI.Instance.CloseModal(); Object.DestroyImmediate(AssessmentUI.Instance.gameObject); }
            if (ReportCardUI.Instance != null) { ReportCardUI.Instance.CloseModal(); Object.DestroyImmediate(ReportCardUI.Instance.gameObject); }
            if (GameMenuManager.Instance != null) Object.DestroyImmediate(GameMenuManager.Instance.gameObject);
            if (DailySummaryUI.Instance != null) Object.DestroyImmediate(DailySummaryUI.Instance.gameObject);
            if (ClassroomChoiceUI.Instance != null) Object.DestroyImmediate(ClassroomChoiceUI.Instance.gameObject);
            Object.DestroyImmediate(player);
            foreach(var config in locations) Object.DestroyImmediate(config);
        }
        [Test] public void DayTwoDoorShowsQuizPreviewWithThreeQuestionsAndBlocksControls()
        {
            var ui = AssessmentUI.EnsureExists(); ui.Configure(sync);
            var doorObject = new GameObject("ICS Door");
            try
            {
                doorObject.AddComponent<BoxCollider>(); var door = doorObject.AddComponent<IcsClassroomInteractable>();
                Field(door,"locationConfig",locations[0]); Assert.That(door.Interact(), Is.Null);
                StringAssert.Contains("QUIZ TODAY", Text(ui)); StringAssert.Contains("15 Marks · 3 Questions",Text(ui));
                Assert.That(Buttons(ui).Any(b=>b.name=="START QUIZ"),Is.True);
                Assert.That(Buttons(ui).Any(b=>b.name.Contains("ATTEND")||b.name.Contains("PROXY")),Is.False);
                Assert.That(movement.enabled || look.enabled || interaction.enabled,Is.False);
                Assert.That(Cursor.visible,Is.True); Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
                Click(ui,"BACK"); Assert.That(movement.enabled && look.enabled && interaction.enabled,Is.True);
            }
            finally {Object.DestroyImmediate(doorObject);}
        }
        [Test] public void AnswersShowServerMarksAndCompleteExactlyThreeQuestions()
        {
            var ui=OpenQuiz();
            Assert.That(sync.StartCount,Is.EqualTo(1)); Assert.That(sync.Result.questionCount,Is.EqualTo(3));
            for(int i=0;i<3;i++) Click(ui,"A. Correct");
            Assert.That(sync.AnswerCount,Is.EqualTo(3)); StringAssert.Contains("Result: 15 / 15",Text(ui));
            Assert.That(Buttons(ui).Any(b=>b.name=="CHEAT"),Is.False);
            Click(ui,"RETURN TO CAMPUS");Assert.That(movement.enabled && look.enabled && interaction.enabled,Is.True);
        }
        [Test] public void CheatDisablesRepeatedInputAndShowsCourseRemovalThenRestoresControls()
        {
            var ui=OpenQuiz(); sync.HoldCheat=true; var cheat=Buttons(ui).Single(b=>b.name=="CHEAT");
            cheat.onClick.Invoke(); cheat.onClick.Invoke();
            Assert.That(sync.CheatCount,Is.EqualTo(1));Assert.That(Buttons(ui).All(b=>!b.interactable),Is.True);
            sync.FinishCheat("CHEAT_CAUGHT");daily.ApplyReportCard(sync.Card);
            StringAssert.Contains("YOU WERE CAUGHT CHEATING",Text(ui));StringAssert.Contains("removed from ICS",Text(ui));
            Assert.That(daily.ShouldShowAttendIcsObjective(save),Is.False);
            Assert.That(daily.BuildAdditionalClassroomObjectives(save).Length,Is.EqualTo(2));
            Click(ui,"RETURN TO CAMPUS");Assert.That(movement.enabled && look.enabled && interaction.enabled,Is.True);
            ui.Show("ICS");StringAssert.Contains("You were removed",Text(ui));
            Assert.That(Buttons(ui).Any(b=>b.name=="START QUIZ"),Is.False);
        }
        [Test] public void CheatSuccessShowsFullMarksAndAppliedClampedDelta()
        {
            var ui=OpenQuiz();sync.HoldCheat=true;Click(ui,"CHEAT");sync.FinishCheat("CHEAT_SUCCESS");
            StringAssert.Contains("Cheated Successfully",Text(ui));StringAssert.Contains("15 / 15",Text(ui));
            StringAssert.Contains("Aura: +2",Text(ui));Assert.That(sync.Card.Course("ICS").IsDropped,Is.False);
        }
        [Test] public void LostCheatResponseOffersExplicitRetryAndCannotResumeAnswers()
        {
            var ui=OpenQuiz();sync.FailCheat=true;Click(ui,"CHEAT");
            Assert.That(Buttons(ui).Any(b=>b.name=="RETRY"),Is.True);
            Assert.That(Buttons(ui).Any(b=>b.name=="A. Correct"||b.name=="CHEAT"),Is.False);
            sync.FailCheat=false;Click(ui,"RETRY");Assert.That(sync.CheatCount,Is.EqualTo(2));
            StringAssert.Contains("YOU WERE CAUGHT CHEATING",Text(ui));
        }
        [Test] public void TimeoutSubmitsUnansweredAndLocksRepeatedTimeouts()
        {
            var ui=OpenQuiz();Field(ui,"deadline",Time.unscaledTime-1);
            typeof(AssessmentUI).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,null);
            Assert.That(sync.LastAnswer,Is.EqualTo(-1));Assert.That(sync.AnswerCount,Is.EqualTo(1));
            Assert.That(sync.Result.marksObtained,Is.EqualTo(0));
        }
        [Test] public void HudUsesPersistentLocationsAndFiltersEachDroppedCourse()
        {
            Assert.That(daily.ShouldShowAttendIcsObjective(save),Is.True);
            Assert.That(daily.AssessmentLocation("ICS"),Is.EqualTo("Go to Room 427 on Floor 4."));
            var views=daily.BuildAdditionalClassroomObjectives(save);
            Assert.That(views[0].Description,Is.EqualTo("Go to Room 702 on Floor 7."));
            Assert.That(views[1].Description,Is.EqualTo("Go to Room 423 on Floor 4."));
            foreach(var course in sync.Card.courses) course.status="DROPPED_CHEATING";
            daily.ApplyReportCard(sync.Card);
            Assert.That(daily.ShouldShowAttendIcsObjective(save),Is.False);Assert.That(daily.BuildAdditionalClassroomObjectives(save),Is.Empty);
            Assert.That(daily.BreakfastStatus,Is.EqualTo(ActivityStatus.Pending));Assert.That(daily.LibraryStudyStatus,Is.EqualTo(ActivityStatus.Pending));
            daily.ResetForNewDay(3);save.SetDayProgressForTesting(1,3);
            Assert.That(daily.IsCourseDropped("ICS"),Is.True);
        }
        [Test] public void ReportCardDropOverridesHistoricalMarksAndModalRestoresOnlyOwnedControls()
        {
            var course=sync.Card.Course("ICS");course.status="DROPPED_CHEATING";course.total=15;course.grade="F";
            course.Component("QUIZ_1").state="COMPLETED";course.Component("QUIZ_1").marksObtained=15;
            look.enabled=false; // unrelated pre-existing disable must be retained
            var ui=ReportCardUI.EnsureExists();ui.Configure(sync);ui.Show();
            StringAssert.Contains("DROPPED — ACADEMIC MISCONDUCT",Text(ui));
            StringAssert.Contains("Final Result: 0 / 100",Text(ui));StringAssert.Contains("Grade: F · Grade Point: 0.00",Text(ui));
            Assert.That(movement.enabled,Is.False);Click(ui,"CLOSE");
            Assert.That(movement.enabled,Is.True);Assert.That(look.enabled,Is.False);Assert.That(AcademicModal.BlocksGameplay,Is.False);
        }
        [Test] public void ActiveReportKeepsFutureComponentsPendingAndGameMenuHandsOffControls()
        {
            var report=ReportCardUI.EnsureExists();report.Configure(sync);
            var menu=GameMenuManager.EnsureExists();menu.Open();Click(menu,"Button_ReportCard");
            Assert.That(GameMenuManager.IsOpen,Is.False);Assert.That(AcademicModal.BlocksGameplay,Is.True);
            StringAssert.Contains("-- / 30",Text(report));StringAssert.Contains("Final Grade: Pending",Text(report));
            Click(report,"CLOSE");Assert.That(movement.enabled && look.enabled && interaction.enabled,Is.True);
            // Batchmode has no focused window, so CursorLockMode.Locked is not applied.
            // The menu/report handoff still hides the cursor when gameplay returns.
            Assert.That(Cursor.visible, Is.False);
        }
        [Test] public void DayOneNormalObjectivesRemainAndNewGameClearsDropCache()
        {
            save.SetDayProgressForTesting(1,1);sync.Card.day=1;sync.Card.scheduledAssessment=null;daily.ApplyReportCard(sync.Card);
            Assert.That(daily.ShouldShowAttendIcsObjective(save),Is.True);
            Assert.That(daily.BuildAdditionalClassroomObjectives(save).All(v=>v.Title.StartsWith("Attend ")),Is.True);
            sync.Card.Course("ICS").status="DROPPED_CHEATING";daily.ResetForNewGame();
            Assert.That(daily.IsCourseDropped("ICS"),Is.False);
        }
        [Test] public void NonAssessmentDayKeepsNormalClassroomEligibility()
        {
            save.SetDayProgressForTesting(1, 3);
            sync.Card.day = 3; sync.Card.scheduledAssessment = null; daily.ApplyReportCard(sync.Card);
            var doorObject = new GameObject("Day3 Door");
            try
            {
                doorObject.AddComponent<BoxCollider>();
                var door = doorObject.AddComponent<IcsClassroomInteractable>();
                Field(door, "locationConfig", locations[0]);
                Assert.That(door.Interact(), Does.Contain("scheduled day"));
                Assert.That(AcademicModal.BlocksGameplay, Is.False);
                Assert.That(ClassroomChoiceUI.IsOpen, Is.False);
            }
            finally { Object.DestroyImmediate(doorObject); }
        }
        private AssessmentUI OpenQuiz(){var ui=AssessmentUI.EnsureExists();ui.Configure(sync);ui.Show("ICS");Click(ui,"START QUIZ");return ui;}
        private static void Field(object target,string name,object value){target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
        private static IcsClassroomLocationConfig Location(string course,string room,int floor)
        {
            var c=ScriptableObject.CreateInstance<IcsClassroomLocationConfig>();Field(c,"courseId",course);Field(c,"activityId","ATTEND_"+course);
            Field(c,"courseName",course);Field(c,"classroomNumber",room);Field(c,"floor",floor);return c;
        }
        private static Button[] Buttons(Component c)=>c.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy).ToArray();
        private static void Click(Component c,string name)=>Buttons(c).Single(b=>b.name==name).onClick.Invoke();
        private static string Text(Component c)=>string.Join("\n",c.GetComponentsInChildren<TextMeshProUGUI>().Select(t=>t.text));
        private sealed class FakeSync:IAssessmentProgressSync
        {
            public ReportCard Card;public AssessmentResult Result;public int StartCount,AnswerCount,CheatCount,LastAnswer;
            public bool HoldCheat,FailCheat;private Action<AssessmentResult> cheatCallback;
            public FakeSync()
            {
                Card=new ReportCard{semester=1,day=2,scheduledAssessment="QUIZ_1",courses=new[]{Course("ICS"),Course("ENGLISH"),Course("DM")}};
            }
            private static CourseResult Course(string id)=>new CourseResult{courseId=id,courseName=id,status="ACTIVE",grade="Pending",components=new[]{
                new AssessmentComponent{assessmentType="QUIZ_1",displayName="Quiz 1",state="PENDING",maxMarks=15,questionCount=3},
                new AssessmentComponent{assessmentType="MIDTERM",displayName="Midterm",state="PENDING",maxMarks=30,questionCount=6},
                new AssessmentComponent{assessmentType="QUIZ_2",displayName="Quiz 2",state="PENDING",maxMarks=15,questionCount=3},
                new AssessmentComponent{assessmentType="FINAL",displayName="Final Exam",state="PENDING",maxMarks=40,questionCount=8}}};
            public void RequestReportCard(Action<ReportCard> ok,Action<string> fail)=>ok(Card);
            public void RequestAssessmentStart(string course,string type,Action<AssessmentResult> ok,Action<string> fail)
            {
                StartCount++;Result=new AssessmentResult{attemptId="test",courseId=course,courseName=course,assessmentType=type,displayName="Quiz 1",state="STARTED",
                    questionCount=3,maxMarks=15,difficulty="NORMAL",secondsRemaining=18,reportCard=Card,
                    question=new AssessmentQuestion{id="sample",text="[SAMPLE] Test?",options=new[]{new AssessmentOption{index=0,text="Correct"},new AssessmentOption{index=1,text="Wrong"},new AssessmentOption{index=2,text="Other"},new AssessmentOption{index=3,text="Another"}}}};
                ok(Result);
            }
            public void RequestAssessmentAnswer(AssessmentResult a,int answer,Action<AssessmentResult> ok,Action<string> fail)
            {AnswerCount++;LastAnswer=answer;Result.marksObtained+=answer==0?5:0;Result.questionIndex++;if(Result.questionIndex==3)Result.state="COMPLETED";ok(Result);}
            public void RequestAssessmentCheat(AssessmentResult a,Action<AssessmentResult> ok,Action<string> fail)
            {CheatCount++;cheatCallback=ok;if(FailCheat){fail("Response lost");return;}if(!HoldCheat)FinishCheat("CHEAT_CAUGHT");}
            public void FinishCheat(string state)
            {
                Result.state=state;Result.marksObtained=state=="CHEAT_SUCCESS"?15:0;Result.auraDelta=state=="CHEAT_SUCCESS"?2:-2;Result.academicReputationDelta=state=="CHEAT_CAUGHT"?-4:0;
                if(state=="CHEAT_CAUGHT")Card.Course("ICS").status="DROPPED_CHEATING";
                cheatCallback(Result);
            }
        }
    }
}
