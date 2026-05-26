namespace ChatBridgeService.Models;

public class SendTextRequest
{
    public string To { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? PhoneNumberId { get; set; }
}

public class SendButtonsRequest
{
    public string To { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public List<ButtonItem> Buttons { get; set; } = [];
    public string? PhoneNumberId { get; set; }
}

public class SendListRequest
{
    public string To { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string ButtonLabel { get; set; } = "Lihat Pilihan";
    public List<ListRow> Rows { get; set; } = [];
    public string? PhoneNumberId { get; set; }
}

public class ButtonItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;  // max 20 chars
}

public class ListRow
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;       // max 24 chars
    public string Description { get; set; } = string.Empty; // max 72 chars
}

public class SendResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? MetaMessageId { get; set; }
}
