using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Security;

public interface INodePolicyEngine
{
    Task<PolicyResult> EvaluateAsync(string nodeId, string tenantId, IDictionary<string, object?> data, CancellationToken ct);
}

public sealed record PolicyResult(bool Allowed, string? DenyReason, IDictionary<string, object?> SanitizedData);

public sealed class NodePolicyEngine : INodePolicyEngine
{
    private readonly ILogger<NodePolicyEngine> _log;

    private static readonly Regex _creditCardRegex = new(@"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex _ssnRegex = new(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex _emailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled);
    private static readonly Regex _apiKeyRegex = new(@"(?i)(api[_-]?key|secret|token|password)\s*[:=]\s*\S+", RegexOptions.Compiled);

    private static readonly HashSet<string> _blockedEgressDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "169.254.169.254",
        "metadata.google.internal",
        "metadata.azure.com"
    };

    public NodePolicyEngine(ILogger<NodePolicyEngine> log)
    {
        _log = log;
    }

    public Task<PolicyResult> EvaluateAsync(string nodeId, string tenantId, IDictionary<string, object?> data, CancellationToken ct)
    {
        var sanitizedData = new Dictionary<string, object?>(data);
        var violations = new List<string>();

        foreach (var key in sanitizedData.Keys.ToList())
        {
            var val = sanitizedData[key]?.ToString();
            if (string.IsNullOrEmpty(val)) continue;

            if (_creditCardRegex.IsMatch(val))
            {
                sanitizedData[key] = _creditCardRegex.Replace(val, "[REDACTED-CC]");
                violations.Add($"PII:CreditCard in field {key}");
                _log.LogWarning("PII detected (CreditCard) in node {NodeId} tenant {TenantId} field {Key}", nodeId, tenantId, key);
            }

            if (_ssnRegex.IsMatch(val))
            {
                sanitizedData[key] = _ssnRegex.Replace(val, "[REDACTED-SSN]");
                violations.Add($"PII:SSN in field {key}");
                _log.LogWarning("PII detected (SSN) in node {NodeId} tenant {TenantId} field {Key}", nodeId, tenantId, key);
            }

            if (_emailRegex.IsMatch(val))
            {
                sanitizedData[key] = _emailRegex.Replace(val, "[REDACTED-EMAIL]");
                _log.LogDebug("PII detected (Email) in node {NodeId} field {Key}", nodeId, key);
            }

            if (_apiKeyRegex.IsMatch(val))
            {
                sanitizedData[key] = _apiKeyRegex.Replace(val, m => $"{m.Groups[1].Value}=[REDACTED]");
                violations.Add($"SecretLeak in field {key}");
                _log.LogWarning("Potential secret leak in node {NodeId} tenant {TenantId} field {Key}", nodeId, tenantId, key);
            }

            if (data.TryGetValue("url", out var urlVal) && urlVal is string urlStr)
            {
                if (IsBlockedEgressTarget(urlStr))
                {
                    _log.LogError("Blocked egress attempt to {Url} from node {NodeId}", urlStr, nodeId);
                    return Task.FromResult(new PolicyResult(false, $"Egress to {urlStr} is blocked by security policy.", sanitizedData));
                }
            }
        }

        return Task.FromResult(new PolicyResult(true, null, sanitizedData));
    }

    private static bool IsBlockedEgressTarget(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return _blockedEgressDomains.Contains(uri.Host);
        }
        return false;
    }
}
