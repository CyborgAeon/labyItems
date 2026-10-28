using System.Linq;
using labyItems.Services;
using labyItems.Pages.Calendar;
using labyItems.Pages.Calculator;
using labyItems.Pages.NonStandard;

namespace labyItems.Pages;

public partial class ItemRoutePage : ContentPage
{
    private bool _isBusy;

    public ItemRoutePage()
    {
        InitializeComponent();
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var allowed = LabyrintheSession.Shared.CanViewCalendar;
        CalendarCard.IsEnabled = allowed;
        CalendarCard.Opacity = allowed ? 1 : 0.55;
        SemanticProperties.SetDescription(CalendarCard, allowed ? "Calendar" : "Calendar locked. Sign in with a member account.");
    }

    private async void OnOpenCalendarTapped(object sender, TappedEventArgs e)
    {
        if (!LabyrintheSession.Shared.CanViewCalendar) return;
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new UnifiedCalendarPage()));
    }

    private async void OnOpenNonStandardDashboardTapped(object sender, TappedEventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new NonStandardWalletPage()));
    }

    private async Task ExecuteNavigationAsync(Func<Task> navigationAction, string errorTitle = "Navigation failed")
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            await navigationAction();
        }
        catch (Exception ex)
        {
            await DisplayAlert(errorTitle, ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
