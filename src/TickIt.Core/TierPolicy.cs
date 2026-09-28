namespace TickIt.Core;

public sealed record TierPolicyOptions
{
    /// <summary>Minimum confidence to qualify for Tier 1 (notify-and-suggest). Default 0.55.</summary>
    public double Tier1ConfidenceThreshold { get; init; } = 0.55;

    /// <summary>Minimum confidence to qualify for Tier 2 (auto-action-low-risk). Default 0.85.</summary>
    public double Tier2ConfidenceThreshold { get; init; } = 0.85;

    /// <summary>
    /// Ticket categories judged low-risk enough to ever be a Tier 2 candidate. Kept short and
    /// explicit deliberately — this is a policy decision, not something the model should infer
    /// on the fly. Category taxonomy itself is still provisional pending the real GOLD
    /// known-issue content structure (see Draft 2 findings, "still open").
    /// </summary>
    public IReadOnlySet<string> Tier2EligibleCategories { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "known-issue-match",
        "duplicate",
        "password-reset",
        "how-to-question"
    };

    /// <summary>P1/P2 tickets never qualify for Tier 2, regardless of confidence — always at most Tier 1.</summary>
    public bool ExcludeHighPriorityFromTier2 { get; init; } = true;
}

/// <summary>
/// Maps a classification result to an autonomy tier decision. This is the piece of logic
/// the PoC never needed, because it hard-capped itself at Tier 1 — see
/// first-line-support-poc-architecture.md. Whether a Tier 2 decision is ever actually
/// *executed* (as opposed to just decided) is a separate, stricter gate — see
/// IActionExecutor / DryRunActionExecutor. In this build it never is.
/// </summary>
public sealed class TierPolicy
{
    private readonly TierPolicyOptions _options;

    public TierPolicy(TierPolicyOptions? options = null)
    {
        _options = options ?? new TierPolicyOptions();
    }

    public TierDecision Evaluate(Ticket ticket, ClassificationResult classification)
    {
        if (classification.ConfidenceScore < _options.Tier1ConfidenceThreshold)
        {
            return new TierDecision
            {
                Tier = AutonomyTier.Observe,
                Reason = $"Confidence {classification.ConfidenceScore:P0} below Tier 1 threshold " +
                         $"({_options.Tier1ConfidenceThreshold:P0}); observing only.",
                RequiresHumanApproval = false
            };
        }

        var isHighPriority = ticket.Priority is TicketPriority.P1 or TicketPriority.P2;
        var isTier2EligibleCategory = _options.Tier2EligibleCategories.Contains(classification.Category);
        var meetsTier2Confidence = classification.ConfidenceScore >= _options.Tier2ConfidenceThreshold;

        if (meetsTier2Confidence && isTier2EligibleCategory && !(isHighPriority && _options.ExcludeHighPriorityFromTier2))
        {
            return new TierDecision
            {
                Tier = AutonomyTier.AutoActionLowRisk,
                Reason = $"Confidence {classification.ConfidenceScore:P0} meets Tier 2 threshold " +
                         $"({_options.Tier2ConfidenceThreshold:P0}) for low-risk category '{classification.Category}'.",
                RequiresHumanApproval = true // always true in this build; see DryRunActionExecutor
            };
        }

        var reason = meetsTier2Confidence
            ? $"Confidence meets Tier 2 threshold but category '{classification.Category}' or priority " +
              $"({ticket.Priority}) is excluded from auto-action; capping at Tier 1."
            : $"Confidence {classification.ConfidenceScore:P0} meets Tier 1 threshold but not Tier 2 " +
              $"({_options.Tier2ConfidenceThreshold:P0}).";

        return new TierDecision
        {
            Tier = AutonomyTier.NotifyAndSuggest,
            Reason = reason,
            RequiresHumanApproval = false
        };
    }
}
