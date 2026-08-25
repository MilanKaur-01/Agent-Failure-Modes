namespace Safeguards.Permissions;

/// <summary>Result of a permission check — always explicit, never implicit.</summary>
public enum PermissionResult
{
    Allowed,
    DeniedNotOnAllowList,
    DeniedByCeiling,
    DeniedExpiredCredential,
    DeniedScopeMismatch
}

/// <summary>Outcome returned by <see cref="PermissionGate.TryAuthorize"/>.</summary>
public record PermissionDecision(PermissionResult Result, string Message)
{
    public bool IsAllowed => Result == PermissionResult.Allowed;
}
