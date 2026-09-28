namespace TickIt.Core;

/// <summary>
/// How a ClassificationResult was produced. Mirrors the Node PoC's two reasoning modes:
/// Simulation is a transparent rule-based stand-in used when no live model credential is
/// configured; Live is the real model call. Every result carries this so nothing downstream
/// (reports, audit log, UI) can mistake a simulated result for a real one.
/// </summary>
public enum ReasoningMode
{
    Simulation,
    Live
}

public sealed record ClassificationResult
{
    public required string TicketId { get; init; }
    public required string Category { get; init; }
    public required string Summary { get; init; }
    public required string SuggestedNextStep { get; init; }

    /// <summary>0.0-1.0 confidence in this classification.</summary>
    public required double ConfidenceScore { get; init; }

    public IReadOnlyList<KnowledgeReference> MatchedKnowledge { get; init; } = Array.Empty<KnowledgeReference>();
    public required ReasoningMode ReasoningMode { get; init; }

    /// <summary>Short human-readable rationale for the classification/confidence, for audit and review.</summary>
    public string? Rationale { get; init; }
}
