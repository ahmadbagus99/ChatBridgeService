using ChatBridgeService.Data;
using ChatBridgeService.Models;

namespace ChatBridgeService.Services;

public interface ILogService
{
    Task LogAsync(Guid instanceId, string type, string? phone, bool success, string? details, CancellationToken ct = default);
}

public class LogService : ILogService
{
    private readonly AppDbContext _db;

    public LogService(AppDbContext db) => _db = db;

    public async Task LogAsync(Guid instanceId, string type, string? phone, bool success, string? details, CancellationToken ct = default)
    {
        _db.MessageLogs.Add(new MessageLog
        {
            InstanceId = instanceId,
            Type = type,
            PhoneNumber = phone,
            Success = success,
            Details = details?[..Math.Min(1000, details.Length)],
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
