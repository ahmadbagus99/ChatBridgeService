namespace ChatBridgeService.Models;

public class MessageStatusUpdate
{
    public string MetaMessageId { get; set; } = "";

    // "sent" | "delivered" | "read" | "failed"
    public string Status { get; set; } = "";
    public string RecipientPhone { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}
