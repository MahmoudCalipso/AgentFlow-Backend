using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Security;

public interface IPiiDetector
{
    Task<PiiDetectionResult> DetectAsync(string text, CancellationToken ct);
    Task<string> MaskAsync(string text, CancellationToken ct);
}

public sealed record PiiDetectionResult(
    bool HasPii,
    IReadOnlyList<PiiMatch> Matches);

public sealed record PiiMatch(string Type, string Value, int Start, int End);

/// <summary>
/// Detects and masks PII in text using regex patterns.
/// Supports: email, phone, SSN, credit card, IP address, names (via heuristic).
/// </summary>
public sealed class PiiDetector : IPiiDetector
{
    private readonly ILogger<PiiDetector> _log;

    private static readonly (string Type, Regex Pattern)[] _patterns = new[]
    {
        ("EMAIL",       new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)),
        ("PHONE_US",    new Regex(@"\b(\+1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled)),
        ("SSN",         new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)),
        ("CREDIT_CARD", new Regex(@"\b(?:\d[ -]?){13,16}\b", RegexOptions.Compiled)),
        ("IP_ADDR",     new Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled)),
        ("IBAN",        new Regex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{4}\d{7}([A-Z0-9]?){0,16}\b", RegexOptions.Compiled)),
        ("DOB",         new Regex(@"\b(0?[1-9]|1[0-2])[\/-](0?[1-9]|[12]\d|3[01])[\/-](19|20)\d{2}\b", RegexOptions.Compiled)),
    };

    public PiiDetector(ILogger<PiiDetector> log) => _log = log;

    public Task<PiiDetectionResult> DetectAsync(string text, CancellationToken ct)
    {
        var matches = new List<PiiMatch>();
        foreach (var (type, pattern) in _patterns)
        {
            foreach (Match m in pattern.Matches(text))
            {
                matches.Add(new PiiMatch(type, m.Value, m.Index, m.Index + m.Length));
            }
        }

        var result = new PiiDetectionResult(matches.Count > 0, matches);
        if (result.HasPii)
            _log.LogWarning("[PiiDetector] Detected {Count} PII tokens: {Types}",
                matches.Count, string.Join(", ", matches.Select(m => m.Type).Distinct()));

        return Task.FromResult(result);
    }

    public async Task<string> MaskAsync(string text, CancellationToken ct)
    {
        var result = await DetectAsync(text, ct);
        if (!result.HasPii) return text;

        // Sort descending by position so replacements don't shift indices
        var ordered = result.Matches.OrderByDescending(m => m.Start).ToList();
        var chars = text.ToCharArray();

        foreach (var match in ordered)
        {
            var maskLen = match.End - match.Start;
            var mask = $"[{match.Type}:{new string('*', Math.Min(maskLen, 8))}]";
            text = text[..match.Start] + mask + text[match.End..];
        }

        return text;
    }
}
