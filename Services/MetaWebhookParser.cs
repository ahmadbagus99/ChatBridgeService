using ChatBridgeService.Models;

namespace ChatBridgeService.Services;

public interface IMetaWebhookParser
{
    IEnumerable<IncomingMessage> Parse(MetaWebhookPayload payload);
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
            var contactLookup = value.Contacts.ToDictionary(c => c.WaId, c => c.Profile.Name);

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
}
