namespace ChatBridgeService.Models;

public class KirimDevConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }
    public CreatioInstance? Instance { get; set; }

    public string PhoneNumber { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
