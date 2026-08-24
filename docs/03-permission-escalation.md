# 03 — Permission Escalation

## The problem

Function-calling / tool-using agents are only as safe as the tools you let them
touch. The risk isn't only "the model does something malicious" — it's much more
often "the model is confused, or a user manipulates it with a clever prompt, and it
ends up calling a tool it should never have been trusted with in the first place."

A helpdesk triage agent that can call `ResetPassword` is useful. The same agent
being able to call `DeleteUser` or `GrantAdmin` autonomously is a very different risk
profile — and "the system prompt told it not to" is not a safeguard, because prompts
are not access control.

## The technique

This repo implements a `PermissionGate` (see
[`src/Safeguards/Permissions/PermissionGate.cs`](../src/Safeguards/Permissions/PermissionGate.cs))
that sits as the single choke point every tool call must pass through, combining
three independent layers of defense:

1. **Deny-by-default allow-list** — a tool must be explicitly present in the
   allow-list to be considered at all. Anything not listed is denied automatically,
   with no special-casing required.
2. **Permission ceiling** — a separate, smaller set of tools is marked "sensitive"
   (`delete_user`, `grant_admin` in this repo) and is *always* denied to the
   autonomous agent, even if it's also present on the allow-list. This models a hard
   ceiling that a config mistake in the allow-list can't accidentally punch through —
   raising it requires a human to change the code, not a prompt.
3. **Short-lived, scoped credentials** — every tool call must present a
   [`CredentialLease`](../src/Safeguards/Permissions/CredentialLease.cs) issued by a
   `CredentialBroker`. Leases are scoped to exactly one tool and expire quickly (a
   few seconds in the demo; minutes in a real system). Calling with no lease, an
   expired lease, or a lease scoped to the wrong tool is denied. This is
   "least privilege" and "short-lived credentials" made concrete: the agent never
   holds a long-lived, broadly-scoped API key it could misuse or leak.

## See it in code

```csharp
var gate = new PermissionGate(
    allowList: new[] { "lookup_ticket", "reset_password", "delete_user" },
    sensitiveCeiling: new[] { "delete_user" });

var broker = new CredentialBroker(defaultLifetime: TimeSpan.FromSeconds(2));
var lease = broker.IssueLease("reset_password");

var decision = gate.TryAuthorize("reset_password", lease); // Allowed
var denied = gate.TryAuthorize("delete_user", broker.IssueLease("delete_user")); // DeniedByCeiling
```

Run the runnable demo to see safe calls succeed, a sensitive call get denied by the
ceiling, an unlisted tool get denied by default, and an expired lease get rejected:

```bash
dotnet run --project samples/PermissionEscalationDemo
```

## Production notes

- Back `CredentialBroker` with a real short-lived credential mechanism (cloud IAM
  temporary tokens, a Vault/Key Vault dynamic secret, a signed JWT with a short
  `exp`) instead of the in-memory stand-in used here.
- Log every `PermissionDecision` — especially denials — so you have an audit trail of
  every escalation attempt, successful or not.
- Keep the permission ceiling list under a *different* review/approval process than
  the general allow-list, so a single PR can't quietly widen both at once.
- Consider requiring human-in-the-loop confirmation (not just denial) for ceiling
  tools, so there's still a path to perform them when genuinely necessary.
