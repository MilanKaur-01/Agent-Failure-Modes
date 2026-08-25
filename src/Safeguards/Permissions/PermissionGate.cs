namespace Safeguards.Permissions;

/// <summary>
/// SAFEGUARD #3 — PERMISSION ESCALATION
///
/// Even if the LLM "decides" to call a dangerous tool (because a user asked, or it
/// hallucinated that it should), the agent process itself must refuse to execute it
/// unless every one of the following holds:
///
///   1. DENY-BY-DEFAULT ALLOW-LIST — the tool name must be explicitly allow-listed.
///      Anything not on the list is denied, full stop.
///   2. PERMISSION CEILING — a *separate*, smaller set of tools is marked "sensitive"
///      and is denied even if it happens to be on the allow-list. This models the
///      idea that some actions (delete a user, grant admin) should never be reachable
///      by an autonomous agent, no matter what allow-list configuration mistakes happen.
///   3. SHORT-LIVED SCOPED CREDENTIALS — calling a tool requires a live
///      <see cref="CredentialLease"/> whose scope matches the tool and which has not
///      expired. Leases are cheap to issue but expire quickly, so a compromised or
///      confused agent can't reuse an old credential indefinitely.
///
/// This class is intentionally the single choke point all tool calls must pass
/// through — that's what makes it a genuine safeguard rather than a suggestion.
/// </summary>
public sealed class PermissionGate
{
    private readonly HashSet<string> _allowList;
    private readonly HashSet<string> _sensitiveCeiling;

    /// <param name="allowList">Tools the agent is permitted to call at all (deny-by-default for anything else).</param>
    /// <param name="sensitiveCeiling">
    /// Tools that are always denied to the autonomous agent, even if present on the
    /// allow-list — the permission ceiling a human must manually raise.
    /// </param>
    public PermissionGate(IEnumerable<string> allowList, IEnumerable<string> sensitiveCeiling)
    {
        _allowList = new HashSet<string>(allowList, StringComparer.OrdinalIgnoreCase);
        _sensitiveCeiling = new HashSet<string>(sensitiveCeiling, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Decides whether a tool call may proceed. Checks the allow-list, then the
    /// permission ceiling, then the credential lease (existence, expiry, and scope).
    /// </summary>
    public PermissionDecision TryAuthorize(string toolName, CredentialLease? lease)
    {
        if (!_allowList.Contains(toolName))
        {
            return new PermissionDecision(
                PermissionResult.DeniedNotOnAllowList,
                $"[PermissionGate] '{toolName}' is not on the allow-list. Denied by default.");
        }

        if (_sensitiveCeiling.Contains(toolName))
        {
            return new PermissionDecision(
                PermissionResult.DeniedByCeiling,
                $"[PermissionGate] '{toolName}' is above the permission ceiling for an autonomous agent. " +
                "Denied — this action requires explicit human approval.");
        }

        if (lease is null)
        {
            return new PermissionDecision(
                PermissionResult.DeniedExpiredCredential,
                $"[PermissionGate] No credential lease supplied for '{toolName}'. Denied.");
        }

        if (lease.IsExpired)
        {
            return new PermissionDecision(
                PermissionResult.DeniedExpiredCredential,
                $"[PermissionGate] Credential lease for '{toolName}' expired at {lease.ExpiresAt:O} " +
                $"(now {DateTimeOffset.UtcNow:O}). Denied.");
        }

        if (!lease.CoversScope(toolName))
        {
            return new PermissionDecision(
                PermissionResult.DeniedScopeMismatch,
                $"[PermissionGate] Credential lease is scoped to '{lease.Scope}', not '{toolName}'. Denied.");
        }

        return new PermissionDecision(
            PermissionResult.Allowed,
            $"[PermissionGate] '{toolName}' authorized under lease scoped to '{lease.Scope}' " +
            $"(expires {lease.ExpiresAt:O}).");
    }
}
