namespace TickIt.Agent.Actions;

/// <summary>
/// Incoming webhook for a Teams channel - the lightest-weight way to post a real Teams
/// notification without a full Graph app registration / delegated-auth flow. Same
/// config-presence-switches-mode pattern as ClassifierOptions/AtlassianOptions.
/// Owner: whoever administers the target Teams channel (Team Ada's, presumably) adds a
/// "Workflows" incoming-webhook connector to it and pastes the URL here as
/// TeamsNotification:WebhookUrl (or the TeamsNotification__WebhookUrl environment variable).
/// Null/empty means: LiveActionExecutor blocks Teams notifications rather than posting them.
/// </summary>
public sealed class TeamsNotificationOptions
{
    public const string SectionName = "TeamsNotification";

    public string? WebhookUrl { get; set; }

    public bool IsLiveConfigured => !string.IsNullOrWhiteSpace(WebhookUrl);
}
