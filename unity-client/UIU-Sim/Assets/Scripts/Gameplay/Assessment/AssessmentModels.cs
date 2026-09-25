using System;
using UIU.Simulator.Gameplay.Activities;
namespace UIU.Simulator.Gameplay.Assessment
{
    [Serializable] public sealed class AssessmentOption { public int index; public string text; }
    [Serializable] public sealed class AssessmentQuestion { public string id, text; public AssessmentOption[] options; }
    [Serializable] public sealed class AssessmentResult
    {
        public string attemptId, courseId, courseName, assessmentType, displayName, state, difficulty;
        public int questionCount, questionIndex, marksObtained, maxMarks, auraDelta, academicReputationDelta, aura, academicReputation;
        public float secondsRemaining;
        public AssessmentQuestion question;
        public ReportCard reportCard;
        public bool IsTerminal => state != "STARTED";
    }
    [Serializable] public sealed class AssessmentAnswer { public int questionIndex, answerIndex; }
    [Serializable] public sealed class AssessmentComponent
    {
        public string assessmentType, displayName, state;
        public int marksObtained, maxMarks, questionCount;
        public bool IsTerminal => state == "COMPLETED" || state == "MISSED" || state == "CHEAT_SUCCESS" || state == "CHEAT_CAUGHT";
        public ActivityStatus HudStatus => !IsTerminal ? ActivityStatus.Pending :
            state == "MISSED" || state == "CHEAT_CAUGHT" ? ActivityStatus.Missed : ActivityStatus.Completed;
    }
    [Serializable] public sealed class CourseResult
    {
        public string courseId, courseName, status, grade, gradeDescription;
        public int total;
        public float gradePoint;
        public AssessmentComponent[] components;
        public bool IsDropped => status == "DROPPED_CHEATING";
        public AssessmentComponent Component(string type) => Array.Find(components ?? Array.Empty<AssessmentComponent>(), c => c.assessmentType == type);
    }
    [Serializable] public sealed class ReportCard
    {
        public int semester, day;
        public string scheduledAssessment;
        // JsonUtility maps nullable numbers to zero. Only FINAL permits displaying cgpa.
        public float cgpa;
        public string cgpaStatus;
        public CourseResult[] courses;
        public CourseResult Course(string id) => Array.Find(courses ?? Array.Empty<CourseResult>(), c => c.courseId == id);
    }
}
