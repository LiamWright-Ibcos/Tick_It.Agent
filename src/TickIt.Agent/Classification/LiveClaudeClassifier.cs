using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TickIt.Core;

namespace TickIt.Agent.Classification;

/// <summary>
/// Real classifier using the Anthropic Messages API directly over HttpClient (no official
/// Anthropic .NET SDK exists, so this is a small, explicit wrapper rather than a dependency
/// on an unofficial third-party package). This mirrors the Node PoC's src/liveAgent.js: the
/// code path is written and unit-testable (with a stubbed HttpMessageHandler - see
/// TickIt.Tests) but has not been executed against the real API in this sandbox, because no
/// ANTHROPIC_API_KEY has been provisioned (first-line-support-poc-architecture.md, item 1).
///
/// When a key lands: set Classifier:AnthropicApiKey in config and nothing else needs to
/// change - AgentPipeline resolves IClassifier from DI, and Program.cs's registration
/// already switches on ClassifierOptions.IsLiveConfigured.
/// </summary>
public sealed class LiveClaudeClassifier : IClassifier
{
    private const string ApiVersion = "2023-06-01";
    private static readonly Uri MessagesEndpoint = new("https://api.anthropic.com/v1/messages");

    private readonly HttpClient _http;
    private readonly ClassifierOptions _options;

    public LiveClaudeClassifier(HttpClient http, IOptions<ClassifierOptions> options)
    {
        _http = http;
        _options = options.Value;

        if (!_options.IsLiveConfigured)
        {
            throw new InvalidOperationException(
                "LiveClaudeClassifier requires ClassifierOptions.AnthropicApiKey to be configured. " +
                "Use SimulationClassifier until a real API key is provisioned.");
        }

        _http.DefaultRequestHeaders.Remove("x-api-key");
        _http.DefaultRequestHeaders.Add("x-api-key", _options.AnthropicApiKey);
        _http.DefaultRequestHeaders.Remove("anthropic-version");
        _http.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ClassificationResult> ClassifyAsync(
        Ticket ticket,
        IReadOnlyList<KnowledgeReference> knowledge,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(ticket, knowledge);

        var requestBody = new MessagesRequest
        {
            Model = _options.Model,
            MaxTokens = _options.MaxOutputTokens,
            Messages = new[] { new MessageInput { Role = "user", Content = prompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOpts),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<MessagesResponse>(body, JsonOpts)
                     ?? throw new InvalidOperationException("Empty response from Anthropic Messages API.");

        var text = string.Concat(parsed.Content?.Select(c => c.Text) ?? Array.Empty<string>());
        return ParseModelOutput(ticket, knowledge, text);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string BuildPrompt(Ticket ticket, IReadOnlyList<KnowledgeReference> knowledge)
    {
        var knowledgeBlock = knowledge.Count == 0
            ? "(no grounding references found)"
            : string.Join("\n", knowledge.Select(k => $"- [{k.Key}] {k.Title}: {k.Snippet}"));

        return $"""
            You are triaging a first-line support ticket for Ibcos GOLD. Respond ONLY with a
            JSON object with keys: category (short kebab-case string), summary (one sentence),
            suggested_next_step (one sentence for a human agent), confidence (0.0-1.0 number),
            rationale (one sentence).

            Ticket:
            Title: {ticket.Title}
            Description: {ticket.Description}
            Priority: {ticket.Priority}
            Product: {ticket.Product}

            Grounding references:
            {knowledgeBlock}
            """;
    }

    private static ClassificationResult ParseModelOutput(
        Ticket ticket, IReadOnlyList<KnowledgeReference> knowledge, string modelText)
    {
        using var doc = JsonDocument.Parse(ExtractJsonObject(modelText));
        var root = doc.RootElement;

        return new ClassificationResult
        {
            TicketId = ticket.Id,
            Category = root.GetProperty("category").GetString() ?? "needs-triage",
            Summary = root.GetProperty("summary").GetString() ?? ticket.Title,
            SuggestedNextStep = root.GetProperty("suggested_next_step").GetString() ?? "Route to a human for triage.",
            ConfidenceScore = root.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.5,
            MatchedKnowledge = knowledge,
            ReasoningMode = ReasoningMode.Live,
            Rationale = root.TryGetProperty("rationale", out var r) ? r.GetString() : null
        };
    }

    /// <summary>Models sometimes wrap JSON in prose or fencing despite instructions; take the outermost braces.</summary>
    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            throw new InvalidOperationException("Model response did not contain a JSON object.");
        }

        return text[start..(end + 1)];
    }

    private sealed record MessagesRequest
    {
        public required string Model { get; init; }
        public required int MaxTokens { get; init; }
        public required MessageInput[] Messages { get; init; }
    }

    private sealed record MessageInput
    {
        public required string Role { get; init; }
        public required string Content { get; init; }
    }

    private sealed record MessagesResponse
    {
        public List<ContentBlock>? Content { get; init; }
    }

    private sealed record ContentBlock
    {
        public string? Type { get; init; }
        public string? Text { get; init; }
    }
}
