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
    Task<SendResponse> SendImageAsync(CreatioInstance instance, SendImageRequest request, CancellationToken ct = default);
    Task<SendResponse> SendDocumentAsync(CreatioInstance instance, SendDocumentRequest request, CancellationToken ct = default);
    Task<SendResponse> SendLocationAsync(CreatioInstance instance, SendLocationRequest request, CancellationToken ct = default);
    Task<SendResponse> SendCtaAsync(CreatioInstance instance, SendCtaRequest request, CancellationToken ct = default);
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

    public Task<SendResponse> SendImageAsync(CreatioInstance instance, SendImageRequest request, CancellationToken ct = default)
    {
        var image = new Dictionary<string, object> { ["link"] = request.MediaUrl };
        if (!string.IsNullOrWhiteSpace(request.Caption)) image["caption"] = request.Caption!;

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "image",
            image
        };
        return PostAsync(instance, request.PhoneNumberId, payload, ct);
    }

    public Task<SendResponse> SendDocumentAsync(CreatioInstance instance, SendDocumentRequest request, CancellationToken ct = default)
    {
        var document = new Dictionary<string, object> { ["link"] = request.MediaUrl };
        if (!string.IsNullOrWhiteSpace(request.FileName)) document["filename"] = request.FileName!;
        if (!string.IsNullOrWhiteSpace(request.Caption)) document["caption"] = request.Caption!;

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "document",
            document
        };
        return PostAsync(instance, request.PhoneNumberId, payload, ct);
    }

    public Task<SendResponse> SendLocationAsync(CreatioInstance instance, SendLocationRequest request, CancellationToken ct = default)
    {
        var location = new Dictionary<string, object>
        {
            ["latitude"] = request.Latitude,
            ["longitude"] = request.Longitude
        };
        if (!string.IsNullOrWhiteSpace(request.Name)) location["name"] = request.Name!;
        if (!string.IsNullOrWhiteSpace(request.Address)) location["address"] = request.Address!;

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "location",
            location
        };
        return PostAsync(instance, request.PhoneNumberId, payload, ct);
    }

    public Task<SendResponse> SendCtaAsync(CreatioInstance instance, SendCtaRequest request, CancellationToken ct = default)
    {
        string displayText = request.ButtonText.Length > 20 ? request.ButtonText[..20] : request.ButtonText;

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "interactive",
            interactive = new
            {
                type = "cta_url",
                body = new { text = request.BodyText },
                action = new
                {
                    name = "cta_url",
                    parameters = new { display_text = displayText, url = request.Url }
                }
            }
        };
        return PostAsync(instance, request.PhoneNumberId, payload, ct);
    }

    private async Task<SendResponse> PostAsync(CreatioInstance instance, string? overridePhoneNumberId, object payload, CancellationToken ct)
    {
        bool useKirimDev = string.Equals(instance.WhatsAppProvider, "KirimDev", StringComparison.OrdinalIgnoreCase);
        string phoneNumberId = overridePhoneNumberId ?? (useKirimDev ? instance.KirimDevPhoneNumberId : instance.MetaPhoneNumberId);
        string accessToken = useKirimDev ? instance.KirimDevApiKey : instance.MetaAccessToken;

        if (string.IsNullOrEmpty(phoneNumberId) || string.IsNullOrEmpty(accessToken))
            return new SendResponse { Success = false, Error = $"{(useKirimDev ? "KirimDev" : "Meta")} phone number ID or access token not configured for this instance" };

        string url = useKirimDev
            ? $"https://api.kirimdev.com/v1/{phoneNumberId}/messages"
            : $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages";
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
            _logger.LogError("{Provider} API error {Status}: {Body}", useKirimDev ? "KirimDev" : "Meta", response.StatusCode, body);
            await _log.LogAsync(instance.Id, "error_meta", phoneNumberId?[..Math.Min(50, phoneNumberId.Length)], false,
                $"{(useKirimDev ? "KirimDev" : "Meta")} API {(int)response.StatusCode}: {body[..Math.Min(300, body.Length)]}");
            return new SendResponse { Success = false, Error = $"{(useKirimDev ? "KirimDev" : "Meta")} API {(int)response.StatusCode}: {body}" };
        }

        string? metaMessageId = null;
        try
        {
            var json = JsonNode.Parse(body);
            metaMessageId = json?["messages"]?[0]?["id"]?.GetValue<string>()
                ?? json?["data"]?["id"]?.GetValue<string>()
                ?? json?["id"]?.GetValue<string>();
        }
        catch { }

        await _log.LogAsync(instance.Id, "agent_reply", phoneNumberId, true, $"MetaMessageId: {metaMessageId}");
        return new SendResponse { Success = true, MetaMessageId = metaMessageId };
    }
}
