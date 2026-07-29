using ChatBridgeService.Models;
using ChatBridgeService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatBridgeService.Controllers;

[ApiController]
public class SendController : ControllerBase
{
    private readonly IInstanceService _instances;
    private readonly IMetaMessageSender _sender;
    private readonly IKirimDevConversationService _kirimDevConversations;
    private readonly ILogger<SendController> _logger;

    public SendController(
        IInstanceService instances,
        IMetaMessageSender sender,
        IKirimDevConversationService kirimDevConversations,
        ILogger<SendController> logger)
    {
        _instances = instances;
        _sender = sender;
        _kirimDevConversations = kirimDevConversations;
        _logger = logger;
    }

    [HttpPost("send/text")]
    public async Task<IActionResult> Text([FromBody] SendTextRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.Body))
            return BadRequest(new { error = "Fields 'To' and 'Body' are required" });

        _logger.LogInformation("[{Name}] Send text ke {To}", instance.Name, request.To);
        var result = await _sender.SendTextAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    [HttpPost("send/buttons")]
    public async Task<IActionResult> Buttons([FromBody] SendButtonsRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || request.Buttons.Count == 0)
            return BadRequest(new { error = "Fields 'To' and at least 1 button are required" });

        _logger.LogInformation("[{Name}] Send buttons ke {To}", instance.Name, request.To);
        var result = await _sender.SendButtonsAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    [HttpPost("send/list")]
    public async Task<IActionResult> List([FromBody] SendListRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || request.Rows.Count == 0)
            return BadRequest(new { error = "Fields 'To' and at least 1 row are required" });

        _logger.LogInformation("[{Name}] Send list ke {To}", instance.Name, request.To);
        var result = await _sender.SendListAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    [HttpPost("send/image")]
    public async Task<IActionResult> Image([FromBody] SendImageRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.MediaUrl))
            return BadRequest(new { error = "Fields 'To' and 'MediaUrl' are required" });

        _logger.LogInformation("[{Name}] Send image ke {To}", instance.Name, request.To);
        var result = await _sender.SendImageAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    [HttpPost("send/document")]
    public async Task<IActionResult> Document([FromBody] SendDocumentRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.MediaUrl))
            return BadRequest(new { error = "Fields 'To' and 'MediaUrl' are required" });

        _logger.LogInformation("[{Name}] Send document ke {To}", instance.Name, request.To);
        var result = await _sender.SendDocumentAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    [HttpPost("send/location")]
    public async Task<IActionResult> Location([FromBody] SendLocationRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To))
            return BadRequest(new { error = "Field 'To' is required" });

        _logger.LogInformation("[{Name}] Send location ke {To}", instance.Name, request.To);
        var result = await _sender.SendLocationAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    [HttpPost("send/cta")]
    public async Task<IActionResult> Cta([FromBody] SendCtaRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.Url))
            return BadRequest(new { error = "Fields 'To' and 'Url' are required" });

        _logger.LogInformation("[{Name}] Send cta ke {To}", instance.Name, request.To);
        var result = await _sender.SendCtaAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    // Called by Creatio when a chat is closed. No-op for Meta Cloud API instances (Meta has
    // no conversation-status concept); for KirimDev instances, marks the conversation resolved.
    [HttpPost("send/resolve")]
    public async Task<IActionResult> Resolve([FromBody] ResolveConversationRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To))
            return BadRequest(new { error = "Field 'To' is required" });

        if (!string.Equals(instance.WhatsAppProvider, "KirimDev", StringComparison.OrdinalIgnoreCase))
            return Ok(new SendResponse { Success = true, Skipped = true });

        string? conversationId = await _kirimDevConversations.GetConversationIdAsync(instance.Id, request.To, ct);
        if (string.IsNullOrEmpty(conversationId))
        {
            _logger.LogWarning("[{Name}] No KirimDev conversation id known for {To}, skipping resolve", instance.Name, request.To);
            return Ok(new SendResponse { Success = false, Error = "No KirimDev conversation known for this phone number" });
        }

        _logger.LogInformation("[{Name}] Resolving KirimDev conversation {ConversationId} for {To}", instance.Name, conversationId, request.To);
        var result = await _sender.ResolveConversationAsync(instance, conversationId, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    private Task<ChatBridgeService.Models.CreatioInstance?> ResolveInstanceAsync(CancellationToken ct)
    {
        string apiKey = Request.Headers["X-Api-Key"].FirstOrDefault() ?? "";
        return _instances.GetByApiKeyAsync(apiKey, ct);
    }
}
