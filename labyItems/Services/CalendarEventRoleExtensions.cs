using labyItems.Models.Calendar;

namespace labyItems.Services;

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
