using labyItems.Models.Calendar;

namespace labyItems.Services;

public interface ICalendarEventCache
{
    Task<CalendarCacheSnapshot?> LoadAsync(string account);
    Task SaveAsync(string account, CalendarCacheSnapshot snapshot);
}
