using System.Text;

namespace FingerTrap.Sidecar.Text;

/// <summary>
/// The sanitization boundary for untrusted free-text (ADR-0022 §sanitization,
/// applying repo-dash's rule): called at row CONSTRUCTION, never at render.
/// Originally status-provider-scoped; the session browser (FT-2 slice 5)
/// pushes session names, first messages, and cwds through the same boundary,
/// so it lives under <c>Text/</c> rather than <c>Status/</c>.
/// Strips every C0/C1 control character (ESC, BEL, CR, LF, CSI/OSC/DCS
/// introducers — terminal-injection surface if the text is ever echoed near
/// a PTY) and the Unicode BiDi/direction controls that let a string display
/// as something other than its bytes (Trojan-Source class). Caps length so a
/// hostile title cannot balloon a snapshot frame.
/// </summary>
internal static class StatusText
{
    public const int MaxFieldLength = 300;

    public static string Sanitize(string? value, int maxLength = MaxFieldLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var rune in value.EnumerateRunes())
        {
            if (IsStripped(rune.Value))
            {
                continue;
            }

            if (builder.Length + rune.Utf16SequenceLength > maxLength)
            {
                builder.Append('…');
                break;
            }

            builder.Append(rune.ToString());
        }

        return builder.ToString().Trim();
    }

    private static bool IsStripped(int codePoint) => codePoint switch
    {
        // C0 controls (includes ESC/BEL/CR/LF/TAB) and DEL.
        < 0x20 or 0x7F => true,
        // C1 controls — CSI (0x9B), OSC (0x9D), DCS (0x90) live here.
        >= 0x80 and <= 0x9F => true,
        // BiDi embedding/override/isolate controls (Trojan Source).
        >= 0x202A and <= 0x202E => true,
        >= 0x2066 and <= 0x2069 => true,
        // LRM/RLM direction marks.
        0x200E or 0x200F => true,
        _ => false,
    };
}
