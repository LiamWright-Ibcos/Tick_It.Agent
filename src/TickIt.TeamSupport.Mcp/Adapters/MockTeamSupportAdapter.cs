using System.Text.Json;
using System.Text.Json.Serialization;
using TickIt.Core;

namespace TickIt.TeamSupport.Mcp.Adapters;

/// <summary>
/// Fixture-backed stand-in for a real Team Support API client. There is no Team Support
/// sandbox/API credential yet (still being chased per first-line-support-poc-architecture.md,
/// item 3) so this reads a static JSON fixture instead of calling anything live.
///
/// When a real credential/API exists: add a new adapter class here implementing
/// <see cref="ITicketSource"/> against the real Team Support API, and change the single
/// DI registration in Program.cs to use it instead of this one. Nothing else in the
/// solution needs to change - every consumer depends on ITicketSource, not on this class.
/// </summary>
public sealed class MockTeamSupportAdapter : ITicketSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Task<FixtureData> _dataTask;

    public MockTeamSupportAdapter(string? fixturePath = null)
    {
        var path = fixturePath ?? Path.Combine(AppContext.BaseDirectory, "Fixtures", "tickets.json");
        _dataTask = LoadAsync(path);
    }

    private static async Task<FixtureData> LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var raw = await JsonSerializer.DeserializeAsync<RawFixtureData>(stream, JsonOptions);
        raw ??= new RawFixtureData();

        var data = new FixtureData { Tickets = raw.Tickets };
        foreach (var (ticketId, actions) in raw.Actions)
        {
            data.Actions[ticketId] = actions
                .Select(a => new TicketAction
                {
                    Id = a.Id,
                    TicketId = ticketId,
                    Type = a.Type,
                    Actor = a.Actor,
                    Body = a.Body,
                    Timestamp = a.Timestamp
                })
                .ToList();
        }

        return data;
    }

    public async Task<IReadOnlyList<Ticket>> ListNewTicketsAsync(CancellationToken ct = default)
    {
        var data = await _dataTask;
        return data.Tickets.Where(t => t.Status == TicketStatus.New).ToList();
    }

    public async Task<IReadOnlyList<Ticket>> ListTicketsAsync(TicketStatus? status = null, CancellationToken ct = default)
    {
        var data = await _dataTask;
        return status is null
            ? data.Tickets
            : data.Tickets.Where(t => t.Status == status).ToList();
    }

    public async Task<Ticket?> GetTicketAsync(string ticketId, CancellationToken ct = default)
    {
        var data = await _dataTask;
        return data.Tickets.FirstOrDefault(t => t.Id.Equals(ticketId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<TicketAction>> ListTicketActionsAsync(string ticketId, CancellationToken ct = default)
    {
        var data = await _dataTask;
        return data.Actions.TryGetValue(ticketId, out var actions)
            ? actions
            : Array.Empty<TicketAction>();
    }

    public async Task<IReadOnlyList<Ticket>> SearchTicketsAsync(string query, CancellationToken ct = default)
    {
        var data = await _dataTask;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<Ticket>();
        }

        return data.Tickets
            .Where(t =>
                t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private sealed class FixtureData
    {
        public List<Ticket> Tickets { get; set; } = new();
        public Dictionary<string, List<TicketAction>> Actions { get; set; } = new();
    }

    /// <summary>Shape of the raw JSON, before TicketId is backfilled from the dictionary key.</summary>
    private sealed class RawFixtureData
    {
        public List<Ticket> Tickets { get; set; } = new();
        public Dictionary<string, List<RawTicketAction>> Actions { get; set; } = new();
    }

    private sealed class RawTicketAction
    {
        public required string Id { get; init; }
        public TicketActionType Type { get; init; } = TicketActionType.Other;
        public string? Actor { get; init; }
        public string? Body { get; init; }
        public DateTimeOffset Timestamp { get; init; }
    }
}
