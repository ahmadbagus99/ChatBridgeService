using ChatBridgeService.Models;
using ChatBridgeService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatBridgeService.Controllers;

[ApiController]
[Route("send")]
public class SendController : ControllerBase
{
    private readonly IMetaMessageSender _sender;
    private readonly IConfiguration _config;
    private readonly ILogger<SendController> _logger;

    public SendController(IMetaMessageSender sender, IConfiguration config, ILogger<SendController> logger)
    {
        _sender = sender;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Kirim pesan teks biasa ke WhatsApp.
    /// Dipanggil dari Creatio Script Task.
    /// </summary>
    [HttpPost("text")]
    public async Task<IActionResult> Text([FromBody] SendTextRequest request, CancellationToken ct)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || string.IsNullOrEmpty(request.Body))
            return BadRequest(new { error = "Field 'To' dan 'Body' wajib diisi" });

        _logger.LogInformation("Send text ke {To}", request.To);
        var result = await _sender.SendTextAsync(request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    /// <summary>
    /// Kirim interactive buttons (max 3) — untuk chat tree menu pilihan.
    /// </summary>
    [HttpPost("buttons")]
    public async Task<IActionResult> Buttons([FromBody] SendButtonsRequest request, CancellationToken ct)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || request.Buttons.Count == 0)
            return BadRequest(new { error = "Field 'To' dan minimal 1 button wajib diisi" });

        _logger.LogInformation("Send buttons ke {To} ({Count} buttons)", request.To, request.Buttons.Count);
        var result = await _sender.SendButtonsAsync(request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    /// <summary>
    /// Kirim list message (max 10 baris) — untuk menu dengan banyak pilihan.
    /// </summary>
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] SendListRequest request, CancellationToken ct)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid API key" });
        if (string.IsNullOrEmpty(request.To) || request.Rows.Count == 0)
            return BadRequest(new { error = "Field 'To' dan minimal 1 row wajib diisi" });

        _logger.LogInformation("Send list ke {To} ({Count} rows)", request.To, request.Rows.Count);
        var result = await _sender.SendListAsync(request, ct);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }

    // Cek API key dari header X-Api-Key — cegah akses unauthorized ke sender endpoint
    private bool IsAuthorized()
    {
        string expected = _config["ChatBridge:ApiKey"] ?? string.Empty;
        if (string.IsNullOrEmpty(expected)) return true; // Kalau belum dikonfigurasi, skip auth
        Request.Headers.TryGetValue("X-Api-Key", out var provided);
        return provided == expected;
    }
}
