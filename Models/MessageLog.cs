namespace ChatBridgeService.Models;

public class MessageLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }
    public CreatioInstance? Instance { get; set; }

    // "webhook_in" | "agent_reply" | "error_creatio" | "error_meta"
    public string Type { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public bool Success { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
