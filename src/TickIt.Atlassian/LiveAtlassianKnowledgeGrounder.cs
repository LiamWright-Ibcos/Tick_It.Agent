using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TickIt.Core;

namespace TickIt.Atlassian;

/// <summary>
/// Real Jira Cloud REST API v3 grounding via a service-account Bearer token. Written and
/// unit-testable (see TickIt.Tests) but not exercised end-to-end against a live tenant in
/// this build, because that credential has not been provisioned yet
/// (first-line-support-poc-architecture.md, item 2: "Requested - not yet provisioned").
/// Confirm the endpoint shape against Atlassian's current docs before relying on this in
/// production - the architecture doc flags that this has moved before.
/// </summary>
public sealed class LiveAtlassianKnowledgeGrounder : IKnowledgeGrounder
{
    private readonly HttpClient _http;
    private readonly AtlassianOptions _options;

    public LiveAtlassianKnowledgeGrounder(HttpClient http, IOptions<AtlassianOptions> options)
    {
        _http = http;
        _options = options.Value;

        if (!_options.IsLiveConfigured)
        {
            throw new InvalidOperationException(
                "LiveAtlassianKnowledgeGrounder requires AtlassianOptions.SiteBaseUrl and BearerToken to be " +
                "configured. Use FixtureKnowledgeGrounder until the service-account credential is provisioned.");
        }

        _http.BaseAddress = new Uri(_options.SiteBaseUrl!.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<KnowledgeReference>> GroundAsync(Ticket ticket, CancellationToken ct = default)
    {
        // Free-text search scoped by the configured JQL plus the ticket's own significant
        // words. Kept deliberately simple - a smarter query builder is a later refinement,
        // not something worth over-engineering before this has ever run against a real tenant.
        var searchTerms = string.Join(' ', new[] { ticket.Title }.Concat(ticket.Tags));
        var jql = $"{_options.Jql} AND text ~ \"{EscapeJql(searchTerms)}\"";

        var url = $"rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&maxResults={_options.MaxResults}" +
                  "&fields=summary,description";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

        if (payload?.Issues is null)
        {
            return Array.Empty<KnowledgeReference>();
        }

        return payload.Issues
            .Select(issue => new KnowledgeReference
            {
                Key = issue.Key,
                Title = issue.Fields?.Summary ?? issue.Key,
                SourceType = KnowledgeSourceType.JiraIssue,
                Url = $"{_options.SiteBaseUrl!.TrimEnd('/')}/browse/{issue.Key}",
                Snippet = issue.Fields?.Description,
                RelevanceScore = 0.5 // Jira's search doesn't return a relevance score; a fixed
                                     // mid-value keeps this honest rather than implying precision
                                     // that isn't there. Revisit once this runs against real data.
            })
            .ToList();
    }

    private static string EscapeJql(string value) => value.Replace("\"", "\\\"");

    private sealed record SearchResponse
    {
        [JsonPropertyName("issues")]
        public List<JiraIssue>? Issues { get; init; }
    }

    private sealed record JiraIssue
    {
        [JsonPropertyName("key")]
        public required string Key { get; init; }

        [JsonPropertyName("fields")]
        public JiraFields? Fields { get; init; }
    }

    private sealed record JiraFields
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }
    }
}
