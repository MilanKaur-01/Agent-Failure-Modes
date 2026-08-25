# 02 — Context Drop

## The problem

Every LLM has a finite context window, and even when the window is technically large
enough, sending the full conversation history on every turn gets expensive fast. So
agent frameworks trim history, and the naive approach is to drop the *oldest* turns
first, on the theory that older = less relevant.

That assumption breaks down constantly in agent workloads. The oldest turn is often
where the important instruction lives: "this is a Tier-1 VIP, never auto-close their
ticket," "always CC security on this," "the customer already agreed to a refund, just
process it." If that turn gets silently truncated, the agent loses track of a
constraint that actually matters, and it won't tell you, because from its point of
view the instruction never existed.

## The technique

This repo implements a `ContextManager` (see
[`src/Safeguards/ContextManagement/ContextManager.cs`](../src/Safeguards/ContextManagement/ContextManager.cs))
that combines three techniques instead of relying on truncation alone:

1. **Pinning** — turns can be marked `IsPinned = true` via `PinFact(...)`. Pinned
   turns (the system prompt, and any critical "must-do" write context) are *never*
   summarized or dropped, no matter how long the conversation runs.
2. **Rolling summarization** — once the un-pinned history exceeds the token budget,
   the *oldest* un-pinned turns are collapsed into a single summary turn rather than
   deleted outright. Information is compressed, not lost.
3. **Recency window** — the most recent N un-pinned turns are always kept verbatim,
   so the agent has full fidelity on what *just* happened, even while older turns are
   being summarized.

Every time a new turn is added, `EnforceBudget()` re-checks the total estimated token
count. If it's over budget, it re-partitions the turn list into `pinned + summary +
recent` and returns a log line describing what happened — you can watch the
conversation get compressed in real time.

## See it in code

```csharp
var context = new ContextManager(tokenBudget: 120, recentTurnsToKeepVerbatim: 2);

context.PinFact(TurnRole.System,
    "SYSTEM/CRITICAL: This user is a Tier-1 VIP. Do NOT auto-close this ticket.");

context.AddTurn(TurnRole.User, "...");
Console.WriteLine(context.EnforceBudget());
```

Run the runnable demo to watch a long, chatty conversation get summarized while the
pinned VIP instruction survives to the very end:

```bash
dotnet run --project samples/ContextDropDemo
```

## Production notes

- The demo's `Summarize` method is intentionally a dumb string-concatenation stub so
  the sample runs without a live LLM call. In production, replace it with a call to a
  cheap summarization model (this is a great place to use a small/cheap model even if
  your main agent uses a larger one).
- Use your model provider's real tokenizer to compute `EstimatedTokens` instead of the
  `length / 4` heuristic used here.
- Consider re-summarizing the rolling summary itself periodically (summary-of-summary)
  so it doesn't grow unbounded over very long-running sessions.
- Be deliberate about what you pin. Pinning everything defeats the purpose — only pin
  facts that are genuinely load-bearing for correctness or safety.
