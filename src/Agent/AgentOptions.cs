namespace Agent;

/// <summary>
/// Configuration for the model backing the triage agent. Values are read from
/// environment variables (never hardcoded) so no secrets end up in source control.
///
/// Defaults to a small/cheap model ("gpt-4o-mini") because the triage task here is
/// simple — pairing a cheap model with the LoopControl budget in
/// src/Safeguards/LoopControl keeps worst-case cost bounded even if something misbehaves.
///
/// To swap in a different model, either set the AGENT_MODEL_ID env var, or change the
/// default below — e.g. `ModelId = "gpt-4o"` for a more capable (and more expensive) model.
/// </summary>
public sealed class AgentOptions
{
    /// <summary>Chat completion model id/deployment name. Small + cheap by default.</summary>
    public string ModelId { get; init; } =
        Environment.GetEnvironmentVariable("AGENT_MODEL_ID") ?? "gpt-4o-mini";

    /// <summary>OpenAI-compatible API key. Left null/empty when running the mock-only demos.</summary>
    public string? ApiKey { get; init; } =
        Environment.GetEnvironmentVariable("AGENT_API_KEY");

    /// <summary>Optional custom endpoint (e.g. Azure OpenAI). Null = default OpenAI endpoint.</summary>
    public string? Endpoint { get; init; } =
        Environment.GetEnvironmentVariable("AGENT_ENDPOINT");

    /// <summary>True when there's no API key configured, meaning only mock/offline demos can run.</summary>
    public bool HasLiveCredentials => !string.IsNullOrWhiteSpace(ApiKey);
}
