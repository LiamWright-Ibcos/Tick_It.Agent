using TickIt.Atlassian;
using TickIt.Tests.Fakes;

namespace TickIt.Tests;

public class FixtureKnowledgeGrounderTests : IDisposable
{
    private readonly string _fixturePath;

    public FixtureKnowledgeGrounderTests()
    {
        _fixturePath = Path.Combine(Path.GetTempPath(), $"gold-snapshot-{Guid.NewGuid():N}.json");
        File.WriteAllText(_fixturePath, """
            [
              {
                "key": "GOLD-1",
                "title": "CDC queue drains all files",
                "url": "https://example.atlassian.net/browse/GOLD-1",
                "snippet": "CDC snippet",
                "keywords": ["cdc", "queue", "goldcdc"]
              }
            ]
            """);
    }

    public void Dispose() => File.Delete(_fixturePath);

    [Fact]
    public async Task MatchingKeywords_ReturnsReference()
    {
        var grounder = new FixtureKnowledgeGrounder(_fixturePath);
        var ticket = TicketFactory.Create(title: "CDC queue issue", description: "The CDC queue for goldcdc is stuck.");

        var results = await grounder.GroundAsync(ticket);

        Assert.Single(results);
        Assert.Equal("GOLD-1", results[0].Key);
        Assert.True(results[0].RelevanceScore > 0);
    }

    [Fact]
    public async Task NoMatchingKeywords_ReturnsEmpty()
    {
        var grounder = new FixtureKnowledgeGrounder(_fixturePath);
        var ticket = TicketFactory.Create(title: "Password reset", description: "Totally unrelated to the snapshot topic.");

        var results = await grounder.GroundAsync(ticket);

        Assert.Empty(results);
    }
}
