namespace TickIt.Core;

/// <summary>
/// Autonomy ladder for what the agent is allowed to do with a triage result, following on
/// from the playbook's tier model. The PoC only ever reached Tier 0/1. This build adds the
/// Tier 2 *decision* logic (recognising when a ticket is a good low-risk auto-action
/// candidate) as the Phase 1 next-roadmap-step work — but see <see cref="TierPolicy"/> and
/// the README: Tier 2/3 actions are still only ever dry-run/logged in this build, never
/// executed against a real system, pending the Team Ada alignment decision and the
/// Anthropic/Atlassian/Team Support credentials that are still outstanding.
/// </summary>
public enum AutonomyTier
{
    /// <summary>Observe-only: the agent reasons about the ticket but produces no visible output.</summary>
    Observe = 0,

    /// <summary>Notify-and-suggest: the agent would notify a human with its suggestion for them to action.</summary>
    NotifyAndSuggest = 1,

    /// <summary>
    /// Auto-action-low-risk: for narrowly-scoped, reversible actions on well-understood ticket
    /// categories, the agent would act automatically subject to a human override window.
    /// Not executed live in this build — see class remarks.
    /// </summary>
    AutoActionLowRisk = 2,

    /// <summary>
    /// Auto-resolve: fully autonomous resolution of a ticket end-to-end. Explicitly out of
    /// scope for Phase 1 — reserved here only so the enum models the full playbook ladder;
    /// nothing in this codebase implements or permits it.
    /// </summary>
    AutoResolve = 3
}

public sealed record TierDecision
{
    public required AutonomyTier Tier { get; init; }
    public required string Reason { get; init; }
    public required bool RequiresHumanApproval { get; init; }
}

public enum ActionType
{
    LogOnly,
    NotifyTeamsChannel,
    LinkJiraIssue,
    ApplyCategoryTag,
    SuggestKnownIssueLink,
    ProposeDuplicateClose
}

public sealed record ProposedAction
{
    public required string TicketId { get; init; }
    public required ActionType Type { get; init; }
    public required string Description { get; init; }
    public required AutonomyTier Tier { get; init; }
    public IReadOnlyDictionary<string, string> Payload { get; init; } = new Dictionary<string, string>();
}

public enum ActionExecutionMode
{
    DryRun,
    Live
}

public sealed record ActionExecutionResult
{
    public required ProposedAction Action { get; init; }
    public required ActionExecutionMode Mode { get; init; }
    public required bool Executed { get; init; }
    public required string Outcome { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
