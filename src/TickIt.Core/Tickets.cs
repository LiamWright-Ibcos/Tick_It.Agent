namespace TickIt.Core;

public enum TicketPriority
{
    P1,
    P2,
    P3,
    P4,
    Unknown
}

public enum TicketStatus
{
    New,
    Open,
    Pending,
    Resolved,
    Closed,
    Unknown
}

/// <summary>
/// Which GOLD product line the ticket relates to. Classic vs. cloud volumes/retirement
/// date are still open questions per the Draft 2 findings, but the field exists so
/// downstream logic (and reporting) can split on it once that's resolved.
/// </summary>
public enum GoldProductLine
{
    Classic,
    Cloud,
    Unknown
}

public sealed record Ticket
{
    public required string Id { get; init; }
    public required string Number { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public TicketPriority Priority { get; init; } = TicketPriority.Unknown;
    public TicketStatus Status { get; init; } = TicketStatus.Unknown;
    public string? Category { get; init; }
    public GoldProductLine Product { get; init; } = GoldProductLine.Unknown;
    public string? RequesterEmail { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

public enum TicketActionType
{
    Comment,
    StatusChange,
    Assignment,
    Note,
    Other
}

public sealed record TicketAction
{
    public required string Id { get; init; }
    public required string TicketId { get; init; }
    public TicketActionType Type { get; init; } = TicketActionType.Other;
    public string? Actor { get; init; }
    public string? Body { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
