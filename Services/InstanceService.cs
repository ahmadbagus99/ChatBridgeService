using ChatBridgeService.Data;
using ChatBridgeService.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatBridgeService.Services;

public interface IInstanceService
{
    Task<CreatioInstance?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default);
    Task<List<CreatioInstance>> GetAllAsync(CancellationToken ct = default);
    Task<CreatioInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CreatioInstance> CreateAsync(CreatioInstance instance, CancellationToken ct = default);
    Task<CreatioInstance> UpdateAsync(CreatioInstance instance, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class InstanceService : IInstanceService
{
    private readonly AppDbContext _db;

    public InstanceService(AppDbContext db) => _db = db;

    public Task<CreatioInstance?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default) =>
        _db.CreatioInstances.FirstOrDefaultAsync(x => x.ApiKey == apiKey && x.IsActive, ct);

    public Task<List<CreatioInstance>> GetAllAsync(CancellationToken ct = default) =>
        _db.CreatioInstances.OrderBy(x => x.Name).ToListAsync(ct);

    public Task<CreatioInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CreatioInstances.FindAsync([id], ct).AsTask();

    public async Task<CreatioInstance> CreateAsync(CreatioInstance instance, CancellationToken ct = default)
    {
        instance.Id = Guid.NewGuid();
        instance.CreatedAt = instance.UpdatedAt = DateTime.UtcNow;
        _db.CreatioInstances.Add(instance);
        await _db.SaveChangesAsync(ct);
        return instance;
    }

    public async Task<CreatioInstance> UpdateAsync(CreatioInstance instance, CancellationToken ct = default)
    {
        instance.UpdatedAt = DateTime.UtcNow;
        _db.CreatioInstances.Update(instance);
        await _db.SaveChangesAsync(ct);
        return instance;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var instance = await _db.CreatioInstances.FindAsync([id], ct);
        if (instance != null)
        {
            _db.CreatioInstances.Remove(instance);
            await _db.SaveChangesAsync(ct);
        }
    }
}
