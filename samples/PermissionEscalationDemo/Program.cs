using Safeguards.Permissions;

Console.WriteLine("=====================================================================");
Console.WriteLine(" SAFEGUARD #3 DEMO — Permission Escalation (ceiling + short leases)");
Console.WriteLine("=====================================================================");
Console.WriteLine();
Console.WriteLine("Scenario: the triage agent is manipulated (or hallucinates) into trying");
Console.WriteLine("to delete a user and grant admin rights, on top of its normal, safe");
Console.WriteLine("tool calls. Watch the PermissionGate allow the safe calls and deny the");
Console.WriteLine("dangerous ones — even though nothing here talks to a real system.");
Console.WriteLine();

// Deny-by-default allow-list: only these tools may ever be called.
var allowList = new[] { "lookup_ticket", "reset_password", "escalate_to_human", "delete_user", "grant_admin" };

// Permission ceiling: these are on the allow-list (so a config typo doesn't silently
// block them from even being considered) but are ALWAYS denied to the autonomous
// agent — a human must perform them out-of-band.
var sensitiveCeiling = new[] { "delete_user", "grant_admin" };

var gate = new PermissionGate(allowList, sensitiveCeiling);

// Short-lived, scoped credential broker — leases expire in 2 seconds for this demo.
var broker = new CredentialBroker(defaultLifetime: TimeSpan.FromSeconds(2));

void TryCall(string tool, CredentialLease? lease)
{
    Console.WriteLine($"--- Agent attempts to call: {tool} ---");
    var decision = gate.TryAuthorize(tool, lease);
    Console.WriteLine(decision.Message);
    Console.WriteLine(decision.IsAllowed ? "RESULT: ✅ ALLOWED" : $"RESULT: ❌ DENIED ({decision.Result})");
    Console.WriteLine();
}

// 1) Normal, safe tool call with a valid, correctly-scoped lease.
var lookupLease = broker.IssueLease("lookup_ticket");
TryCall("lookup_ticket", lookupLease);

// 2) Another safe tool call, own lease.
var resetLease = broker.IssueLease("reset_password");
TryCall("reset_password", resetLease);

// 3) Sensitive tool call — denied by the permission ceiling, even though it's on
//    the allow-list and even if a (mis-scoped) lease is supplied.
var deleteLease = broker.IssueLease("delete_user");
TryCall("delete_user", deleteLease);

// 4) Another sensitive tool call — same story.
var adminLease = broker.IssueLease("grant_admin");
TryCall("grant_admin", adminLease);

// 5) A tool that was never on the allow-list at all.
TryCall("read_payroll_database", null);

// 6) Expired credential: wait past the lease expiry, then try to reuse it.
Console.WriteLine("--- Waiting for the reset_password lease to expire... ---");
await Task.Delay(TimeSpan.FromSeconds(2.2));
TryCall("reset_password", resetLease);

Console.WriteLine("=====================================================================");
Console.WriteLine("Summary: safe tools were allowed while a live lease covered them;");
Console.WriteLine("sensitive tools were denied outright by the ceiling; unlisted tools");
Console.WriteLine("were denied by the deny-by-default allow-list; and reusing an expired");
Console.WriteLine("lease was rejected. This is least-privilege + credential leasing in code.");
