using System;
using TMPro;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.UI;
using UnityEngine;
namespace UIU.Simulator.Gameplay.Assessment
{
    public sealed class AssessmentUI : AcademicModal
    {
        public static AssessmentUI Instance { get; private set; }
        private IAssessmentProgressSync progress;
        public void Configure(IAssessmentProgressSync sync) { progress = sync; }
        private AssessmentResult attempt;
        private bool busy, failed;
        private float deadline;
        private TextMeshProUGUI timer;
        private string courseId, assessmentType;
        private Action retry;
        public static AssessmentUI EnsureExists()
        {
            if (Instance == null) Instance = new GameObject("AssessmentUI").AddComponent<AssessmentUI>();
            return Instance;
        }
        protected override void Awake() { base.Awake(); Instance = this; }
        protected override void OnDestroy() { base.OnDestroy(); if (Instance == this) Instance = null; }
        public void Show(string course)
        {
            if (BlocksGameplay) return;
            courseId = course; attempt = null; progress ??= PlayerProgressSync.Active;
            OpenModal(); LoadPreview();
        }
        private void LoadPreview()
        {
            busy = true; failed = false; Clear(); Label("ASSESSMENT", 28, UiTheme.BrightOrange); Label("Loading course status…");
            retry = LoadPreview;
            if (progress == null) { Fail("Progress sync is unavailable."); return; }
            progress.RequestReportCard(card =>
            {
                if (!IsVisible) return;
                busy = false; Clear(); var course = card.Course(courseId);
                if (course == null) { Label("No assessment available for this student."); Button("BACK", CloseModal); return; }
                Label(course.courseName.ToUpperInvariant(), 26, UiTheme.BrightOrange, 70);
                if (course.IsDropped)
                { Label("You were removed from this course after being caught cheating.", 22, UiTheme.Danger, 100); Button("BACK", CloseModal); return; }
                assessmentType = card.scheduledAssessment;
                if (string.IsNullOrEmpty(assessmentType))
                { Label("No assessment is scheduled today."); Button("BACK", CloseModal); return; }
                var component = course.Component(assessmentType);
                if (component == null) { Fail("Assessment definition is unavailable."); return; }
                Label(assessmentType.StartsWith("QUIZ") ? "QUIZ TODAY" : assessmentType == "MIDTERM" ? "MIDTERM EXAM" : "FINAL EXAM", 24);
                Label($"{component.displayName}\n{component.maxMarks} Marks · {component.questionCount} Questions", 24, null, 90);
                if (component.IsTerminal)
                { Label($"{component.marksObtained} / {component.maxMarks} — {OutcomeLabel(component.state)}", 22, null, 80); }
                else Button(component.state == "STARTED" ? "RESUME " + component.displayName.ToUpperInvariant() : "START " + (assessmentType.StartsWith("QUIZ") ? "QUIZ" : component.displayName.ToUpperInvariant()), StartAttempt);
                Button("BACK", CloseModal, true);
            }, Fail);
        }
        private void StartAttempt()
        {
            if (busy) return;
            Send(() => progress.RequestAssessmentStart(courseId, assessmentType, Apply, Fail));
        }
        private void Send(Action request)
        {
            busy = true; failed = false; DisableButtons(); retry = () => Send(request); request();
        }
        public void Apply(AssessmentResult result)
        {
            if (!IsVisible) return;
            attempt = result; busy = false; failed = false; Clear();
            Label(result.courseName.ToUpperInvariant(), 26, UiTheme.BrightOrange, 70);
            Label(result.displayName.ToUpperInvariant(), 22);
            if (result.IsTerminal)
            {
                if (result.state == "CHEAT_CAUGHT")
                    Label($"YOU WERE CAUGHT CHEATING.\n\nYou have been removed from {result.courseName} for this trimester.\n\nCOURSE DROPPED — Final Result: 0 / 100 · F · 0.00", 23, UiTheme.Danger, 230);
                else Label(OutcomeLabel(result.state), 25, UiTheme.Success, 70);
                Label($"Result: {result.marksObtained} / {result.maxMarks}\nAura: {result.auraDelta:+0;-0;0} · Academic Reputation: {result.academicReputationDelta:+0;-0;0}", 22, null, 90);
                Button("RETURN TO CAMPUS", CloseModal); return;
            }
            Label($"Question {result.questionIndex + 1} / {result.questionCount} · {result.difficulty}", 20, UiTheme.Grey);
            Label(result.question.text, 23, null, 100);
            foreach (var option in result.question.options)
            {
                int index = option.index;
                Button($"{(char)('A' + index)}. {option.text}", () => Answer(index), true);
            }
            deadline = Time.unscaledTime + result.secondsRemaining;
            timer = Label($"Time Remaining: {Mathf.CeilToInt(result.secondsRemaining)}s · Current Marks: {result.marksObtained} / {result.maxMarks}", 20);
            Button("CHEAT", Cheat);
        }
        private void Update()
        {
            if (!IsVisible || busy || failed || attempt == null || attempt.IsTerminal) return;
            float remaining = Mathf.Max(0, deadline - Time.unscaledTime);
            if (timer != null) timer.text = $"Time Remaining: {Mathf.CeilToInt(remaining)}s · Current Marks: {attempt.marksObtained} / {attempt.maxMarks}";
            if (remaining <= 0) Answer(-1);
        }
        private void Answer(int answer)
        {
            if (busy || failed || attempt == null || attempt.IsTerminal) return;
            var sent = attempt; Send(() => progress.RequestAssessmentAnswer(sent, answer, Apply, Fail));
        }
        private void Cheat()
        {
            if (busy || failed || attempt == null || attempt.IsTerminal) return;
            var sent = attempt; Send(() => progress.RequestAssessmentCheat(sent, Apply, Fail));
        }
        private void Fail(string message)
        {
            if (!IsVisible) return;
            busy = false; failed = true; Clear(); Label("CONNECTION / PROGRESS", 26, UiTheme.BrightOrange);
            Label(message + "\nYour saved attempt will be recovered on retry.", 22, null, 160);
            Button("RETRY", () => retry?.Invoke()); Button("BACK TO CAMPUS", CloseModal, true);
        }
        public static string OutcomeLabel(string outcome)
        {
            switch (outcome)
            {
                case "CHEAT_CAUGHT": return "Caught Cheating / Course Dropped";
                case "CHEAT_SUCCESS": return "Cheated Successfully";
                case "MISSED": return "Missed";
                case "COMPLETED": return "Completed";
                default: return outcome;
            }
        }
    }
}
