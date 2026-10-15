namespace labyItems.Controls;

using labyItems.Services;

public partial class DetailPageHeader : ContentView
{
    public static readonly BindableProperty HeaderTextProperty = BindableProperty.Create(
        nameof(HeaderText), typeof(string), typeof(DetailPageHeader), string.Empty,
        propertyChanged: OnHeaderTextChanged);

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public DetailPageHeader()
    {
        InitializeComponent();
        UpdateHeaderAffordance();
    }

    private static void OnHeaderTextChanged(BindableObject bindable, object oldValue, object newValue)
        => ((DetailPageHeader)bindable).UpdateHeaderAffordance();

    private void UpdateHeaderAffordance()
    {
        if (HeaderLabel is null)
            return;

        HeaderLabel.TextDecorations = BookDirectory.TryResolve(HeaderText, out _)
            ? TextDecorations.Underline
            : TextDecorations.None;
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
        if (!BookDirectory.TryResolve(HeaderText, out _))
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
