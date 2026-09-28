using TickIt.Core;

namespace TickIt.Tests.Fakes;

internal static class TicketFactory
{
    public static Ticket Create(
        string id = "TS-1",
        string title = "Sample ticket",
        string description = "Sample description",
        TicketPriority priority = TicketPriority.P3,
        string? category = null)
        => new()
        {
            Id = id,
            Number = id.Replace("TS-", ""),
            Title = title,
            Description = description,
            Priority = priority,
            Status = TicketStatus.New,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow
        };

    public static ClassificationResult Classification(
        string ticketId = "TS-1",
        string category = "needs-triage",
        double confidence = 0.5,
        IReadOnlyList<KnowledgeReference>? knowledge = null)
        => new()
        {
            TicketId = ticketId,
            Category = category,
            Summary = "Summary",
            SuggestedNextStep = "Do the thing",
            ConfidenceScore = confidence,
            MatchedKnowledge = knowledge ?? Array.Empty<KnowledgeReference>(),
            ReasoningMode = ReasoningMode.Simulation
        };
}
