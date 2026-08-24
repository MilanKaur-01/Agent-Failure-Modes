using Microsoft.SemanticKernel;
using Tools;

namespace Agent;

/// <summary>
/// Deliberately trivial helpdesk triage agent.
///
/// This class exists only to give the three safeguards something realistic to wrap
/// around; it intentionally does NOT contain any interesting logic itself. Building
/// the Semantic Kernel <see cref="Kernel"/> with the mock <see cref="HelpdeskTools"/>
/// plugin is the entire "agent" — everything that makes this repo worth reading is in
/// src/Safeguards/*.
///
/// When <see cref="AgentOptions.HasLiveCredentials"/> is false (no API key configured),
/// the samples run in "offline/mock" mode so a reader can see every safeguard fire
/// without needing an API key. See docs/*.md and the sample Program.cs files for how
/// a live model would be wired in via AddOpenAIChatCompletion / AddAzureOpenAIChatCompletion.
/// </summary>
public static class TriageAgentFactory
{
    /// <summary>
    /// Builds a Semantic Kernel instance with the mock helpdesk tools registered as a
    /// plugin. Only call this when <see cref="AgentOptions.HasLiveCredentials"/> is
    /// true — the safeguard demos otherwise run fully offline.
    /// </summary>
    public static Kernel BuildKernel(AgentOptions options)
    {
        if (!options.HasLiveCredentials)
        {
            throw new InvalidOperationException(
                "No live model credentials configured (AGENT_API_KEY is empty). " +
                "The safeguard samples in this repo are designed to run without a live " +
                "LLM call — see README.md for how to plug in a real model.");
        }

        var builder = Kernel.CreateBuilder();

        // Swap the model by changing AgentOptions.ModelId (env var AGENT_MODEL_ID),
        // e.g. "gpt-4o-mini" (default, cheap) vs. "gpt-4o" (more capable, pricier).
        if (options.Endpoint is not null)
        {
            builder.AddOpenAIChatCompletion(
                modelId: options.ModelId,
                apiKey: options.ApiKey!,
                endpoint: new Uri(options.Endpoint));
        }
        else
        {
            builder.AddOpenAIChatCompletion(
                modelId: options.ModelId,
                apiKey: options.ApiKey!);
        }

        builder.Plugins.AddFromType<HelpdeskTools>("Helpdesk");

        return builder.Build();
    }

    /// <summary>The system prompt for the triage agent — kept intentionally short.</summary>
    public const string SystemPrompt =
        "You are a helpdesk triage assistant. Use the available tools to look up " +
        "tickets, reset passwords, and escalate to a human when you cannot resolve " +
        "the issue yourself. Never attempt to delete users or grant admin rights.";
}
