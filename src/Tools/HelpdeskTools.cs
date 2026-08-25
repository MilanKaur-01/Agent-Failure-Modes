using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Tools;

/// <summary>
/// Mock helpdesk tools used by the triage agent.
///
/// IMPORTANT (teaching note): every method here is a stub. None of them talk to a
/// real ticketing system, identity provider, or database. That is deliberate — the
/// whole point of this repository is the *safeguards*, not the tools. Keeping the
/// tools trivial means the reader's attention stays on:
///   - src/Safeguards/LoopControl
///   - src/Safeguards/ContextManagement
///   - src/Safeguards/Permissions
///
/// The <see cref="KernelFunction"/> attributes let Semantic Kernel expose these
/// C# methods as callable "tools" for the LLM (function calling). The safeguard
/// layers (especially Permissions) sit *in front of* these calls so that even a
/// tool the model is technically capable of calling can be denied.
/// </summary>
public class HelpdeskTools
{
    /// <summary>Safe, read-only tool: look up a mock ticket by id.</summary>
    [KernelFunction("lookup_ticket")]
    [Description("Looks up the status of a helpdesk ticket by its id.")]
    public string LookupTicket([Description("The ticket id, e.g. TCK-1042")] string id)
        => $"Ticket {id}: status=OPEN, user=alice, issue=password reset";

    /// <summary>Safe, low-risk tool: send a mock password reset link.</summary>
    [KernelFunction("reset_password")]
    [Description("Sends a mock password reset link to the given user.")]
    public string ResetPassword([Description("The username to reset, e.g. alice")] string user)
        => $"Password reset link sent to {user}. (mock)";

    /// <summary>Safe tool: hand the ticket off to a human agent.</summary>
    [KernelFunction("escalate_to_human")]
    [Description("Escalates the current ticket to an on-call human support engineer.")]
    public string EscalateToHuman([Description("Why this needs a human")] string reason)
        => $"ESCALATED to on-call human. Reason: {reason}";

    /// <summary>
    /// SENSITIVE tool, intentionally included so the Permissions safeguard has
    /// something dangerous to deny. A real triage agent should never be able to
    /// delete a user account on its own initiative — see docs/03-permission-escalation.md.
    /// </summary>
    [KernelFunction("delete_user")]
    [Description("DANGEROUS: permanently deletes a user account. Should never be reachable without human approval.")]
    public string DeleteUser([Description("The username to delete")] string user)
        => $"(mock) User {user} deleted.";

    /// <summary>
    /// SENSITIVE tool, intentionally included so the Permissions safeguard has
    /// something dangerous to deny. Granting admin rights is exactly the kind of
    /// privilege escalation a permission ceiling exists to stop.
    /// </summary>
    [KernelFunction("grant_admin")]
    [Description("DANGEROUS: grants admin rights to a user. Should never be reachable without human approval.")]
    public string GrantAdmin([Description("The username to promote")] string user)
        => $"(mock) User {user} granted admin rights.";
}
