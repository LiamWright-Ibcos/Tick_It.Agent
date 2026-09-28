namespace TickIt.Atlassian;

/// <summary>
/// Config-driven mode switch, same pattern as the Node PoC's config.js: presence of a
/// real credential switches from the fixture grounder to the live one. Neither the
/// service-account API key nor confirmation of the current MCP/REST endpoint URL has
/// been provisioned yet (first-line-support-poc-architecture.md, item 2), so
/// <see cref="BearerToken"/> is expected to be null/empty in this build.
/// </summary>
public sealed class AtlassianOptions
{
    public const string SectionName = "Atlassian";

    /// <summary>e.g. https://godealeruk.atlassian.net</summary>
    public string? SiteBaseUrl { get; set; }

    /// <summary>Service-account Bearer token. Null/empty means: use the fixture grounder.</summary>
    public string? BearerToken { get; set; }

    /// <summary>JQL restricting grounding search, e.g. "project = GOLD".</summary>
    public string Jql { get; set; } = "project = GOLD";

    public int MaxResults { get; set; } = 5;

    public bool IsLiveConfigured => !string.IsNullOrWhiteSpace(SiteBaseUrl) && !string.IsNullOrWhiteSpace(BearerToken);
}
