using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UIU.Simulator.Gameplay.Editor.Tests
{
    /// <summary>
    /// State-level coverage for the rebuilt <see cref="PlayerMovement"/> controller.
    /// Drives private Tick via reflection so Keyboard.current is not required.
    /// </summary>
    [TestFixture]
    public sealed class PlayerMovementTests
    {
        private GameObject playerObject;
        private CharacterController characterController;
        private PlayerMovement playerMovement;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("TestPlayerMovement");
            playerObject.transform.position = new Vector3(0f, 2f, 0f);
            playerObject.transform.rotation = Quaternion.identity;

            characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.slopeLimit = 45f;
            characterController.stepOffset = 0.3f;
            characterController.skinWidth = 0.05f;

            playerMovement = playerObject.AddComponent<PlayerMovement>();
        }

        [TearDown]
        public void TearDown()
        {
            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void W_MovesPlayerForward()
        {
            Vector3 start = playerObject.transform.position;
            AccelerateFully(new Vector2(0f, 1f), sprint: false);

            Assert.That(playerObject.transform.position.z, Is.GreaterThan(start.z));
            Assert.That(HorizontalSpeed(), Is.EqualTo(GetFloat("walkSpeed")).Within(0.05f));
        }

        [Test]
        public void S_MovesPlayerBackward()
        {
            Vector3 start = playerObject.transform.position;
            AccelerateFully(new Vector2(0f, -1f), sprint: false);

            Assert.That(playerObject.transform.position.z, Is.LessThan(start.z));
        }

        [Test]
        public void AD_StrafeLeftAndRight()
        {
            Vector3 start = playerObject.transform.position;
            AccelerateFully(new Vector2(-1f, 0f), sprint: false);
            float leftX = playerObject.transform.position.x;
            Assert.That(leftX, Is.LessThan(start.x));

            SetField("horizontalVelocity", Vector3.zero);
            playerObject.transform.position = start;
            AccelerateFully(new Vector2(1f, 0f), sprint: false);
            Assert.That(playerObject.transform.position.x, Is.GreaterThan(start.x));
        }

        [Test]
        public void DiagonalInput_IsNormalized_NotFasterThanStraight()
        {
            AccelerateFully(new Vector2(1f, 1f), sprint: false);
            float diagonalSpeed = HorizontalSpeed();

            SetField("horizontalVelocity", Vector3.zero);
            AccelerateFully(new Vector2(0f, 1f), sprint: false);
            float straightSpeed = HorizontalSpeed();

            float walk = GetFloat("walkSpeed");
            Assert.That(diagonalSpeed, Is.EqualTo(walk).Within(0.05f));
            Assert.That(straightSpeed, Is.EqualTo(walk).Within(0.05f));
            Assert.That(diagonalSpeed, Is.EqualTo(straightSpeed).Within(0.05f));
        }

        [Test]
        public void NormalMovement_UsesWalkSpeed()
        {
            AccelerateFully(new Vector2(0f, 1f), sprint: false);
            Assert.That(playerMovement.IsSprinting, Is.False);
            Assert.That(HorizontalSpeed(), Is.EqualTo(GetFloat("walkSpeed")).Within(0.05f));
        }

        [Test]
        public void LeftShift_UsesSprintSpeed()
        {
            AccelerateFully(new Vector2(0f, 1f), sprint: true);
            Assert.That(playerMovement.IsSprinting, Is.True);
            Assert.That(HorizontalSpeed(), Is.EqualTo(GetFloat("sprintSpeed")).Within(0.05f));
        }

        [Test]
        public void ReleasingShift_ReturnsTowardWalkSpeed()
        {
            AccelerateFully(new Vector2(0f, 1f), sprint: true);
            Assert.That(HorizontalSpeed(), Is.EqualTo(GetFloat("sprintSpeed")).Within(0.05f));

            // Continue forward without sprint; deceleration/acceleration toward walk.
            for (int i = 0; i < 30; i++)
            {
                Tick(0.05f, new Vector2(0f, 1f), jumpPressed: false, sprintHeld: false);
            }

            Assert.That(playerMovement.IsSprinting, Is.False);
            Assert.That(HorizontalSpeed(), Is.EqualTo(GetFloat("walkSpeed")).Within(0.1f));
        }

        [Test]
        public void Movement_AcceleratesTowardTargetSpeed()
        {
            Tick(0.02f, new Vector2(0f, 1f), jumpPressed: false, sprintHeld: false);
            float early = HorizontalSpeed();
            Assert.That(early, Is.GreaterThan(0f));
            Assert.That(early, Is.LessThan(GetFloat("walkSpeed")));

            AccelerateFully(new Vector2(0f, 1f), sprint: false);
            Assert.That(HorizontalSpeed(), Is.EqualTo(GetFloat("walkSpeed")).Within(0.05f));
        }

        [Test]
        public void ReleasingMovement_Decelerates()
        {
            AccelerateFully(new Vector2(0f, 1f), sprint: false);
            Assert.That(HorizontalSpeed(), Is.GreaterThan(1f));

            Tick(0.05f, Vector2.zero, jumpPressed: false, sprintHeld: false);
            float afterOne = HorizontalSpeed();
            Assert.That(afterOne, Is.LessThan(GetFloat("walkSpeed")));

            for (int i = 0; i < 40; i++)
            {
                Tick(0.05f, Vector2.zero, jumpPressed: false, sprintHeld: false);
            }

            Assert.That(HorizontalSpeed(), Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void SpaceWhileGrounded_Jumps()
        {
            ForceGrounded();
            float before = GetFloat("verticalVelocity");
            Tick(0.02f, Vector2.zero, jumpPressed: true, sprintHeld: false);

            float expected = Mathf.Sqrt(GetFloat("jumpHeight") * -2f * GetFloat("gravity"));
            Assert.That(GetFloat("verticalVelocity"), Is.EqualTo(expected).Within(0.01f));
            Assert.That(GetFloat("verticalVelocity"), Is.GreaterThan(before));
            Assert.That(playerMovement.IsGrounded, Is.False);
        }

        [Test]
        public void HeldOrPressedSpace_CannotCreateRepeatedAirJumps()
        {
            ForceGrounded();
            Tick(0.02f, Vector2.zero, jumpPressed: true, sprintHeld: false);
            float afterFirst = GetFloat("verticalVelocity");

            // Still airborne: further jump presses must not relaunch.
            Tick(0.02f, Vector2.zero, jumpPressed: true, sprintHeld: false);
            Tick(0.02f, Vector2.zero, jumpPressed: true, sprintHeld: false);

            Assert.That(GetFloat("verticalVelocity"), Is.LessThan(afterFirst));
            Assert.That(GetFloat("coyoteCounter"), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void JumpBuffer_AllowsJumpShortlyAfterPressBeforeGrounded()
        {
            // Press while airborne with no coyote, then gain coyote/ground within buffer window.
            SetField("grounded", false);
            SetField("coyoteCounter", 0f);
            SetField("jumpBufferCounter", 0f);
            SetField("verticalVelocity", -1f);

            Tick(0.02f, Vector2.zero, jumpPressed: true, sprintHeld: false);
            Assert.That(GetFloat("jumpBufferCounter"), Is.GreaterThan(0f));
            Assert.That(GetFloat("verticalVelocity"), Is.LessThan(0f), "Should not jump without coyote.");

            SetField("grounded", true);
            SetField("coyoteCounter", GetFloat("coyoteTime"));
            Tick(0.02f, Vector2.zero, jumpPressed: false, sprintHeld: false);

            float expected = Mathf.Sqrt(GetFloat("jumpHeight") * -2f * GetFloat("gravity"));
            Assert.That(GetFloat("verticalVelocity"), Is.EqualTo(expected).Within(0.01f));
            Assert.That(GetFloat("jumpBufferCounter"), Is.EqualTo(0f));
        }

        [Test]
        public void CoyoteTime_AllowsJumpShortlyAfterLeavingGround()
        {
            SetField("grounded", false);
            SetField("coyoteCounter", GetFloat("coyoteTime"));
            SetField("jumpBufferCounter", 0f);
            SetField("verticalVelocity", -1f);

            Tick(0.02f, Vector2.zero, jumpPressed: true, sprintHeld: false);

            float expected = Mathf.Sqrt(GetFloat("jumpHeight") * -2f * GetFloat("gravity"));
            Assert.That(GetFloat("verticalVelocity"), Is.EqualTo(expected).Within(0.01f));
            Assert.That(GetFloat("coyoteCounter"), Is.EqualTo(0f));
        }

        [Test]
        public void Gravity_AppliesWhileAirborne()
        {
            SetField("grounded", false);
            SetField("coyoteCounter", 0f);
            SetField("verticalVelocity", 0f);

            float g = GetFloat("gravity");
            Tick(0.1f, Vector2.zero, jumpPressed: false, sprintHeld: false);

            Assert.That(GetFloat("verticalVelocity"), Is.EqualTo(g * 0.1f).Within(0.01f));
        }

        [Test]
        public void GroundedPlayer_DoesNotAccumulateExcessiveDownwardVelocity()
        {
            ForceGrounded();
            SetField("verticalVelocity", -50f);

            Tick(0.05f, Vector2.zero, jumpPressed: false, sprintHeld: false);

            Assert.That(GetFloat("verticalVelocity"), Is.EqualTo(GetFloat("groundedVerticalVelocity")).Within(0.01f));
        }

        [Test]
        public void CeilingCollision_CancelsUpwardVelocity()
        {
            SetField("grounded", false);
            SetField("coyoteCounter", 0f);
            SetField("verticalVelocity", 8f);

            MethodInfo resolve = typeof(PlayerMovement).GetMethod(
                "ResolveCollisions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolve, Is.Not.Null);
            resolve.Invoke(playerMovement, new object[] { CollisionFlags.Above });

            Assert.That(GetFloat("verticalVelocity"), Is.EqualTo(0f));
        }

        [Test]
        public void DisablingPlayerMovement_PreventsMovement()
        {
            AccelerateFully(new Vector2(0f, 1f), sprint: false);
            Vector3 before = playerObject.transform.position;

            playerMovement.enabled = false;
            Assert.That(HorizontalSpeed(), Is.EqualTo(0f).Within(0.001f));

            // Update would no-op while disabled; Tick is private and not called.
            Assert.That(playerObject.transform.position, Is.EqualTo(before));
        }

        [Test]
        public void ReEnabling_DoesNotPreserveStaleHorizontalMotion()
        {
            AccelerateFully(new Vector2(0f, 1f), sprint: false);
            Assert.That(HorizontalSpeed(), Is.GreaterThan(1f));

            playerMovement.enabled = false;
            Assert.That(HorizontalSpeed(), Is.EqualTo(0f).Within(0.001f));
            Assert.That(GetFloat("jumpBufferCounter"), Is.EqualTo(0f));
            Assert.That(GetFloat("coyoteCounter"), Is.EqualTo(0f));

            playerMovement.enabled = true;
            Assert.That(HorizontalSpeed(), Is.EqualTo(0f).Within(0.001f));
            Assert.That(playerMovement.IsSprinting, Is.False);
        }

        [Test]
        public void DisabledCharacterController_IsHandledSafely()
        {
            ForceGrounded();
            Vector3 before = playerObject.transform.position;
            characterController.enabled = false;

            Assert.DoesNotThrow(() =>
            {
                Tick(0.05f, new Vector2(0f, 1f), jumpPressed: true, sprintHeld: true);
            });

            Assert.That(playerObject.transform.position, Is.EqualTo(before));
            Assert.That(HorizontalSpeed(), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void AdvisorAndElevator_StillCompileAgainstPlayerMovementType()
        {
            // Smoke: type remains findable/disableable as modal UIs expect.
            PlayerMovement found = Object.FindFirstObjectByType<PlayerMovement>();
            Assert.That(found, Is.SameAs(playerMovement));
            found.enabled = false;
            Assert.That(found.enabled, Is.False);
            found.enabled = true;
            Assert.That(found.enabled, Is.True);
        }

        private void AccelerateFully(Vector2 input, bool sprint)
        {
            for (int i = 0; i < 40; i++)
            {
                Tick(0.05f, input, jumpPressed: false, sprintHeld: sprint);
            }
        }

        private void ForceGrounded()
        {
            SetField("grounded", true);
            SetField("coyoteCounter", GetFloat("coyoteTime"));
            SetField("jumpBufferCounter", 0f);
            SetField("verticalVelocity", GetFloat("groundedVerticalVelocity"));
        }

        private float HorizontalSpeed()
        {
            Vector3 h = (Vector3)GetField("horizontalVelocity");
            h.y = 0f;
            return h.magnitude;
        }

        private void Tick(float deltaTime, Vector2 moveInput, bool jumpPressed, bool sprintHeld)
        {
            MethodInfo method = typeof(PlayerMovement).GetMethod(
                "Tick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "PlayerMovement.Tick must exist for tests.");
            method.Invoke(playerMovement, new object[] { deltaTime, moveInput, jumpPressed, sprintHeld });
        }

        private float GetFloat(string fieldName)
        {
            return (float)GetField(fieldName);
        }

        private object GetField(string fieldName)
        {
            FieldInfo field = typeof(PlayerMovement).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            return field.GetValue(playerMovement);
        }

        private void SetField(string fieldName, object value)
        {
            FieldInfo field = typeof(PlayerMovement).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(playerMovement, value);
        }
    }
}
