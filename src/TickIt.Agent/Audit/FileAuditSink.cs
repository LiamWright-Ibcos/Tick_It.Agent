using System.Text.Json;
using Microsoft.Extensions.Logging;
using TickIt.Core;

namespace TickIt.Agent.Audit;

public sealed class FileAuditSinkOptions
{
    public const string SectionName = "Audit";

    public string LogFilePath { get; set; } = "audit-log.jsonl";
}

/// <summary>
/// Append-only JSON-lines audit trail of every observation, classification, tier decision
/// and (dry-run) action. This is the record a human reviewer or the eventual kill switch
/// design (still entirely conceptual per the Draft 2 findings) would need to inspect.
/// </summary>
public sealed class FileAuditSink : IAuditSink
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _path;
    private readonly ILogger<FileAuditSink> _logger;

    public FileAuditSink(FileAuditSinkOptions options, ILogger<FileAuditSink> logger)
    {
        _path = options.LogFilePath;
        _logger = logger;
    }

    public async Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(entry, JsonOptions);

        await WriteLock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_path, line + Environment.NewLine, ct);
        }
        finally
        {
            WriteLock.Release();
        }

        _logger.LogDebug("Audit: {Kind} for {TicketId}: {Summary}", entry.Kind, entry.TicketId, entry.Summary);
    }
}
