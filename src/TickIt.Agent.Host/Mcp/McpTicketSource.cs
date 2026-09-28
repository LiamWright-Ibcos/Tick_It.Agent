using System.Text.Json;
using ModelContextProtocol.Client;
using TickIt.Core;

namespace TickIt.Agent.Host.Mcp;

/// <summary>
/// ITicketSource implementation backed by a real MCP client talking to the
/// TickIt.TeamSupport.Mcp server process over stdio - the .NET equivalent of the Node PoC's
/// agent spawning team-support-mcp as a subprocess. Every call is a genuine MCP tools/call
/// round trip, not an in-process shortcut, so this is the same "separate project" boundary
/// the original ask required, exercised for real rather than just built alongside.
/// </summary>
public sealed class McpTicketSource : ITicketSource, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly McpClient _client;

    private McpTicketSource(McpClient client)
    {
        _client = client;
    }

    public static async Task<McpTicketSource> ConnectAsync(string teamSupportMcpDllPath, CancellationToken ct = default)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "TickIt.TeamSupport.Mcp",
            Command = "dotnet",
            Arguments = [teamSupportMcpDllPath]
        });

        var client = await McpClient.CreateAsync(transport, cancellationToken: ct);
        return new McpTicketSource(client);
    }

    public async Task<IReadOnlyList<Ticket>> ListNewTicketsAsync(CancellationToken ct = default)
        => await CallForListAsync<Ticket>("list_new_tickets", null, ct);

    public async Task<IReadOnlyList<Ticket>> ListTicketsAsync(TicketStatus? status = null, CancellationToken ct = default)
    {
        var args = status is null
            ? null
            : new Dictionary<string, object?> { ["status"] = status.ToString() };
        return await CallForListAsync<Ticket>("list_tickets", args, ct);
    }

    public async Task<Ticket?> GetTicketAsync(string ticketId, CancellationToken ct = default)
    {
        var json = await CallForRawAsync("get_ticket", new Dictionary<string, object?> { ["ticketId"] = ticketId }, ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("error", out _))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Ticket>(json, JsonOptions);
    }

    public async Task<IReadOnlyList<TicketAction>> ListTicketActionsAsync(string ticketId, CancellationToken ct = default)
        => await CallForListAsync<TicketAction>("list_ticket_actions", new Dictionary<string, object?> { ["ticketId"] = ticketId }, ct);

    public async Task<IReadOnlyList<Ticket>> SearchTicketsAsync(string query, CancellationToken ct = default)
        => await CallForListAsync<Ticket>("search_tickets", new Dictionary<string, object?> { ["query"] = query }, ct);

    private async Task<IReadOnlyList<T>> CallForListAsync<T>(string tool, IReadOnlyDictionary<string, object?>? args, CancellationToken ct)
    {
        var json = await CallForRawAsync(tool, args, ct);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
    }

    private async Task<string> CallForRawAsync(string tool, IReadOnlyDictionary<string, object?>? args, CancellationToken ct)
    {
        var result = await _client.CallToolAsync(tool, args ?? new Dictionary<string, object?>(), cancellationToken: ct);

        var text = result.Content
            .OfType<ModelContextProtocol.Protocol.TextContentBlock>()
            .Select(c => c.Text)
            .FirstOrDefault();

        if (text is null)
        {
            throw new InvalidOperationException($"MCP tool '{tool}' returned no text content.");
        }

        return text;
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
