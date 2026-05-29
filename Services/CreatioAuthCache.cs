using System.Collections.Concurrent;

namespace ChatBridgeService.Services;

// Singleton — menyimpan auth cookie Creatio per instance agar tidak login ulang setiap request
public class CreatioAuthCache
{
    private record AuthEntry(string Cookie, string Csrf, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<Guid, AuthEntry> _cache = new();

    public bool TryGet(Guid instanceId, out string cookie, out string csrf)
    {
        if (_cache.TryGetValue(instanceId, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            cookie = entry.Cookie;
            csrf = entry.Csrf;
            return true;
        }
        cookie = csrf = "";
        return false;
    }

    public void Set(Guid instanceId, string cookie, string csrf)
    {
        // Cookie Creatio biasanya valid 20 menit, cache 15 menit untuk safety margin
        _cache[instanceId] = new AuthEntry(cookie, csrf, DateTime.UtcNow.AddMinutes(15));
    }

    public void Invalidate(Guid instanceId) => _cache.TryRemove(instanceId, out _);
}
