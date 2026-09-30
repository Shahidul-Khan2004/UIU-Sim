using NUnit.Framework;
using UIU.Simulator.Gameplay.Faculty;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class FacultyDayScheduleTests
    {
        [TestCase(1, false, "")]
        [TestCase(2, true, "Exam Day")]
        [TestCase(3, true, "Midterm Exam Day")]
        [TestCase(4, false, "")]
        [TestCase(5, true, "Exam Day")]
        [TestCase(6, true, "Final Exam Day")]
        public void Semester1_Days_MatchExamSchedule(int day, bool expectedExam, string expectedLabel)
        {
            Assert.That(FacultyDaySchedule.IsExamDay(1, day), Is.EqualTo(expectedExam));
            Assert.That(FacultyDaySchedule.GetDayLabel(1, day), Is.EqualTo(expectedLabel));
        }

        [Test]
        public void NonSemester1_IsNeverExamDay()
        {
            Assert.That(FacultyDaySchedule.IsExamDay(2, 3), Is.False);
            Assert.That(FacultyDaySchedule.GetDayLabel(2, 3), Is.EqualTo(string.Empty));
        }

        [TestCase(2, "Exam Day — use your office computer")]
        [TestCase(3, "Midterm Exam Day — use your office computer")]
        [TestCase(5, "Exam Day — use your office computer")]
        [TestCase(6, "Final Exam Day — use your office computer")]
        public void PrepareQuestionsNextActivity_NamesDayAndOfficeComputer(int day, string expectedSnippet)
        {
            string text = FacultyDaySchedule.BuildPrepareQuestionsNextActivity(1, day);
            Assert.That(text, Does.Contain(expectedSnippet));
        }
    }
}
