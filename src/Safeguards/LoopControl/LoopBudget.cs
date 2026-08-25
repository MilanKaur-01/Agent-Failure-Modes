namespace Safeguards.LoopControl;

/// <summary>
/// Configuration for <see cref="LoopGuard"/>. Keeping these as simple numbers (rather
/// than something clever) is deliberate: the whole safeguard is "count down a budget,
/// and stop when it hits zero, or when nothing is changing".
/// </summary>
public sealed class LoopBudget
{
    /// <summary>Hard cap on the number of agent reasoning iterations, regardless of anything else.</summary>
    public int MaxIterations { get; init; } = 5;

    /// <summary>
    /// Optional token budget. In a real Semantic Kernel agent you would decrement this by
    /// the tokens actually reported on each completion; here it's modeled as a simple
    /// integer so the demo can run without a live LLM call.
    /// </summary>
    public int MaxTokenBudget { get; init; } = 4000;

    /// <summary>Optional wall-clock budget for the whole task.</summary>
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How many consecutive iterations may repeat the exact same tool call / state
    /// before we conclude the agent is stalled (looping without making progress).
    /// </summary>
    public int StallThreshold { get; init; } = 2;
}
