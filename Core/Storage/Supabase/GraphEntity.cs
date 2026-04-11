using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace AgentFlow.Backend.Core.Storage.Supabase;

[Table("graphs")]
public sealed class GraphEntity : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("definition_json")]
    public string DefinitionJson { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
    
    [Column("version")]
    public int Version { get; set; }
}
