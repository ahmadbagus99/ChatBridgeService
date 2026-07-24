using ChatBridgeService.Models;

namespace ChatBridgeService.Services;

public class AutoCloseChatWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoCloseChatWorker> _logger;

    public AutoCloseChatWorker(IServiceScopeFactory scopeFactory, ILogger<AutoCloseChatWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessInstancesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto close chat worker cycle failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessInstancesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var instances = scope.ServiceProvider.GetRequiredService<IInstanceService>();
        var forwarder = scope.ServiceProvider.GetRequiredService<ICreatioForwarder>();

        List<CreatioInstance> allInstances = await instances.GetAllAsync(ct);
        foreach (CreatioInstance instance in allInstances.Where(x => x.IsActive))
        {
            try
            {
                string result = await forwarder.ProcessAutoCloseChatsAsync(instance, ct);
                _logger.LogDebug("Auto close processed for {Name}: {Result}", instance.Name, result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto close processing failed for instance {Name}", instance.Name);
            }
        }
    }
}
