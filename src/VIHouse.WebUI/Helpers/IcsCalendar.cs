using System.Text;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// One event as an iCalendar (RFC 5545) file — what "Add to calendar" downloads, and what Apple
/// Calendar, Google Calendar and Outlook all import. Times are written in UTC ("Z"), so the
/// calendar app shows them in the reader's own zone without the file having to carry a VTIMEZONE.
///
/// Only public facts go in: title, dates, venue or city, and the public page link. The meeting
/// link of an online session is deliberately left out — it is the ticket, and a calendar entry
/// gets forwarded and synced to places the site cannot see.
/// </summary>
public static class IcsCalendar
{
    public const string ContentType = "text/calendar; charset=utf-8";

    public record Event(string Uid, string Title, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? Location, string? Description, string? Url);

    public static string Build(Event e)
    {
        var sb = new StringBuilder();
        void Line(string text) => Fold(sb, text);

        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//The VI House//thevihouse.com//EN");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line("BEGIN:VEVENT");
        // Stable per event, so importing the file again (after a date change) updates the entry
        // instead of adding a second one.
        Line($"UID:{e.Uid}");
        Line($"DTSTAMP:{Utc(DateTimeOffset.UtcNow)}");
        Line($"DTSTART:{Utc(e.StartUtc)}");
        Line($"DTEND:{Utc(e.EndUtc > e.StartUtc ? e.EndUtc : e.StartUtc.AddHours(1))}");
        Line($"SUMMARY:{Escape(e.Title)}");
        if (!string.IsNullOrWhiteSpace(e.Location)) Line($"LOCATION:{Escape(e.Location)}");
        if (!string.IsNullOrWhiteSpace(e.Description)) Line($"DESCRIPTION:{Escape(e.Description)}");
        if (!string.IsNullOrWhiteSpace(e.Url)) Line($"URL:{e.Url}");
        Line("END:VEVENT");
        Line("END:VCALENDAR");
        return sb.ToString();
    }

    /// <summary>A file name that survives every OS: letters, digits and hyphens.</summary>
    public static string FileName(string slug) =>
        $"vi-house-{new string(slug.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray())}.ics";

    private static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

    /// <summary>TEXT values escape backslash, semicolon, comma and newlines (RFC 5545 §3.3.11).</summary>
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "");

    /// <summary>Lines longer than 75 octets are folded with CRLF + space (§3.1), never splitting a
    /// UTF-8 character; every line ends in CRLF.</summary>
    private static void Fold(StringBuilder sb, string line)
    {
        var octets = 0;
        var limit = 75;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > limit)
            {
                sb.Append("\r\n ");
                octets = 0;
                limit = 74; // the leading space counts
            }
            sb.Append(rune.ToString());
            octets += size;
        }
        sb.Append("\r\n");
    }
}
