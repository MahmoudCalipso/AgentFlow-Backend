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
        _url = config["SUPABASE_URL"] ?? throw new InvalidOperationException("Missing SUPABASE_URL");
        _key = config["SUPABASE_KEY"] ?? throw new InvalidOperationException("Missing SUPABASE_KEY");
    }

    public Client CreateClient()
    {
        return new Client(_url, _key, new SupabaseOptions { AutoConnectRealtime = true });
    }
}
