using Microsoft.Extensions.Logging;
using TickIt.Core;

namespace TickIt.Agent.Actions;

/// <summary>
/// The only IActionExecutor registered in this build, regardless of autonomy tier. Every
/// proposed action - Tier 1 notify-and-suggest or Tier 2 auto-action-low-risk alike - is
/// logged, never actually sent/applied. This is a deliberate, hard-coded gate, not a config
/// toggle: it stays this way until the Team Ada / Support alignment question is resolved and
/// the Anthropic/Atlassian/Team Support credentials in the provisioning checklist land -
/// same posture as the Node PoC's dry-run log for Tier 1's "notify" and "link Jira issue"
/// actions, just extended to cover Tier 2 as well.
/// </summary>
public sealed class DryRunActionExecutor : IActionExecutor
{
    private readonly ILogger<DryRunActionExecutor> _logger;

    public DryRunActionExecutor(ILogger<DryRunActionExecutor> logger)
    {
        _logger = logger;
    }

    public Task<ActionExecutionResult> ExecuteAsync(ProposedAction action, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[DRY RUN] Would execute {ActionType} for ticket {TicketId} (tier {Tier}): {Description}",
            action.Type, action.TicketId, action.Tier, action.Description);

        var result = new ActionExecutionResult
        {
            Action = action,
            Mode = ActionExecutionMode.DryRun,
            Executed = false,
            Outcome = "Logged only - no live system was called. See provisioning checklist for what's outstanding."
        };

        return Task.FromResult(result);
    }
}
