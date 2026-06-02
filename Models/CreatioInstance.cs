namespace ChatBridgeService.Models;

public class CreatioInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    // Digunakan sebagai identifier di URL: /webhook/{ApiKey}, /chat/{ApiKey}/...
    public string ApiKey { get; set; } = "";

    // Creatio OAuth credentials
    public string CreatioBaseUrl { get; set; } = "";
    public string CreatioIdentityUrl { get; set; } = "";
    public string CreatioClientId { get; set; } = "";
    public string CreatioClientSecret { get; set; } = "";

    // Meta WhatsApp Cloud API credentials
    public string MetaAccessToken { get; set; } = "";
    public string MetaPhoneNumberId { get; set; } = "";
    public string MetaVerifyToken { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
