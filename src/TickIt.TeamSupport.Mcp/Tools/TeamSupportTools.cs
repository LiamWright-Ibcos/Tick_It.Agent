using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using TickIt.Core;

namespace TickIt.TeamSupport.Mcp.Tools;

/// <summary>
/// Five read tools, no write tool anywhere in this class - enforced by omission, not a
/// flag, same as the Node PoC's team-support-mcp server. If a write capability is ever
/// wanted, it belongs in a new, explicitly-named tool added deliberately, never bolted
/// onto one of these.
/// </summary>
[McpServerToolType]
public static class TeamSupportTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [McpServerTool(Name = "list_new_tickets")]
    [Description("List tickets currently in New status. Read-only.")]
    public static async Task<string> ListNewTickets(ITicketSource ticketSource, CancellationToken ct)
    {
        var tickets = await ticketSource.ListNewTicketsAsync(ct);
        return JsonSerializer.Serialize(tickets, JsonOptions);
    }

    [McpServerTool(Name = "list_tickets")]
    [Description("List tickets, optionally filtered by status (New, Open, Pending, Resolved, Closed). Read-only.")]
    public static async Task<string> ListTickets(
        ITicketSource ticketSource,
        [Description("Optional status filter: New, Open, Pending, Resolved, Closed")] string? status,
        CancellationToken ct)
    {
        TicketStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TicketStatus>(status, ignoreCase: true, out var s))
        {
            parsed = s;
        }

        var tickets = await ticketSource.ListTicketsAsync(parsed, ct);
        return JsonSerializer.Serialize(tickets, JsonOptions);
    }

    [McpServerTool(Name = "get_ticket")]
    [Description("Get a single ticket by id. Read-only.")]
    public static async Task<string> GetTicket(
        ITicketSource ticketSource,
        [Description("Ticket id, e.g. TS-10231")] string ticketId,
        CancellationToken ct)
    {
        var ticket = await ticketSource.GetTicketAsync(ticketId, ct);
        return ticket is null
            ? JsonSerializer.Serialize(new { error = $"Ticket '{ticketId}' not found" }, JsonOptions)
            : JsonSerializer.Serialize(ticket, JsonOptions);
    }

    [McpServerTool(Name = "list_ticket_actions")]
    [Description("List the action/comment history for a ticket. Read-only.")]
    public static async Task<string> ListTicketActions(
        ITicketSource ticketSource,
        [Description("Ticket id, e.g. TS-10231")] string ticketId,
        CancellationToken ct)
    {
        var actions = await ticketSource.ListTicketActionsAsync(ticketId, ct);
        return JsonSerializer.Serialize(actions, JsonOptions);
    }

    [McpServerTool(Name = "search_tickets")]
    [Description("Full-text search across ticket titles, descriptions and tags. Read-only.")]
    public static async Task<string> SearchTickets(
        ITicketSource ticketSource,
        [Description("Search text")] string query,
        CancellationToken ct)
    {
        var tickets = await ticketSource.SearchTicketsAsync(query, ct);
        return JsonSerializer.Serialize(tickets, JsonOptions);
    }
}
