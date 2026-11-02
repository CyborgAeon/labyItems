using labyItems.Models.Calendar;

namespace labyItems.Services;

public sealed class WorkspaceCalendarEventCache(AccountWorkspace workspace) : ICalendarEventCache
{
    public async Task<CalendarCacheSnapshot?> LoadAsync(string account)
    {
        var directory = workspace.RequireDirectory(account);
        var result = await new FileCalendarEventCache(Path.Combine(directory, "calendar")).LoadAsync(account);
        workspace.RequireDirectory(account);
        return result;
    }
    public async Task SaveAsync(string account, CalendarCacheSnapshot snapshot)
    {
        var directory = workspace.RequireDirectory(account);
        await new FileCalendarEventCache(Path.Combine(directory, "calendar")).SaveAsync(account, snapshot);
        workspace.RequireDirectory(account);
        workspace.Checkpoint();
    }
}
