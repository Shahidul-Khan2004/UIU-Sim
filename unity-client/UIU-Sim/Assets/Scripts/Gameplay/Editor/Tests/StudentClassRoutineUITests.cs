using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Activities;
using UIU.Simulator.Gameplay.Classroom;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Gameplay.UI;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class StudentClassRoutineUITests
    {
        [SetUp]
        public void SetUp()
        {
            DestroyExisting(ClassRoutineUI.Instance != null ? ClassRoutineUI.Instance.gameObject : null);
            DestroyExisting(FindDailyActivityState());
        }

        [TearDown]
        public void TearDown()
        {
            if (ClassRoutineUI.IsOpen && ClassRoutineUI.Instance != null)
            {
                ClassRoutineUI.Instance.Hide();
            }

            DestroyExisting(ClassRoutineUI.Instance != null ? ClassRoutineUI.Instance.gameObject : null);
            DestroyExisting(FindDailyActivityState());
        }

        [Test]
        public void BuildRoutine_UsesExistingClassroomCatalog_WithoutPhysics()
        {
            ApiClient.FacultyRoutineItemDto[] routine = StudentClassRoutineUI.BuildRoutine();

            Assert.That(routine, Has.Length.EqualTo(3));
            Assert.That(routine[0].courseId, Is.EqualTo("ICS"));
            Assert.That(routine[0].courseName, Is.EqualTo("Introduction to Computer Science"));
            Assert.That(routine[0].classroomNumber, Is.EqualTo("427"));
            Assert.That(routine[0].floor, Is.EqualTo(4));
            Assert.That(routine[1].courseId, Is.EqualTo("ENGLISH"));
            Assert.That(routine[1].courseName, Is.EqualTo("English"));
            Assert.That(routine[1].classroomNumber, Is.EqualTo("702"));
            Assert.That(routine[1].floor, Is.EqualTo(7));
            Assert.That(routine[2].courseId, Is.EqualTo("DM"));
            Assert.That(routine[2].courseName, Is.EqualTo("Discrete Mathematics"));
            Assert.That(routine[2].classroomNumber, Is.EqualTo("423"));
            Assert.That(routine[2].floor, Is.EqualTo(4));
        }

        [Test]
        public void Show_OpensSharedClassRoutineUi()
        {
            StudentClassRoutineUI.Show();

            Assert.That(ClassRoutineUI.IsOpen, Is.True);
            Assert.That(FindLabel("Title").text, Is.EqualTo(StudentClassRoutineUI.Title));
            Assert.That(FindRoutineLabel("ICS", "CourseValue").text, Is.EqualTo("Introduction to Computer Science"));
            Assert.That(FindRoutineLabel("ENGLISH", "CourseValue").text, Is.EqualTo("English"));
            Assert.That(FindRoutineLabel("DM", "CourseValue").text, Is.EqualTo("Discrete Mathematics"));
        }

        [Test]
        public void BuildRoutine_OverlaysLiveClassroomAssets()
        {
            IcsClassroomLocationConfig english = ScriptableObject.CreateInstance<IcsClassroomLocationConfig>();
            SetLocation(english, "ENGLISH", "English Literature", "801", 8);

            GameObject dailyObject = new GameObject("TestDailyActivityState");
            DailyActivityState daily = dailyObject.AddComponent<DailyActivityState>();
            daily.SetAdditionalLocationConfigsForTesting(new[] { english });

            try
            {
                ApiClient.FacultyRoutineItemDto[] routine = StudentClassRoutineUI.BuildRoutine();
                Assert.That(routine[1].courseName, Is.EqualTo("English Literature"));
                Assert.That(routine[1].classroomNumber, Is.EqualTo("801"));
                Assert.That(routine[1].floor, Is.EqualTo(8));
                Assert.That(routine[0].courseId, Is.EqualTo("ICS"));
                Assert.That(routine[2].courseId, Is.EqualTo("DM"));
            }
            finally
            {
                Object.DestroyImmediate(english);
                Object.DestroyImmediate(dailyObject);
            }
        }

        private static void SetLocation(
            IcsClassroomLocationConfig config,
            string courseId,
            string courseName,
            string classroomNumber,
            int floor)
        {
            System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            System.Type type = typeof(IcsClassroomLocationConfig);
            type.GetField("courseId", flags)?.SetValue(config, courseId);
            type.GetField("courseName", flags)?.SetValue(config, courseName);
            type.GetField("classroomNumber", flags)?.SetValue(config, classroomNumber);
            type.GetField("floor", flags)?.SetValue(config, floor);
        }

        private static TextMeshProUGUI FindLabel(string objectName)
        {
            if (ClassRoutineUI.Instance == null)
            {
                return null;
            }

            return FindNamedLabel(ClassRoutineUI.Instance.transform, objectName);
        }

        private static TextMeshProUGUI FindRoutineLabel(string courseId, string objectName)
        {
            if (ClassRoutineUI.Instance == null)
            {
                return null;
            }

            Transform card = FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_" + courseId);
            return card != null ? FindNamedLabel(card, objectName) : null;
        }

        private static TextMeshProUGUI FindNamedLabel(Transform root, string objectName)
        {
            TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].gameObject.name == objectName)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private static Transform FindNamedChild(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindNamedChild(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static GameObject FindDailyActivityState()
        {
            DailyActivityState daily = Object.FindFirstObjectByType<DailyActivityState>();
            return daily != null ? daily.gameObject : null;
        }

        private static void DestroyExisting(GameObject target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
