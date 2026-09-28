using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TickIt.Agent.Actions;
using TickIt.Atlassian;
using TickIt.Core;

namespace TickIt.Tests;

/// <summary>
/// Covers the gating logic only (blocked-before-any-call, and the always-throws action
/// types). The two HTTP-calling paths (Teams webhook, Jira comment) are not exercised here
/// with a fake handler, same posture as LiveAtlassianKnowledgeGrounder/LiveClaudeClassifier
/// today - nothing in this build has run those against a live endpoint yet. Every test here
/// is written so it never reaches the HttpClient at all.
/// </summary>
public class LiveActionExecutorTests
{
    private static LiveActionExecutor CreateExecutor(
        ActionExecutorOptions? gates = null,
        AtlassianOptions? atlassian = null,
        TeamsNotificationOptions? teams = null)
    {
        return new LiveActionExecutor(
            new HttpClient(),
            Options.Create(atlassian ?? new AtlassianOptions()),
            Options.Create(teams ?? new TeamsNotificationOptions()),
            Options.Create(gates ?? new ActionExecutorOptions()),
            NullLogger<LiveActionExecutor>.Instance);
    }

    private static ProposedAction Action(ActionType type, IReadOnlyDictionary<string, string>? payload = null) => new()
    {
        TicketId = "TS-1",
        Type = type,
        Description = "test action",
        Tier = AutonomyTier.AutoActionLowRisk,
        Payload = payload ?? new Dictionary<string, string>()
    };

    [Fact]
    public async Task NotifyTeamsChannel_BlockedWhenGateOff_NeverThrows()
    {
        var executor = CreateExecutor(gates: new ActionExecutorOptions { EnableLiveNotifications = false });

        var result = await executor.ExecuteAsync(Action(ActionType.NotifyTeamsChannel));

        Assert.False(result.Executed);
        Assert.Equal(ActionExecutionMode.Live, result.Mode);
        Assert.Contains("EnableLiveNotifications", result.Outcome);
    }

    [Fact]
    public async Task NotifyTeamsChannel_BlockedWhenNoWebhookConfigured()
    {
        var executor = CreateExecutor(
            gates: new ActionExecutorOptions { EnableLiveNotifications = true },
            teams: new TeamsNotificationOptions { WebhookUrl = null });

        var result = await executor.ExecuteAsync(Action(ActionType.NotifyTeamsChannel));

        Assert.False(result.Executed);
        Assert.Contains("webhook", result.Outcome, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LinkJiraIssue_BlockedWhenTier2GateOff()
    {
        var executor = CreateExecutor(gates: new ActionExecutorOptions { EnableLiveTier2Actions = false });
        var action = Action(ActionType.LinkJiraIssue, new Dictionary<string, string> { ["jiraKey"] = "GOLD-1" });

        var result = await executor.ExecuteAsync(action);

        Assert.False(result.Executed);
        Assert.Contains("EnableLiveTier2Actions", result.Outcome);
    }

    [Fact]
    public async Task LinkJiraIssue_BlockedWhenNoAtlassianCredential()
    {
        var executor = CreateExecutor(
            gates: new ActionExecutorOptions { EnableLiveTier2Actions = true },
            atlassian: new AtlassianOptions { SiteBaseUrl = null, BearerToken = null });
        var action = Action(ActionType.LinkJiraIssue, new Dictionary<string, string> { ["jiraKey"] = "GOLD-1" });

        var result = await executor.ExecuteAsync(action);

        Assert.False(result.Executed);
        Assert.Contains("Atlassian", result.Outcome);
    }

    [Fact]
    public async Task LinkJiraIssue_BlockedWhenPayloadMissingJiraKey()
    {
        var executor = CreateExecutor(
            gates: new ActionExecutorOptions { EnableLiveTier2Actions = true },
            atlassian: new AtlassianOptions { SiteBaseUrl = "https://example.atlassian.net", BearerToken = "token" });

        var result = await executor.ExecuteAsync(Action(ActionType.LinkJiraIssue));

        Assert.False(result.Executed);
        Assert.Contains("jiraKey", result.Outcome);
    }

    [Theory]
    [InlineData(ActionType.ApplyCategoryTag)]
    [InlineData(ActionType.ProposeDuplicateClose)]
    [InlineData(ActionType.SuggestKnownIssueLink)]
    public async Task ActionTypesWithNoLiveIntegration_AlwaysThrow(ActionType type)
    {
        var executor = CreateExecutor(gates: new ActionExecutorOptions
        {
            EnableLiveNotifications = true,
            EnableLiveTier2Actions = true
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => executor.ExecuteAsync(Action(type)));
    }

    [Fact]
    public async Task LogOnly_NeverExecutesButDoesNotThrow()
    {
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync(Action(ActionType.LogOnly));

        Assert.False(result.Executed);
    }
}
