namespace labyItems.Models.Calendar;

public sealed class CalendarCacheSnapshot
{
    public List<CalendarEventRecord> Events { get; set; } = new();
    public Dictionary<string, DateTimeOffset> MonthsUpdatedAtUtc { get; set; } = new();
    public DateTimeOffset? BookingsUpdatedAtUtc { get; set; }
}
