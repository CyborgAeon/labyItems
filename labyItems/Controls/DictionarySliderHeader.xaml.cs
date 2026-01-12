using Microsoft.Maui.Controls;

namespace labyItems.Controls
{
    public partial class DictionarySliderHeader : ContentView
    {
        public static readonly BindableProperty KeyTextProperty = BindableProperty.Create(
            nameof(KeyText),
            typeof(string),
            typeof(DictionarySliderHeader),
            string.Empty,
            propertyChanged: OnKeyTextChanged);

        public string KeyText
        {
            get => (string)GetValue(KeyTextProperty);
            set => SetValue(KeyTextProperty, value);
        }

        public static readonly BindableProperty ValueTextProperty = BindableProperty.Create(
            nameof(ValueText),
            typeof(string),
            typeof(DictionarySliderHeader),
            string.Empty,
            propertyChanged: OnValueTextChanged);

        public string ValueText
        {
            get => (string)GetValue(ValueTextProperty);
            set => SetValue(ValueTextProperty, value);
        }

        public DictionarySliderHeader()
        {
            InitializeComponent();
            ApplyKeyText(KeyText);
            ApplyValueText(ValueText);
        }

        private static void OnKeyTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (DictionarySliderHeader)bindable;
            control.ApplyKeyText(newValue as string ?? string.Empty);
        }

        private static void OnValueTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (DictionarySliderHeader)bindable;
            control.ApplyValueText(newValue as string ?? string.Empty);
        }

        private void ApplyKeyText(string text) => KeyLabel.Text = text ?? string.Empty;

        private void ApplyValueText(string text)
        {
            var safeText = text ?? string.Empty;
            ValueLabel.Text = safeText;
            ValueLabel.IsVisible = !string.IsNullOrWhiteSpace(safeText);
        }
    }
}
