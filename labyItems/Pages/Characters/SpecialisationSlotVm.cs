

using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Controls.Pickers;
using labyItems.Models.Enums;

namespace labyItems.Pages.Characters;

public sealed class SpecialisationSlotVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private readonly Action _onChanged;

    public int Level { get; }
    public string LevelLabel => $"Lvl {Level}";

    public bool UseWardPactEnum { get; }

    private StandardWardPacts? _selectedWardPact;
    public StandardWardPacts? SelectedWardPact
    {
        get => _selectedWardPact;
        set
        {
            if (!Set(ref _selectedWardPact, value)) return;

            if (value is StandardWardPacts v)
                SelectedOption = WardPactOptions.LabelFor(v);
            else
                SelectedOption = null;
        }
    }

    private string? _selectedOption;
    public string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (!Set(ref _selectedOption, value)) return;
            _onChanged();
        }
    }

    private Func<IReadOnlyList<string>>? _getFilteredOptions;
    public IReadOnlyList<string> FilteredOptionNames => _getFilteredOptions?.Invoke() ?? Array.Empty<string>();

    public SpecialisationSlotVm(int level, bool useWardPactEnum, Action onChanged)
    {
        Level = level;
        UseWardPactEnum = useWardPactEnum;
        _onChanged = onChanged;
    }

    public void SetOptionsSource(Func<IReadOnlyList<string>> getFilteredOptions)
    {
        _getFilteredOptions = getFilteredOptions;
        Raise(nameof(FilteredOptionNames));
    }

    public void RaiseFilteredOptionsChanged()
    {
        Raise(nameof(FilteredOptionNames));
    }
}
