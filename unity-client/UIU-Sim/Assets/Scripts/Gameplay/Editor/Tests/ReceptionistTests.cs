#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Reflection;
using NUnit.Framework;
using UIU.Simulator.Gameplay.Player;
using UIU.Simulator.Gameplay.UI;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Tests
{
    [TestFixture]
    public sealed class ReceptionistTests
    {
        private GameObject playerObject;
        private PlayerInventory playerInventory;
        private PlayerStats playerStats;
        private DialogueUI dialogueUI;

        private GameObject receptionistObject;
        private Receptionist receptionist;
        private TestPlayerProgressSync fakeProgressSync;

        private sealed class TestPlayerProgressSync : IPlayerProgressSync
        {
            private readonly PlayerStats stats;
            public bool IsHydrated => true;
            public bool IsMutationInFlight => false;
            public int RequestedAuraDelta { get; private set; }
            public int RequestedAcademicReputationDelta { get; private set; }
            public int RequestCount { get; private set; }

            public TestPlayerProgressSync(PlayerStats stats)
            {
                this.stats = stats;
            }

            public void RequestStatDelta(int auraDelta, int academicReputationDelta)
            {
                RequestedAuraDelta += auraDelta;
                RequestedAcademicReputationDelta += academicReputationDelta;
                RequestCount++;
                if (stats != null)
                {
                    stats.ApplyServerState(stats.Aura + auraDelta, stats.AcademicReputation + academicReputationDelta, StatUpdateSource.GameplayMutation);
                }
            }
        }

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("TestPlayer");
            playerInventory = playerObject.AddComponent<PlayerInventory>();
            InvokeMethod(playerInventory, "Awake");
            playerStats = playerObject.AddComponent<PlayerStats>();
            InvokeMethod(playerStats, "Awake");
            dialogueUI = playerObject.AddComponent<DialogueUI>();
            InvokeMethod(dialogueUI, "Awake");

            receptionistObject = new GameObject("TestReceptionist");
            receptionistObject.AddComponent<BoxCollider>();
            receptionist = receptionistObject.AddComponent<Receptionist>();

            fakeProgressSync = new TestPlayerProgressSync(playerStats);
            receptionist.SetProgressSyncForTesting(fakeProgressSync);
        }

        [TearDown]
        public void TearDown()
        {
            if (DialogueUI.Instance != null && DialogueUI.IsOpen)
            {
                DialogueUI.Instance.Hide();
            }

            var notif = Object.FindFirstObjectByType<SystemNotificationUI>();
            if (notif != null)
            {
                Object.DestroyImmediate(notif.gameObject);
            }

            if (receptionistObject != null)
            {
                Object.DestroyImmediate(receptionistObject);
            }

            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void Interact_NoIDProblem_DismissesPlayer_NoAuraRequested()
        {
            string response = receptionist.Interact();

            Assert.That(response, Is.EqualTo("Your ID card seems to be working fine."));
            Assert.That(fakeProgressSync.RequestCount, Is.EqualTo(0));
            Assert.That(DialogueUI.IsOpen, Is.False);
        }

        [Test]
        public void Interact_AlreadyHasTemporaryID_DismissesPlayer_NoAuraRequested()
        {
            // Simulate ID problem and temporary ID in hand
            SetField(playerInventory, "hasIDProblem", true);
            playerInventory.AddTemporaryID();

            string response = receptionist.Interact();

            Assert.That(response, Is.EqualTo("You already have a temporary ID. Use it at the scanner first."));
            Assert.That(fakeProgressSync.RequestCount, Is.EqualTo(0));
            Assert.That(DialogueUI.IsOpen, Is.False);
        }

        [Test]
        public void ForgotID_RequestsMinus5Aura_AndIssuesTemporaryID()
        {
            float initialAura = playerStats.Aura;

            // Trigger forgot ID choice callback
            InvokeMethod(receptionist, "OnForgotID");

            Assert.That(fakeProgressSync.RequestCount, Is.EqualTo(1));
            Assert.That(fakeProgressSync.RequestedAuraDelta, Is.EqualTo(-5));
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura - 5f).Within(0.001f));
            Assert.That(playerInventory.TemporaryIDCount, Is.EqualTo(1));
        }

        [Test]
        public void LostID_RequestsMinus10Aura_AndIssuesTemporaryID()
        {
            float initialAura = playerStats.Aura;

            // Trigger lost ID choice callback
            InvokeMethod(receptionist, "OnLostID");

            Assert.That(fakeProgressSync.RequestCount, Is.EqualTo(1));
            Assert.That(fakeProgressSync.RequestedAuraDelta, Is.EqualTo(-10));
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura - 10f).Within(0.001f));
            Assert.That(playerInventory.TemporaryIDCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingProgressSync_DoesNotMutateStatsLocally()
        {
            float initialAura = playerStats.Aura;

            // Clear test seam
            receptionist.SetProgressSyncForTesting(null);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[Receptionist] PlayerProgressSync missing. Cannot persist Aura change.");

            // Trigger forgot ID
            InvokeMethod(receptionist, "OnForgotID");

            // Temporary ID still issued so player isn't softlocked, but persistent Aura NOT modified locally
            Assert.That(playerStats.Aura, Is.EqualTo(initialAura).Within(0.001f), "Aura must NOT be mutated locally when PlayerProgressSync is missing.");
            Assert.That(playerInventory.TemporaryIDCount, Is.EqualTo(1));
        }

        private static void InvokeMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"Method '{methodName}' not found on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
