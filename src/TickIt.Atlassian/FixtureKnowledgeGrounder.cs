using System.Text.Json;
using TickIt.Core;

namespace TickIt.Atlassian;

/// <summary>
/// Grounds tickets against a small, real snapshot of GOLD Jira issues (pulled once via the
/// Atlassian connector while building this project - GOLD-16270, GOLD-16269, GOLD-16268)
/// rather than a synthetic fixture, following the same idea as the Node PoC's "small real
/// snapshot of two live GOLD Jira issues". This is honest about what it is: a fixed
/// snapshot, scored by simple keyword overlap, not a live search. Most tickets legitimately
/// won't match anything in it - that's expected and correct, not a bug, since the snapshot
/// only covers whatever happened to be recent in GOLD when it was taken.
///
/// Used whenever <see cref="AtlassianOptions.IsLiveConfigured"/> is false, which is always
/// true in this build (no Atlassian service-account credential has been provisioned yet).
/// </summary>
public sealed class FixtureKnowledgeGrounder : IKnowledgeGrounder
{
    private readonly Task<List<SnapshotEntry>> _snapshotTask;

    public FixtureKnowledgeGrounder(string? fixturePath = null)
    {
        var path = fixturePath ?? Path.Combine(AppContext.BaseDirectory, "Fixtures", "gold-jira-snapshot.json");
        _snapshotTask = LoadAsync(path);
    }

    private static async Task<List<SnapshotEntry>> LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var entries = await JsonSerializer.DeserializeAsync<List<SnapshotEntry>>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return entries ?? new List<SnapshotEntry>();
    }

    public async Task<IReadOnlyList<KnowledgeReference>> GroundAsync(Ticket ticket, CancellationToken ct = default)
    {
        var snapshot = await _snapshotTask;
        var haystack = $"{ticket.Title} {ticket.Description} {string.Join(' ', ticket.Tags)}".ToLowerInvariant();

        var scored = snapshot
            .Select(entry =>
            {
                var matches = entry.Keywords.Count(k => haystack.Contains(k, StringComparison.OrdinalIgnoreCase));
                var score = entry.Keywords.Count == 0 ? 0.0 : (double)matches / entry.Keywords.Count;
                return (entry, score);
            })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(3)
            .Select(x => new KnowledgeReference
            {
                Key = x.entry.Key,
                Title = x.entry.Title,
                SourceType = KnowledgeSourceType.JiraIssue,
                Url = x.entry.Url,
                Snippet = x.entry.Snippet,
                RelevanceScore = Math.Round(x.score, 2)
            })
            .ToList();

        return scored;
    }

    private sealed record SnapshotEntry
    {
        public required string Key { get; init; }
        public required string Title { get; init; }
        public required string Url { get; init; }
        public required string Snippet { get; init; }
        public List<string> Keywords { get; init; } = new();
    }
}
