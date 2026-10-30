namespace labyItems.Models.Calendar;

public sealed record CalendarDataStatus(DateTimeOffset UpdatedAtUtc, bool IsOffline, bool IsPartial = false)
{
    public string GetMessage(DateTimeOffset now)
    {
        var age = now - UpdatedAtUtc;
        var suffix = age > TimeSpan.FromHours(1) ? " · Out of date (over 1 hour old)" : "";
        return $"Last updated {UpdatedAtUtc.ToLocalTime():d MMM yyyy HH:mm}{(IsOffline ? " · Offline, saved data" : "")}{suffix}{(IsPartial ? " · Saved bookings only; other events may be missing" : "")}";
    }
}
