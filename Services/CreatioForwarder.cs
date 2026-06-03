using System.Text;
using System.Text.Json;
using ChatBridgeService.Models;

namespace ChatBridgeService.Services;

public interface ICreatioForwarder
{
    Task ForwardAsync(CreatioInstance instance, IncomingMessage message, CancellationToken ct = default);
    Task ForwardStatusAsync(CreatioInstance instance, MessageStatusUpdate status, CancellationToken ct = default);
    Task SetMetaMessageIdAsync(CreatioInstance instance, string phoneNumber, string message, string metaMessageId, CancellationToken ct = default);
    Task<string> GetMessagesAsync(CreatioInstance instance, string conversationId, CancellationToken ct = default);
    Task<string> AgentReplyAsync(CreatioInstance instance, string phoneNumber, string message, CancellationToken ct = default);
    Task RequestAgentAsync(CreatioInstance instance, string phoneNumber, string customerName, CancellationToken ct = default);
    Task HeartbeatAsync(CreatioInstance instance, string contactId, CancellationToken ct = default);
    Task SetOnlineAsync(CreatioInstance instance, string contactId, bool isOnline, CancellationToken ct = default);
    Task<string> GetMyChatsAsync(CreatioInstance instance, string contactId, CancellationToken ct = default);
    Task<string> CloseChatAsync(CreatioInstance instance, string agentChatId, string contactId, CancellationToken ct = default);
}

public class CreatioForwarder : ICreatioForwarder
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CreatioAuthCache _authCache;
    private readonly ILogService _log;
    private readonly ILogger<CreatioForwarder> _logger;

    public CreatioForwarder(IHttpClientFactory httpClientFactory, CreatioAuthCache authCache, ILogService log, ILogger<CreatioForwarder> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authCache = authCache;
        _log = log;
        _logger = logger;
    }

    public async Task ForwardAsync(CreatioInstance instance, IncomingMessage message, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);

        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeWebhookService/Receive";
        var payload = new
        {
            MessageId = message.MessageId,
            PhoneNumberId = message.PhoneNumberId,
            From = message.From,
            CustomerName = message.CustomerName,
            Type = message.Type.ToString(),
            TextBody = message.TextBody,
            InteractiveReplyId = message.InteractiveReplyId,
            InteractiveReplyTitle = message.InteractiveReplyTitle,
            ReceivedAt = message.ReceivedAt
        };

        var response = await PostToCreatioAsync(instance, endpoint, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Creatio forward failed {Status}: {Body}", response.StatusCode, body);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                _authCache.Invalidate(instance.Id);
            await _log.LogAsync(instance.Id, "error_creatio", message.From, false,
                $"Forward failed {response.StatusCode}: {body[..Math.Min(200, body.Length)]}", ct);
        }
        else
        {
            await _log.LogAsync(instance.Id, "webhook_in", message.From, true,
                $"[{message.Type}] {message.TextBody?[..Math.Min(200, message.TextBody?.Length ?? 0)]}", ct);
        }
    }

    public async Task SetMetaMessageIdAsync(CreatioInstance instance, string phoneNumber, string message, string metaMessageId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);
        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeAgentService/SetMetaMessageId";

        var response = await PostToCreatioAsync(instance, endpoint, new
        {
            PhoneNumber = phoneNumber,
            Message = message,
            MetaMessageId = metaMessageId
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("SetMetaMessageId failed {Status}: {Body}", response.StatusCode, body);
        }
    }

    public async Task ForwardStatusAsync(CreatioInstance instance, MessageStatusUpdate status, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);
        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeAgentService/UpdateStatus";

        var payload = new
        {
            MetaMessageId = status.MetaMessageId,
            Status = status.Status,
            RecipientPhone = status.RecipientPhone,
            Timestamp = status.Timestamp,
            ErrorMessage = status.ErrorMessage
        };

        var response = await PostToCreatioAsync(instance, endpoint, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("ForwardStatus failed {Status}: {Body}", response.StatusCode, body);
        }
        else
        {
            _logger.LogInformation("Status {Status} for MetaMsg {Id} forwarded to Creatio", status.Status, status.MetaMessageId);
        }
    }

    public async Task<string> GetMessagesAsync(CreatioInstance instance, string conversationId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);
        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeAgentService/GetMessages";
        var response = await PostToCreatioAsync(instance, endpoint, new { ConversationId = conversationId }, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> AgentReplyAsync(CreatioInstance instance, string phoneNumber, string message, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);
        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeAgentService/Reply";
        var response = await PostToCreatioAsync(instance, endpoint, new { phoneNumber, message }, ct);
        string body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            await _log.LogAsync(instance.Id, "error_creatio", phoneNumber, false,
                $"AgentReply failed {response.StatusCode}: {body[..Math.Min(200, body.Length)]}", ct);
        }
        else
        {
            await _log.LogAsync(instance.Id, "agent_reply", phoneNumber, true,
                message[..Math.Min(200, message.Length)], ct);
        }

        return body;
    }

    private async Task<HttpResponseMessage> PostToCreatioAsync(CreatioInstance instance, string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        content.Headers.ContentType!.CharSet = null; // WCF rejects charset=utf-8

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

        if (_authCache.TryGet(instance.Id, out var token))
            request.Headers.Add("Authorization", $"Bearer {token}");

        var http = _httpClientFactory.CreateClient("creatio");
        return await http.SendAsync(request, ct);
    }

    private async Task EnsureAuthenticatedAsync(CreatioInstance instance, CancellationToken ct)
    {
        if (_authCache.TryGet(instance.Id, out _)) return;

        string tokenUrl = $"{instance.CreatioIdentityUrl.TrimEnd('/')}/connect/token";
        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = instance.CreatioClientId,
            ["client_secret"] = instance.CreatioClientSecret
        });

        var http = _httpClientFactory.CreateClient("creatio");
        var response = await http.PostAsync(tokenUrl, formData, ct);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Creatio OAuth failed for instance {Name} {Status}: {Body}", instance.Name, response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        string responseBody = await response.Content.ReadAsStringAsync(ct);
        var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            throw new Exception($"Creatio OAuth response invalid for instance {instance.Name}");

        _authCache.Set(instance.Id, tokenResponse.AccessToken, tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3600);
        _logger.LogInformation("OAuth token acquired for Creatio instance {Name}", instance.Name);
    }

    public async Task RequestAgentAsync(CreatioInstance instance, string phoneNumber, string customerName, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);
        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeAgentService/RequestAgent";
        var response = await PostToCreatioAsync(instance, endpoint, new { phoneNumber, customerName }, ct);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("RequestAgent failed {Status}", response.StatusCode);
    }

    public async Task HeartbeatAsync(CreatioInstance instance, string contactId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);
        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeAgentService/Heartbeat";
        var response = await PostToCreatioAsync(instance, endpoint, new { contactId }, ct);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Heartbeat failed {Status}", response.StatusCode);
    }

    public async Task SetOnlineAsync(CreatioInstance instance, string contactId, bool isOnline, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);
        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeAgentService/SetOnline";
        var response = await PostToCreatioAsync(instance, endpoint, new { contactId, isOnline }, ct);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("SetOnline failed {Status}", response.StatusCode);
    }

    public async Task<string> GetMyChatsAsync(CreatioInstance instance, string contactId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);
        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeAgentService/GetMyChats";
        var response = await PostToCreatioAsync(instance, endpoint, new { contactId }, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> CloseChatAsync(CreatioInstance instance, string agentChatId, string contactId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(instance, ct);
        string endpoint = $"{instance.CreatioBaseUrl.TrimEnd('/')}/0/rest/ChatBridgeAgentService/CloseChat";
        var response = await PostToCreatioAsync(instance, endpoint, new { agentChatId, contactId }, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private sealed class OAuthTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
