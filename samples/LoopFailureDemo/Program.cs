using Safeguards.LoopControl;

Console.WriteLine("=====================================================================");
Console.WriteLine(" SAFEGUARD #1 DEMO — Task Loop Failure (budget + stall detection)");
Console.WriteLine("=====================================================================");
Console.WriteLine();
Console.WriteLine("Scenario: the triage agent keeps trying to resolve a ticket it cannot");
Console.WriteLine("actually fix. Without a safeguard, it would call the same tool forever.");
Console.WriteLine("Watch the LoopGuard count down the budget and eventually escalate.");
Console.WriteLine();

// A small, cheap budget on purpose, so the demo trips the safeguard quickly.
var budget = new LoopBudget
{
    MaxIterations = 4,
    MaxTokenBudget = 300,
    MaxDuration = TimeSpan.FromSeconds(10),
    StallThreshold = 2
};

var guard = new LoopGuard(budget);

// Scripted (deterministic, offline) "agent steps" standing in for what a real
// Semantic Kernel agent loop would produce turn by turn. The agent keeps calling
// LookupTicket on the same ticket id — a classic stuck-in-a-loop pattern.
var scriptedSteps = new[]
{
    ("lookup_ticket(TCK-1042)", 80),
    ("lookup_ticket(TCK-1042)", 80),
    ("lookup_ticket(TCK-1042)", 80),
    ("lookup_ticket(TCK-1042)", 80),
    ("lookup_ticket(TCK-1042)", 80),
};

foreach (var (signature, tokens) in scriptedSteps)
{
    Console.WriteLine($"--- Agent step: calling {signature} ---");
    var evaluation = guard.Evaluate(signature, tokens);
    Console.WriteLine(evaluation.Message);

    if (evaluation.Decision != LoopDecision.Continue)
    {
        Console.WriteLine();
        Console.WriteLine($"FINAL DECISION: {evaluation.Decision} (reason: {evaluation.Reason})");
        Console.WriteLine();
        if (evaluation.Decision == LoopDecision.EscalateToHuman)
        {
            Console.WriteLine("The agent stops here and hands the ticket to a human instead of");
            Console.WriteLine("retrying forever. This is the entire point of SAFEGUARD #1.");
        }

        return;
    }

    Console.WriteLine();
}

Console.WriteLine("(The scripted steps ran out before the guard tripped — try lowering");
Console.WriteLine(" LoopBudget.MaxIterations to see the escalation sooner.)");
