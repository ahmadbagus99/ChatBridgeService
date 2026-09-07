using System.Globalization;
using System.Text;

namespace ChatBridgeService.Services;

internal static class WhatsAppText
{
    /// <summary>
    /// Clamps text for WhatsApp interactive payloads (button titles, list rows, CTA labels).
    ///
    /// Meta and KirimDev both document these limits in characters, so the budget stays the
    /// same as a plain slice. What a plain slice gets wrong is *where* it cuts: value[..limit]
    /// can land in the middle of a surrogate pair, leaving a lone surrogate that serializes
    /// as U+FFFD and reaches the customer as a broken glyph. Cutting only on text-element
    /// boundaries keeps emoji and combining sequences whole.
    ///
    /// For ASCII text this is byte-for-byte identical to the previous behaviour.
    /// </summary>
    public static string Clamp(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
            return string.Empty;

        if (value.Length <= maxLength)
            return value;

        var builder = new StringBuilder(maxLength);
        int length = 0;

        var enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            string element = (string)enumerator.Current;
            if (length + element.Length > maxLength)
                break;

            builder.Append(element);
            length += element.Length;
        }

        return builder.ToString();
    }
}
