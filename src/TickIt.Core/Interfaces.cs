namespace TickIt.Core;

public interface ITicketSource
{
    Task<IReadOnlyList<Ticket>> ListNewTicketsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Ticket>> ListTicketsAsync(TicketStatus? status = null, CancellationToken ct = default);
    Task<Ticket?> GetTicketAsync(string ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<TicketAction>> ListTicketActionsAsync(string ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<Ticket>> SearchTicketsAsync(string query, CancellationToken ct = default);
}

public interface IKnowledgeGrounder
{
    Task<IReadOnlyList<KnowledgeReference>> GroundAsync(Ticket ticket, CancellationToken ct = default);
}

public interface IClassifier
{
    Task<ClassificationResult> ClassifyAsync(
        Ticket ticket,
        IReadOnlyList<KnowledgeReference> knowledge,
        CancellationToken ct = default);
}

public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(ProposedAction action, CancellationToken ct = default);
}

public enum AuditEntryKind
{
    TicketObserved,
    ClassificationProduced,
    TierDecided,
    ActionProposed,
    ActionExecuted
}

public sealed record AuditEntry
{
    public required AuditEntryKind Kind { get; init; }
    public required string TicketId { get; init; }
    public required string Summary { get; init; }
    public string? DetailJson { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public interface IAuditSink
{
    Task RecordAsync(AuditEntry entry, CancellationToken ct = default);
}

/// <summary>Full record of one ticket's pass through the pipeline, for reporting.</summary>
public sealed record TriageResult
{
    public required Ticket Ticket { get; init; }
    public required ClassificationResult Classification { get; init; }
    public required TierDecision TierDecision { get; init; }
    public IReadOnlyList<ProposedAction> ProposedActions { get; init; } = Array.Empty<ProposedAction>();
    public IReadOnlyList<ActionExecutionResult> ExecutionResults { get; init; } = Array.Empty<ActionExecutionResult>();
    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;
}
