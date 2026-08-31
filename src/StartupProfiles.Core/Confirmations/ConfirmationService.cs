using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace StartupProfiles.Core.Confirmations;

/// <summary>
/// Server-side enforcement of the "double confirm" rule for destructive operations. A caller first
/// requests a token bound to the exact action signature, then resubmits with that token. Tokens are
/// single-use and expire quickly, so an agent cannot skip the confirmation prompt.
/// </summary>
public sealed class ConfirmationService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(120);
    private readonly ConcurrentDictionary<string, (string Signature, DateTimeOffset Expires)> _tokens = new();

    /// <summary>Canonical signature for an action so a token cannot be reused for a different one.</summary>
    public static string Signature(string operation, string target) => $"{operation}|{target}";

    public string Issue(string signature)
    {
        Sweep();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _tokens[token] = (signature, DateTimeOffset.UtcNow.Add(Ttl));
        return token;
    }

    /// <summary>Validates and consumes a token; false if missing, expired, or bound to another action.</summary>
    public bool TryConsume(string? token, string signature)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (!_tokens.TryRemove(token, out var entry)) return false;
        return entry.Expires > DateTimeOffset.UtcNow && entry.Signature == signature;
    }

    private void Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _tokens)
            if (pair.Value.Expires <= now) _tokens.TryRemove(pair.Key, out _);
    }
}
