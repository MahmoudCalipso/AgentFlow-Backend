using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace AgentFlow.Backend.Core.Storage.Supabase;

[Table("execution_logs")]
public sealed class ExecutionLogEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = null!; // LogId

    [Column("correlation_id")]
    public string CorrelationId { get; set; } = null!;

    [Column("graph_id")]
    public string GraphId { get; set; } = null!;

    [Column("node_id")]
    public string? NodeId { get; set; }

    [Column("success")]
    public bool Success { get; set; }

    [Column("error")]
    public string? Error { get; set; }

    [Column("output_json")]
    public string? OutputJson { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    [Column("start_time")]
    public DateTime? StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }
}
