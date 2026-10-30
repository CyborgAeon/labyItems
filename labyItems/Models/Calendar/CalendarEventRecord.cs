namespace labyItems.Models.Calendar;

public sealed class CalendarEventRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
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
    public string CurrentUserName { get; set; } = string.Empty;

    public string DayLabel => Date.ToString("ddd d MMM");
    public string MonthDayLabel => Date.ToString("dddd d MMM");
    public string SummaryLine => $"P {Players.Count}/{PlayerCapacity}  R {Referees.Count}/{RefereeCapacity}  A {ARefs.Count}/{ARefCapacity}  C {Crew.Count}/{CrewCapacity}";
    public string ThresholdBadge => Threshold.Equals("No-Max", StringComparison.OrdinalIgnoreCase) ? "No-Max" : Threshold;
    public string AreaSummary => BookedAreas.Count == 0 ? "No area booked" : $"Area {string.Join(", ", BookedAreas)}";
    public IReadOnlyList<string> AllParticipants => Players.Concat(Referees).Concat(ARefs).Concat(Crew).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
