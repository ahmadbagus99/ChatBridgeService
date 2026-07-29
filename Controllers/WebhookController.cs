using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using ChatBridgeService.Models;
using ChatBridgeService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatBridgeService.Controllers;

[ApiController]
public class WebhookController : ControllerBase
{
    private readonly IInstanceService _instances;
    private readonly IMetaWebhookParser _parser;
    private readonly IKirimDevConversationService _kirimDevConversations;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IInstanceService instances,
        IMetaWebhookParser parser,
        IKirimDevConversationService kirimDevConversations,
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookController> logger)
    {
        _instances = instances;
        _parser = parser;
        _kirimDevConversations = kirimDevConversations;
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

        string rawBody;
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            rawBody = await reader.ReadToEndAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read webhook payload for instance {Name}", instance.Name);
            return Ok();
        }

        if (IsKirimDev(instance) && !VerifyKirimDevSignature(instance, rawBody))
        {
            _logger.LogWarning("KirimDev webhook signature failed for instance {Name}", instance.Name);
            return Unauthorized();
        }

        MetaWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<MetaWebhookPayload>(
                rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse webhook payload for instance {Name}", instance.Name);
            return Ok();
        }

        if (payload == null) return Ok();

        var messages = _parser.Parse(payload).ToList();
        var statuses = _parser.ParseStatuses(payload).ToList();
        _logger.LogInformation("Received {MsgCount} message(s), {StCount} status update(s) for instance {Name}",
            messages.Count, statuses.Count, instance.Name);

        if (IsKirimDev(instance) && messages.Count > 0)
        {
            string? conversationId = TryGetKirimDevConversationId(rawBody);
            if (!string.IsNullOrEmpty(conversationId))
            {
                foreach (var msg in messages)
                {
                    try
                    {
                        await _kirimDevConversations.UpsertAsync(instance.Id, msg.From, conversationId, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to store KirimDev conversation id for {From}", msg.From);
                    }
                }
            }
        }

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

            foreach (var st in statuses)
            {
                try
                {
                    await forwarder.ForwardStatusAsync(instance, st);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to forward status {Status} for {Id}", st.Status, st.MetaMessageId);
                }
            }
        }, CancellationToken.None);

        return Ok();
    }

    private static bool IsKirimDev(CreatioInstance instance) =>
        string.Equals(instance.WhatsAppProvider, "KirimDev", StringComparison.OrdinalIgnoreCase);

    // KirimDev enriches the Meta passthrough payload with a top-level "kirim" object,
    // e.g. { "kirim": { "conversation_id": "cnv_...", ... }, "object": "...", "entry": [...] }.
    private static string? TryGetKirimDevConversationId(string rawBody)
    {
        try
        {
            var node = JsonNode.Parse(rawBody);
            return node?["kirim"]?["conversation_id"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private bool VerifyKirimDevSignature(CreatioInstance instance, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(instance.KirimDevWebhookSecret))
        {
            _logger.LogWarning("KirimDev webhook secret is empty for instance {Name}; skipping signature verification", instance.Name);
            return true;
        }

        string signatureHeader = Request.Headers["X-Kirim-Signature"].FirstOrDefault() ?? "";
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;

        string? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "t") timestamp = kv[1];
            if (kv[0] == "v1") signatures.Add(kv[1]);
        }

        if (string.IsNullOrWhiteSpace(timestamp) || signatures.Count == 0) return false;

        string signedPayload = $"{timestamp}.{rawBody}";
        byte[] secretBytes = Encoding.UTF8.GetBytes(instance.KirimDevWebhookSecret);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(signedPayload);
        byte[] expectedBytes = HMACSHA256.HashData(secretBytes, payloadBytes);
        string expectedHex = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        return signatures.Any(sig =>
            sig.Length == expectedHex.Length &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(sig.ToLowerInvariant()),
                Encoding.UTF8.GetBytes(expectedHex)));
    }
}
