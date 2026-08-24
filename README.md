# Agent-Failure-Modes

**A hands-on, runnable teaching repo for three ways LLM agents fail in production —
and the concrete safeguards that fix each one.**

If you're building (or reviewing) an agent that can call tools on its own, this repo
is for you. It's built around a deliberately *simple* helpdesk triage agent using
[Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) in C#/.NET
— the agent itself is trivial on purpose, so all your attention goes to the
safeguards wrapped around it.

> 🎓 This is teaching material, not a production framework. Every safeguard is real,
> runnable code — but simplified so the *idea* is easy to see. See
> [Key takeaways / production notes](#key-takeaways--production-notes) for what to
> harden before you ship anything like this for real.

---

## Why agent failure modes matter

Once you let an LLM decide what to do next — which tool to call, whether to keep
trying, what to remember — you've handed it a surprising amount of control over your
system's behavior. Three failure patterns show up again and again:

- **The agent won't stop.** It keeps retrying the same failing action, burning tokens
  and API calls, because nothing tells it "this isn't working, stop and hand off."
- **The agent forgets what matters.** As a conversation grows, something has to give
  way to fit the context window — and if you truncate blindly, the very first
  instruction (often the most important one) is usually the first thing dropped.
- **The agent reaches for more than it should.** A confused model, a manipulated
  prompt, or a buggy tool-selection step can lead an agent to attempt an action —
  delete a record, grant a permission — that it should never be trusted to perform
  on its own.

None of these are exotic edge cases. They're the default behavior of a loop with no
guardrails. This repo shows you how to add those guardrails in plain C# code.

## What you'll learn

- How to wrap an agent's reasoning loop with an iteration/token/time **budget** and a
  **stall detector**, so it always resolves to *continue*, *exit*, or
  *escalate-to-human* — never "loop forever."
- How to keep critical instructions alive in a long conversation using **pinned
  facts**, **rolling summarization**, and **token-budget-based trimming**.
- How to enforce **least privilege** for agent tool calls with a **deny-by-default
  allow-list**, a **permission ceiling**, and **short-lived, scoped credential
  leases**.
- How to structure this kind of safety code so it's a genuine choke point the agent
  can't talk its way around — not just a comment in a system prompt.

## The base example

The scenario is an **IT helpdesk triage agent**: it looks up tickets, resets
passwords, and escalates to a human when it's stuck. All of its tools are **mock
stubs** — `LookupTicket`, `ResetPassword`, and `EscalateToHuman` return fake strings,
and nothing here talks to a real ticketing system. There's also one deliberately
**sensitive** pair of tools, `DeleteUser` / `GrantAdmin`, included *only* so the
permission-ceiling safeguard has something dangerous to deny.

Why so simple? Because the goal of this repo is the safeguards, not the agent. If the
agent were doing something complicated, it would compete for your attention with the
part that actually matters.

```
   ┌──────────┐        ┌───────────────────┐        ┌────────────────────┐
   │  Agent   │──1──▶  │   LoopGuard        │──2──▶  │  PermissionGate     │
   │ (Semantic│        │ (budget + stall    │        │ (allow-list +       │
   │  Kernel) │        │  detection)        │        │  ceiling + lease)   │
   └────┬─────┘        └───────────────────┘        └─────────┬───────────┘
        │                                                       │
        │        ┌────────────────────────┐                    │
        └───3───▶│   ContextManager        │◀───────────────────┘
                  │ (pinned facts + rolling │
                  │  summarization)         │
                  └────────────┬────────────┘
                                │
                                ▼
                     ┌────────────────────┐
                     │   Mock Helpdesk     │
                     │   Tools (stubs)     │
                     └────────────────────┘
```

1. Every loop iteration is reported to the `LoopGuard` *before* another iteration is
   allowed to run.
2. Every tool call is checked by the `PermissionGate` before it's actually invoked.
3. Every turn added to the conversation passes through the `ContextManager`, which
   keeps pinned facts alive and summarizes the rest once it's over budget.

## The three failure modes

### 1. Task loop failure

**The problem:** an agent stuck trying (and re-trying) something it can't actually
accomplish, with no mechanism to notice and stop.

**The technique:** a `LoopGuard` tracks iteration count, a token/time budget, and
repeated ("stalled") tool calls, and returns a structured decision — `Continue`,
`Exit`, or `EscalateToHuman` — every single iteration.

```csharp
// src/Safeguards/LoopControl/LoopGuard.cs
var guard = new LoopGuard(new LoopBudget { MaxIterations = 4, StallThreshold = 2 });
var evaluation = guard.Evaluate("lookup_ticket(TCK-1042)", tokensUsedThisStep: 80);
// evaluation.Decision => Continue | Exit | EscalateToHuman
```

📖 Deep dive: [`docs/01-task-loop-failure.md`](docs/01-task-loop-failure.md)

### 2. Context drop

**The problem:** long conversations exceed the context/token budget, and naive
truncation drops the oldest turn — often exactly where the critical instruction
lives.

**The technique:** a `ContextManager` pins "always-keep" facts, keeps the most recent
turns verbatim, and rolls the middle of the conversation into a summary instead of
deleting it.

```csharp
// src/Safeguards/ContextManagement/ContextManager.cs
context.PinFact(TurnRole.System, "SYSTEM/CRITICAL: never auto-close this VIP ticket.");
context.AddTurn(TurnRole.User, "...");
Console.WriteLine(context.EnforceBudget()); // summarizes older turns once over budget
```

📖 Deep dive: [`docs/02-context-drop.md`](docs/02-context-drop.md)

### 3. Permission escalation

**The problem:** an agent (confused, manipulated, or buggy) attempts an action —
deleting a user, granting admin — that it should never be trusted to perform
autonomously.

**The technique:** a `PermissionGate` enforces a deny-by-default allow-list plus a
hard permission ceiling for sensitive tools, and requires a live, correctly-scoped,
non-expired `CredentialLease` for every call.

```csharp
// src/Safeguards/Permissions/PermissionGate.cs
var gate = new PermissionGate(allowList: new[] { "reset_password", "delete_user" },
                               sensitiveCeiling: new[] { "delete_user" });
gate.TryAuthorize("delete_user", lease); // => DeniedByCeiling, even though it's allow-listed
```

📖 Deep dive: [`docs/03-permission-escalation.md`](docs/03-permission-escalation.md)

## Repository structure

```
Agent-Failure-Modes/
├── README.md                     # you are here
├── Agent-Failure-Modes.sln        # solution wiring all projects together
├── src/
│   ├── Agent/                    # simple Semantic Kernel triage agent (SK wiring + config)
│   ├── Tools/                    # mock helpdesk tools (stubs, incl. sensitive ones)
│   └── Safeguards/
│       ├── LoopControl/          # iteration/budget + stall detection + exit/escalate decision
│       ├── ContextManagement/    # pinned facts + summarization + token-budget trimming
│       └── Permissions/          # allow-list + permission ceiling + short-lived credential lease
├── samples/                      # runnable demos, one per failure mode
│   ├── LoopFailureDemo/
│   ├── ContextDropDemo/
│   └── PermissionEscalationDemo/
└── docs/                         # one explainer markdown per failure mode
    ├── 01-task-loop-failure.md
    ├── 02-context-drop.md
    └── 03-permission-escalation.md
```

## Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) or later.
- **No API key required** to run the three safeguard demos — they're designed to run
  fully offline using scripted/deterministic data, so you can see every safeguard
  fire without spending a cent or configuring anything.
- If you want to wire the `Agent` project up to a real Semantic Kernel model, set
  these environment variables (there are no secrets checked into this repo):

  | Variable | Purpose | Default |
  |---|---|---|
  | `AGENT_MODEL_ID` | Chat completion model/deployment name | `gpt-4o-mini` (small & cheap — swap to `gpt-4o` for a more capable model) |
  | `AGENT_API_KEY` | OpenAI-compatible API key | *(none — required only for live calls)* |
  | `AGENT_ENDPOINT` | Optional custom endpoint, e.g. Azure OpenAI | *(none — defaults to public OpenAI)* |

## How to run

```bash
# Build everything
dotnet build

# Safeguard #1 — watch the loop budget count down and escalate to a human
dotnet run --project samples/LoopFailureDemo

# Safeguard #2 — watch a long conversation get summarized while a pinned
# critical instruction survives to the end
dotnet run --project samples/ContextDropDemo

# Safeguard #3 — watch safe tool calls succeed, sensitive ones get denied by
# the permission ceiling, and an expired credential lease get rejected
dotnet run --project samples/PermissionEscalationDemo
```

Each demo prints a narrated, step-by-step trace to the console — you'll see the
budget or token count ticking down, the exact decision made at each step, and a final
summary explaining what just happened and why it matters.

## Key takeaways / production notes

These are **teaching illustrations**, not a production-ready SDK. Before adapting
this pattern for real systems, consider hardening:

- **LoopGuard**: feed it real token usage from your model provider's response
  metadata, and make `EscalateToHuman` actually notify a human (Slack/Teams/PagerDuty)
  instead of just logging.
- **ContextManager**: replace the toy string-concatenation summarizer with a real
  (ideally cheap) summarization model call, and use your provider's actual tokenizer
  instead of the `length / 4` heuristic.
- **PermissionGate / CredentialBroker**: back the credential lease with a real
  short-lived-credential mechanism (cloud IAM temp tokens, Vault/Key Vault dynamic
  secrets, short-`exp` signed JWTs), and log every permission decision for auditability.
- All three safeguards should be **tested independently of the LLM** — that's exactly
  why the samples in this repo run without a live model, and it's a pattern worth
  keeping in real systems too.

## Further reading

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) — the
  agent orchestration framework used here.
- [`docs/01-task-loop-failure.md`](docs/01-task-loop-failure.md),
  [`docs/02-context-drop.md`](docs/02-context-drop.md),
  [`docs/03-permission-escalation.md`](docs/03-permission-escalation.md) — the
  deep-dive explainer for each safeguard.

## Contributing

This is a teaching sample maintained for a developer-advocate audience. Issues and
PRs that improve clarity, fix bugs, or add another well-scoped failure mode/safeguard
are welcome.

## License

No license file is currently included. Treat this repository as "all rights
reserved" for reuse beyond personal learning until a license is added.
