namespace Safeguards.ContextManagement;

/// <summary>Who "said" a turn — used only for display formatting in the demo.</summary>
public enum TurnRole
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary>
/// One turn in the conversation. <see cref="IsPinned"/> marks turns that must never
/// be summarized away or dropped, no matter how long the conversation grows — this is
/// how the ContextManager keeps critical "write context" (e.g. "the customer is a
/// Tier-1 VIP, do NOT auto-close this ticket") alive across a long session.
/// </summary>
public record ConversationTurn(TurnRole Role, string Text, bool IsPinned = false)
{
    /// <summary>
    /// Rough token estimate. Real Semantic Kernel usage would read this from the
    /// model's usage metadata; here we approximate ~4 characters per token so the
    /// demo can run deterministically without a live LLM call.
    /// </summary>
    public int EstimatedTokens => Math.Max(1, Text.Length / 4);
}
