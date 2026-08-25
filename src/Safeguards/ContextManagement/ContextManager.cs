using System.Text;

namespace Safeguards.ContextManagement;

/// <summary>
/// SAFEGUARD #2 — CONTEXT DROP
///
/// Long-running agent conversations eventually exceed the model's context window (or
/// simply become expensive to keep sending in full). The naive fix — truncate the
/// oldest turns — is dangerous, because the *oldest* turn is often where the critical
/// instruction lives (e.g. "always CC the security team" or "do not close this ticket
/// without manager sign-off").
///
/// This ContextManager keeps context bounded using three techniques together:
///   1. PINNING       — turns marked <c>IsPinned = true</c> (system prompt + critical
///                       write-context) are *never* summarized or dropped.
///   2. SUMMARIZATION — once the un-pinned history exceeds the token budget, the
///                       oldest un-pinned turns are collapsed into a single rolling
///                       summary turn instead of being deleted outright.
///   3. RECENCY       — the most recent turns are always kept verbatim so the agent
///                       has full fidelity on what just happened.
///
/// The result: no matter how long the conversation runs, the pinned facts and the
/// recent turns survive, and everything else is compressed rather than lost.
/// </summary>
public sealed class ContextManager
{
    private readonly List<ConversationTurn> _turns = new();
    private readonly int _tokenBudget;
    private readonly int _recentTurnsToKeepVerbatim;

    /// <param name="tokenBudget">Total token budget for the (non-pinned) conversation history.</param>
    /// <param name="recentTurnsToKeepVerbatim">How many of the most recent un-pinned turns to always keep in full.</param>
    public ContextManager(int tokenBudget = 200, int recentTurnsToKeepVerbatim = 2)
    {
        _tokenBudget = tokenBudget;
        _recentTurnsToKeepVerbatim = recentTurnsToKeepVerbatim;
    }

    /// <summary>Pin an "always-keep" fact — e.g. the system prompt or a critical write instruction.</summary>
    public void PinFact(TurnRole role, string text) => _turns.Add(new ConversationTurn(role, text, IsPinned: true));

    /// <summary>Add a normal, droppable/summarizable conversation turn.</summary>
    public void AddTurn(TurnRole role, string text) => _turns.Add(new ConversationTurn(role, text));

    /// <summary>All turns currently held, in order (pinned + summarized + recent).</summary>
    public IReadOnlyList<ConversationTurn> Turns => _turns;

    /// <summary>
    /// Re-checks the token budget and, if exceeded, folds the oldest un-pinned turns
    /// (beyond the "keep verbatim" window) into a single rolling summary turn.
    /// Returns a log line describing what happened, for the demo's narrated output.
    /// </summary>
    public string EnforceBudget()
    {
        var totalTokens = _turns.Sum(t => t.EstimatedTokens);
        if (totalTokens <= _tokenBudget)
        {
            return $"[ContextManager] {totalTokens}/{_tokenBudget} tokens used — within budget, no trimming needed.";
        }

        var pinned = _turns.Where(t => t.IsPinned).ToList();
        var unpinned = _turns.Where(t => !t.IsPinned).ToList();

        // Anything already produced by a previous summarization pass starts with this
        // marker; keep treating it as summarizable so repeated overflows keep rolling up.
        var recent = unpinned.TakeLast(_recentTurnsToKeepVerbatim).ToList();
        var toSummarize = unpinned.Take(Math.Max(0, unpinned.Count - _recentTurnsToKeepVerbatim)).ToList();

        if (toSummarize.Count == 0)
        {
            return $"[ContextManager] {totalTokens}/{_tokenBudget} tokens used — over budget, " +
                   "but nothing left that is safe to summarize (only pinned + recent turns remain).";
        }

        var summary = Summarize(toSummarize);

        _turns.Clear();
        _turns.AddRange(pinned);
        _turns.Add(new ConversationTurn(TurnRole.System, summary, IsPinned: false));
        _turns.AddRange(recent);

        var newTotal = _turns.Sum(t => t.EstimatedTokens);
        return $"[ContextManager] {totalTokens}/{_tokenBudget} tokens used — over budget. " +
               $"Summarized {toSummarize.Count} older turn(s) into a rolling summary. " +
               $"Pinned facts kept verbatim ({pinned.Count}). New total: {newTotal} tokens.";
    }

    /// <summary>
    /// Very small, deterministic "summarizer" so the demo runs without a live LLM call.
    /// In production you would replace this with a call to a cheap summarization model.
    /// </summary>
    private static string Summarize(IReadOnlyList<ConversationTurn> turns)
    {
        var sb = new StringBuilder("[rolling summary of earlier turns] ");
        foreach (var turn in turns)
        {
            var snippet = turn.Text.Length > 40 ? turn.Text[..40] + "..." : turn.Text;
            sb.Append($"({turn.Role}: {snippet}) ");
        }

        return sb.ToString().TrimEnd();
    }
}
