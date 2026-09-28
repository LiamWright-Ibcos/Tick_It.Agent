using TickIt.Agent.Actions;
using TickIt.Core;
using TickIt.Tests.Fakes;

namespace TickIt.Tests;

public class ActionProposerTests
{
    private readonly ActionProposer _proposer = new();

    [Fact]
    public void Observe_ProposesNoActions()
    {
        var ticket = TicketFactory.Create();
        var classification = TicketFactory.Classification();
        var decision = new TierDecision { Tier = AutonomyTier.Observe, Reason = "test", RequiresHumanApproval = false };

        var actions = _proposer.Propose(ticket, classification, decision);

        Assert.Empty(actions);
    }

    [Fact]
    public void NotifyAndSuggest_ProposesExactlyOneNotifyAction()
    {
        var ticket = TicketFactory.Create();
        var classification = TicketFactory.Classification();
        var decision = new TierDecision { Tier = AutonomyTier.NotifyAndSuggest, Reason = "test", RequiresHumanApproval = false };

        var actions = _proposer.Propose(ticket, classification, decision);

        Assert.Single(actions);
        Assert.Equal(ActionType.NotifyTeamsChannel, actions[0].Type);
    }

    [Fact]
    public void AutoActionLowRisk_Duplicate_ProposesTagAndCloseActions()
    {
        var ticket = TicketFactory.Create();
        var classification = TicketFactory.Classification(category: "duplicate");
        var decision = new TierDecision { Tier = AutonomyTier.AutoActionLowRisk, Reason = "test", RequiresHumanApproval = true };

        var actions = _proposer.Propose(ticket, classification, decision);

        Assert.Contains(actions, a => a.Type == ActionType.ApplyCategoryTag);
        Assert.Contains(actions, a => a.Type == ActionType.ProposeDuplicateClose);
    }

    [Fact]
    public void AutoActionLowRisk_WithKnowledge_ProposesLinkJiraIssue()
    {
        var ticket = TicketFactory.Create();
        var knowledge = new[]
        {
            new KnowledgeReference { Key = "GOLD-1", Title = "T", RelevanceScore = 0.9 }
        };
        var classification = TicketFactory.Classification(category: "known-issue-match", knowledge: knowledge);
        var decision = new TierDecision { Tier = AutonomyTier.AutoActionLowRisk, Reason = "test", RequiresHumanApproval = true };

        var actions = _proposer.Propose(ticket, classification, decision);

        Assert.Contains(actions, a => a.Type == ActionType.LinkJiraIssue && a.Payload["jiraKey"] == "GOLD-1");
    }

    [Fact]
    public void AutoResolve_IsNotSupported()
    {
        var ticket = TicketFactory.Create();
        var classification = TicketFactory.Classification();
        var decision = new TierDecision { Tier = AutonomyTier.AutoResolve, Reason = "test", RequiresHumanApproval = true };

        Assert.Throws<NotSupportedException>(() => _proposer.Propose(ticket, classification, decision));
    }
}
