using System.Collections;
using System.Windows.Input;

namespace labyItems.Controls;

public partial class SearchFilterModal : ContentView
{
    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(SearchFilterModal));
    public static readonly BindableProperty SectionsProperty = BindableProperty.Create(nameof(Sections), typeof(IEnumerable), typeof(SearchFilterModal));
    public static readonly BindableProperty ToggleChipCommandProperty = BindableProperty.Create(nameof(ToggleChipCommand), typeof(ICommand), typeof(SearchFilterModal));
    public static readonly BindableProperty CancelCommandProperty = BindableProperty.Create(nameof(CancelCommand), typeof(ICommand), typeof(SearchFilterModal));
    public static readonly BindableProperty ApplyCommandProperty = BindableProperty.Create(nameof(ApplyCommand), typeof(ICommand), typeof(SearchFilterModal));
    public SearchFilterModal() => InitializeComponent();
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public IEnumerable? Sections { get => (IEnumerable?)GetValue(SectionsProperty); set => SetValue(SectionsProperty, value); }
    public ICommand? ToggleChipCommand { get => (ICommand?)GetValue(ToggleChipCommandProperty); set => SetValue(ToggleChipCommandProperty, value); }
    public ICommand? CancelCommand { get => (ICommand?)GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
    public ICommand? ApplyCommand { get => (ICommand?)GetValue(ApplyCommandProperty); set => SetValue(ApplyCommandProperty, value); }
}
