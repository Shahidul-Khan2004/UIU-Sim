using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the opening and closing of a dual-pivot security turnstile gate.
/// Rotates left and right pivots around their local Y axis relative to their initial closed rotations.
/// </summary>
[DisallowMultipleComponent]
public class GateController : MonoBehaviour
{
    [Header("Pivots")]
    [Tooltip("Left barrier pivot transform.")]
    [SerializeField] private Transform leftPivot;

    [Tooltip("Right barrier pivot transform.")]
    [SerializeField] private Transform rightPivot;

    [Header("Rotation Settings")]
    [Tooltip("Target rotation angle (degrees) around local Y axis for the left pivot when opening.")]
    [SerializeField] private float leftOpenAngle = 80f;

    [Tooltip("Target rotation angle (degrees) around local Y axis for the right pivot when opening.")]
    [SerializeField] private float rightOpenAngle = -80f;

    [Header("Animation Settings")]
    [Tooltip("Duration in seconds to transition between closed and open rotations.")]
    [SerializeField, Min(0f)] private float openDuration = 0.5f;

    [Tooltip("Seconds to hold the gate open before auto-closing.")]
    [SerializeField, Min(0f)] private float holdOpenSeconds = 2.5f;

    [Tooltip("Whether the gate automatically closes after holdOpenSeconds.")]
    [SerializeField] private bool autoClose = true;

    // ── Rotation State ─────────────────────────────────────────────────

    private Quaternion leftClosedRotation;
    private Quaternion rightClosedRotation;
    private Quaternion leftOpenRotation;
    private Quaternion rightOpenRotation;

    private Coroutine activeRoutine;
    private bool isBusy;
    private bool isOpen;

    // ── Public API / Properties ────────────────────────────────────────

    /// <summary>True while the gate is actively opening, waiting, or closing.</summary>
    public bool IsBusy => isBusy;

    /// <summary>True while the gate is in the opened state.</summary>
    public bool IsOpen => isOpen;

    public Transform LeftPivot
    {
        get => leftPivot;
        set => leftPivot = value;
    }

    public Transform RightPivot
    {
        get => rightPivot;
        set => rightPivot = value;
    }

    public float LeftOpenAngle
    {
        get => leftOpenAngle;
        set => leftOpenAngle = value;
    }

    public float RightOpenAngle
    {
        get => rightOpenAngle;
        set => rightOpenAngle = value;
    }

    public float OpenDuration
    {
        get => openDuration;
        set => openDuration = Mathf.Max(0f, value);
    }

    public float HoldOpenSeconds
    {
        get => holdOpenSeconds;
        set => holdOpenSeconds = Mathf.Max(0f, value);
    }

    public bool AutoClose
    {
        get => autoClose;
        set => autoClose = value;
    }

    public Quaternion LeftClosedRotation => leftClosedRotation;
    public Quaternion RightClosedRotation => rightClosedRotation;
    public Quaternion LeftOpenRotation => leftOpenRotation;
    public Quaternion RightOpenRotation => rightOpenRotation;

    // ── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        InitializeRotations();
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
        isBusy = false;
    }

    /// <summary>
    /// Records current local rotations of pivots as closed state and calculates open rotations.
    /// Can be called explicitly if pivots or angles are configured after Awake.
    /// </summary>
    public void InitializeRotations()
    {
        if (leftPivot != null)
        {
            leftClosedRotation = leftPivot.localRotation;
            leftOpenRotation = leftClosedRotation * Quaternion.Euler(0f, leftOpenAngle, 0f);
        }

        if (rightPivot != null)
        {
            rightClosedRotation = rightPivot.localRotation;
            rightOpenRotation = rightClosedRotation * Quaternion.Euler(0f, rightOpenAngle, 0f);
        }
    }

    // ── Gate Control ───────────────────────────────────────────────────

    /// <summary>
    /// Opens the gate smoothly. If already busy, duplicate calls are safely ignored.
    /// If autoClose is true, holds for holdOpenSeconds then smoothly returns to closed.
    /// </summary>
    public void Open()
    {
        if (isBusy)
        {
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        activeRoutine = StartCoroutine(OpenSequenceRoutine());
    }

    /// <summary>
    /// Closes the gate smoothly. Safely cancels any active open/hold routine so only
    /// one coroutine operates on pivots at any time.
    /// </summary>
    public void Close()
    {
        if (!isOpen && !isBusy)
        {
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        activeRoutine = StartCoroutine(CloseSequenceRoutine());
    }

    // ── Coroutines ─────────────────────────────────────────────────────

    private IEnumerator OpenSequenceRoutine()
    {
        isBusy = true;

        yield return RotatePivots(leftClosedRotation, leftOpenRotation, rightClosedRotation, rightOpenRotation, openDuration);
        isOpen = true;

        if (autoClose)
        {
            if (holdOpenSeconds > 0f)
            {
                yield return new WaitForSeconds(holdOpenSeconds);
            }

            yield return RotatePivots(leftOpenRotation, leftClosedRotation, rightOpenRotation, rightClosedRotation, openDuration);
            isOpen = false;
        }

        isBusy = false;
        activeRoutine = null;
    }

    private IEnumerator CloseSequenceRoutine()
    {
        isBusy = true;
        Quaternion leftStart = leftPivot != null ? leftPivot.localRotation : leftClosedRotation;
        Quaternion rightStart = rightPivot != null ? rightPivot.localRotation : rightClosedRotation;

        yield return RotatePivots(leftStart, leftClosedRotation, rightStart, rightClosedRotation, openDuration);

        isOpen = false;
        isBusy = false;
        activeRoutine = null;
    }

    private IEnumerator RotatePivots(Quaternion leftFrom, Quaternion leftTo, Quaternion rightFrom, Quaternion rightTo, float duration)
    {
        if (duration <= 0f)
        {
            ApplyLocalRotation(leftTo, rightTo);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Quaternion currentLeft = Quaternion.Slerp(leftFrom, leftTo, smoothT);
            Quaternion currentRight = Quaternion.Slerp(rightFrom, rightTo, smoothT);
            ApplyLocalRotation(currentLeft, currentRight);

            yield return null;
        }

        ApplyLocalRotation(leftTo, rightTo);
    }

    private void ApplyLocalRotation(Quaternion left, Quaternion right)
    {
        if (leftPivot != null)
        {
            leftPivot.localRotation = left;
        }

        if (rightPivot != null)
        {
            rightPivot.localRotation = right;
        }
    }
}
