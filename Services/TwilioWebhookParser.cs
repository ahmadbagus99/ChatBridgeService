using ChatBridgeService.Models;
using Microsoft.AspNetCore.Http;
using Twilio.Security;

namespace ChatBridgeService.Services;

public interface ITwilioWebhookParser
{
    bool Validate(CreatioInstance instance, string requestUrl, IFormCollection form, string signature);
    IncomingMessage? ParseMessage(IFormCollection form);
    MessageStatusUpdate? ParseStatus(IFormCollection form);
}

public class TwilioWebhookParser : ITwilioWebhookParser
{
    public bool Validate(CreatioInstance instance, string requestUrl, IFormCollection form, string signature)
    {
        if (string.IsNullOrWhiteSpace(instance.TwilioAuthToken) || string.IsNullOrWhiteSpace(signature))
            return false;

        string accountSid = form["AccountSid"].ToString();
        if (!string.IsNullOrWhiteSpace(instance.TwilioAccountSid)
            && !string.Equals(accountSid, instance.TwilioAccountSid, StringComparison.Ordinal))
            return false;

        var parameters = form.ToDictionary(item => item.Key, item => item.Value.ToString());
        return new RequestValidator(instance.TwilioAuthToken).Validate(requestUrl, parameters, signature);
    }

    public IncomingMessage? ParseMessage(IFormCollection form)
    {
        string messageSid = form["MessageSid"].ToString();
        string from = NormalizeWhatsAppAddress(form["From"].ToString());
        if (string.IsNullOrWhiteSpace(messageSid) || string.IsNullOrWhiteSpace(from))
            return null;

        // Status callbacks also contain a MessageSid, but an inbound message does not
        // carry MessageStatus/EventType.
        if (!string.IsNullOrWhiteSpace(form["MessageStatus"])
            || !string.IsNullOrWhiteSpace(form["EventType"]))
            return null;

        string body = form["Body"].ToString();
        string buttonPayload = form["ButtonPayload"].ToString();
        string buttonText = form["ButtonText"].ToString();
        string contentType = form["MediaContentType0"].ToString();
        int.TryParse(form["NumMedia"].ToString(), out int mediaCount);

        MessageType type = !string.IsNullOrWhiteSpace(buttonPayload)
            ? MessageType.Interactive
            : mediaCount > 0
                ? MapMediaType(contentType)
                : MessageType.Text;

        if (!string.IsNullOrWhiteSpace(form["Latitude"]) && !string.IsNullOrWhiteSpace(form["Longitude"]))
        {
            type = MessageType.Interactive;
            body = BuildLocationText(form);
        }

        return new IncomingMessage
        {
            Provider = "Twilio",
            MessageId = messageSid,
            PhoneNumberId = NormalizeWhatsAppAddress(form["To"].ToString()),
            From = from,
            CustomerName = string.IsNullOrWhiteSpace(form["ProfileName"])
                ? from
                : form["ProfileName"].ToString(),
            Type = type,
            TextBody = body,
            InteractiveReplyId = buttonPayload,
            InteractiveReplyTitle = string.IsNullOrWhiteSpace(buttonText) ? body : buttonText,
            ReceivedAt = DateTime.UtcNow
        };
    }

    public MessageStatusUpdate? ParseStatus(IFormCollection form)
    {
        string messageSid = form["MessageSid"].ToString();
        string rawStatus = form["MessageStatus"].ToString();
        string eventType = form["EventType"].ToString();
        if (string.IsNullOrWhiteSpace(messageSid)
            || (string.IsNullOrWhiteSpace(rawStatus) && string.IsNullOrWhiteSpace(eventType)))
            return null;

        string status = string.Equals(eventType, "READ", StringComparison.OrdinalIgnoreCase)
            ? "read"
            : rawStatus.ToLowerInvariant() switch
            {
                "sent" => "sent",
                "delivered" => "delivered",
                "read" => "read",
                "failed" or "undelivered" => "failed",
                _ => ""
            };

        if (string.IsNullOrEmpty(status))
            return null;

        string errorCode = form["ErrorCode"].ToString();
        string channelError = form["ChannelStatusMessage"].ToString();
        string? error = string.IsNullOrWhiteSpace(errorCode) && string.IsNullOrWhiteSpace(channelError)
            ? null
            : $"[{errorCode}] {channelError}".Trim();

        return new MessageStatusUpdate
        {
            MetaMessageId = messageSid,
            Status = status,
            RecipientPhone = NormalizeWhatsAppAddress(form["To"].ToString()),
            Timestamp = DateTime.UtcNow,
            ErrorMessage = error
        };
    }

    private static MessageType MapMediaType(string contentType)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return MessageType.Image;
        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return MessageType.Audio;
        return MessageType.Document;
    }

    private static string BuildLocationText(IFormCollection form)
    {
        string label = form["Label"].ToString();
        string address = form["Address"].ToString();
        string latitude = form["Latitude"].ToString();
        string longitude = form["Longitude"].ToString();
        return string.Join("\n", new[] { label, address, $"https://maps.google.com/?q={latitude},{longitude}" }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    internal static string NormalizeWhatsAppAddress(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["whatsapp:".Length..];
        return normalized.TrimStart('+');
    }
}
