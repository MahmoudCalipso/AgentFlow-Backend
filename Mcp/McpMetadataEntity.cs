using System;
using Postgrest.Models;
using Postgrest.Attributes;

namespace AgentFlow.Backend.Mcp;

public sealed class McpMetadataEntity : BaseModel
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("server_name")]
    public string ServerName { get; set; } = string.Empty;

    [Column("server_url")]
    public string ServerUrl { get; set; } = string.Empty;

    [Column("last_updated")]
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
