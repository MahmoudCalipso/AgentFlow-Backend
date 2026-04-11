using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Nodes.Data;

public sealed class CryptoNode : INodeHandler
{
    private readonly ILogger<CryptoNode> _log;
    public string NodeId { get; }

    public CryptoNode(string nodeId, ILogger<CryptoNode> log)
    {
        NodeId = nodeId;
        _log   = log;
    }

    public async ValueTask<NodeResult> HandleAsync(NodeContext ctx, CancellationToken ct)
    {
        var operation = ctx.GetConfig<string>(NodeId, "operation", "hash-sha256");
        var key       = ctx.GetConfig<string>(NodeId, "key", "");
        var field     = ctx.GetConfig<string>(NodeId, "field", "data");

        var results = new List<ExecutionItem>();
        foreach (var item in ctx.InputItems)
        {
            ct.ThrowIfCancellationRequested();
            var payload = item.Data.TryGetValue(field, out var d) ? d?.ToString() ?? "" : "";

            var output = operation switch
            {
                "hash-sha256"   => HashSha256(payload),
                "hash-md5"      => HashMd5(payload),
                "hmac-sha256"   => HmacSha256(payload, key),
                "base64-encode" => Base64Encode(payload),
                "base64-decode" => Base64Decode(payload),
                "aes-encrypt"   => AesEncrypt(payload, key),
                "aes-decrypt"   => AesDecrypt(payload, key),
                _               => HashSha256(payload)
            };

            var outData = new Dictionary<string, object?>(item.Data) { ["result"] = output, ["operation"] = operation };
            results.Add(new ExecutionItem(outData));
        }

        _log.LogInformation("[CryptoNode] {Op} applied to {Count} items", operation, results.Count);
        await Task.CompletedTask;
        return NodeResult.Ok(new List<IReadOnlyList<ExecutionItem>> { results });
    }

    private static string HashSha256(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static string HashMd5(string input)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static string HmacSha256(string input, string key)
    {
        var kb = Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
        return Convert.ToHexString(HMACSHA256.HashData(kb, Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static string Base64Encode(string input)  => Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
    private static string Base64Decode(string input)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(input)); }
        catch { return $"[invalid-base64]"; }
    }

    private static string AesEncrypt(string plaintext, string key)
    {
        var kb = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        using var aes = Aes.Create();
        aes.Key = kb;
        aes.GenerateIV();
        using var ms        = new MemoryStream();
        ms.Write(aes.IV, 0, 16);
        using var enc       = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        var data = Encoding.UTF8.GetBytes(plaintext);
        enc.Write(data);
        enc.FlushFinalBlock();
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string AesDecrypt(string ciphertext, string key)
    {
        var kb        = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var full      = Convert.FromBase64String(ciphertext);
        using var aes = Aes.Create();
        aes.Key = kb;
        aes.IV  = full[..16];
        using var ms  = new MemoryStream(full[16..]);
        using var dec = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr  = new StreamReader(dec);
        return sr.ReadToEnd();
    }
}
