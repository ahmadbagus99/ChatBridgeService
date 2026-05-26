using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChatBridgeService.Models;

namespace ChatBridgeService.Services;

public interface IMetaMessageSender
{
    Task<SendResponse> SendTextAsync(SendTextRequest request, CancellationToken ct = default);
    Task<SendResponse> SendButtonsAsync(SendButtonsRequest request, CancellationToken ct = default);
    Task<SendResponse> SendListAsync(SendListRequest request, CancellationToken ct = default);
}

public class MetaMessageSender : IMetaMessageSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<MetaMessageSender> _logger;

    public MetaMessageSender(HttpClient http, IConfiguration config, ILogger<MetaMessageSender> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<SendResponse> SendTextAsync(SendTextRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "text",
            text = new { preview_url = false, body = request.Body }
        };

        return await PostAsync(request.PhoneNumberId, payload, ct);
    }

    public async Task<SendResponse> SendButtonsAsync(SendButtonsRequest request, CancellationToken ct = default)
    {
        // Meta maksimal 3 buttons, title max 20 karakter
        var buttons = request.Buttons
            .Take(3)
            .Select(b => new
            {
                type = "reply",
                reply = new
                {
                    id = b.Id,
                    title = b.Title.Length > 20 ? b.Title[..20] : b.Title
                }
            });

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "interactive",
            interactive = new
            {
                type = "button",
                body = new { text = request.BodyText },
                action = new { buttons }
            }
        };

        return await PostAsync(request.PhoneNumberId, payload, ct);
    }

    public async Task<SendResponse> SendListAsync(SendListRequest request, CancellationToken ct = default)
    {
        // Meta maksimal 10 rows dalam satu section
        var rows = request.Rows
            .Take(10)
            .Select(r => new
            {
                id = r.Id,
                title = r.Title.Length > 24 ? r.Title[..24] : r.Title,
                description = r.Description.Length > 72 ? r.Description[..72] : r.Description
            });

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "interactive",
            interactive = new
            {
                type = "list",
                body = new { text = request.BodyText },
                action = new
                {
                    button = request.ButtonLabel,
                    sections = new[]
                    {
                        new { rows }
                    }
                }
            }
        };

        return await PostAsync(request.PhoneNumberId, payload, ct);
    }

    private async Task<SendResponse> PostAsync(string? phoneNumberId, object payload, CancellationToken ct)
    {
        string resolvedPhoneNumberId = phoneNumberId ?? _config["Meta:PhoneNumberId"] ?? string.Empty;
        string accessToken = _config["Meta:AccessToken"] ?? string.Empty;

        if (string.IsNullOrEmpty(resolvedPhoneNumberId) || string.IsNullOrEmpty(accessToken))
            return new SendResponse { Success = false, Error = "Meta:PhoneNumberId atau Meta:AccessToken belum dikonfigurasi" };

        string url = $"https://graph.facebook.com/v20.0/{resolvedPhoneNumberId}/messages";
        string json = JsonSerializer.Serialize(payload);

        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _http.SendAsync(req, ct);
        string body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Meta API error {Status}: {Body}", response.StatusCode, body);
            return new SendResponse { Success = false, Error = $"Meta API {(int)response.StatusCode}: {body}" };
        }

        // Ambil message ID dari response Meta
        string? metaMessageId = null;
        try
        {
            var node = JsonNode.Parse(body);
            metaMessageId = node?["messages"]?[0]?["id"]?.GetValue<string>();
        }
        catch { }

        _logger.LogInformation("Pesan terkirim ke {To}, Meta message ID: {Id}", payload, metaMessageId);
        return new SendResponse { Success = true, MetaMessageId = metaMessageId };
    }
}
