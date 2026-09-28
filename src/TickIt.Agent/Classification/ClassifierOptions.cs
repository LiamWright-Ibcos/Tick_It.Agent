namespace TickIt.Agent.Classification;

/// <summary>
/// Same config-driven switch as the Node PoC's src/config.js: if a real Anthropic API key
/// is present, use the live model; otherwise fall back to the transparent rule-based
/// simulation classifier. No ANTHROPIC_API_KEY is configured in this build - it's still
/// "Requested - not yet provisioned" per first-line-support-poc-architecture.md, item 1.
/// </summary>
public sealed class ClassifierOptions
{
    public const string SectionName = "Classifier";

    public string? AnthropicApiKey { get; set; }

    public string Model { get; set; } = "claude-sonnet-4-5";

    public int MaxOutputTokens { get; set; } = 1024;

    public bool IsLiveConfigured => !string.IsNullOrWhiteSpace(AnthropicApiKey);
}
