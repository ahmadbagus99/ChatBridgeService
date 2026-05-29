using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChatBridgeService.Models;

namespace ChatBridgeService.Services;

public interface IMetaMessageSender
{
    Task<SendResponse> SendTextAsync(CreatioInstance instance, SendTextRequest request, CancellationToken ct = default);
    Task<SendResponse> SendButtonsAsync(CreatioInstance instance, SendButtonsRequest request, CancellationToken ct = default);
    Task<SendResponse> SendListAsync(CreatioInstance instance, SendListRequest request, CancellationToken ct = default);
}

public class MetaMessageSender : IMetaMessageSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogService _log;
    private readonly ILogger<MetaMessageSender> _logger;

    public MetaMessageSender(IHttpClientFactory httpClientFactory, ILogService log, ILogger<MetaMessageSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _log = log;
        _logger = logger;
    }

    public Task<SendResponse> SendTextAsync(CreatioInstance instance, SendTextRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "text",
            text = new { preview_url = false, body = request.Body }
        };
        return PostAsync(instance, request.PhoneNumberId, payload, ct);
    }

    public Task<SendResponse> SendButtonsAsync(CreatioInstance instance, SendButtonsRequest request, CancellationToken ct = default)
    {
        var buttons = request.Buttons
            .Take(3)
            .Select(b => new
            {
                type = "reply",
                reply = new { id = b.Id, title = b.Title.Length > 20 ? b.Title[..20] : b.Title }
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
        return PostAsync(instance, request.PhoneNumberId, payload, ct);
    }

    public Task<SendResponse> SendListAsync(CreatioInstance instance, SendListRequest request, CancellationToken ct = default)
    {
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
                action = new { button = request.ButtonLabel, sections = new[] { new { rows } } }
            }
        };
        return PostAsync(instance, request.PhoneNumberId, payload, ct);
    }

    private async Task<SendResponse> PostAsync(CreatioInstance instance, string? overridePhoneNumberId, object payload, CancellationToken ct)
    {
        string phoneNumberId = overridePhoneNumberId ?? instance.MetaPhoneNumberId;
        string accessToken = instance.MetaAccessToken;

        if (string.IsNullOrEmpty(phoneNumberId) || string.IsNullOrEmpty(accessToken))
            return new SendResponse { Success = false, Error = "MetaPhoneNumberId or MetaAccessToken not configured for this instance" };

        string url = $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages";
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var http = _httpClientFactory.CreateClient("meta");
        var response = await http.SendAsync(req, ct);
        string body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Meta API error {Status}: {Body}", response.StatusCode, body);
            await _log.LogAsync(instance.Id, "error_meta", phoneNumberId?[..Math.Min(50, phoneNumberId.Length)], false,
                $"Meta API {(int)response.StatusCode}: {body[..Math.Min(300, body.Length)]}");
            return new SendResponse { Success = false, Error = $"Meta API {(int)response.StatusCode}: {body}" };
        }

        string? metaMessageId = null;
        try { metaMessageId = JsonNode.Parse(body)?["messages"]?[0]?["id"]?.GetValue<string>(); }
        catch { }

        await _log.LogAsync(instance.Id, "agent_reply", phoneNumberId, true, $"MetaMessageId: {metaMessageId}");
        return new SendResponse { Success = true, MetaMessageId = metaMessageId };
    }
}
