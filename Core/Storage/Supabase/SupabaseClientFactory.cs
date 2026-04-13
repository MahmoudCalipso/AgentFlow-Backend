using Supabase;
using Microsoft.Extensions.Configuration;

namespace AgentFlow.Backend.Core.Storage.Supabase;

public interface ISupabaseClientFactory
{
    Client CreateClient();
}

public sealed class SupabaseClientFactory : ISupabaseClientFactory
{
    private readonly string _url;
    private readonly string _key;

    public SupabaseClientFactory(IConfiguration config)
    {
        var section = config.GetSection("SupabaseConnection");
        _url = section["Url"] ?? throw new InvalidOperationException("Missing SupabaseConnection:Url");
        _key = section["Key"] ?? throw new InvalidOperationException("Missing SupabaseConnection:Key");
    }

    public Client CreateClient()
    {
        return new Client(_url, _key, new SupabaseOptions { AutoConnectRealtime = true });
    }
}
