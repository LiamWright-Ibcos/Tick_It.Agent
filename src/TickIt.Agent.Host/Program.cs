using Microsoft.Extensions.Options;
using TickIt.Agent.Actions;
using TickIt.Agent.Audit;
using TickIt.Agent.Classification;
using TickIt.Agent.Host.Mcp;
using TickIt.Agent.Host.Reporting;
using TickIt.Agent.Pipeline;
using TickIt.Atlassian;
using TickIt.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ClassifierOptions>(builder.Configuration.GetSection(ClassifierOptions.SectionName));
builder.Services.Configure<AtlassianOptions>(builder.Configuration.GetSection(AtlassianOptions.SectionName));
builder.Services.Configure<ActionExecutorOptions>(builder.Configuration.GetSection(ActionExecutorOptions.SectionName));
builder.Services.Configure<TeamsNotificationOptions>(builder.Configuration.GetSection(TeamsNotificationOptions.SectionName));

builder.Services.AddSingleton(new FileAuditSinkOptions
{
    LogFilePath = builder.Configuration["Audit:LogFilePath"] ?? "audit-log.jsonl"
});
builder.Services.AddSingleton<IAuditSink, FileAuditSink>();

builder.Services.AddSingleton<TierPolicy>();
builder.Services.AddSingleton<ActionProposer>();

// Effective action executor: DryRunActionExecutor unless ActionExecutor:UseLiveExecutor is
// deliberately set true. That flag alone does not bypass LiveActionExecutor's own
// per-action-type/per-tier/credential gates - see its class remarks and
// docs/live-action-executor-signoff.md before ever setting it true outside local testing.
builder.Services.AddSingleton<DryRunActionExecutor>();
builder.Services.AddHttpClient<LiveActionExecutor>();
builder.Services.AddSingleton<IActionExecutor>(sp =>
{
    var gates = sp.GetRequiredService<IOptions<ActionExecutorOptions>>().Value;
    return gates.UseLiveExecutor
        ? sp.GetRequiredService<LiveActionExecutor>()
        : sp.GetRequiredService<DryRunActionExecutor>();
});

// Grounding: fixture snapshot unless a real Atlassian credential is configured.
builder.Services.AddHttpClient<LiveAtlassianKnowledgeGrounder>();
builder.Services.AddSingleton<IKnowledgeGrounder>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AtlassianOptions>>().Value;
    return options.IsLiveConfigured
        ? sp.GetRequiredService<LiveAtlassianKnowledgeGrounder>()
        : new FixtureKnowledgeGrounder();
});

// Classification: simulation unless a real Anthropic API key is configured.
builder.Services.AddHttpClient<LiveClaudeClassifier>();
builder.Services.AddSingleton<IClassifier>(sp =>
{
    var options = sp.GetRequiredService<IOptions<ClassifierOptions>>().Value;
    return options.IsLiveConfigured
        ? sp.GetRequiredService<LiveClaudeClassifier>()
        : new SimulationClassifier();
});

// Ticket source: a real MCP client talking to the separate TickIt.TeamSupport.Mcp process.
var teamSupportMcpDllPath = ResolveTeamSupportMcpDllPath(builder.Configuration);
var mcpTicketSource = await McpTicketSource.ConnectAsync(teamSupportMcpDllPath);
builder.Services.AddSingleton<ITicketSource>(mcpTicketSource);

builder.Services.AddSingleton<AgentPipeline>();
builder.Services.AddSingleton<LatestReportStore>();
builder.Services.AddHostedService<TriagePollingWorker>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

app.MapPost("/trigger", async (AgentPipeline pipeline, LatestReportStore store, CancellationToken ct) =>
{
    var results = await pipeline.RunAsync(ct);
    store.Update(results);
    return Results.Ok(new { ticketsProcessed = results.Count });
});

app.MapGet("/report", (LatestReportStore store) =>
{
    var report = store.GetMarkdown();
    return report is null
        ? Results.NotFound(new { message = "No triage run has completed yet. POST /trigger first." })
        : Results.Text(report, "text/markdown");
});

app.Lifetime.ApplicationStopping.Register(() => mcpTicketSource.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5)));

app.Run();

static string ResolveTeamSupportMcpDllPath(IConfiguration configuration)
{
    var configured = configuration["McpServer:DllPath"];
    if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
    {
        return configured;
    }

    // Dev-time convenience: walk up from this process's own build output looking for the
    // "src" directory, then descend into the sibling TickIt.TeamSupport.Mcp project's own
    // build output. Framework-version and Debug/Release-agnostic by design, since walking
    // up avoids hard-coding how many directories deep dotnet's output layout happens to be.
    var buildOutputDir = Path.GetFileName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)); // e.g. "net8.0"
    var buildConfigDir = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)); // .../bin/<Config>
    var buildConfig = buildConfigDir?.Name; // e.g. "Debug"

    var current = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
    for (var i = 0; i < 8 && current is not null; i++, current = current.Parent)
    {
        var sibling = Path.Combine(current.FullName, "TickIt.TeamSupport.Mcp", "bin", buildConfig ?? "Debug", buildOutputDir, "TickIt.TeamSupport.Mcp.dll");
        if (File.Exists(sibling))
        {
            return sibling;
        }
    }

    throw new InvalidOperationException(
        "Could not locate TickIt.TeamSupport.Mcp.dll. Build TickIt.TeamSupport.Mcp first, or set " +
        "McpServer:DllPath in appsettings.json to its published/build output path.");
}
