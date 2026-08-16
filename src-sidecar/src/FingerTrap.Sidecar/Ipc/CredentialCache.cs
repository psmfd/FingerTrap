using System.Collections.Concurrent;

namespace FingerTrap.Sidecar.Ipc;

/// <summary>
/// In-memory-only store for provider tokens delivered by the shell over
/// stdin (`credentials/set`, ADR-0022). Never persisted, never enumerated
/// into any RPC response, never logged — the token's whole lifecycle in this
/// process is: arrive on stdin, sit here, be read into an outbound
/// <c>Authorization</c> header. The shell owns durable storage (OS keychain)
/// and re-pushes on sidecar respawn.
/// </summary>
internal sealed class CredentialCache
{
    private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.Ordinal);

    /// <summary>A null or empty token clears the provider's entry.</summary>
    public void Set(string provider, string? token)
    {
        ArgumentException.ThrowIfNullOrEmpty(provider);
        if (string.IsNullOrEmpty(token))
        {
            _tokens.TryRemove(provider, out _);
        }
        else
        {
            _tokens[provider] = token;
        }
    }

    public bool TryGet(string provider, out string token)
    {
        if (_tokens.TryGetValue(provider, out var value))
        {
            token = value;
            return true;
        }

        token = string.Empty;
        return false;
    }

    public bool Has(string provider) => _tokens.ContainsKey(provider);
}
