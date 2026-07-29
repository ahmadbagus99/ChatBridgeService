using ChatBridgeService.Data;
using ChatBridgeService.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatBridgeService.Services;

public interface IKirimDevConversationService
{
    Task UpsertAsync(Guid instanceId, string phoneNumber, string conversationId, CancellationToken ct = default);
    Task<string?> GetConversationIdAsync(Guid instanceId, string phoneNumber, CancellationToken ct = default);
}

public class KirimDevConversationService : IKirimDevConversationService
{
    private readonly AppDbContext _db;

    public KirimDevConversationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task UpsertAsync(Guid instanceId, string phoneNumber, string conversationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(conversationId))
            return;

        var existing = await _db.KirimDevConversations
            .FirstOrDefaultAsync(x => x.InstanceId == instanceId && x.PhoneNumber == phoneNumber, ct);

        if (existing == null)
        {
            _db.KirimDevConversations.Add(new KirimDevConversation
            {
                InstanceId = instanceId,
                PhoneNumber = phoneNumber,
                ConversationId = conversationId,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (existing.ConversationId != conversationId)
        {
            existing.ConversationId = conversationId;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            return;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetConversationIdAsync(Guid instanceId, string phoneNumber, CancellationToken ct = default)
    {
        var entry = await _db.KirimDevConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InstanceId == instanceId && x.PhoneNumber == phoneNumber, ct);
        return entry?.ConversationId;
    }
}
