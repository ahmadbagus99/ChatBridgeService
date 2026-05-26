using System.Text.Json;
using ChatBridgeService.Models;
using ChatBridgeService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatBridgeService.Controllers;

[ApiController]
[Route("webhook")]
public class WebhookController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IMetaWebhookParser _parser;
    private readonly ICreatioForwarder _forwarder;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IConfiguration config,
        IMetaWebhookParser parser,
        ICreatioForwarder forwarder,
        ILogger<WebhookController> logger)
    {
        _config = config;
        _parser = parser;
        _forwarder = forwarder;
        _logger = logger;
    }

    /// <summary>
    /// Meta webhook verification handshake.
    /// Meta mengirim GET ke sini saat pertama kali setup webhook.
    /// </summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
    {
        string expectedToken = _config["Meta:VerifyToken"] ?? string.Empty;

        if (mode == "subscribe" && verifyToken == expectedToken)
        {
            _logger.LogInformation("Webhook verified successfully");
            return Ok(int.Parse(challenge));
        }

        _logger.LogWarning("Webhook verification failed — token mismatch");
        return Forbid();
    }

    /// <summary>
    /// Menerima pesan masuk dari Meta Cloud API.
    /// Meta menggunakan fire-and-forget: harus balas 200 dalam 20 detik.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
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
            _logger.LogError(ex, "Failed to parse Meta webhook payload");
            return Ok(); // Tetap 200 agar Meta tidak retry terus
        }

        if (payload == null) return Ok();

        var messages = _parser.Parse(payload).ToList();
        _logger.LogInformation("Received {Count} message(s) from Meta", messages.Count);

        // Forward ke Creatio secara fire-and-forget, tidak block response ke Meta
        _ = Task.Run(async () =>
        {
            foreach (var msg in messages)
            {
                try
                {
                    await _forwarder.ForwardAsync(msg);
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
