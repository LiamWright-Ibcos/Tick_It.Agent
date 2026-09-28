namespace TickIt.Core;

public enum KnowledgeSourceType
{
    JiraIssue,
    ConfluencePage,
    KnownIssue,
    Other
}

/// <summary>
/// A grounding reference pulled from Jira/Confluence (or, eventually, a GOLD known-issue
/// store — content structure for that is still an open item per the Draft 2 findings).
/// </summary>
public sealed record KnowledgeReference
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public KnowledgeSourceType SourceType { get; init; } = KnowledgeSourceType.Other;
    public string? Url { get; init; }
    public string? Snippet { get; init; }

    /// <summary>0.0-1.0, how relevant this reference is judged to be to the ticket.</summary>
    public double RelevanceScore { get; init; }
}
