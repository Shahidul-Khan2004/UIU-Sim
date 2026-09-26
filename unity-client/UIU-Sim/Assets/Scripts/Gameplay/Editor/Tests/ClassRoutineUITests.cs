using NUnit.Framework;
using TMPro;
using UIU.Simulator.Gameplay.Faculty;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    [TestFixture]
    public sealed class ClassRoutineUITests
    {
        [SetUp]
        public void SetUp()
        {
            DestroyExisting(ClassRoutineUI.Instance != null ? ClassRoutineUI.Instance.gameObject : null);
        }

        [TearDown]
        public void TearDown()
        {
            if (ClassRoutineUI.IsOpen && ClassRoutineUI.Instance != null)
            {
                ClassRoutineUI.Instance.Hide();
            }

            DestroyExisting(ClassRoutineUI.Instance != null ? ClassRoutineUI.Instance.gameObject : null);
        }

        [Test]
        public void Show_DisplaysHeaderAndBackendRoutineCards()
        {
            ClassRoutineUI ui = ClassRoutineUI.EnsureExists();
            ui.Show(new[]
            {
                new ApiClient.FacultyRoutineItemDto
                {
                    courseId = "ICS",
                    courseName = "Introduction to Computer Science",
                    classroomNumber = "427",
                    floor = 4
                },
                new ApiClient.FacultyRoutineItemDto
                {
                    courseId = "DM",
                    courseName = "Discrete Mathematics",
                    classroomNumber = "423",
                    floor = 4
                }
            });

            Assert.That(ClassRoutineUI.IsOpen, Is.True);
            Assert.That(FindLabel("Title").text, Is.EqualTo("CLASS ROUTINE"));
            Assert.That(FindRoutineLabel("ICS", "CourseValue").text, Is.EqualTo("Introduction to Computer Science"));
            Assert.That(FindRoutineLabel("ICS", "ClassroomValue").text, Is.EqualTo("Room 427"));
            Assert.That(FindRoutineLabel("ICS", "FloorValue").text, Is.EqualTo("4"));
            Assert.That(FindRoutineLabel("DM", "CourseValue").text, Is.EqualTo("Discrete Mathematics"));
            Assert.That(FindRoutineLabel("DM", "ClassroomValue").text, Is.EqualTo("Room 423"));
            Assert.That(FindRoutineLabel("DM", "FloorValue").text, Is.EqualTo("4"));
        }

        [Test]
        public void Hide_ClosesRoutine()
        {
            ClassRoutineUI ui = ClassRoutineUI.EnsureExists();
            ui.Show(new[]
            {
                new ApiClient.FacultyRoutineItemDto
                {
                    courseId = "ICS",
                    courseName = "Introduction to Computer Science",
                    classroomNumber = "427",
                    floor = 4
                }
            });

            Assert.That(ClassRoutineUI.IsOpen, Is.True);
            ui.Hide();
            Assert.That(ClassRoutineUI.IsOpen, Is.False);
        }

        [Test]
        public void Show_EmptyRoutine_ShowsStatusWithoutCards()
        {
            ClassRoutineUI ui = ClassRoutineUI.EnsureExists();
            ui.Show(System.Array.Empty<ApiClient.FacultyRoutineItemDto>());

            Assert.That(ClassRoutineUI.IsOpen, Is.True);
            Assert.That(FindLabel("Title").text, Is.EqualTo("CLASS ROUTINE"));
            Assert.That(FindLabel("StatusLabel").text, Is.EqualTo("No assigned classes."));
            Assert.That(FindNamedChild(ui.transform, "Routine_ICS"), Is.Null);
        }

        private static TextMeshProUGUI FindLabel(string objectName)
        {
            if (ClassRoutineUI.Instance == null)
            {
                return null;
            }

            TextMeshProUGUI[] labels = ClassRoutineUI.Instance.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].gameObject.name == objectName)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private static TextMeshProUGUI FindRoutineLabel(string courseId, string objectName)
        {
            if (ClassRoutineUI.Instance == null)
            {
                return null;
            }

            Transform card = FindNamedChild(ClassRoutineUI.Instance.transform, "Routine_" + courseId);
            if (card == null)
            {
                return null;
            }

            TextMeshProUGUI[] labels = card.GetComponentsInChildren<TextMeshProUGUI>(true);
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

        private static void DestroyExisting(GameObject target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
