namespace Safeguards.LoopControl;

/// <summary>
/// The structured decision a <see cref="LoopGuard"/> hands back to the caller after
/// evaluating each iteration of an agent's reasoning loop. This is the "safeguard
/// contract": instead of an agent looping silently forever, it must always receive
/// one of these three explicit signals and act on it.
/// </summary>
public enum LoopDecision
{
    /// <summary>Keep going — budget remains and progress is being made.</summary>
    Continue,

    /// <summary>Stop cleanly. Used when the task legitimately finished.</summary>
    Exit,

    /// <summary>
    /// Stop the automated loop and hand off to a human. Used when the budget is
    /// exhausted or the agent is stalled (repeating itself / making no progress).
    /// This is the key behavior that prevents "infinite retry" failures.
    /// </summary>
    EscalateToHuman
}

/// <summary>Why the loop guard reached the decision it did — used for logging/telemetry.</summary>
public enum LoopStopReason
{
    None,
    TaskCompleted,
    MaxIterationsExceeded,
    BudgetExhausted,
    StallDetected
}

/// <summary>Result returned from <see cref="LoopGuard.Evaluate"/> for a single iteration.</summary>
public record LoopEvaluation(LoopDecision Decision, LoopStopReason Reason, string Message);
