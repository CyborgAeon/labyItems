using System.Globalization;
using labyItems.Models.Authentication;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using labyItems.Models.Calendar;

namespace labyItems.Services;

public static class LabyrintheEventParser
{
    private static HtmlDocument Document(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    private static bool HasClass(HtmlNode node, string name)
        => node.GetAttributeValue("class", "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name);
    private static string Text(HtmlNode? node) => Regex.Replace(HtmlEntity.DeEntitize(node?.InnerText ?? ""), @"\s+", " ").Trim();
    private static bool IsBattleSchool(string text)
        => Regex.IsMatch(text, @"battle[\s-]+school", RegexOptions.IgnoreCase);

    public static string ParseAccountName(string html) => ParseAccountProfile(html).DisplayName;

    public static AccountProfile ParseAccountProfile(string html)
    {
        var doc = Document(html);
        string Value(string name) => HtmlEntity.DeEntitize(doc.DocumentNode
            .SelectSingleNode($"//input[@name='field[{name}]']")?.GetAttributeValue("value", "") ?? "").Trim();
        var profile = new AccountProfile(Value("first_name"), Value("last_name"));
        if (profile.DisplayName.Length == 0) throw new InvalidOperationException("Unable to read your account name from the website.");
        return profile;
    }

    public static bool IsLoginPage(string html)
        => Document(html).DocumentNode.SelectSingleNode("//*[@name='submit_login']") != null;

    public static IReadOnlyList<CalendarEventRecord> ParseMonth(string html, DateTime month)
        => ParseEventItems(html, dateNode =>
        {
            var match = Regex.Match(Text(dateNode), @"^\s*(\d{1,2})");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var day)
                || day < 1 || day > DateTime.DaysInMonth(month.Year, month.Month))
                throw new InvalidOperationException("Unable to read an event date from the website.");
            return new DateTime(month.Year, month.Month, day);
        }, requireBookingRole: false);

    public static IReadOnlyList<CalendarEventRecord> ParseBookings(string html, DateTime today)
        => ParseEventItems(html, dateNode =>
        {
            // Booking rows omit the year. Resolve their dates as upcoming occurrences.
            var dateText = Regex.Replace(dateNode?.InnerHtml ?? "", @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
            dateText = Text(Document(dateText).DocumentNode);
            var match = Regex.Match(dateText, @"^(\d{1,2})\s*([A-Za-z]+)(?:\s+(\d{4}))?$", RegexOptions.CultureInvariant);
            if (!match.Success)
                throw new InvalidOperationException("Unable to read a booking date from the website.");
            var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var monthName = match.Groups[2].Value;
            var month = Enumerable.Range(1, 12).FirstOrDefault(m =>
                string.Equals(CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m), monthName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m), monthName, StringComparison.OrdinalIgnoreCase));
            if (month == 0 || day < 1 || day > DateTime.DaysInMonth(2000, month))
                throw new InvalidOperationException("Unable to read a booking date from the website.");
            if (match.Groups[3].Success)
            {
                var year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                if (year < 1 || day > DateTime.DaysInMonth(year, month))
                    throw new InvalidOperationException("Unable to read a booking date from the website.");
                return new DateTime(year, month, day);
            }
            for (var year = today.Year; year <= today.Year + 4; year++)
            {
                if (day > DateTime.DaysInMonth(year, month)) continue;
                var date = new DateTime(year, month, day);
                if (date >= today.Date) return date;
            }
            throw new InvalidOperationException("Unable to resolve a booking date from the website.");
        }, requireBookingRole: true);

    private static IReadOnlyList<CalendarEventRecord> ParseEventItems(
        string html, Func<HtmlNode?, DateTime> resolveDate, bool requireBookingRole)
    {
        var records = new List<CalendarEventRecord>();
        foreach (var item in Document(html).DocumentNode.Descendants("div").Where(n => HasClass(n, "event-item")))
        {
            var title = item.Descendants("div").FirstOrDefault(n => HasClass(n, "ei-title"));
            if (IsBattleSchool(Text(title))) continue;
            var href = item.Descendants("a").Select(n => n.GetAttributeValue("href", ""))
                .FirstOrDefault(h => Regex.IsMatch(h, @"^/members/event/\d+/?$"));
            if (href == null) continue;
            var date = item.Descendants("div").FirstOrDefault(n => HasClass(n, "ei-date"));
            var subtitle = Text(title?.Descendants().FirstOrDefault(n => HasClass(n, "ei-subtitle")));
            string Field(string name) => Regex.Match(subtitle, $@"{name}:\s*([^,]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            var titleText = Text(title?.Elements("p").FirstOrDefault());
            var booking = Regex.Match(titleText, @"\[\s*Booked as\s+([^\]]+)\]", RegexOptions.IgnoreCase);
            CalendarEventRole? role = booking.Groups[1].Value.Trim().ToLowerInvariant() switch
            {
                "player" => CalendarEventRole.Player,
                "referee" or "ref" => CalendarEventRole.Referee,
                "assistant referee" or "a referee" or "a-ref" or "a ref" => CalendarEventRole.ARef,
                "crew" => CalendarEventRole.Crew,
                _ => null
            };
            if (requireBookingRole && !role.HasValue)
                throw new InvalidOperationException("Unable to read your booking role from the website.");
            records.Add(new CalendarEventRecord
            {
                Id = Regex.Match(href, @"\d+").Value,
                Name = booking.Success ? titleText.Remove(booking.Index, booking.Length).Trim() : titleText,
                Date = resolveDate(date),
                Threshold = Field("Threshold"), EventType = Field("Type"), MyBookedRole = role
            });
        }
        return records.DistinctBy(r => r.Id).OrderBy(r => r.Date).ToList();
    }

    public static bool PopulateDetails(CalendarEventRecord record, string html, string displayName)
    {
        var doc = Document(html);
        if (IsBattleSchool(Text(doc.DocumentNode.SelectSingleNode("//h1")))
            || doc.DocumentNode.SelectNodes("//input[@name='event_type']")?.Any(n => IsBattleSchool(n.GetAttributeValue("value", ""))) == true)
            return false;
        var tabs = doc.GetElementbyId("tabs");
        if (tabs == null) throw new InvalidOperationException("Unable to read event bookings from the website.");
        record.MyBookedRole = null;
        record.CurrentUserName = displayName;
        foreach (var link in tabs.Descendants("a"))
        {
            var match = Regex.Match(Text(link), @"^(Player|Referee|Assistant Referee|Crew)\s+(\d+)\s*/\s*(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            var role = match.Groups[1].Value.ToLowerInvariant() switch
            {
                "player" => CalendarEventRole.Player, "referee" => CalendarEventRole.Referee,
                "assistant referee" => CalendarEventRole.ARef, _ => CalendarEventRole.Crew
            };
            var panel = doc.GetElementbyId(link.GetAttributeValue("href", "").TrimStart('#'));
            if (panel == null) throw new InvalidOperationException("Unable to read an event roster.");
            var column = panel.Descendants("div").FirstOrDefault(n => HasClass(n, "gs-10"));
            var paragraphs = column?.Elements("p").ToList();
            var namesHtml = paragraphs?.Skip(1).FirstOrDefault()?.InnerHtml ?? "";
            var names = Regex.Split(namesHtml, @"<br\s*/?>", RegexOptions.IgnoreCase)
                .Select(fragment => Text(Document(fragment).DocumentNode)).Where(s => s.Length > 0).ToList();
            var capacity = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            switch (role)
            {
                case CalendarEventRole.Player: record.Players = names; record.PlayerCapacity = capacity; break;
                case CalendarEventRole.Referee: record.Referees = names; record.RefereeCapacity = capacity; break;
                case CalendarEventRole.ARef: record.ARefs = names; record.ARefCapacity = capacity; break;
                case CalendarEventRole.Crew: record.Crew = names; record.CrewCapacity = capacity; break;
            }
            // Match the full name from the authenticated account, or the server's
            // session-specific cancellation form, rather than another member's name.
            if (names.Any(n => string.Equals(n, displayName, StringComparison.OrdinalIgnoreCase))
                || panel.Descendants().Any(n => n.GetAttributeValue("name", "") == "submit_cancel"))
            {
                record.MyBookedRole = role;

            }
        }
        return true;
    }
}
