using TickIt.Core;

namespace TickIt.Agent.Actions;

/// <summary>Turns a tier decision + classification into concrete proposed actions.</summary>
public sealed class ActionProposer
{
    public IReadOnlyList<ProposedAction> Propose(Ticket ticket, ClassificationResult classification, TierDecision decision)
    {
        return decision.Tier switch
        {
            AutonomyTier.Observe => Array.Empty<ProposedAction>(),

            AutonomyTier.NotifyAndSuggest => new[]
            {
                new ProposedAction
                {
                    TicketId = ticket.Id,
                    Type = ActionType.NotifyTeamsChannel,
                    Description = $"Notify support channel: '{classification.Summary}' - suggested next step: {classification.SuggestedNextStep}",
                    Tier = decision.Tier,
                    Payload = new Dictionary<string, string>
                    {
                        ["ticketNumber"] = ticket.Number,
                        ["category"] = classification.Category,
                        ["confidence"] = classification.ConfidenceScore.ToString("P0")
                    }
                }
            },

            AutonomyTier.AutoActionLowRisk => ProposeLowRiskActions(ticket, classification, decision),

            // Tier 3 (AutoResolve) is out of scope - TierPolicy never returns it, but guard anyway.
            _ => throw new NotSupportedException($"Autonomy tier '{decision.Tier}' has no defined action proposal.")
        };
    }

    private static IReadOnlyList<ProposedAction> ProposeLowRiskActions(
        Ticket ticket, ClassificationResult classification, TierDecision decision)
    {
        var actions = new List<ProposedAction>
        {
            new()
            {
                TicketId = ticket.Id,
                Type = ActionType.ApplyCategoryTag,
                Description = $"Apply category tag '{classification.Category}' to ticket {ticket.Number}.",
                Tier = decision.Tier,
                Payload = new Dictionary<string, string> { ["category"] = classification.Category }
            }
        };

        if (classification.Category == "duplicate")
        {
            actions.Add(new ProposedAction
            {
                TicketId = ticket.Id,
                Type = ActionType.ProposeDuplicateClose,
                Description = $"Propose closing ticket {ticket.Number} as a duplicate, pending human confirmation.",
                Tier = decision.Tier
            });
        }

        if (classification.MatchedKnowledge.Count > 0)
        {
            var best = classification.MatchedKnowledge.OrderByDescending(k => k.RelevanceScore).First();
            actions.Add(new ProposedAction
            {
                TicketId = ticket.Id,
                Type = ActionType.LinkJiraIssue,
                Description = $"Link ticket {ticket.Number} to {best.Key} ({best.Title}).",
                Tier = decision.Tier,
                Payload = new Dictionary<string, string> { ["jiraKey"] = best.Key, ["jiraUrl"] = best.Url ?? "" }
            });
        }

        return actions;
    }
}
