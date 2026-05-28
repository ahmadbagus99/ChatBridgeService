using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ChatBridgeService.Models;

namespace ChatBridgeService.Services;

public interface ICreatioForwarder
{
    Task ForwardAsync(IncomingMessage message, CancellationToken ct = default);
    Task<string> GetMessagesAsync(string conversationId, CancellationToken ct = default);
    Task<string> AgentReplyAsync(string phoneNumber, string message, CancellationToken ct = default);
}

public class CreatioForwarder : ICreatioForwarder
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<CreatioForwarder> _logger;
    private string? _authCookie;
    private string? _bpmCsrf;

    public CreatioForwarder(HttpClient http, IConfiguration config, ILogger<CreatioForwarder> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task ForwardAsync(IncomingMessage message, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        string baseUrl = _config["Creatio:BaseUrl"]!.TrimEnd('/');
        string endpoint = $"{baseUrl}/0/rest/ChatBridgeWebhookService/Receive";

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

        var response = await PostToCreatioAsync(endpoint, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Creatio forward failed {Status}: {Body}", response.StatusCode, body);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                _authCookie = null;
        }
    }

    public async Task<string> GetMessagesAsync(string conversationId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);
        string baseUrl = _config["Creatio:BaseUrl"]!.TrimEnd('/');
        string endpoint = $"{baseUrl}/0/rest/ChatBridgeAgentService/GetMessages";

        var response = await PostToCreatioAsync(endpoint, new { ConversationId = conversationId }, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> AgentReplyAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);
        string baseUrl = _config["Creatio:BaseUrl"]!.TrimEnd('/');
        string endpoint = $"{baseUrl}/0/rest/ChatBridgeAgentService/Reply";

        var response = await PostToCreatioAsync(endpoint, new { phoneNumber, message }, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<HttpResponseMessage> PostToCreatioAsync(string endpoint, object payload, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        content.Headers.ContentType!.CharSet = null; // WCF rejects charset=utf-8

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        if (!string.IsNullOrEmpty(_authCookie))
            request.Headers.Add("Cookie", _authCookie);
        if (!string.IsNullOrEmpty(_bpmCsrf))
            request.Headers.Add("BPMCSRF", _bpmCsrf);

        return await _http.SendAsync(request, ct);
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_authCookie)) return;

        string baseUrl = _config["Creatio:BaseUrl"]!.TrimEnd('/');
        string username = _config["Creatio:Username"]!;
        string password = _config["Creatio:Password"]!;

        var loginPayload = new { UserName = username, UserPassword = password };
        var loginContent = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
        loginContent.Headers.ContentType!.CharSet = null;
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/ServiceModel/AuthService.svc/Login")
        {
            Content = loginContent
        };

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Creatio login failed {Status}: {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var cookieList = cookies.ToList();
            _authCookie = string.Join("; ", cookieList.Select(c => c.Split(';')[0]));

            var csrfCookie = cookieList.FirstOrDefault(c => c.TrimStart().StartsWith("BPMCSRF="));
            if (csrfCookie != null)
                _bpmCsrf = csrfCookie.Split(';')[0].Split('=', 2)[1];
        }

        _logger.LogInformation("Authenticated to Creatio successfully");
    }
}
