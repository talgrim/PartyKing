using Microsoft.Extensions.Caching.Memory;
using PartyKing.Application.Ports;

namespace PartyKing.Infrastructure.Identity;

/// <summary>
/// The answers of the identity service, in a cache of its own: the application-wide <see cref="IMemoryCache"/> has no
/// size limit, and this one is keyed by what a stranger can send (a cookie value), so it must have one.
/// </summary>
internal sealed class IdentityCache : IDisposable
{
    private const int MaxEntries = 10_000;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaxEntries });

    public bool TryGet(string key, out IdentityCacheEntry? entry)
    {
        return _cache.TryGetValue(key, out entry);
    }

    public void Set(string key, IdentityCacheEntry entry, DateTimeOffset removeAt, DateTimeOffset now)
    {
        _cache.Set(
            key,
            entry,
            new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = removeAt - now });
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}

/// <summary>
/// An answer, the moment it stops being used without asking again, and the moment it stops being used at all (an
/// outage of the identity service keeps it in use until then). Both are checked against the same clock, never left to
/// the cache's own eviction.
/// </summary>
internal sealed record IdentityCacheEntry(IdentityResult Result, DateTimeOffset FreshUntil, DateTimeOffset StaleUntil);
