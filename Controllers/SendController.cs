using ChatBridgeService.Models;
using ChatBridgeService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatBridgeService.Controllers;

[ApiController]
public class SendController : ControllerBase
{
    private readonly IInstanceService _instances;
    private readonly IMetaMessageSender _sender;
    private readonly ILogger<SendController> _logger;

    public SendController(IInstanceService instances, IMetaMessageSender sender, ILogger<SendController> logger)
    {
        _instances = instances;
        _sender = sender;
        _logger = logger;
    }

    [HttpPost("send/{apiKey}/text")]
    public async Task<IActionResult> Text(string apiKey, [FromBody] SendTextRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(apiKey, ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.Body))
            return BadRequest(new { error = "Fields 'To' and 'Body' are required" });

        _logger.LogInformation("[{Name}] Send text ke {To}", instance.Name, request.To);
        var result = await _sender.SendTextAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    [HttpPost("send/{apiKey}/buttons")]
    public async Task<IActionResult> Buttons(string apiKey, [FromBody] SendButtonsRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(apiKey, ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || request.Buttons.Count == 0)
            return BadRequest(new { error = "Fields 'To' and at least 1 button are required" });

        _logger.LogInformation("[{Name}] Send buttons ke {To}", instance.Name, request.To);
        var result = await _sender.SendButtonsAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    [HttpPost("send/{apiKey}/list")]
    public async Task<IActionResult> List(string apiKey, [FromBody] SendListRequest request, CancellationToken ct)
    {
        var instance = await ResolveInstanceAsync(apiKey, ct);
        if (instance == null) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || request.Rows.Count == 0)
            return BadRequest(new { error = "Fields 'To' and at least 1 row are required" });

        _logger.LogInformation("[{Name}] Send list ke {To}", instance.Name, request.To);
        var result = await _sender.SendListAsync(instance, request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    private Task<ChatBridgeService.Models.CreatioInstance?> ResolveInstanceAsync(string apiKey, CancellationToken ct) =>
        _instances.GetByApiKeyAsync(apiKey, ct);
}
