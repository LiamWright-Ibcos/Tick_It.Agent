using TickIt.Agent.Pipeline;
using TickIt.Core;

namespace TickIt.Agent.Host.Reporting;

/// <summary>Holds the most recent triage run's results so /report can serve them without re-running the pipeline.</summary>
public sealed class LatestReportStore
{
    private readonly object _lock = new();
    private string? _markdown;
    private IReadOnlyList<TriageResult> _results = Array.Empty<TriageResult>();

    public void Update(IReadOnlyList<TriageResult> results)
    {
        lock (_lock)
        {
            _results = results;
            _markdown = TriageReportWriter.Render(results, DateTimeOffset.UtcNow);
        }
    }

    public string? GetMarkdown()
    {
        lock (_lock)
        {
            return _markdown;
        }
    }

    public IReadOnlyList<TriageResult> GetResults()
    {
        lock (_lock)
        {
            return _results;
        }
    }
}
