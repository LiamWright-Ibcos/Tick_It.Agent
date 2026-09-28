using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TickIt.Atlassian;
using TickIt.Core;

namespace TickIt.Agent.Actions;

/// <summary>
/// Real action executor - posts genuine Teams notifications and Jira comments once the
/// relevant credential AND sign-off gate are both in place; explicitly refuses (without ever
/// calling out) when either is missing, and still throws for the two action types that have
/// no real integration to call at all.
///
/// NOT the effective IActionExecutor by default: Program.cs resolves this only when
/// ActionExecutorOptions.UseLiveExecutor is true. Leave that false until
/// docs/live-action-executor-signoff.md has been signed off by Team Ada.
///
/// Per action type, once UseLiveExecutor is true:
///   - NotifyTeamsChannel (Tier 1): live if EnableLiveNotifications is true AND a Teams
///     webhook is configured. Lower risk - no external state change, just a message.
///   - LinkJiraIssue (Tier 2): live if EnableLiveTier2Actions is true AND an Atlassian
///     service-account credential is configured. Adds a comment to the matched Jira issue
///     referencing the ticket; does not create/modify a formal issue-link relationship,
///     which is a heavier permission to grant and wasn't judged necessary for a first cut.
///   - ApplyCategoryTag, ProposeDuplicateClose (Tier 2): always throw NotSupportedException,
///     regardless of any flag. There is no real Team Support write API/credential to call -
///     item 3 in first-line-support-poc-architecture.md is a "does Team Support expose a
///     write API at all" decision, not a missing key. Nothing to wire up until that's
///     resolved - do not stub around this with a fake success.
///   - SuggestKnownIssueLink: throws - no live path has been designed for this one yet.
///   - LogOnly: no-op success, nothing to call by design.
/// </summary>
public sealed class LiveActionExecutor : IActionExecutor
{
    private readonly HttpClient _http;
    private readonly AtlassianOptions _atlassian;
    private readonly TeamsNotificationOptions _teams;
    private readonly ActionExecutorOptions _gates;
    private readonly ILogger<LiveActionExecutor> _logger;

    public LiveActionExecutor(
        HttpClient http,
        IOptions<AtlassianOptions> atlassianOptions,
        IOptions<TeamsNotificationOptions> teamsOptions,
        IOptions<ActionExecutorOptions> gateOptions,
        ILogger<LiveActionExecutor> logger)
    {
        _http = http;
        _atlassian = atlassianOptions.Value;
        _teams = teamsOptions.Value;
        _gates = gateOptions.Value;
        _logger = logger;

        // Same guarded-header pattern as LiveAtlassianKnowledgeGrounder, but this class must
        // not throw just because Atlassian isn't configured - Teams-only usage is valid.
        if (_atlassian.IsLiveConfigured)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _atlassian.BearerToken);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public Task<ActionExecutionResult> ExecuteAsync(ProposedAction action, CancellationToken ct = default)
    {
        return action.Type switch
        {
            ActionType.NotifyTeamsChannel => NotifyTeamsAsync(action, ct),
            ActionType.LinkJiraIssue => LinkJiraAsync(action, ct),

            ActionType.ApplyCategoryTag or ActionType.ProposeDuplicateClose => throw new NotSupportedException(
                $"'{action.Type}' has no real Team Support write API/credential to call yet - " +
                "see first-line-support-poc-architecture.md item 3. This is a decision gap, " +
                "not a config flag; do not stub around it."),

            ActionType.SuggestKnownIssueLink => throw new NotSupportedException(
                "'SuggestKnownIssueLink' has no live integration designed yet."),

            ActionType.LogOnly => Task.FromResult(new ActionExecutionResult
            {
                Action = action,
                Mode = ActionExecutionMode.Live,
                Executed = false,
                Outcome = "LogOnly action type - nothing to execute by design."
            }),

            _ => throw new NotSupportedException($"Unhandled action type '{action.Type}'.")
        };
    }

    private async Task<ActionExecutionResult> NotifyTeamsAsync(ProposedAction action, CancellationToken ct)
    {
        if (!_gates.EnableLiveNotifications)
        {
            return Blocked(action, "ActionExecutor:EnableLiveNotifications is false.");
        }

        if (!_teams.IsLiveConfigured)
        {
            return Blocked(action, "No Teams webhook configured (TeamsNotification:WebhookUrl).");
        }

        var card = new
        {
            text = $"**{action.Description}**\n\nTicket: {action.TicketId} • Tier: {action.Tier}"
        };

        using var response = await _http.PostAsJsonAsync(_teams.WebhookUrl, card, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("[LIVE] Posted Teams notification for ticket {TicketId}.", action.TicketId);

        return new ActionExecutionResult
        {
            Action = action,
            Mode = ActionExecutionMode.Live,
            Executed = true,
            Outcome = $"Posted to Teams webhook (HTTP {(int)response.StatusCode})."
        };
    }

    private async Task<ActionExecutionResult> LinkJiraAsync(ProposedAction action, CancellationToken ct)
    {
        if (!_gates.EnableLiveTier2Actions)
        {
            return Blocked(action, "ActionExecutor:EnableLiveTier2Actions is false - see docs/live-action-executor-signoff.md.");
        }

        if (!_atlassian.IsLiveConfigured)
        {
            return Blocked(action, "No Atlassian credential configured (Atlassian:SiteBaseUrl / BearerToken).");
        }

        if (!action.Payload.TryGetValue("jiraKey", out var jiraKey) || string.IsNullOrWhiteSpace(jiraKey))
        {
            return Blocked(action, "ProposedAction.Payload is missing 'jiraKey'.");
        }

        var commentBody = new
        {
            body = new
            {
                type = "doc",
                version = 1,
                content = new object[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new object[]
                        {
                            new { type = "text", text = $"Linked from Team Support ticket {action.TicketId}: {action.Description}" }
                        }
                    }
                }
            }
        };

        var url = $"{_atlassian.SiteBaseUrl!.TrimEnd('/')}/rest/api/3/issue/{jiraKey}/comment";
        using var response = await _http.PostAsJsonAsync(url, commentBody, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("[LIVE] Commented on Jira issue {JiraKey} for ticket {TicketId}.", jiraKey, action.TicketId);

        return new ActionExecutionResult
        {
            Action = action,
            Mode = ActionExecutionMode.Live,
            Executed = true,
            Outcome = $"Added a comment to Jira issue {jiraKey} (HTTP {(int)response.StatusCode})."
        };
    }

    private static ActionExecutionResult Blocked(ProposedAction action, string reason) => new()
    {
        Action = action,
        Mode = ActionExecutionMode.Live,
        Executed = false,
        Outcome = $"Blocked before calling any live system: {reason}"
    };
}
