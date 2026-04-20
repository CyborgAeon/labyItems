using System.Collections.ObjectModel;

namespace labyItems.Services;

public sealed class CalendarEventStore
{
    private const string CurrentUserDisplayName = "admin";

    private static readonly string[] Thresholds =
    {
        "100", "250", "500", "750", "1000", "1500", "3000", "5250", "7500", "10000", "15000", "30000", "No-Max"
    };

    private static readonly string[] EventTypes =
    {
        "Single", "Double", "Triple", "Overland", "Extended Length"
    };

    private static readonly string[] Locations =
    {
        "Caves", "East Wood", "Broken Tower", "River Gate", "Old Keep", "Stone Vale"
    };

    private static readonly string[] NamePrefixes =
    {
        "Goblin Hunt", "Caves Open", "Border Patrol", "Relic Search", "Market Escort", "Moonlit Ruins"
    };

    private static readonly string[] PeoplePool =
    {
        "Alex", "Jordan", "Taylor", "Morgan", "Jamie", "Casey", "Riley", "Parker", "Harper", "Sam", "Robin", "Drew", "Avery", "Quinn"
    };

    private readonly List<CalendarEventRecord> _events = new();
    private readonly HashSet<DateTime> _disabledDates = new();
    private readonly object _sync = new();

    public static CalendarEventStore Shared { get; } = new();

    public CalendarEventStore()
    {
        Seed();
    }

    public IReadOnlyList<CalendarEventRecord> GetEventsForMonth(DateTime month)
    {
        lock (_sync)
        {
            return _events
                .Where(evt => evt.Date.Year == month.Year && evt.Date.Month == month.Month)
                .OrderBy(evt => evt.Date)
                .ThenBy(evt => evt.Name, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
        }
    }

    public IReadOnlyList<CalendarEventRecord> GetBookedEvents()
    {
        lock (_sync)
        {
            return _events
                .Where(evt => ResolveBookedRole(evt, CurrentUserDisplayName).HasValue)
                .OrderBy(evt => evt.Date)
                .ThenBy(evt => evt.Name, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
        }
    }

    public CalendarEventRecord? GetEvent(string id)
    {
        lock (_sync)
        {
            var evt = _events.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            return evt == null ? null : Clone(evt);
        }
    }

    public bool IsDateDisabled(DateTime date)
    {
        lock (_sync)
        {
            return _disabledDates.Contains(date.Date);
        }
    }

    public IReadOnlyCollection<DateTime> GetDisabledDatesForMonth(DateTime month)
    {
        lock (_sync)
        {
            return _disabledDates
                .Where(date => date.Year == month.Year && date.Month == month.Month)
                .OrderBy(date => date)
                .ToList();
        }
    }

    public CalendarEventRecord AddEvent(CreateCalendarEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_sync)
        {
            if (_disabledDates.Contains(request.Date.Date))
                throw new InvalidOperationException("That date is unavailable for booking.");

            var threshold = string.IsNullOrWhiteSpace(request.Threshold) ? "100" : request.Threshold.Trim();
            var eventType = string.IsNullOrWhiteSpace(request.EventType) ? "Single" : request.EventType.Trim();
            var title = string.IsNullOrWhiteSpace(request.Title) ? $"New Event {threshold}" : request.Title.Trim();
            var location = string.IsNullOrWhiteSpace(request.Location) ? "TBC" : request.Location.Trim();

            var playerCapacity = threshold.Equals("No-Max", StringComparison.OrdinalIgnoreCase) ? 24 : 16;
            var referees = new List<string>();
            var aRefs = new List<string>();
            var crew = new List<string>();
            var players = request.Invitees
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var record = new CalendarEventRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = title,
                Date = request.Date.Date,
                Location = location,
                Threshold = threshold,
                EventType = eventType,
                PlayerCapacity = playerCapacity,
                RefereeCapacity = 2,
                ARefCapacity = 1,
                CrewCapacity = 6,
                Players = players,
                Referees = referees,
                ARefs = aRefs,
                Crew = crew,
                BookedAreas = request.BookedAreas
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Select(code => code.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            _events.Add(record);
            SortEvents();
            return Clone(record);
        }
    }

    public CalendarEventRecord UpdateEvent(string id, CreateCalendarEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_sync)
        {
            var record = _events.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (record == null)
                throw new InvalidOperationException("Event not found.");

            if (_disabledDates.Contains(request.Date.Date) && request.Date.Date != record.Date.Date)
                throw new InvalidOperationException("That date is unavailable for booking.");

            record.Name = string.IsNullOrWhiteSpace(request.Title) ? record.Name : request.Title.Trim();
            record.Date = request.Date.Date;
            record.Location = string.IsNullOrWhiteSpace(request.Location) ? "TBC" : request.Location.Trim();
            record.Threshold = string.IsNullOrWhiteSpace(request.Threshold) ? record.Threshold : request.Threshold.Trim();
            record.EventType = string.IsNullOrWhiteSpace(request.EventType) ? record.EventType : request.EventType.Trim();
            record.BookedAreas = request.BookedAreas
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var invitees = request.Invitees
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            record.Players = invitees;
            SortEvents();
            return Clone(record);
        }
    }

    public JoinRoleResult TryJoinRole(string id, CalendarEventRole role, string userDisplayName)
    {
        lock (_sync)
        {
            var evt = _events.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (evt == null)
                return new JoinRoleResult(false, "Event not found.");

            var user = string.IsNullOrWhiteSpace(userDisplayName) ? "admin" : userDisplayName.Trim();
            var roleList = GetRoleList(evt, role);
            var capacity = GetRoleCapacity(evt, role);

            if (roleList.Any(existing => string.Equals(existing, user, StringComparison.OrdinalIgnoreCase)))
                return new JoinRoleResult(false, $"You are already booked as {role.GetDisplayName().ToLowerInvariant()}.");

            if (roleList.Count >= capacity)
                return new JoinRoleResult(false, $"{role.GetDisplayName()} is already full.");

            RemoveExistingRoles(evt, user);
            roleList.Add(user);
            return new JoinRoleResult(true, $"Placeholder role request submitted for {role.GetDisplayName().ToLowerInvariant()}.");
        }
    }

    private void Seed()
    {
        lock (_sync)
        {
            if (_events.Count > 0)
                return;

            SeedDisabledWeekends();

            var start = new DateTime(DateTime.Today.Year - 1, 1, 1);
            var end = new DateTime(DateTime.Today.Year + 1, 12, 31);
            var index = 0;

            for (var day = start; day <= end; day = day.AddDays(1))
            {
                if (day.DayOfWeek is not (DayOfWeek.Wednesday or DayOfWeek.Saturday or DayOfWeek.Sunday))
                    continue;

                if (_disabledDates.Contains(day.Date))
                    continue;

                var threshold = Thresholds[index % Thresholds.Length];
                var eventType = EventTypes[index % EventTypes.Length];
                var location = Locations[index % Locations.Length];
                var name = $"{NamePrefixes[index % NamePrefixes.Length]} {threshold}";
                var playerCapacity = day.DayOfWeek == DayOfWeek.Wednesday ? 12 : 16;
                var refereeCapacity = day.DayOfWeek == DayOfWeek.Wednesday ? 1 : 2;
                var crewCapacity = day.DayOfWeek == DayOfWeek.Wednesday ? 4 : 6;

                _events.Add(new CalendarEventRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = name,
                    Date = day,
                    Location = location,
                    Threshold = threshold,
                    EventType = eventType,
                    PlayerCapacity = playerCapacity,
                    RefereeCapacity = refereeCapacity,
                    ARefCapacity = 1,
                    CrewCapacity = crewCapacity,
                    Players = BuildNames(index, Math.Min(playerCapacity - 2, 4 + (index % 6))),
                    Referees = BuildNames(index + 3, Math.Min(refereeCapacity, 1)),
                    ARefs = index % 3 == 0 ? BuildNames(index + 6, 1) : new List<string>(),
                    Crew = BuildNames(index + 9, Math.Min(crewCapacity - 1, 2 + (index % 3))),
                    BookedAreas = new List<string> { $"{(char)('A' + (index % 4))}" }
                });

                index++;
            }

            SortEvents();
        }
    }

    private void SortEvents()
    {
        _events.Sort((left, right) =>
        {
            var date = left.Date.CompareTo(right.Date);
            if (date != 0)
                return date;

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static List<string> BuildNames(int seed, int count)
    {
        var list = new List<string>();
        for (var i = 0; i < count; i++)
            list.Add(PeoplePool[(seed + i) % PeoplePool.Length]);

        return list;
    }

    private static CalendarEventRecord Clone(CalendarEventRecord source)
        => new()
        {
            Id = source.Id,
            Name = source.Name,
            Date = source.Date,
            Location = source.Location,
            Threshold = source.Threshold,
            EventType = source.EventType,
            PlayerCapacity = source.PlayerCapacity,
            RefereeCapacity = source.RefereeCapacity,
            ARefCapacity = source.ARefCapacity,
            CrewCapacity = source.CrewCapacity,
            Players = new List<string>(source.Players),
            Referees = new List<string>(source.Referees),
            ARefs = new List<string>(source.ARefs),
            Crew = new List<string>(source.Crew),
            BookedAreas = new List<string>(source.BookedAreas),
            MyBookedRole = ResolveBookedRole(source, CurrentUserDisplayName)
        };

    private static CalendarEventRole? ResolveBookedRole(CalendarEventRecord source, string userDisplayName)
    {
        if (source.Players.Any(name => string.Equals(name, userDisplayName, StringComparison.OrdinalIgnoreCase)))
            return CalendarEventRole.Player;
        if (source.Referees.Any(name => string.Equals(name, userDisplayName, StringComparison.OrdinalIgnoreCase)))
            return CalendarEventRole.Referee;
        if (source.ARefs.Any(name => string.Equals(name, userDisplayName, StringComparison.OrdinalIgnoreCase)))
            return CalendarEventRole.ARef;
        if (source.Crew.Any(name => string.Equals(name, userDisplayName, StringComparison.OrdinalIgnoreCase)))
            return CalendarEventRole.Crew;

        return null;
    }

    private static void RemoveExistingRoles(CalendarEventRecord record, string user)
    {
        record.Players.RemoveAll(name => string.Equals(name, user, StringComparison.OrdinalIgnoreCase));
        record.Referees.RemoveAll(name => string.Equals(name, user, StringComparison.OrdinalIgnoreCase));
        record.ARefs.RemoveAll(name => string.Equals(name, user, StringComparison.OrdinalIgnoreCase));
        record.Crew.RemoveAll(name => string.Equals(name, user, StringComparison.OrdinalIgnoreCase));
    }

    private void SeedDisabledWeekends()
    {
        var anchor = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var blockedSaturday = NextDayOfWeek(anchor, DayOfWeek.Saturday).AddDays(14);
        var blockedSunday = blockedSaturday.AddDays(1);
        var laterSaturday = blockedSaturday.AddMonths(1);
        while (laterSaturday.DayOfWeek != DayOfWeek.Saturday)
            laterSaturday = laterSaturday.AddDays(1);

        var laterSunday = laterSaturday.AddDays(1);
        _disabledDates.Add(blockedSaturday.Date);
        _disabledDates.Add(blockedSunday.Date);
        _disabledDates.Add(laterSaturday.Date);
        _disabledDates.Add(laterSunday.Date);
    }

    private static DateTime NextDayOfWeek(DateTime start, DayOfWeek dayOfWeek)
    {
        var date = start.Date;
        while (date.DayOfWeek != dayOfWeek)
            date = date.AddDays(1);

        return date;
    }

    private static List<string> GetRoleList(CalendarEventRecord record, CalendarEventRole role)
        => role switch
        {
            CalendarEventRole.Player => record.Players,
            CalendarEventRole.Referee => record.Referees,
            CalendarEventRole.ARef => record.ARefs,
            CalendarEventRole.Crew => record.Crew,
            _ => record.Players
        };

    private static int GetRoleCapacity(CalendarEventRecord record, CalendarEventRole role)
        => role switch
        {
            CalendarEventRole.Player => record.PlayerCapacity,
            CalendarEventRole.Referee => record.RefereeCapacity,
            CalendarEventRole.ARef => record.ARefCapacity,
            CalendarEventRole.Crew => record.CrewCapacity,
            _ => record.PlayerCapacity
        };
}

public sealed class CalendarEventRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Threshold { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int PlayerCapacity { get; set; }
    public int RefereeCapacity { get; set; }
    public int ARefCapacity { get; set; }
    public int CrewCapacity { get; set; }
    public List<string> Players { get; set; } = new();
    public List<string> Referees { get; set; } = new();
    public List<string> ARefs { get; set; } = new();
    public List<string> Crew { get; set; } = new();
    public List<string> BookedAreas { get; set; } = new();
    public CalendarEventRole? MyBookedRole { get; set; }

    public string DayLabel => Date.ToString("ddd d MMM");
    public string MonthDayLabel => Date.ToString("dddd d MMMM");
    public string SummaryLine => $"P {Players.Count}/{PlayerCapacity}  R {Referees.Count}/{RefereeCapacity}  A {ARefs.Count}/{ARefCapacity}  C {Crew.Count}/{CrewCapacity}";
    public string ThresholdBadge => Threshold.Equals("No-Max", StringComparison.OrdinalIgnoreCase) ? "No-Max" : Threshold;
    public string AreaSummary => BookedAreas.Count == 0 ? "No area booked" : $"Area {string.Join(", ", BookedAreas)}";
    public IReadOnlyList<string> AllParticipants => Players.Concat(Referees).Concat(ARefs).Concat(Crew).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

public sealed class CreateCalendarEventRequest
{
    public string Title { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Threshold { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public IReadOnlyList<string> Invitees { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BookedAreas { get; init; } = Array.Empty<string>();
}

public sealed record JoinRoleResult(bool Succeeded, string Message);

public enum CalendarEventRole
{
    Player,
    Referee,
    ARef,
    Crew
}

public static class CalendarEventRoleExtensions
{
    public static string GetDisplayName(this CalendarEventRole role)
        => role switch
        {
            CalendarEventRole.Player => "Player",
            CalendarEventRole.Referee => "Referee",
            CalendarEventRole.ARef => "A-ref",
            CalendarEventRole.Crew => "Crew",
            _ => "Player"
        };

    public static string GetCalendarColorKey(this CalendarEventRole role)
        => role switch
        {
            CalendarEventRole.Player => "CalendarBookedPlayerColor",
            CalendarEventRole.Referee => "CalendarBookedRefereeColor",
            CalendarEventRole.ARef => "CalendarBookedARefColor",
            CalendarEventRole.Crew => "CalendarBookedCrewColor",
            _ => "Primary"
        };
}
