namespace labyItems.Controls;

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
}
