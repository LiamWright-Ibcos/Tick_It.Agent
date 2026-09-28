using TickIt.Core;
using TickIt.Tests.Fakes;

namespace TickIt.Tests;

public class TierPolicyTests
{
    private readonly TierPolicy _policy = new();

    [Fact]
    public void LowConfidence_StaysAtObserve()
    {
        var ticket = TicketFactory.Create(priority: TicketPriority.P3);
        var classification = TicketFactory.Classification(confidence: 0.2);

        var decision = _policy.Evaluate(ticket, classification);

        Assert.Equal(AutonomyTier.Observe, decision.Tier);
        Assert.False(decision.RequiresHumanApproval);
    }

    [Fact]
    public void MidConfidence_ReachesNotifyAndSuggest()
    {
        var ticket = TicketFactory.Create(priority: TicketPriority.P3);
        var classification = TicketFactory.Classification(category: "needs-triage", confidence: 0.6);

        var decision = _policy.Evaluate(ticket, classification);

        Assert.Equal(AutonomyTier.NotifyAndSuggest, decision.Tier);
    }

    [Fact]
    public void HighConfidence_LowRiskCategory_LowPriority_ReachesAutoActionLowRisk()
    {
        var ticket = TicketFactory.Create(priority: TicketPriority.P4);
        var classification = TicketFactory.Classification(category: "password-reset", confidence: 0.9);

        var decision = _policy.Evaluate(ticket, classification);

        Assert.Equal(AutonomyTier.AutoActionLowRisk, decision.Tier);
        Assert.True(decision.RequiresHumanApproval, "Tier 2 decisions must always require human approval in this build.");
    }

    [Theory]
    [InlineData(TicketPriority.P1)]
    [InlineData(TicketPriority.P2)]
    public void HighConfidence_LowRiskCategory_ButHighPriority_CapsAtNotifyAndSuggest(TicketPriority priority)
    {
        var ticket = TicketFactory.Create(priority: priority);
        var classification = TicketFactory.Classification(category: "password-reset", confidence: 0.9);

        var decision = _policy.Evaluate(ticket, classification);

        Assert.Equal(AutonomyTier.NotifyAndSuggest, decision.Tier);
    }

    [Fact]
    public void HighConfidence_ButIneligibleCategory_CapsAtNotifyAndSuggest()
    {
        var ticket = TicketFactory.Create(priority: TicketPriority.P3);
        var classification = TicketFactory.Classification(category: "billing-discrepancy", confidence: 0.95);

        var decision = _policy.Evaluate(ticket, classification);

        Assert.Equal(AutonomyTier.NotifyAndSuggest, decision.Tier);
    }

    [Fact]
    public void NeverReturnsAutoResolve()
    {
        // AutoResolve (Tier 3) is explicitly out of scope for Phase 1 - the policy must never produce it,
        // regardless of how confidence/category/priority inputs are varied.
        var priorities = Enum.GetValues<TicketPriority>();
        var categories = new[] { "password-reset", "duplicate", "how-to-question", "known-issue-match", "billing-discrepancy", "needs-triage" };

        foreach (var priority in priorities)
        {
            foreach (var category in categories)
            {
                for (var confidence = 0.0; confidence <= 1.0; confidence += 0.1)
                {
                    var ticket = TicketFactory.Create(priority: priority);
                    var classification = TicketFactory.Classification(category: category, confidence: confidence);
                    var decision = _policy.Evaluate(ticket, classification);
                    Assert.NotEqual(AutonomyTier.AutoResolve, decision.Tier);
                }
            }
        }
    }
}
