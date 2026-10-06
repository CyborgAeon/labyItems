namespace labyItems.Controls;

using labyItems.Services;

public partial class DetailPageHeader : ContentView
{
    public static readonly BindableProperty HeaderTextProperty = BindableProperty.Create(
        nameof(HeaderText), typeof(string), typeof(DetailPageHeader), string.Empty);

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public DetailPageHeader()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
            return;
        }

        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync();
    }

    private async void OnHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (!SourceBookPdfCatalog.TryResolve(HeaderText, out _))
            return;

        HeaderLabel.InputTransparent = true;
        try
        {
            await SourceBookPdfLauncher.OpenAsync(HeaderText);
        }
        catch (Exception ex)
        {
            var page = GetParentPage();
            if (page is not null)
                await page.DisplayAlert("Unable to open source book", ex.Message, "OK");
        }
        finally
        {
            HeaderLabel.InputTransparent = false;
        }
    }

    private Page? GetParentPage()
    {
        Element? current = this;
        while (current is not null)
        {
            if (current is Page page)
                return page;
            current = current.Parent;
        }

        return null;
    }
}
