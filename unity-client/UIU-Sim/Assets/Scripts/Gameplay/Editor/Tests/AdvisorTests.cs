#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UIU.Simulator.Gameplay.Advisor;
using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Advisor.Tests
{
    [TestFixture]
    public sealed class AdvisorTests
    {
        private GameObject playerObject;
        private PlayerMovement playerMovement;
        private AdvisorUI advisorUI;
        private GameObject npcObject;
        private AcademicAdvisorInteractable advisorInteractable;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("TestPlayer");
            playerMovement = playerObject.AddComponent<PlayerMovement>();

            // Setup Advisor UI host
            GameObject advisorHost = new GameObject("TestAdvisorUI");
            advisorHost.AddComponent<AdvisorClient>();
            advisorUI = advisorHost.AddComponent<AdvisorUI>();

            // Setup NPC Capsule
            npcObject = new GameObject("AdvisorCapsule");
            npcObject.AddComponent<CapsuleCollider>();
            advisorInteractable = npcObject.AddComponent<AcademicAdvisorInteractable>();
        }

        [TearDown]
        public void TearDown()
        {
            if (AdvisorUI.IsOpen)
            {
                advisorUI.Hide();
            }

            if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
            if (npcObject != null) UnityEngine.Object.DestroyImmediate(npcObject);
            if (advisorUI != null) UnityEngine.Object.DestroyImmediate(advisorUI.gameObject);
        }

        [Test]
        public void Interactable_ReturnsCorrectPrompt()
        {
            Assert.That(advisorInteractable.InteractionPrompt, Is.EqualTo("Talk to Academic Advisor"));
        }

        [Test]
        public void Interactable_Interact_OpensAdvisorUI()
        {
            Assert.That(AdvisorUI.IsOpen, Is.False);

            advisorInteractable.Interact();

            Assert.That(AdvisorUI.IsOpen, Is.True);
        }

        [Test]
        public void QuickActionPrompts_MatchSpecifications()
        {
            Assert.That(AdvisorUI.QuickAction1Prompt, Is.EqualTo("What should I focus on to improve my academic performance?"));
            Assert.That(AdvisorUI.QuickAction2Prompt, Is.EqualTo("How can I improve my Academic Reputation and overall standing at university?"));
            Assert.That(AdvisorUI.QuickAction3Prompt, Is.EqualTo("Can you help me think about my career direction and what kinds of interests or skills I should explore?"));
            Assert.That(AdvisorUI.QuickAction4Prompt, Is.EqualTo("How can I make better choices and balance academics with university life?"));
        }

        [Test]
        public void InputLocks_DisabledOnShow_RestoredOnHide()
        {
            Assert.That(playerMovement.enabled, Is.True);

            advisorUI.Show();
            Assert.That(AdvisorUI.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False, "Player movement should be disabled while Advisor UI is open.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "Cursor should be unlocked while Advisor UI is open.");
            Assert.That(Cursor.visible, Is.True, "Cursor should be visible while Advisor UI is open.");

            advisorUI.Hide();
            Assert.That(AdvisorUI.IsOpen, Is.False);
            Assert.That(playerMovement.enabled, Is.True, "Player movement should be restored after Advisor UI closes.");
        }

        [Test]
        public void EmptyOrWhitespaceMessage_IsIgnored()
        {
            advisorUI.Show();

            FieldInfo historyField = typeof(AdvisorUI).GetField("history", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(historyField, Is.Not.Null);

            var history = (List<AdvisorClient.AdvisorHistoryMessageDto>)historyField.GetValue(advisorUI);
            int initialCount = history.Count;

            advisorUI.SendUserMessage("   ");
            Assert.That(history.Count, Is.EqualTo(initialCount), "Whitespace message should not be recorded in history.");

            advisorUI.SendUserMessage("");
            Assert.That(history.Count, Is.EqualTo(initialCount), "Empty message should not be recorded in history.");
        }

        [Test]
        public void History_BoundedToMaxHistoryCount()
        {
            advisorUI.Show();

            MethodInfo recordHistoryMethod = typeof(AdvisorUI).GetMethod("RecordHistory", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(recordHistoryMethod, Is.Not.Null);

            FieldInfo historyField = typeof(AdvisorUI).GetField("history", BindingFlags.NonPublic | BindingFlags.Instance);
            var history = (List<AdvisorClient.AdvisorHistoryMessageDto>)historyField.GetValue(advisorUI);

            for (int i = 1; i <= 12; i++)
            {
                recordHistoryMethod.Invoke(advisorUI, new object[] { i % 2 == 0 ? "advisor" : "user", "Message " + i });
            }

            Assert.That(history.Count, Is.EqualTo(AdvisorUI.MaxHistoryCount), $"History should be capped at {AdvisorUI.MaxHistoryCount}.");
            Assert.That(history[0].content, Is.EqualTo("Message 5"), "Oldest messages should be discarded first.");
            Assert.That(history[history.Count - 1].content, Is.EqualTo("Message 12"), "Newest message should be preserved.");
        }

        [Test]
        public void NoTokenLifecycleInAdvisorGameplayScripts()
        {
            Type[] types = new Type[]
            {
                typeof(AcademicAdvisorInteractable),
                typeof(AdvisorUI),
                typeof(AdvisorClient)
            };

            string[] forbiddenTerms = new string[]
            {
                "refreshSecret",
                "clerk_user_id",
                "UserSession",
                "JwtToken",
                "RefreshToken"
            };

            foreach (Type t in types)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    foreach (string forbidden in forbiddenTerms)
                    {
                        Assert.That(field.Name.ToLowerInvariant(), Does.Not.Contain(forbidden.ToLowerInvariant()),
                            $"Type {t.Name} must not contain token/auth lifecycle field {field.Name}");
                    }
                }
            }
        }

        [Test]
        public void AdvisorClient_SerializesRequestCorrectly()
        {
            var history = new List<AdvisorClient.AdvisorHistoryMessageDto>
            {
                new AdvisorClient.AdvisorHistoryMessageDto("user", "Hello"),
                new AdvisorClient.AdvisorHistoryMessageDto("advisor", "Hi there")
            };

            var request = new AdvisorClient.AdvisorChatRequestDto("How do I study?", history, false);
            string json = JsonUtility.ToJson(request);

            Assert.That(json, Does.Contain("How do I study?"));
            Assert.That(json, Does.Contain("Hello"));
            Assert.That(json, Does.Contain("Hi there"));
            Assert.That(json, Does.Contain("\"initialGreeting\":false"));
        }
    }
}
