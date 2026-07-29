using ChatBridgeService.Models;

namespace ChatBridgeService.Services;

public interface IMetaWebhookParser
{
    IEnumerable<IncomingMessage> Parse(MetaWebhookPayload payload);
    IEnumerable<MessageStatusUpdate> ParseStatuses(MetaWebhookPayload payload);
}

public class MetaWebhookParser : IMetaWebhookParser
{
    public IEnumerable<IncomingMessage> Parse(MetaWebhookPayload payload)
    {
        if (payload.Object != "whatsapp_business_account")
            yield break;

        foreach (var entry in payload.Entry)
        foreach (var change in entry.Changes)
        {
            if (change.Field != "messages") continue;

            var value = change.Value;
            // Grouped rather than ToDictionary: a payload carrying two entries with the
            // same wa_id would otherwise throw and fail the whole webhook request.
            var contactLookup = value.Contacts
                .Where(c => !string.IsNullOrEmpty(c.WaId))
                .GroupBy(c => c.WaId)
                .ToDictionary(g => g.Key, g => g.First().Profile.Name);

            foreach (var msg in value.Messages)
            {
                var incoming = new IncomingMessage
                {
                    MessageId = msg.Id,
                    PhoneNumberId = value.Metadata.PhoneNumberId,
                    From = msg.From,
                    CustomerName = contactLookup.TryGetValue(msg.From, out var name) ? name : msg.From,
                    ReceivedAt = DateTimeOffset.FromUnixTimeSeconds(long.TryParse(msg.Timestamp, out var ts) ? ts : 0).UtcDateTime,
                    Type = msg.Type switch
                    {
                        "text" => MessageType.Text,
                        "image" => MessageType.Image,
                        "document" => MessageType.Document,
                        "audio" => MessageType.Audio,
                        "interactive" => MessageType.Interactive,
                        _ => MessageType.Unknown
                    }
                };

                if (msg.Type == "text")
                    incoming.TextBody = msg.Text?.Body ?? string.Empty;

                if (msg.Type == "interactive" && msg.Interactive != null)
                {
                    if (msg.Interactive.Type == "button_reply" && msg.Interactive.ButtonReply != null)
                    {
                        incoming.InteractiveReplyId = msg.Interactive.ButtonReply.Id;
                        incoming.InteractiveReplyTitle = msg.Interactive.ButtonReply.Title;
                        incoming.TextBody = msg.Interactive.ButtonReply.Title;
                    }
                    else if (msg.Interactive.Type == "list_reply" && msg.Interactive.ListReply != null)
                    {
                        incoming.InteractiveReplyId = msg.Interactive.ListReply.Id;
                        incoming.InteractiveReplyTitle = msg.Interactive.ListReply.Title;
                        incoming.TextBody = msg.Interactive.ListReply.Title;
                    }
                }

                yield return incoming;
            }
        }
    }

    public IEnumerable<MessageStatusUpdate> ParseStatuses(MetaWebhookPayload payload)
    {
        if (payload.Object != "whatsapp_business_account")
            yield break;

        foreach (var entry in payload.Entry)
        foreach (var change in entry.Changes)
        {
            if (change.Field != "messages") continue;

            foreach (var status in change.Value.Statuses)
            {
                string? errorMsg = status.Errors.Count > 0
                    ? $"[{status.Errors[0].Code}] {status.Errors[0].Title}: {status.Errors[0].Message}"
                    : null;

                yield return new MessageStatusUpdate
                {
                    MetaMessageId = status.Id,
                    Status = status.Status,
                    RecipientPhone = status.RecipientId,
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(
                        long.TryParse(status.Timestamp, out var ts) ? ts : 0).UtcDateTime,
                    ErrorMessage = errorMsg
                };
            }
        }
    }
}
