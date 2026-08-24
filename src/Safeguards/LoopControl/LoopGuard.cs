namespace Safeguards.LoopControl;

/// <summary>
/// SAFEGUARD #1 — TASK LOOP FAILURE
///
/// Wraps an agent's reasoning loop so it can never retry forever. Every iteration of
/// the agent's "think -> call a tool -> observe" cycle should be reported to
/// <see cref="Evaluate"/> *before* the agent is allowed to run another iteration.
///
/// The guard tracks three independent things that can each end the loop:
///   1. Iteration count vs. <see cref="LoopBudget.MaxIterations"/>.
///   2. Consumed budget (tokens + wall-clock time) vs. the configured limits.
///   3. "Stall" detection — the same tool call (or no state change) repeating.
///
/// Whenever any of these trip, the guard returns <see cref="LoopDecision.EscalateToHuman"/>
/// instead of silently stopping or looping forever. That is the core lesson: an agent
/// should always know how to *hand off*, not just how to retry.
/// </summary>
public sealed class LoopGuard
{
    private readonly LoopBudget _budget;
    private readonly DateTimeOffset _startedAt;
    private readonly List<string> _recentSignatures = new();

    private int _iterations;
    private int _tokensSpent;

    public LoopGuard(LoopBudget budget)
    {
        _budget = budget;
        _startedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Iterations consumed so far — exposed for logging/telemetry.</summary>
    public int Iterations => _iterations;

    /// <summary>Tokens consumed so far — exposed for logging/telemetry.</summary>
    public int TokensSpent => _tokensSpent;

    /// <summary>
    /// Records one iteration of the agent loop and decides whether it may continue.
    /// </summary>
    /// <param name="stepSignature">
    /// A short string identifying "what the agent just did" (e.g. the tool name + args).
    /// Used purely for stall detection — repeating the same signature over and over
    /// means the agent isn't making progress.
    /// </param>
    /// <param name="tokensUsedThisStep">Tokens consumed by this step (0 if unknown/mocked).</param>
    /// <param name="taskCompleted">Set true when the agent itself reports the task is done.</param>
    public LoopEvaluation Evaluate(string stepSignature, int tokensUsedThisStep = 0, bool taskCompleted = false)
    {
        _iterations++;
        _tokensSpent += tokensUsedThisStep;
        _recentSignatures.Add(stepSignature);

        if (taskCompleted)
        {
            return new LoopEvaluation(
                LoopDecision.Exit,
                LoopStopReason.TaskCompleted,
                $"[LoopGuard] Iteration {_iterations}: task reported complete. Exiting cleanly.");
        }

        // --- Budget check #1: iteration count -------------------------------------
        var remainingIterations = _budget.MaxIterations - _iterations;
        if (remainingIterations <= 0)
        {
            return new LoopEvaluation(
                LoopDecision.EscalateToHuman,
                LoopStopReason.MaxIterationsExceeded,
                $"[LoopGuard] Iteration {_iterations}/{_budget.MaxIterations}: max iterations exceeded. Escalating to human.");
        }

        // --- Budget check #2: token / time budget ----------------------------------
        var elapsed = DateTimeOffset.UtcNow - _startedAt;
        var remainingTokens = _budget.MaxTokenBudget - _tokensSpent;
        var remainingTime = _budget.MaxDuration - elapsed;
        if (remainingTokens <= 0 || remainingTime <= TimeSpan.Zero)
        {
            return new LoopEvaluation(
                LoopDecision.EscalateToHuman,
                LoopStopReason.BudgetExhausted,
                $"[LoopGuard] Iteration {_iterations}: budget exhausted " +
                $"(tokens left={remainingTokens}, time left={remainingTime.TotalSeconds:0.0}s). Escalating to human.");
        }

        // --- Stall check: same signature repeated N times in a row -----------------
        if (IsStalled())
        {
            return new LoopEvaluation(
                LoopDecision.EscalateToHuman,
                LoopStopReason.StallDetected,
                $"[LoopGuard] Iteration {_iterations}: no progress detected — " +
                $"'{stepSignature}' repeated {_budget.StallThreshold}+ times in a row. Escalating to human.");
        }

        return new LoopEvaluation(
            LoopDecision.Continue,
            LoopStopReason.None,
            $"[LoopGuard] Iteration {_iterations}/{_budget.MaxIterations}: " +
            $"budget remaining (tokens={remainingTokens}, time={remainingTime.TotalSeconds:0.0}s). Continuing.");
    }

    private bool IsStalled()
    {
        if (_budget.StallThreshold <= 0 || _recentSignatures.Count < _budget.StallThreshold)
        {
            return false;
        }

        var window = _recentSignatures.TakeLast(_budget.StallThreshold).ToList();
        return window.Distinct().Count() == 1;
    }
}
