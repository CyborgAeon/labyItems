using labyItems.Models.Calendar;

namespace labyItems.Pages.Calendar;

/// <summary>Keeps the one-hour warning accurate while a page remains open.</summary>
internal sealed class CalendarStatusLabelUpdater(Label label, Func<CalendarDataStatus?> getStatus, Func<Task> onDateChanged)
{
    private IDispatcherTimer? _timer;
    private DateTime _displayDate = DateTime.Today;
    public void Start()
    {
        Update();
        if (_timer != null) return;
        _timer = label.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMinutes(1);
        _timer.Tick += OnTick;
        _timer.Start();
    }
    public void Stop()
    {
        if (_timer == null) return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }
    private async void OnTick(object? sender, EventArgs e)
    {
        if (_displayDate != DateTime.Today)
        {
            _displayDate = DateTime.Today;
            try { await onDateChanged(); }
            catch (InvalidOperationException) { Stop(); } // Session changed while the page was closing.
        }
        Update();
    }
    public void Update()
    {
        var status = getStatus();
        label.IsVisible = status != null;
        label.Text = status?.GetMessage(DateTimeOffset.UtcNow) ?? "";
    }
}
