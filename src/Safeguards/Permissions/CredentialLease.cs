namespace Safeguards.Permissions;

/// <summary>
/// A short-lived, scoped credential — the code equivalent of a cloud IAM temporary
/// token. Modeling this explicitly (rather than a single long-lived API key) is the
/// "short-lived credentials" half of SAFEGUARD #3.
/// </summary>
public sealed class CredentialLease
{
    public string Scope { get; }
    public DateTimeOffset IssuedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    public CredentialLease(string scope, TimeSpan lifetime)
    {
        Scope = scope;
        IssuedAt = DateTimeOffset.UtcNow;
        ExpiresAt = IssuedAt + lifetime;
    }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    /// <summary>Whether this lease's scope covers the requested tool/action.</summary>
    public bool CoversScope(string requestedScope) =>
        string.Equals(Scope, requestedScope, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Issues <see cref="CredentialLease"/>s. Mirrors a real credential broker (e.g. an
/// STS or Key Vault) but entirely in-memory for the demo. Every lease is scoped to a
/// single purpose and expires quickly — the agent can never accumulate standing,
/// long-lived access.
/// </summary>
public sealed class CredentialBroker
{
    private readonly TimeSpan _defaultLifetime;

    public CredentialBroker(TimeSpan? defaultLifetime = null)
    {
        _defaultLifetime = defaultLifetime ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>Issue a new lease scoped to <paramref name="scope"/>, valid for <paramref name="lifetime"/> (or the broker default).</summary>
    public CredentialLease IssueLease(string scope, TimeSpan? lifetime = null)
        => new(scope, lifetime ?? _defaultLifetime);
}
