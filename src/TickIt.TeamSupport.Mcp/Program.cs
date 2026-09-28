using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TickIt.Core;
using TickIt.TeamSupport.Mcp.Adapters;

var builder = Host.CreateApplicationBuilder(args);

// Route all logging to stderr - stdout is reserved for MCP protocol frames on stdio transport.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Single registration point: swap this for a real Team Support API adapter once a
// sandbox/credential exists (see first-line-support-poc-architecture.md, item 3).
// Nothing else in this project needs to change.
builder.Services.AddSingleton<ITicketSource, MockTeamSupportAdapter>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
