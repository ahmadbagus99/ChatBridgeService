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

    /// <summary>
    /// Marks a KirimDev conversation as resolved. No-op for Meta Cloud API instances,
    /// which have no equivalent conversation-status concept.
    /// </summary>
    Task<SendResponse> ResolveConversationAsync(CreatioInstance instance, string conversationId, CancellationToken ct = default);
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
        if (IsTwilio(instance))
            return PostTwilioAsync(instance, request.To, request.Body, null, null, request.PhoneNumberId, ct);

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
        if (IsTwilio(instance))
        {
            string body = BuildTwilioOptionsText(request.BodyText,
                request.Buttons.Take(3).Select((button, index) => $"{index + 1}. {button.Title}"));
            return PostTwilioAsync(instance, request.To, body, null, null, request.PhoneNumberId, ct);
        }

        bool useKirimDev = IsKirimDev(instance);
        var buttons = request.Buttons
            .Take(3)
            .Select(b => new
            {
                type = "reply",
                reply = new { id = b.Id, title = WhatsAppText.Clamp(b.Title, 20) }
            });

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "interactive",
            interactive = new
            {
                // KirimDev validates quick replies as "reply_buttons", while
                // Meta Cloud API uses the original "button" discriminator.
                type = useKirimDev ? "reply_buttons" : "button",
                body = new { text = request.BodyText },
                action = new { buttons }
            }
        };
        return PostAsync(instance, request.PhoneNumberId, payload, ct);
    }

    public Task<SendResponse> SendListAsync(CreatioInstance instance, SendListRequest request, CancellationToken ct = default)
    {
        if (IsTwilio(instance))
        {
            string body = BuildTwilioOptionsText(request.BodyText,
                request.Rows.Take(10).Select((row, index) => string.IsNullOrWhiteSpace(row.Description)
                    ? $"{index + 1}. {row.Title}"
                    : $"{index + 1}. {row.Title} — {row.Description}"));
            return PostTwilioAsync(instance, request.To, body, null, null, request.PhoneNumberId, ct);
        }

        var rows = request.Rows
            .Take(10)
            .Select(r => new
            {
                id = r.Id,
                title = WhatsAppText.Clamp(r.Title, 24),
                description = WhatsAppText.Clamp(r.Description, 72)
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
                action = new { button = WhatsAppText.Clamp(request.ButtonLabel, 20), sections = new[] { new { rows } } }
            }
        };
        return PostAsync(instance, request.PhoneNumberId, payload, ct);
    }

    public Task<SendResponse> SendImageAsync(CreatioInstance instance, SendImageRequest request, CancellationToken ct = default)
    {
        if (IsTwilio(instance))
            return PostTwilioAsync(instance, request.To, request.Caption, request.MediaUrl, null, request.PhoneNumberId, ct);

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
        if (IsTwilio(instance))
        {
            string? body = string.Join(" — ", new[] { request.FileName, request.Caption }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            return PostTwilioAsync(instance, request.To, body, request.MediaUrl, null, request.PhoneNumberId, ct);
        }

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
        if (IsTwilio(instance))
        {
            string name = string.IsNullOrWhiteSpace(request.Name) ? "Location" : request.Name.Trim();
            string? label = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
            string coordinates = $"{request.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{request.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            string persistentAction = label == null ? $"geo:{coordinates}" : $"geo:{coordinates}|{label}";
            return PostTwilioAsync(instance, request.To, name, null, persistentAction, request.PhoneNumberId, ct);
        }

        if (IsKirimDev(instance))
        {
            string mapUrl = $"https://www.google.com/maps/search/?api=1&query={request.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{request.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Name)) lines.Add(request.Name!.Trim());
            if (!string.IsNullOrWhiteSpace(request.Address)) lines.Add(request.Address!.Trim());
            lines.Add(mapUrl);

            var fallbackPayload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = request.To,
                type = "text",
                text = new { preview_url = true, body = string.Join("\n", lines) }
            };

            return PostAsync(instance, request.PhoneNumberId, fallbackPayload, ct);
        }

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
        if (IsTwilio(instance))
        {
            string body = $"{request.BodyText}\n\n{request.ButtonText}: {request.Url}";
            return PostTwilioAsync(instance, request.To, body, null, null, request.PhoneNumberId, ct);
        }

        string displayText = WhatsAppText.Clamp(request.ButtonText, 20);

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

    public async Task<SendResponse> ResolveConversationAsync(CreatioInstance instance, string conversationId, CancellationToken ct = default)
    {
        if (!IsKirimDev(instance))
            return new SendResponse { Success = true, Skipped = true, Provider = instance.WhatsAppProvider };

        if (string.IsNullOrEmpty(instance.KirimDevPhoneNumberId) || string.IsNullOrEmpty(instance.KirimDevApiKey))
            return new SendResponse { Success = false, Error = "KirimDev phone number ID or API key not configured for this instance" };

        string url = $"https://api.kirimdev.com/v1/{instance.KirimDevPhoneNumberId}/conversations/{conversationId}";
        var req = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", instance.KirimDevApiKey) },
            Content = new StringContent(JsonSerializer.Serialize(new { status = "resolved" }), Encoding.UTF8, "application/json")
        };

        var http = _httpClientFactory.CreateClient("meta");
        var response = await http.SendAsync(req, ct);
        string body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("KirimDev resolve conversation {ConversationId} failed {Status}: {Body}",
                conversationId, response.StatusCode, body);
            await _log.LogAsync(instance.Id, "error_meta", conversationId, false,
                $"KirimDev resolve {(int)response.StatusCode}: {body[..Math.Min(300, body.Length)]}");
            return new SendResponse { Success = false, Error = $"KirimDev API {(int)response.StatusCode}: {body}" };
        }

        return new SendResponse { Success = true, Provider = "KirimDev" };
    }

    private async Task<SendResponse> PostAsync(CreatioInstance instance, string? overridePhoneNumberId, object payload, CancellationToken ct)
    {
        bool useKirimDev = IsKirimDev(instance);
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

        string provider = useKirimDev ? "KirimDev" : "MetaCloud";
        await _log.LogAsync(instance.Id, "agent_reply", phoneNumberId, true, $"ProviderMessageId: {metaMessageId}");
        return new SendResponse
        {
            Success = true,
            Provider = provider,
            ProviderMessageId = metaMessageId,
            MetaMessageId = metaMessageId
        };
    }

    private async Task<SendResponse> PostTwilioAsync(CreatioInstance instance, string to, string? body,
        string? mediaUrl, string? persistentAction, string? overrideFrom, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(instance.TwilioAccountSid)
            || string.IsNullOrWhiteSpace(instance.TwilioAuthToken))
            return new SendResponse { Success = false, Provider = "Twilio", Error = "Twilio Account SID or Auth Token is not configured for this instance" };

        string from = overrideFrom ?? instance.TwilioWhatsAppFrom;
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(instance.TwilioMessagingServiceSid))
            return new SendResponse { Success = false, Provider = "Twilio", Error = "Twilio WhatsApp From or Messaging Service SID is not configured for this instance" };

        var values = new Dictionary<string, string>
        {
            ["To"] = NormalizeTwilioAddress(to)
        };
        if (!string.IsNullOrWhiteSpace(from)) values["From"] = NormalizeTwilioAddress(from);
        else values["MessagingServiceSid"] = instance.TwilioMessagingServiceSid;
        if (!string.IsNullOrWhiteSpace(body)) values["Body"] = body;
        if (!string.IsNullOrWhiteSpace(mediaUrl)) values["MediaUrl"] = mediaUrl;
        if (!string.IsNullOrWhiteSpace(persistentAction)) values["PersistentAction"] = persistentAction;
        if (!string.IsNullOrWhiteSpace(instance.TwilioStatusCallbackUrl))
            values["StatusCallback"] = instance.TwilioStatusCallbackUrl;

        string url = $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(instance.TwilioAccountSid)}/Messages.json";
        string basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{instance.TwilioAccountSid}:{instance.TwilioAuthToken}"));
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", basicToken) },
            Content = new FormUrlEncodedContent(values)
        };

        var http = _httpClientFactory.CreateClient("meta");
        var response = await http.SendAsync(request, ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            string safeBody = responseBody[..Math.Min(300, responseBody.Length)];
            _logger.LogError("Twilio API error {Status}: {Body}", response.StatusCode, responseBody);
            await _log.LogAsync(instance.Id, "error_meta", TwilioWebhookParser.NormalizeWhatsAppAddress(to), false,
                $"Twilio API {(int)response.StatusCode}: {safeBody}");
            return new SendResponse { Success = false, Provider = "Twilio", Error = $"Twilio API {(int)response.StatusCode}: {responseBody}" };
        }

        string? messageSid = null;
        try
        {
            messageSid = JsonNode.Parse(responseBody)?["sid"]?.GetValue<string>();
        }
        catch { }

        await _log.LogAsync(instance.Id, "agent_reply", TwilioWebhookParser.NormalizeWhatsAppAddress(to), true,
            $"ProviderMessageId: {messageSid}");
        return new SendResponse
        {
            Success = true,
            Provider = "Twilio",
            ProviderMessageId = messageSid,
            MetaMessageId = messageSid
        };
    }

    private static string BuildTwilioOptionsText(string body, IEnumerable<string> options) =>
        $"{body}\n\n{string.Join("\n", options)}".Trim();

    private static string NormalizeTwilioAddress(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["whatsapp:".Length..];
        normalized = normalized.StartsWith('+') ? normalized : $"+{normalized}";
        return $"whatsapp:{normalized}";
    }

    private static bool IsKirimDev(CreatioInstance instance) =>
        string.Equals(instance.WhatsAppProvider, "KirimDev", StringComparison.OrdinalIgnoreCase);

    private static bool IsTwilio(CreatioInstance instance) =>
        string.Equals(instance.WhatsAppProvider, "Twilio", StringComparison.OrdinalIgnoreCase);
}
