namespace labyItems.Pages.Calculator;

public partial class IspComponentTypePage : ContentPage
{
    private readonly IspCalculator _calculator;

    public IspComponentTypePage(IspCalculator calculator)
    {
        _calculator = calculator;
        InitializeComponent();
        BuildComponentButtons();
    }

    private void BuildComponentButtons()
    {
        ComponentList.Children.Clear();

        foreach (var kind in Enum.GetValues<IspComponentKind>())
        {
            var button = new Button
            {
                Text = kind switch
                {
                    IspComponentKind.Utility => "Utility",
                    _ => kind.ToString()
                },
                CornerRadius = 18,
                HeightRequest = 56,
                FontSize = 18
            };

            button.Clicked += async (_, _) =>
            {
                await Navigation.PopAsync();
                await _calculator.BeginAddComponentAsync(kind);
            };

            ComponentList.Children.Add(button);
        }
    }
}
