# 01 — Task Loop Failure

## The problem

Give an LLM agent a loop ("think, act, observe, repeat") and a tool it can call, and
sooner or later you'll hit a scenario where the model just... doesn't stop. Maybe it
keeps calling the same tool with the same arguments because it doesn't realize the
result isn't changing. Maybe it's stuck trying to satisfy an instruction that's
actually impossible with the tools it has. Maybe a bug in your orchestration code
means "try again" never actually terminates.

In production, this can turn into an expensive loop burning tokens.

The fix is to have deterministic guardrails in place and define exit conditions.

## The technique

This repo implements a `LoopGuard` (see
[`src/Safeguards/LoopControl/LoopGuard.cs`](../src/Safeguards/LoopControl/LoopGuard.cs))
that every iteration of the agent loop must report to before it's allowed to run
another iteration. The guard checks three independent things:

1. **Max iteration count** — a hard ceiling (`LoopBudget.MaxIterations`) on how many
   times the loop may run, full stop.
2. **Budget (tokens / time)** — `LoopBudget.MaxTokenBudget` and `MaxDuration` cap the
   total token spend and wall-clock time for the whole task, not just the loop count.
   This matters because a handful of very "chatty" iterations can be just as costly
   as many small ones.
3. **Stall detection** — if the agent repeats the exact same tool call
   (`StallThreshold` times in a row), the guard concludes there's no progress being
   made and stops the loop *before* the iteration budget is even exhausted.

Whichever check trips, `LoopGuard.Evaluate` returns a structured
[`LoopEvaluation`](../src/Safeguards/LoopControl/LoopDecision.cs) with one of three
decisions:

| Decision | Meaning |
|---|---|
| `Continue` | Budget remains, no stall detected — keep going. |
| `Exit` | The task legitimately finished. Stop cleanly. |
| `EscalateToHuman` | Budget exhausted or stalled. Stop the automated loop and hand off. |

Notice that "loop forever" is not a possible outcome. The agent must always resolve
to one of those three states.

## See it in code

```csharp
var guard = new LoopGuard(new LoopBudget { MaxIterations = 4, StallThreshold = 2 });

var evaluation = guard.Evaluate("lookup_ticket(TCK-1042)", tokensUsedThisStep: 80);
// evaluation.Decision is Continue, Exit, or EscalateToHuman
```

Run the runnable demo to see the whole thing in action, fully offline:

```bash
dotnet run --project samples/LoopFailureDemo
```

You'll see the budget counted down turn by turn, a stall detected after the agent
repeats the same tool call, and a final `EscalateToHuman` decision — instead of an
infinite loop.

## Production notes

- Wire real token usage from your model provider's response metadata into
  `tokensUsedThisStep` instead of a mock constant.
- Consider making `EscalateToHuman` actually page a human (ticket assignment,
  Slack/Teams alert, PagerDuty, etc.) rather than just logging.
- Track *why* stalls happen — repeated identical tool calls are one signal, but you
  may also want to detect "no new information added to the conversation" as a
  broader form of stalling.
