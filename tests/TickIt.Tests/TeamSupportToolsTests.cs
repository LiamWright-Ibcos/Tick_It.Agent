using System.Text.Json;
using TickIt.Core;
using TickIt.TeamSupport.Mcp.Tools;
using TickIt.Tests.Fakes;

namespace TickIt.Tests;

/// <summary>
/// Exercises the MCP tool methods directly (in-process, no stdio transport) so the tool
/// contracts are covered by fast unit tests. End-to-end coverage over the real MCP wire
/// protocol was done manually while building this project - see the README - and is
/// deliberately not re-run as part of the automated suite, which would need to spawn a
/// subprocess per test.
/// </summary>
public class TeamSupportToolsTests
{
    private static InMemoryTicketSource BuildSource() => new(new[]
    {
        TicketFactory.Create(id: "TS-1", title: "First ticket"),
        TicketFactory.Create(id: "TS-2", title: "Second ticket")
    });

    [Fact]
    public async Task ListNewTickets_ReturnsJsonArrayOfTickets()
    {
        var json = await TeamSupportTools.ListNewTickets(BuildSource(), CancellationToken.None);
        var tickets = JsonSerializer.Deserialize<List<Ticket>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(tickets);
        Assert.Equal(2, tickets!.Count);
    }

    [Fact]
    public async Task GetTicket_UnknownId_ReturnsErrorPayload()
    {
        var json = await TeamSupportTools.GetTicket(BuildSource(), "does-not-exist", CancellationToken.None);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task GetTicket_KnownId_ReturnsTicket()
    {
        var json = await TeamSupportTools.GetTicket(BuildSource(), "TS-1", CancellationToken.None);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("TS-1", doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task SearchTickets_MatchesTitle()
    {
        var json = await TeamSupportTools.SearchTickets(BuildSource(), "Second", CancellationToken.None);
        var tickets = JsonSerializer.Deserialize<List<Ticket>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Single(tickets!);
        Assert.Equal("TS-2", tickets![0].Id);
    }
}
