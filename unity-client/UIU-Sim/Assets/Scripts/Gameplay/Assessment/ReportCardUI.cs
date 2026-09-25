using System.Text;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.UI;
using UnityEngine;
using UnityEngine.InputSystem;
namespace UIU.Simulator.Gameplay.Assessment
{
    public sealed class ReportCardUI : AcademicModal
    {
        public static ReportCardUI Instance { get; private set; }
        private ReportCard card;
        private IAssessmentProgressSync progress;
        public void Configure(IAssessmentProgressSync sync) { progress = sync; }
        private int courseIndex, openedFrame;
        public static ReportCardUI EnsureExists()
        {
            if (Instance == null) Instance = new GameObject("ReportCardUI").AddComponent<ReportCardUI>();
            return Instance;
        }
        protected override void Awake() { base.Awake(); Instance = this; }
        protected override void OnDestroy() { base.OnDestroy(); if (Instance == this) Instance = null; }
        public void Show()
        {
            if (BlocksGameplay) return;
            OpenModal(); openedFrame = Time.frameCount; courseIndex = 0; Load();
        }
        private void Load()
        {
            Clear(); Label("REPORT CARD", 28, UiTheme.BrightOrange); Label("Loading persisted results…");
            progress ??= PlayerProgressSync.Active;
            if (progress == null) { Error("Progress sync is unavailable."); return; }
            progress.RequestReportCard(result => { if (IsVisible) { card = result; Render(); } }, Error);
        }
        private void Error(string error)
        {
            if (!IsVisible) return;
            Clear(); Label("REPORT CARD", 28, UiTheme.BrightOrange); Label(error, 22, null, 100);
            Button("RETRY", Load); Button("CLOSE", CloseModal, true);
        }
        private void Render()
        {
            Clear(); Label("REPORT CARD · SEMESTER " + card.semester, 28, UiTheme.BrightOrange);
            if (card.courses == null || card.courses.Length == 0)
            { Label("No assessment courses available."); Button("CLOSE", CloseModal); return; }
            courseIndex = Mathf.Clamp(courseIndex, 0, card.courses.Length - 1);
            var course = card.courses[courseIndex];
            Label(course.courseName.ToUpperInvariant(), 25, null, 70);
            Label(course.IsDropped ? "DROPPED — ACADEMIC MISCONDUCT" : "ACTIVE", 21, course.IsDropped ? UiTheme.Danger : UiTheme.Grey);
            var text = new StringBuilder();
            foreach (var component in course.components)
            {
                text.Append(component.displayName).Append("     ");
                if (component.IsTerminal)
                {
                    text.Append(component.marksObtained).Append(" / ").Append(component.maxMarks);
                    if (component.state != "COMPLETED") text.Append(" — ").Append(AssessmentUI.OutcomeLabel(component.state));
                }
                else if (course.IsDropped) text.Append("DROPPED");
                else text.Append("-- / ").Append(component.maxMarks);
                text.AppendLine().AppendLine();
            }
            Label(text.ToString(), 21, null, 210);
            Label(FormatFinalResult(course), 23, course.IsDropped ? UiTheme.Danger : UiTheme.White, 100);
            if (card.courses.Length > 1)
            {
                Button("NEXT COURSE  (" + (courseIndex + 1) + " / " + card.courses.Length + ")", () => { courseIndex = (courseIndex + 1) % card.courses.Length; Render(); }, true);
            }
            Button("CLOSE", CloseModal);
        }
        public static string FormatFinalResult(CourseResult course)
        {
            if (course.IsDropped) return "COURSE DROPPED\nFinal Result: 0 / 100\nGrade: F · Grade Point: 0.00";
            return $"Current Total: {course.total} / 100\nFinal Grade: {course.grade}" +
                (course.grade == "Pending" ? "" : $" · Grade Point: {course.gradePoint:F2}");
        }
        private void Update()
        {
            if (IsVisible && Time.frameCount > openedFrame && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseModal();
        }
    }
}
