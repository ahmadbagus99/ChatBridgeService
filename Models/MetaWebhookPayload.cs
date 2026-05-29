namespace ChatBridgeService.Models;

public class MetaWebhookPayload
{
    public string Object { get; set; } = string.Empty;
    public List<Entry> Entry { get; set; } = [];
}

public class Entry
{
    public string Id { get; set; } = string.Empty;
    public List<Change> Changes { get; set; } = [];
}

public class Change
{
    public ChangeValue Value { get; set; } = new();
    public string Field { get; set; } = string.Empty;
}

public class ChangeValue
{
    public string MessagingProduct { get; set; } = string.Empty;
    public Metadata Metadata { get; set; } = new();
    public List<Contact> Contacts { get; set; } = [];
    public List<WhatsAppMessage> Messages { get; set; } = [];
    public List<MessageStatus> Statuses { get; set; } = [];
}

public class Metadata
{
    public string DisplayPhoneNumber { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
}

public class Contact
{
    public ContactProfile Profile { get; set; } = new();
    public string WaId { get; set; } = string.Empty;
}

public class ContactProfile
{
    public string Name { get; set; } = string.Empty;
}

public class WhatsAppMessage
{
    public string From { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public TextContent? Text { get; set; }
    public ImageContent? Image { get; set; }
    public DocumentContent? Document { get; set; }
    public AudioContent? Audio { get; set; }
    public InteractiveContent? Interactive { get; set; }
}

public class TextContent
{
    public string Body { get; set; } = string.Empty;
}

public class ImageContent
{
    public string Id { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
}

public class DocumentContent
{
    public string Id { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
}

public class AudioContent
{
    public string Id { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
}

public class InteractiveContent
{
    public string Type { get; set; } = string.Empty;
    public ButtonReply? ButtonReply { get; set; }
    public ListReply? ListReply { get; set; }
}

public class ButtonReply
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class ListReply
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class MessageStatus
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public List<MetaStatusError> Errors { get; set; } = [];
}

public class MetaStatusError
{
    public int Code { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
