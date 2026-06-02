using System.Collections.Concurrent;

namespace ChatBridgeService.Services;

// Singleton — cache OAuth Bearer token Creatio per instance
public class CreatioAuthCache
{
    private record AuthEntry(string Token, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<Guid, AuthEntry> _cache = new();

    public bool TryGet(Guid instanceId, out string token)
    {
        if (_cache.TryGetValue(instanceId, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            token = entry.Token;
            return true;
        }
        token = "";
        return false;
    }

    public void Set(Guid instanceId, string token, int expiresInSeconds)
    {
        _cache[instanceId] = new AuthEntry(token, DateTime.UtcNow.AddSeconds(expiresInSeconds - 60));
    }

    public void Invalidate(Guid instanceId) => _cache.TryRemove(instanceId, out _);
}
