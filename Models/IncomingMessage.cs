namespace ChatBridgeService.Models;

public enum MessageType
{
    Text,
    Image,
    Document,
    Audio,
    Interactive,
    Unknown
}

public class IncomingMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string TextBody { get; set; } = string.Empty;
    public string InteractiveReplyId { get; set; } = string.Empty;
    public string InteractiveReplyTitle { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
