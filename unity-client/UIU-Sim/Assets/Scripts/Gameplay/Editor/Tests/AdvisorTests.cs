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
using UnityEngine.UI;

namespace UIU.Simulator.Advisor.Tests
{
    [TestFixture]
    public sealed class AdvisorTests
    {
        private GameObject playerObject;
        private PlayerMovement playerMovement;
        private FirstPersonLook firstPersonLook;
        private GameObject cameraObject;

        private GameObject advisorHost;
        private MockAdvisorClient mockClient;
        private AdvisorUI advisorUI;
        private GameObject npcObject;
        private AcademicAdvisorInteractable advisorInteractable;

        public class MockAdvisorClient : AdvisorClient
        {
            public AdvisorChatRequestDto LastRequest;
            public int RequestCount;
            public bool CompleteImmediately = true;
            public bool ShouldSucceed = true;
            public string ResponseReply = "Test advisor reply.";
            public string ErrorMessage = "Service Unavailable";
            public long ErrorCode = 503;
            public Action<AdvisorChatResponseDto> PendingSuccess;
            public Action<string, long> PendingError;

            public override void SendChat(
                AdvisorChatRequestDto request,
                Action<AdvisorChatResponseDto> onSuccess,
                Action<string, long> onError)
            {
                LastRequest = request;
                RequestCount++;
                if (CompleteImmediately)
                {
                    if (ShouldSucceed)
                    {
                        var response = new AdvisorChatResponseDto
                        {
                            success = true,
                            inScope = true,
                            reply = ResponseReply
                        };
                        onSuccess?.Invoke(response);
                    }
                    else
                    {
                        onError?.Invoke(ErrorMessage, ErrorCode);
                    }
                }
                else
                {
                    PendingSuccess = onSuccess;
                    PendingError = onError;
                }
            }
        }

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("TestPlayer");
            playerMovement = playerObject.AddComponent<PlayerMovement>();
            firstPersonLook = playerObject.AddComponent<FirstPersonLook>();

            cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.AddComponent<Camera>();

            FieldInfo camField = typeof(FirstPersonLook).GetField("cameraTransform", BindingFlags.NonPublic | BindingFlags.Instance);
            if (camField != null)
            {
                camField.SetValue(firstPersonLook, cameraObject.transform);
            }

            // Setup Advisor UI host with mock client
            advisorHost = new GameObject("TestAdvisorUI");
            mockClient = advisorHost.AddComponent<MockAdvisorClient>();
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

            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
            if (npcObject != null) UnityEngine.Object.DestroyImmediate(npcObject);
            if (advisorHost != null) UnityEngine.Object.DestroyImmediate(advisorHost);
        }

        private List<Button> GetQuickActionButtons()
        {
            FieldInfo qaField = typeof(AdvisorUI).GetField("quickActionButtons", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(qaField, Is.Not.Null, "quickActionButtons field must exist on AdvisorUI.");
            return (List<Button>)qaField.GetValue(advisorUI);
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
        public void Requirement1_AdvisorUIOpen_CursorVisibleAndUnlocked_MovementAndLookDisabled()
        {
            Assert.That(playerMovement.enabled, Is.True);
            Assert.That(firstPersonLook.enabled, Is.True);

            advisorUI.Show();

            Assert.That(AdvisorUI.IsOpen, Is.True, "AdvisorUI.IsOpen must be true after Show().");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "Cursor must be unlocked (None) when AdvisorUI is open.");
            Assert.That(Cursor.visible, Is.True, "Cursor must be visible when AdvisorUI is open.");
            Assert.That(playerMovement.enabled, Is.False, "PlayerMovement must be disabled when AdvisorUI is open.");
            Assert.That(firstPersonLook.enabled, Is.False, "FirstPersonLook must be disabled when AdvisorUI is open.");
        }

        [Test]
        public void Requirements2To5_ClickingImproveAcademically_KeepsModalOpen_CursorUnlocked_MovementAndLookDisabled()
        {
            advisorUI.Show();
            List<Button> buttons = GetQuickActionButtons();
            Assert.That(buttons.Count, Is.GreaterThanOrEqualTo(1), "Expected at least 1 quick-action button.");

            Button improveAcademicallyBtn = buttons[0];
            Assert.That(improveAcademicallyBtn, Is.Not.Null);

            mockClient.ResponseReply = "Focus on attending lectures and studying regularly.";
            improveAcademicallyBtn.onClick.Invoke();

            // Requirement 2: AdvisorUI remains open
            Assert.That(AdvisorUI.IsOpen, Is.True, "AdvisorUI must remain open after clicking Improve Academically.");

            // Requirement 3: Cursor remains visible and unlocked
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "Cursor must remain unlocked after clicking Improve Academically.");
            Assert.That(Cursor.visible, Is.True, "Cursor must remain visible after clicking Improve Academically.");

            // Requirement 4: PlayerMovement remains disabled
            Assert.That(playerMovement.enabled, Is.False, "PlayerMovement must remain disabled after clicking Improve Academically.");

            // Requirement 5: FirstPersonLook remains disabled
            Assert.That(firstPersonLook.enabled, Is.False, "FirstPersonLook must remain disabled after clicking Improve Academically.");

            // Verify prompt sent
            Assert.That(mockClient.LastRequest, Is.Not.Null);
            Assert.That(mockClient.LastRequest.message, Is.EqualTo(AdvisorUI.QuickAction1Prompt));
        }

        [Test]
        public void Requirement6_RequestCompletion_ReplyDisplayed_AdvisorUIRemainsOpen()
        {
            mockClient.CompleteImmediately = false;
            advisorUI.Show();

            // Complete initial greeting if pending
            if (mockClient.PendingSuccess != null)
            {
                mockClient.PendingSuccess.Invoke(new AdvisorClient.AdvisorChatResponseDto
                {
                    success = true,
                    inScope = true,
                    reply = "Welcome, student!"
                });
                mockClient.PendingSuccess = null;
            }

            List<Button> buttons = GetQuickActionButtons();
            Button btn = buttons[0];

            // Trigger request in flight
            btn.onClick.Invoke();

            Assert.That(AdvisorUI.IsOpen, Is.True, "AdvisorUI must remain open while request is in flight.");
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
            Assert.That(btn.interactable, Is.False, "Quick action button should be disabled during flight.");

            // Complete response
            string advisorReply = "Make sure to review your assignments before submission.";
            mockClient.PendingSuccess.Invoke(new AdvisorClient.AdvisorChatResponseDto
            {
                success = true,
                inScope = true,
                reply = advisorReply
            });

            Assert.That(AdvisorUI.IsOpen, Is.True, "AdvisorUI must remain open after request completes.");
            Assert.That(playerMovement.enabled, Is.False, "PlayerMovement must remain disabled after response arrives.");
            Assert.That(firstPersonLook.enabled, Is.False, "FirstPersonLook must remain disabled after response arrives.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "Cursor must remain unlocked after response arrives.");
            Assert.That(Cursor.visible, Is.True, "Cursor must remain visible after response arrives.");
            Assert.That(btn.interactable, Is.True, "Quick action button must be restored after response completes.");

            // Verify history updated
            FieldInfo historyField = typeof(AdvisorUI).GetField("history", BindingFlags.NonPublic | BindingFlags.Instance);
            var history = (List<AdvisorClient.AdvisorHistoryMessageDto>)historyField.GetValue(advisorUI);
            Assert.That(history.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(history[history.Count - 1].content, Is.EqualTo(advisorReply));
        }

        [Test]
        public void Requirement7_RequestFailure_AdvisorUIRemainsOpen_ControlsUsable_GameplayLocksPreserved()
        {
            mockClient.CompleteImmediately = false;
            advisorUI.Show();

            // Complete initial greeting
            if (mockClient.PendingSuccess != null)
            {
                mockClient.PendingSuccess.Invoke(new AdvisorClient.AdvisorChatResponseDto
                {
                    success = true,
                    inScope = true,
                    reply = "Hello!"
                });
                mockClient.PendingSuccess = null;
            }

            List<Button> buttons = GetQuickActionButtons();
            Button btn = buttons[0];

            btn.onClick.Invoke();
            Assert.That(btn.interactable, Is.False);

            // Fail request
            mockClient.PendingError.Invoke("Advisor unavailable", 503);

            // Verify modal lock is STILL intact
            Assert.That(AdvisorUI.IsOpen, Is.True, "AdvisorUI must remain open on request failure.");
            Assert.That(playerMovement.enabled, Is.False, "PlayerMovement must remain disabled on failure.");
            Assert.That(firstPersonLook.enabled, Is.False, "FirstPersonLook must remain disabled on failure.");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "Cursor must remain unlocked on failure.");
            Assert.That(Cursor.visible, Is.True, "Cursor must remain visible on failure.");

            // Verify UI controls are restored for user to retry
            Assert.That(btn.interactable, Is.True, "Quick action button must become usable again on failure.");
        }

        [Test]
        public void Requirement8_AllFourQuickActions_UseSameSendPath_KeepModalOpen()
        {
            advisorUI.Show();
            List<Button> buttons = GetQuickActionButtons();
            Assert.That(buttons.Count, Is.EqualTo(4), "Expected exactly 4 quick-action buttons.");

            string[] expectedPrompts = new string[]
            {
                AdvisorUI.QuickAction1Prompt,
                AdvisorUI.QuickAction2Prompt,
                AdvisorUI.QuickAction3Prompt,
                AdvisorUI.QuickAction4Prompt
            };

            for (int i = 0; i < 4; i++)
            {
                mockClient.LastRequest = null;
                buttons[i].onClick.Invoke();

                Assert.That(mockClient.LastRequest, Is.Not.Null, $"Button {i} should have sent a request.");
                Assert.That(mockClient.LastRequest.message, Is.EqualTo(expectedPrompts[i]),
                    $"Button {i} should send the expected prompt text.");
                Assert.That(AdvisorUI.IsOpen, Is.True, $"AdvisorUI must remain open after clicking action {i}.");
                Assert.That(playerMovement.enabled, Is.False, $"PlayerMovement must stay disabled after clicking action {i}.");
                Assert.That(firstPersonLook.enabled, Is.False, $"FirstPersonLook must stay disabled after clicking action {i}.");
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), $"Cursor must stay unlocked after clicking action {i}.");
                Assert.That(Cursor.visible, Is.True, $"Cursor must stay visible after clicking action {i}.");
            }
        }

        [Test]
        public void Requirement9_OnlyExplicitClose_RestoresPlayerControlsAndCursor()
        {
            advisorUI.Show();

            List<Button> buttons = GetQuickActionButtons();
            buttons[0].onClick.Invoke();

            // All input locks must still be held
            Assert.That(AdvisorUI.IsOpen, Is.True);
            Assert.That(playerMovement.enabled, Is.False);
            Assert.That(firstPersonLook.enabled, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);

            // Now explicitly close AdvisorUI
            advisorUI.Hide();

            // Now and ONLY now should gameplay input be restored
            Assert.That(AdvisorUI.IsOpen, Is.False, "AdvisorUI.IsOpen must be false after Hide().");
            Assert.That(playerMovement.enabled, Is.True, "PlayerMovement must be restored after Hide().");
            Assert.That(firstPersonLook.enabled, Is.True, "FirstPersonLook must be restored after Hide().");

            // In Unity EditMode / batchmode, the engine enforces CursorLockMode.None for the editor host.
            // In runtime PlayMode, CursorLockMode.Locked is applied.
            if (Application.isPlaying)
            {
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked), "Cursor must be locked after Hide().");
                Assert.That(Cursor.visible, Is.False, "Cursor must be hidden after Hide().");
            }
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

        [Test]
        public void AdvisorUI_LayoutAllocation_ConversationAreaDominatesAndConsumesUsableSpace()
        {
            advisorUI.Show();

            Transform canvasTransform = advisorHost.transform.Find("AdvisorCanvas");
            Assert.That(canvasTransform, Is.Not.Null, "AdvisorCanvas should be instantiated.");

            Transform panelTransform = canvasTransform.Find("AdvisorPanel");
            Assert.That(panelTransform, Is.Not.Null, "AdvisorPanel should be instantiated.");

            RectTransform panelRect = panelTransform.GetComponent<RectTransform>();
            VerticalLayoutGroup panelVLG = panelTransform.GetComponent<VerticalLayoutGroup>();
            Assert.That(panelVLG, Is.Not.Null);
            Assert.That(panelVLG.childControlHeight, Is.True, "panelVLG must control child height for predictable layout distribution.");

            Transform scrollTransform = panelTransform.Find("ScrollView");
            Assert.That(scrollTransform, Is.Not.Null);
            LayoutElement scrollLE = scrollTransform.GetComponent<LayoutElement>();
            Assert.That(scrollLE, Is.Not.Null);
            Assert.That(scrollLE.flexibleHeight, Is.EqualTo(1f), "ScrollView must have flexibleHeight = 1 to consume remaining vertical space.");

            Transform qaTransform = panelTransform.Find("QuickActionsGrid");
            Assert.That(qaTransform, Is.Not.Null);
            LayoutElement qaLE = qaTransform.GetComponent<LayoutElement>();
            Assert.That(qaLE, Is.Not.Null);
            Assert.That(qaLE.preferredHeight, Is.EqualTo(78f), "Quick-actions region must have compact fixed height.");
            Assert.That(qaLE.flexibleHeight, Is.EqualTo(0f), "Quick-actions region must not compete with flexible history area.");

            Transform inputTransform = panelTransform.Find("InputRow");
            Assert.That(inputTransform, Is.Not.Null);
            LayoutElement inputLE = inputTransform.GetComponent<LayoutElement>();
            Assert.That(inputLE, Is.Not.Null);
            Assert.That(inputLE.preferredHeight, Is.EqualTo(44f), "Input row must have compact fixed height.");
            Assert.That(inputLE.flexibleHeight, Is.EqualTo(0f), "Input row must not compete with flexible history area.");

            // Force layout rebuild and assert resolved runtime dimensions
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            RectTransform scrollRectTransform = scrollTransform.GetComponent<RectTransform>();
            float usableHeight = panelRect.rect.height - panelVLG.padding.top - panelVLG.padding.bottom;
            float scrollHeight = scrollRectTransform.rect.height;
            float scrollRatio = scrollHeight / usableHeight;

            Assert.That(scrollHeight, Is.GreaterThan(350f), "ScrollView height should be substantially increased.");
            Assert.That(scrollRatio, Is.GreaterThanOrEqualTo(0.55f).And.LessThanOrEqualTo(0.70f),
                $"Conversation area should occupy roughly 55-65% of usable panel height. Actual ratio: {scrollRatio:P1} ({scrollHeight}px / {usableHeight}px)");
        }
    }
}
