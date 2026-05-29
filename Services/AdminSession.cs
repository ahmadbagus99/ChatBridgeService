using System.Collections.Concurrent;

namespace ChatBridgeService.Services;

// Singleton — manages admin login sessions in memory
public class AdminSession
{
    private readonly ConcurrentDictionary<string, DateTime> _tokens = new();

    public string CreateToken()
    {
        string token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "").Replace("/", "").Replace("=", "");
        _tokens[token] = DateTime.UtcNow.AddHours(8);
        return token;
    }

    public bool IsValid(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (_tokens.TryGetValue(token, out var expires) && expires > DateTime.UtcNow)
            return true;
        _tokens.TryRemove(token, out _);
        return false;
    }

    public void Revoke(string? token)
    {
        if (!string.IsNullOrEmpty(token))
            _tokens.TryRemove(token, out _);
    }
}
