using Safeguards.ContextManagement;

Console.WriteLine("=====================================================================");
Console.WriteLine(" SAFEGUARD #2 DEMO — Context Drop (pinning + rolling summarization)");
Console.WriteLine("=====================================================================");
Console.WriteLine();
Console.WriteLine("Scenario: a long back-and-forth support thread grows past the token");
Console.WriteLine("budget. Naive truncation would drop the oldest turn — which happens to");
Console.WriteLine("hold a critical instruction. Watch the ContextManager summarize the");
Console.WriteLine("middle of the conversation while keeping that instruction pinned.");
Console.WriteLine();

// Small budget on purpose, so the demo overflows quickly.
var context = new ContextManager(tokenBudget: 120, recentTurnsToKeepVerbatim: 2);

// The critical "must-do" write context — this must survive no matter how long the
// conversation grows. Pinned turns are never summarized or dropped.
context.PinFact(TurnRole.System,
    "SYSTEM/CRITICAL: This user is a Tier-1 VIP. Do NOT auto-close this ticket " +
    "under any circumstances — always escalate unresolved issues to a human.");

var chattyTurns = new (TurnRole role, string text)[]
{
    (TurnRole.User, "Hi, I can't log into my account again, same issue as last week."),
    (TurnRole.Assistant, "Sure, let me look up your ticket history and password status."),
    (TurnRole.Tool, "lookup_ticket(TCK-9001) -> status=OPEN, user=vip_carol, issue=login"),
    (TurnRole.User, "I already tried resetting my password twice, it still doesn't work."),
    (TurnRole.Assistant, "Understood, let's check if the reset link actually reached your inbox."),
    (TurnRole.Tool, "reset_password(vip_carol) -> Password reset link sent to vip_carol. (mock)"),
    (TurnRole.User, "Still nothing. This is getting frustrating, I've been locked out for days."),
    (TurnRole.Assistant, "I'm sorry about the delay — let's try one more diagnostic step."),
    (TurnRole.Tool, "lookup_ticket(TCK-9001) -> status=OPEN, user=vip_carol, issue=login"),
    (TurnRole.User, "Can we just fix this today please?"),
};

Console.WriteLine($"Pinned fact: \"{context.Turns[0].Text}\"");
Console.WriteLine();

foreach (var (role, text) in chattyTurns)
{
    context.AddTurn(role, text);
    Console.WriteLine($"[{role}] {text}");
    Console.WriteLine(context.EnforceBudget());
    Console.WriteLine();
}

Console.WriteLine("=====================================================================");
Console.WriteLine("Final context window sent to the model:");
Console.WriteLine("=====================================================================");
foreach (var turn in context.Turns)
{
    var tag = turn.IsPinned ? "PINNED" : "turn";
    Console.WriteLine($"[{tag}/{turn.Role}] {turn.Text}");
}

Console.WriteLine();
var stillPresent = context.Turns.Any(t => t.IsPinned && t.Text.Contains("Tier-1 VIP"));
Console.WriteLine(stillPresent
    ? "✅ The critical VIP/escalation instruction survived the entire conversation."
    : "❌ The critical instruction was lost — this should never happen!");
