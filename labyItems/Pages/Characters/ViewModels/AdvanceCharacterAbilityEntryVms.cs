using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using labyItems.Helpers;
using labyItems.Models;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class AbilityEntryVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private readonly Action _onChanged;
    private readonly string _abilityKey;
    private string _name;
    private int _cost;
    private int _runningTotal;
    private int _rowIndex;

    public string AbilityKey => _abilityKey;

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
    public Color RowBackgroundColor => (_rowIndex % 2) == 0
        ? Colors.White
        : Color.FromArgb("#FAF8F3");

    public AbilityEntryVm(string name, int cost, string abilityKey, Action onChanged)
    {
        _name = name ?? string.Empty;
        _cost = cost;
        _onChanged = onChanged;
        _abilityKey = (abilityKey ?? string.Empty).Trim();
        _rowIndex = 0;
    }

    public void SetRunningTotal(int total)
    {
        RunningTotal = total;
    }

    public void SetRowIndex(int rowIndex)
    {
        var next = Math.Max(0, rowIndex);
        if (_rowIndex == next)
            return;

        _rowIndex = next;
        Raise(nameof(RowBackgroundColor));
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

public sealed class CharacterItemEntryVm
{
    public CharacterItemEntryVm(Item item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Name = ItemDisplayHelper.BuildDisplayName(item);
        Isp = item.Isp;
        IsMonsterPointItem = ItemDisplayHelper.IsMonsterPointItem(item);
    }

    public Item Item { get; }
    public string Name { get; }
    public int Isp { get; }
    public string IspText => Isp.ToString();
    public bool IsMonsterPointItem { get; }
}

public sealed class MultiClassEntryVm
{
    public MultiClassEntryVm(
        string key,
        string name,
        int level,
        int maxLevel,
        int cost,
        IReadOnlyList<MultiClassAbilityLinkVm>? abilityDetails = null,
        IReadOnlyList<string>? choiceSetRefs = null)
    {
        Key = key ?? string.Empty;
        Name = name ?? string.Empty;
        Level = level;
        MaxLevel = Math.Max(1, maxLevel);
        Cost = cost;
        AbilityDetails = (abilityDetails ?? Array.Empty<MultiClassAbilityLinkVm>())
            .Where(detail => detail != null && detail.IsValid)
            .ToList();
        ChoiceSetRefs = (choiceSetRefs ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string Key { get; }
    public string Name { get; }
    public int Level { get; }
    public int MaxLevel { get; }
    public int Cost { get; }
    public IReadOnlyList<MultiClassAbilityLinkVm> AbilityDetails { get; }
    public IReadOnlyList<string> ChoiceSetRefs { get; }
    public bool HasInfo => AbilityDetails.Count > 0;
    public bool HasChoiceSets => ChoiceSetRefs.Count > 0;
    public string LevelText => $"{Level}/{MaxLevel}";
    public string CostText => Cost.ToString();
}

public sealed class MultiRaceEntryVm
{
    public MultiRaceEntryVm(
        string key,
        string name,
        int level,
        int maxLevel,
        int cost,
        IReadOnlyList<MultiClassAbilityLinkVm>? abilityDetails = null,
        IReadOnlyList<string>? choiceSetRefs = null)
    {
        Key = key ?? string.Empty;
        Name = name ?? string.Empty;
        Level = level;
        MaxLevel = Math.Max(1, maxLevel);
        Cost = cost;
        AbilityDetails = (abilityDetails ?? Array.Empty<MultiClassAbilityLinkVm>())
            .Where(detail => detail != null && detail.IsValid)
            .ToList();
        ChoiceSetRefs = (choiceSetRefs ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string Key { get; }
    public string Name { get; }
    public int Level { get; }
    public int MaxLevel { get; }
    public int Cost { get; }
    public IReadOnlyList<MultiClassAbilityLinkVm> AbilityDetails { get; }
    public IReadOnlyList<string> ChoiceSetRefs { get; }
    public bool HasInfo => AbilityDetails.Count > 0;
    public bool HasChoiceSets => ChoiceSetRefs.Count > 0;
    public string LevelText => $"{Level}/{MaxLevel}";
    public string CostText => Cost.ToString();
}

public sealed record MultiClassAbilityLinkVm(string DisplayName, string LookupKey)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(DisplayName)
        || !string.IsNullOrWhiteSpace(LookupKey);
}
