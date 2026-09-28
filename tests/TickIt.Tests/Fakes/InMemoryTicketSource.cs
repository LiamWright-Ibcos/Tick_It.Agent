using TickIt.Core;

namespace TickIt.Tests.Fakes;

internal sealed class InMemoryTicketSource : ITicketSource
{
    private readonly List<Ticket> _tickets;

    public InMemoryTicketSource(IEnumerable<Ticket> tickets) => _tickets = tickets.ToList();

    public Task<IReadOnlyList<Ticket>> ListNewTicketsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Ticket>>(_tickets.Where(t => t.Status == TicketStatus.New).ToList());

    public Task<IReadOnlyList<Ticket>> ListTicketsAsync(TicketStatus? status = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Ticket>>(status is null ? _tickets : _tickets.Where(t => t.Status == status).ToList());

    public Task<Ticket?> GetTicketAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult(_tickets.FirstOrDefault(t => t.Id == ticketId));

    public Task<IReadOnlyList<TicketAction>> ListTicketActionsAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TicketAction>>(Array.Empty<TicketAction>());

    public Task<IReadOnlyList<Ticket>> SearchTicketsAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Ticket>>(_tickets.Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());
}
