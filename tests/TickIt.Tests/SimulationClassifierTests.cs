using TickIt.Agent.Classification;
using TickIt.Core;
using TickIt.Tests.Fakes;

namespace TickIt.Tests;

public class SimulationClassifierTests
{
    private readonly SimulationClassifier _classifier = new();

    [Fact]
    public async Task AlwaysStampsSimulationReasoningMode()
    {
        var ticket = TicketFactory.Create(description: "anything at all");
        var result = await _classifier.ClassifyAsync(ticket, Array.Empty<KnowledgeReference>());

        Assert.Equal(ReasoningMode.Simulation, result.ReasoningMode);
    }

    [Fact]
    public async Task PasswordKeywords_ClassifyAsPasswordReset()
    {
        var ticket = TicketFactory.Create(
            title: "Cannot log in",
            description: "User resets password and is stuck in a login loop.");

        var result = await _classifier.ClassifyAsync(ticket, Array.Empty<KnowledgeReference>());

        Assert.Equal("password-reset", result.Category);
        Assert.True(result.ConfidenceScore > 0.5);
    }

    [Fact]
    public async Task MoreSpecificRule_WinsOverLessSpecificRule()
    {
        // "invoice" alone would match billing-discrepancy (1 hit), but "period locked" and
        // "gl period" and "locked" together give known-issue-match 3 hits - the more
        // strongly evidenced rule should win, not whichever happens to be listed first.
        var ticket = TicketFactory.Create(
            title: "Cannot post invoice - GL period locked",
            description: "GL period locked even though period was reopened.");

        var result = await _classifier.ClassifyAsync(ticket, Array.Empty<KnowledgeReference>());

        Assert.Equal("known-issue-match", result.Category);
    }

    [Fact]
    public async Task NoRuleOrGroundingMatch_FallsBackToNeedsTriage()
    {
        var ticket = TicketFactory.Create(
            title: "Completely unrelated request",
            description: "Nothing here matches any keyword rule at all.");

        var result = await _classifier.ClassifyAsync(ticket, Array.Empty<KnowledgeReference>());

        Assert.Equal("needs-triage", result.Category);
        Assert.True(result.ConfidenceScore < 0.5);
    }

    [Fact]
    public async Task P1Priority_GetsConfidenceHaircut()
    {
        var p1Ticket = TicketFactory.Create(title: "Password reset issue", description: "password reset login", priority: TicketPriority.P1);
        var p3Ticket = TicketFactory.Create(title: "Password reset issue", description: "password reset login", priority: TicketPriority.P3);

        var p1Result = await _classifier.ClassifyAsync(p1Ticket, Array.Empty<KnowledgeReference>());
        var p3Result = await _classifier.ClassifyAsync(p3Ticket, Array.Empty<KnowledgeReference>());

        Assert.True(p1Result.ConfidenceScore < p3Result.ConfidenceScore);
    }
}
