using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class AbilityEntryVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private readonly Action _onChanged;
    private string _name;
    private int _cost;
    private int _runningTotal;

    public string Name
    {
        get => _name;
        set
        {
            var next = value ?? string.Empty;
            if (_name == next) return;
            _name = next;
            Raise();
            Raise(nameof(NameWithCost));
            _onChanged();
        }
    }

    public int Cost
    {
        get => _cost;
        private set
        {
            if (_cost == value) return;
            _cost = value;
            Raise();
            Raise(nameof(NameWithCost));
            _onChanged();
        }
    }

    public int RunningTotal
    {
        get => _runningTotal;
        private set
        {
            if (_runningTotal == value) return;
            _runningTotal = value;
            Raise();
            Raise(nameof(RunningTotalText));
        }
    }

    public string NameWithCost => $"{Name} ({Cost})";
    public string RunningTotalText => $"Total: {RunningTotal}";

    public AbilityEntryVm(string name, int cost, Action onChanged)
    {
        _name = name ?? string.Empty;
        _cost = cost;
        _onChanged = onChanged;
    }

    public void SetRunningTotal(int total)
    {
        RunningTotal = total;
    }
}

public sealed class ItemLineVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _text;
    private readonly bool _isReadOnly;

    public bool IsReadOnly => _isReadOnly;
    public bool ShowDeleteButton => !_isReadOnly;

    public string Text
    {
        get => _text;
        set
        {
            if (_isReadOnly)
                return;
            if (_text == value) return;
            _text = value ?? string.Empty;
            Raise();
        }
    }

    public ItemLineVm(string text, bool isReadOnly = false)
    {
        _isReadOnly = isReadOnly;
        _text = text ?? string.Empty;
    }
}

