using TickIt.Agent.Pipeline;

namespace TickIt.Agent.Host.Reporting;

/// <summary>
/// Periodically runs the pipeline against whatever is currently in "New" status - the
/// background-polling equivalent of manually invoking the Node PoC's pipeline. Manual
/// runs are also available via POST /trigger for on-demand use (e.g. during a demo).
/// </summary>
public sealed class TriagePollingWorker : BackgroundService
{
    private readonly AgentPipeline _pipeline;
    private readonly LatestReportStore _store;
    private readonly ILogger<TriagePollingWorker> _logger;
    private readonly TimeSpan _interval;

    public TriagePollingWorker(
        AgentPipeline pipeline,
        LatestReportStore store,
        IConfiguration configuration,
        ILogger<TriagePollingWorker> logger)
    {
        _pipeline = pipeline;
        _store = store;
        _logger = logger;

        var seconds = configuration.GetValue<int?>("Polling:IntervalSeconds") ?? 300;
        _interval = TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var results = await _pipeline.RunAsync(stoppingToken);
                _store.Update(results);
                _logger.LogInformation("Triage run complete: {Count} ticket(s) processed.", results.Count);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Triage run failed.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
