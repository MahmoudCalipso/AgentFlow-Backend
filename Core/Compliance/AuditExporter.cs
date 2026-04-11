using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Compliance;

public interface IAuditExporter
{
    Task<string> ExportAsync(AuditExportRequest request, CancellationToken ct);
}

public sealed record AuditExportRequest(
    string TenantId,
    DateTimeOffset From,
    DateTimeOffset To,
    string Format = "json");

/// <summary>
/// In-process audit log accumulator. Stores entries that the AuditExporter can query.
/// Register as Singleton. The OpenTelemetryAuditLogger also writes to structured logs;
/// this service allows in-process querying for compliance export endpoints.
/// </summary>
public sealed class AuditLogStore
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<AuditLogEntry> _entries = new();

    public void Add(AuditLogEntry entry) => _entries.Enqueue(entry);

    public IReadOnlyList<AuditLogEntry> Query(string tenantId, DateTimeOffset from, DateTimeOffset to)
        => System.Linq.Enumerable.Where(_entries, e =>
                string.Equals(e.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                && e.Timestamp >= from && e.Timestamp <= to)
            .ToList();
}

public sealed record AuditLogEntry(
    string CorrelationId,
    string TenantId,
    string NodeId,
    string Action,
    DateTimeOffset Timestamp,
    bool Success,
    double CostUsd = 0,
    string? Details = null);

public sealed class AuditExporter : IAuditExporter
{
    private readonly AuditLogStore _store;
    private readonly ILogger<AuditExporter> _log;

    public AuditExporter(AuditLogStore store, ILogger<AuditExporter> log)
    {
        _store = store;
        _log   = log;
    }

    public Task<string> ExportAsync(AuditExportRequest request, CancellationToken ct)
    {
        _log.LogInformation("[AuditExporter] Exporting {Format} audit for tenant {Tenant} [{From} → {To}]",
            request.Format, request.TenantId, request.From, request.To);

        var entries = _store.Query(request.TenantId, request.From, request.To);
        var output  = request.Format.ToLowerInvariant() == "csv" ? ToCsv(entries) : ToJson(entries);
        return Task.FromResult(output);
    }

    private static string ToJson(IReadOnlyList<AuditLogEntry> entries)
        => JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });

    private static string ToCsv(IReadOnlyList<AuditLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CorrelationId,TenantId,NodeId,Action,Timestamp,Success,CostUsd,Details");
        foreach (var e in entries)
        {
            var details = (e.Details ?? "").Replace("\"", "\"\"");
            sb.AppendLine($"\"{e.CorrelationId}\",\"{e.TenantId}\",\"{e.NodeId}\",\"{e.Action}\",\"{e.Timestamp:O}\",{e.Success},{e.CostUsd:F6},\"{details}\"");
        }
        return sb.ToString();
    }
}
