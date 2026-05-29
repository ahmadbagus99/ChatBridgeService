using System.Text.Json;
using ChatBridgeService.Models;
using ChatBridgeService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatBridgeService.Controllers;

[ApiController]
public class WebhookController : ControllerBase
{
    private readonly IInstanceService _instances;
    private readonly IMetaWebhookParser _parser;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IInstanceService instances,
        IMetaWebhookParser parser,
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookController> logger)
    {
        _instances = instances;
        _parser = parser;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpGet("webhook/{apiKey}")]
    public async Task<IActionResult> Verify(
        string apiKey,
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken,
        CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return NotFound();

        if (mode == "subscribe" && verifyToken == instance.MetaVerifyToken)
        {
            _logger.LogInformation("Webhook verified for instance {Name}", instance.Name);
            return Ok(int.Parse(challenge));
        }

        _logger.LogWarning("Webhook verification failed for instance {Name} — token mismatch", instance.Name);
        return Forbid();
    }

    [HttpPost("webhook/{apiKey}")]
    public async Task<IActionResult> Receive(string apiKey, CancellationToken ct)
    {
        var instance = await _instances.GetByApiKeyAsync(apiKey, ct);
        if (instance == null) return NotFound();

        MetaWebhookPayload? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<MetaWebhookPayload>(
                Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Meta webhook payload for instance {Name}", instance.Name);
            return Ok();
        }

        if (payload == null) return Ok();

        var messages = _parser.Parse(payload).ToList();
        _logger.LogInformation("Received {Count} message(s) for instance {Name}", messages.Count, instance.Name);

        // Buat scope baru agar DbContext tidak di-dispose saat request selesai
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var forwarder = scope.ServiceProvider.GetRequiredService<ICreatioForwarder>();

            foreach (var msg in messages)
            {
                try
                {
                    await forwarder.ForwardAsync(instance, msg);
                    _logger.LogInformation("Forwarded message {Id} from {From}", msg.MessageId, msg.From);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to forward message {Id}", msg.MessageId);
                }
            }
        }, CancellationToken.None);

        return Ok();
    }
}
