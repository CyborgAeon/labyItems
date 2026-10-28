namespace labyItems.Models.Calendar;

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
