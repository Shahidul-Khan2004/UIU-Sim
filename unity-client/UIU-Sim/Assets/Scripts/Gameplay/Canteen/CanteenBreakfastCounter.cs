using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Breakfast state machine enum for the middle canteen counter.
/// </summary>
public enum CanteenBreakfastState
{
    NotStarted,
    Ordering,
    WaitingInQueue,
    Completed
}

/// <summary>
/// Interactive middle counter at the campus canteen.
/// Implements <see cref="IInteractable"/> to provide the breakfast dilemma:
/// <list type="bullet">
///   <item><b>Rice</b>: Instant breakfast, +5 Aura bonus (shortcut).</item>
///   <item><b>Porotta</b>: Real-time queue timer. Player enters a modal waiting state.</item>
///   <item><b>Wait to Completion</b>: Progress reaches 100%, 0 Aura delta, breakfast received.</item>
///   <item><b>Skip Breakfast</b>: -5 Aura penalty, no food received, breakfast event completed.</item>
///   <item><b>Skip the Line</b>: -10 Aura penalty, instant food received, breakfast event completed.</item>
/// </list>
/// Gated by <see cref="CampusDayState.HasCompletedBreakfastEvent"/> to prevent farming.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class CanteenBreakfastCounter : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [Tooltip("Label shown when the player looks at the counter.")]
    [SerializeField] private string prompt = "Order Breakfast";

    [Header("Dialogue - Speaker & Prompt")]
    [Tooltip("Speaker name displayed in DialogueUI header.")]
    [SerializeField] private string workerName = "Canteen Worker";

    [Tooltip("Prompt asked by the worker when ordering breakfast.")]
    [SerializeField, TextArea] private string breakfastPrompt = "What are you having?";

    [Header("Dialogue - Choices")]
    [Tooltip("Choice label for ordering Porotta.")]
    [SerializeField] private string porottaChoiceLabel = "Porotta";

    [Tooltip("Choice label for ordering Rice.")]
    [SerializeField] private string riceChoiceLabel = "Rice";

    [Header("Dialogue - Messages & Feedback")]
    [Tooltip("Notice message displayed regarding the Porotta queue wait time.")]
    [SerializeField, TextArea] private string porottaWaitMessage = "\"Porotta? That'll take around 10 minutes.\"";

    [Tooltip("Message returned if the player tries to order breakfast again today.")]
    [SerializeField, TextArea] private string alreadyCompletedMessage = "You've already sorted out breakfast today.";

    [Tooltip("Message when counter references are missing/unavailable.")]
    [SerializeField, TextArea] private string unavailableMessage = "The canteen counter is not available right now.";

    [Tooltip("Success text / reason when Rice is chosen.")]
    [SerializeField] private string riceSuccessReason = "Knew the canteen shortcut";

    [Tooltip("Ready text when Porotta queue is completed.")]
    [SerializeField] private string porottaReadyMessage = "Porotta ready!";

    [Tooltip("Result / reason when Skip Breakfast is chosen.")]
    [SerializeField] private string skipBreakfastReason = "Running on an empty stomach";

    [Tooltip("Result / reason when Skip the Line is chosen.")]
    [SerializeField] private string skipLineReason = "Skipped the line";

    [Header("Queue Timing")]
    [Tooltip("Compressed real-time duration in seconds for the porotta queue. Production default: ~75s. Testing: 5-10s.")]
    [SerializeField, Min(0.1f)] private float queueDuration = 75f;

    // ── State ─────────────────────────────────────────────────────────

    private CanteenBreakfastState currentState = CanteenBreakfastState.NotStarted;
    private bool isQueueActive;
    private Coroutine queueCoroutine;

    // Cached references for defensive restoration
    private PlayerMovement cachedPlayerMovement;
    private FirstPersonLook cachedFirstPersonLook;
    private PlayerStats cachedPlayerStats;
    private CampusDayState cachedDayState;
    private InteractionFeedback feedback;

    private bool wasMovementEnabled = true;
    private bool wasLookEnabled = true;

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Current state of the breakfast counter.</summary>
    public CanteenBreakfastState CurrentState => currentState;

    /// <summary>True while the player is waiting in the modal porotta queue.</summary>
    public bool IsQueueActive => isQueueActive;

    /// <summary>
    /// Configurable duration of the queue in seconds.
    /// Can be tuned down to 5–10 seconds for dev/automated testing.
    /// </summary>
    public float QueueDuration
    {
        get => queueDuration;
        set => queueDuration = Mathf.Max(0.1f, value);
    }

    // ── IInteractable ──────────────────────────────────────────────────

    public string InteractionPrompt => prompt;

    public string Interact()
    {
        // Guard: Queue or dialogue already open
        if (DialogueUI.IsOpen || CanteenQueueUI.IsOpen || isQueueActive)
        {
            return null;
        }

        if (!EnsureReferences())
        {
            return unavailableMessage;
        }

        // Check if breakfast has already been completed today
        if (cachedDayState.HasCompletedBreakfastEvent)
        {
            Debug.Log("[CanteenBreakfastCounter] Player already completed breakfast event today.");
            return alreadyCompletedMessage;
        }

        // Open breakfast dialogue choices
        currentState = CanteenBreakfastState.Ordering;

        DialogueUI.Instance.Show(
            workerName,
            breakfastPrompt,
            new[]
            {
                new DialogueUI.Choice(porottaChoiceLabel, OnSelectPorotta),
                new DialogueUI.Choice(riceChoiceLabel, OnSelectRice)
            }
        );

        return null;
    }

    // ── Choice Handlers ────────────────────────────────────────────────

    /// <summary>
    /// Rice route: Instant breakfast, +5 Aura bonus, event completed.
    /// Consequence is hidden until after choice.
    /// </summary>
    private void OnSelectRice()
    {
        if (!EnsureReferences())
        {
            return;
        }

        currentState = CanteenBreakfastState.Completed;
        cachedDayState.CompleteBreakfastEvent();
        cachedPlayerStats.ModifyAura(5f);

        if (feedback != null)
        {
            feedback.PlaySuccess();
        }

        Debug.Log($"[CanteenBreakfastCounter] {riceChoiceLabel} chosen: Instant breakfast (+5 Aura). Reason: {riceSuccessReason}.");
    }

    /// <summary>
    /// Porotta route: Enters the modal waiting queue.
    /// Disables player movement and camera look, shows queue UI with progress bar.
    /// </summary>
    private void OnSelectPorotta()
    {
        if (!EnsureReferences())
        {
            return;
        }

        if (CanteenQueueUI.Instance == null)
        {
            Debug.LogError("[CanteenBreakfastCounter] CanteenQueueUI.Instance is null. Ensure CanteenQueueUI is on the Player prefab.", this);
            return;
        }

        // Cache previous player component enabled states
        wasMovementEnabled = cachedPlayerMovement == null || cachedPlayerMovement.enabled;
        wasLookEnabled = cachedFirstPersonLook == null || cachedFirstPersonLook.enabled;

        // Freeze player movement and camera look
        if (cachedPlayerMovement != null)
        {
            cachedPlayerMovement.enabled = false;
        }

        if (cachedFirstPersonLook != null)
        {
            cachedFirstPersonLook.enabled = false; // Automatically unlocks cursor via FirstPersonLook.OnDisable()
        }

        // Guarantee cursor is unlocked and visible for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isQueueActive = true;
        currentState = CanteenBreakfastState.WaitingInQueue;

        // Open modal queue UI with configurable porotta wait notice
        CanteenQueueUI.Instance.Show(OnSkipBreakfast, OnSkipLine, porottaWaitMessage);

        // Start real-time queue countdown coroutine
        queueCoroutine = StartCoroutine(QueueTimerRoutine());

        Debug.Log($"[CanteenBreakfastCounter] {porottaChoiceLabel} chosen: Entered queue (duration={queueDuration:F1}s). Movement & look locked.");
    }

    // ── Queue Timer ────────────────────────────────────────────────────

    private IEnumerator QueueTimerRoutine()
    {
        float elapsed = 0f;

        while (elapsed < queueDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / queueDuration);

            if (CanteenQueueUI.Instance != null && CanteenQueueUI.IsOpen)
            {
                CanteenQueueUI.Instance.SetProgress(progress);
            }

            yield return null;
        }

        // Progress bar reached 100%
        OnQueueCompleted();
    }

    // ── Terminal Outcomes ──────────────────────────────────────────────

    /// <summary>
    /// Wait until completion: 100% progress, 0 Aura delta, breakfast received.
    /// </summary>
    private void OnQueueCompleted()
    {
        TeardownQueue(isDefensive: false);

        currentState = CanteenBreakfastState.Completed;
        cachedDayState?.CompleteBreakfastEvent();

        if (feedback != null)
        {
            feedback.PlaySuccess();
        }

        Debug.Log($"[CanteenBreakfastCounter] {porottaReadyMessage} Waited full queue duration. 0 Aura delta.");
    }

    /// <summary>
    /// Skip Breakfast: Cancels queue, -5 Aura penalty, no food received, event completed.
    /// </summary>
    private void OnSkipBreakfast()
    {
        TeardownQueue(isDefensive: false);

        currentState = CanteenBreakfastState.Completed;
        cachedDayState?.CompleteBreakfastEvent();
        cachedPlayerStats?.ModifyAura(-5f);

        if (feedback != null)
        {
            feedback.PlayFailure();
        }

        Debug.Log($"[CanteenBreakfastCounter] Skip Breakfast chosen: Queue cancelled (-5 Aura). Reason: {skipBreakfastReason}.");
    }

    /// <summary>
    /// Skip the Line: Cancels queue, -10 Aura penalty, breakfast received immediately, event completed.
    /// </summary>
    private void OnSkipLine()
    {
        TeardownQueue(isDefensive: false);

        currentState = CanteenBreakfastState.Completed;
        cachedDayState?.CompleteBreakfastEvent();
        cachedPlayerStats?.ModifyAura(-10f);

        if (feedback != null)
        {
            feedback.PlaySuccess();
        }

        Debug.Log($"[CanteenBreakfastCounter] Skip the Line chosen: Queue cancelled (-10 Aura). Reason: {skipLineReason}.");
    }

    // ── Centralized Idempotent Teardown ────────────────────────────────

    /// <summary>
    /// Centralized, idempotent teardown and restoration of the queue state.
    /// Restores player movement, look, cursor, stops the timer, and hides the UI.
    /// <para>
    /// Used by:
    /// <list type="bullet">
    ///   <item>Timer completion</item>
    ///   <item>Skip Breakfast</item>
    ///   <item>Skip the Line</item>
    ///   <item>Defensive cleanup on disable/destroy</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="isDefensive">When true, skips completion logic and does NOT alter Aura.</param>
    public void TeardownQueue(bool isDefensive = false)
    {
        if (!isQueueActive)
        {
            return;
        }

        isQueueActive = false;

        // 1. Stop active timer
        if (queueCoroutine != null)
        {
            StopCoroutine(queueCoroutine);
            queueCoroutine = null;
        }

        // 2. Hide queue UI
        if (CanteenQueueUI.Instance != null && CanteenQueueUI.IsOpen)
        {
            CanteenQueueUI.Instance.Hide();
        }

        // 3. Restore player movement
        if (cachedPlayerMovement != null)
        {
            cachedPlayerMovement.enabled = wasMovementEnabled;
        }

        // 4. Restore camera look (FirstPersonLook.OnEnable auto-locks cursor)
        if (cachedFirstPersonLook != null)
        {
            cachedFirstPersonLook.enabled = wasLookEnabled;
        }
        else
        {
            // Fallback if no FirstPersonLook component found
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Debug.Log($"[CanteenBreakfastCounter] TeardownQueue executed. isDefensive={isDefensive}");
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        feedback = GetComponent<InteractionFeedback>();
    }

    private void OnDisable()
    {
        // Defensive cleanup if destroyed or disabled mid-queue
        if (isQueueActive)
        {
            TeardownQueue(isDefensive: true);
            currentState = CanteenBreakfastState.NotStarted;
        }
    }

    private void OnDestroy()
    {
        if (isQueueActive)
        {
            TeardownQueue(isDefensive: true);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private bool EnsureReferences()
    {
        if (cachedDayState == null)
        {
            cachedDayState = FindFirstObjectByType<CampusDayState>();
        }

        if (cachedPlayerStats == null)
        {
            cachedPlayerStats = FindFirstObjectByType<PlayerStats>();
        }

        if (cachedPlayerMovement == null)
        {
            cachedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
        }

        if (cachedFirstPersonLook == null)
        {
            cachedFirstPersonLook = FindFirstObjectByType<FirstPersonLook>();
        }

        if (feedback == null)
        {
            feedback = GetComponent<InteractionFeedback>();
        }

        if (cachedDayState == null)
        {
            Debug.LogError("[CanteenBreakfastCounter] CampusDayState not found in scene.", this);
            return false;
        }

        if (cachedPlayerStats == null)
        {
            Debug.LogError("[CanteenBreakfastCounter] PlayerStats not found in scene.", this);
            return false;
        }

        if (DialogueUI.Instance == null)
        {
            Debug.LogError("[CanteenBreakfastCounter] DialogueUI.Instance is null.", this);
            return false;
        }

        return true;
    }
}
