using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class MiracleListVm : INotifyPropertyChanged
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

    private readonly IReadOnlyList<MiracleService.MiracRaw> _allMiracles;
    private readonly Action _onValidationChanged;
    private readonly Dictionary<string, string> _sphereLookup;
    private readonly ICharacterAdvancementDomainService _domainService;
    private readonly Func<Alignment?> _getAlignment;
    private readonly Func<int> _getPoints;
    private readonly Action<MiracleListVm>? _onExpandRequested;

    private const int MaxListPower = 60;
    private const int MaxAdvancedPower = 10;

    public MiracleListDraft Draft { get; }

    public string Name
    {
        get => Draft.Name;
        set
        {
            if (Draft.Name == value) return;
            Draft.Name = value ?? string.Empty;
            Raise();
        }
    }

    public bool IsScriptures => Draft.IsScriptures;

    public string SourceName => Draft.SourceName ?? string.Empty;

    public string HeaderTitle
    {
        get
        {
            if (IsScriptures)
                return "Scriptures of Faith";

            return string.IsNullOrWhiteSpace(SourceName)
                ? "Base List"
                : $"Base List - {SourceName}";
        }
    }

    public bool IsSaved
    {
        get => Draft.IsImported || Draft.IsSaved;
        set
        {
            if (Draft.IsImported) return;
            if (Draft.IsSaved == value) return;
            Draft.IsSaved = value;
            Raise();
            Raise(nameof(IsReadOnly));
            Raise(nameof(CanEdit));
            Raise(nameof(CanRemoveList));
            Raise(nameof(CanAddSelected));
            Raise(nameof(CanSaveList));
            Raise(nameof(StateLabel));
            Raise(nameof(CanReopenScriptures));
        }
    }

    public bool IsMinimized
    {
        get => Draft.IsMinimized;
        set
        {
            if (Draft.IsMinimized == value) return;
            Draft.IsMinimized = value;
            Raise();
            Raise(nameof(IsExpanded));
            Raise(nameof(CanAddSelected));
        }
    }

    public bool IsExpanded => !IsMinimized;

    public bool IsReadOnly => Draft.IsImported || Draft.IsSaved;
    public bool CanEdit => !IsReadOnly;
    public bool CanRemoveList => CanEdit && !IsScriptures;
    public bool CanReopenScriptures => IsScriptures && IsSaved && !Draft.IsImported;

    public ObservableCollection<MiracleEntryVm> Entries { get; } = new();

    private MiracleOption? _selectedMiracleOption;
    public MiracleOption? SelectedMiracleOption
    {
        get => _selectedMiracleOption;
        set
        {
            if (!Set(ref _selectedMiracleOption, value)) return;
            Raise(nameof(CanAddSelected));
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value ?? string.Empty);
    }

    public ObservableCollection<string> SphereFilterOptions { get; } = new();
    public ObservableCollection<string> SelectedSphereFilters { get; } = new();
    public ObservableCollection<string> AdvancedFilterOptions { get; } = new() { "Advanced", "Handbook" };
    public ObservableCollection<string> SelectedAdvancedFilters { get; } = new();
    public ObservableCollection<string> NeutralAlignmentOptions { get; } = new() { "Good", "Evil" };

    private Dictionary<string, MiracleOption> _filteredOptions = new();
    public Dictionary<string, MiracleOption> FilteredOptions
    {
        get => _filteredOptions;
        private set => Set(ref _filteredOptions, value);
    }

    public ICommand AddSelectedCommand { get; }
    public ICommand RemoveEntryCommand { get; }
    public ICommand SaveListCommand { get; }
    public ICommand ToggleExpandedCommand { get; }
    public ICommand ReopenScripturesCommand { get; }

    private int _goodPoints;
    public int GoodPoints { get => _goodPoints; private set => Set(ref _goodPoints, value); }

    private int _neutralPoints;
    public int NeutralPoints { get => _neutralPoints; private set => Set(ref _neutralPoints, value); }

    private int _evilPoints;
    public int EvilPoints { get => _evilPoints; private set => Set(ref _evilPoints, value); }

    private int _totalPoints;
    public int TotalPoints { get => _totalPoints; private set => Set(ref _totalPoints, value); }

    private int _advancedPoints;
    public int AdvancedPoints { get => _advancedPoints; private set => Set(ref _advancedPoints, value); }

    public int UnselectedPoints => Math.Max(0, MaxListPower - TotalPoints);

    public GridLength GoodWidth => new GridLength(GoodPoints, GridUnitType.Star);
    public GridLength NeutralWidth => new GridLength(NeutralPoints, GridUnitType.Star);
    public GridLength EvilWidth => new GridLength(EvilPoints, GridUnitType.Star);
    public GridLength UnselectedWidth => new GridLength(UnselectedPoints, GridUnitType.Star);

    private int _scripturesAllowed;
    public int ScripturesAllowed
    {
        get => _scripturesAllowed;
        private set
        {
            if (!Set(ref _scripturesAllowed, value)) return;
            RaiseScripturesProgressProperties();
            UpdateScripturesVisuals();
            Raise(nameof(CanAddSelected));
        }
    }

    public int ScripturesUsed => Entries.Count(e => !string.IsNullOrWhiteSpace(e.Draft.Name));

    public int ScripturesRemaining => Math.Max(0, ScripturesAllowed - ScripturesUsed);

    public string ScripturesProgressText => $"Scriptures remaining: {ScripturesRemaining}/{ScripturesAllowed}";

    public string ScripturesUnusedText => $"Unused scriptures: {ScripturesRemaining}/{ScripturesAllowed}";

    public double ScripturesUsedRatio
        => ScripturesAllowed <= 0 ? 0 : Math.Clamp((double)ScripturesUsed / ScripturesAllowed, 0d, 1d);

    public double ScripturesRemainingRatio
        => ScripturesAllowed <= 0 ? 1 : Math.Clamp((double)ScripturesRemaining / ScripturesAllowed, 0d, 1d);

    public bool HasUsedScriptures => ScripturesUsed > 0;

    public GridLength ScripturesUsedWidth
    {
        get
        {
            if (ScripturesAllowed <= 0)
                return new GridLength(0, GridUnitType.Star);

            var used = Math.Clamp(ScripturesUsed, 0, ScripturesAllowed);
            return new GridLength(used, GridUnitType.Star);
        }
    }

    public GridLength ScripturesUnusedWidth
    {
        get
        {
            if (ScripturesAllowed <= 0)
                return new GridLength(1, GridUnitType.Star);

            var used = Math.Clamp(ScripturesUsed, 0, ScripturesAllowed);
            var remaining = Math.Max(0, ScripturesAllowed - used);
            return new GridLength(remaining, GridUnitType.Star);
        }
    }

    public ObservableCollection<ScriptureSegmentVm> ScripturesSegments { get; } = new();
    public bool HasScripturesSegments => ScripturesSegments.Count > 0;

    private Color _scripturesFillColor = Colors.White;
    public Color ScripturesFillColor
    {
        get => _scripturesFillColor;
        private set => Set(ref _scripturesFillColor, value);
    }

    private Color _scripturesFillStrokeColor = Color.FromArgb("#374151");
    public Color ScripturesFillStrokeColor
    {
        get => _scripturesFillStrokeColor;
        private set => Set(ref _scripturesFillStrokeColor, value);
    }

    public string StateLabel => IsSaved ? "Saved" : "Editing";

    public bool CanAddSelected =>
        CanEdit
        && IsExpanded
        && !string.IsNullOrWhiteSpace(SelectedMiracleOption?.Name)
        && (!IsScriptures || ScripturesRemaining > 0);

    public bool CanSaveList => CanEdit && !HasValidationError && Entries.Any(e => !string.IsNullOrWhiteSpace(e.Draft.Name));

    public string EffectiveAlignment
    {
        get
        {
            if (GoodPoints > 10) return "Good";
            if (EvilPoints > 10) return "Evil";
            if (ShowNeutralAlignmentChoice && !string.IsNullOrWhiteSpace(Draft.NeutralAlignmentChoice))
                return Draft.NeutralAlignmentChoice!;
            return "Neutral";
        }
    }

    public string NeutralAlignmentChoice
    {
        get => Draft.NeutralAlignmentChoice ?? string.Empty;
        set
        {
            if (Draft.NeutralAlignmentChoice == value) return;
            Draft.NeutralAlignmentChoice = value;
            Raise();
            UpdateValidation();
        }
    }

    private bool _hasValidationError;
    public bool HasValidationError { get => _hasValidationError; private set => Set(ref _hasValidationError, value); }

    private string _validationMessage = string.Empty;
    public string ValidationMessage { get => _validationMessage; private set => Set(ref _validationMessage, value); }

    private bool _showNeutralAlignmentChoice;
    public bool ShowNeutralAlignmentChoice { get => _showNeutralAlignmentChoice; private set => Set(ref _showNeutralAlignmentChoice, value); }

    public bool HasSelection => Entries.Any(e => e.SelectedMiracle != null);

    public MiracleListVm(
        MiracleListDraft draft,
        IReadOnlyList<MiracleService.MiracRaw> allMiracles,
        ICharacterAdvancementDomainService domainService,
        Action onValidationChanged,
        Func<Alignment?> getAlignment,
        Func<int> getPoints,
        Action<MiracleListVm>? onExpandRequested)
    {
        Draft = draft;
        _allMiracles = allMiracles ?? Array.Empty<MiracleService.MiracRaw>();
        _domainService = domainService;
        _onValidationChanged = onValidationChanged;
        _getAlignment = getAlignment;
        _getPoints = getPoints;
        _onExpandRequested = onExpandRequested;
        _sphereLookup = BuildSphereLookup();

        foreach (var sphere in Enum.GetValues<SpiritualSpheres>())
        {
            var label = StripMajorMinorPrefix(EnumDisplayFormatter.Format(sphere));
            if (IsUniversalSphere(label))
                continue;
            SphereFilterOptions.Add(label);
        }

        AddSelectedCommand = new Command(AddSelectedMiracle);
        RemoveEntryCommand = new Command<MiracleEntryVm>(RemoveEntry);
        SaveListCommand = new Command(SaveList);
        ToggleExpandedCommand = new Command(() => _onExpandRequested?.Invoke(this));
        ReopenScripturesCommand = new Command(ReopenScriptures);

        SelectedSphereFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        SelectedAdvancedFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        ScripturesSegments.CollectionChanged += (_, __) => Raise(nameof(HasScripturesSegments));

        LoadEntriesFromDraft();
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void LoadEntriesFromDraft()
    {
        Entries.Clear();
        foreach (var entry in Draft.Entries ?? new List<MiracleListEntryDraft>())
        {
            var vm = new MiracleEntryVm(entry, OnEntryChanged);
            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                var sphereLabel = MapSphereLabel(entry.Sphere);
                vm.SelectedMiracle = new MiracleOption(
                    entry.Name,
                    entry.Power,
                    entry.Alignment ?? string.Empty,
                    sphereLabel,
                    entry.IsAdvanced);
            }
            Entries.Add(vm);
        }
        ReindexEntries();
    }

    private void AddSelectedMiracle()
    {
        if (!CanAddSelected || SelectedMiracleOption == null)
            return;

        var draft = new MiracleListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new MiracleEntryVm(draft, OnEntryChanged);
        vm.SelectedMiracle = SelectedMiracleOption.Value;
        Entries.Add(vm);
        ReindexEntries();
        SearchPickerStateHelper.ClearForNextSearch<MiracleOption>(
            setSelection: v => SelectedMiracleOption = v,
            setSearchText: text => SearchText = text);
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void RemoveEntry(MiracleEntryVm? entry)
    {
        if (entry == null || !CanEdit) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        ReindexEntries();
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void ReindexEntries()
    {
        for (var i = 0; i < Entries.Count; i++)
            Entries[i].SetRowIndex(i);
    }

    private void OnEntryChanged()
    {
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void SaveList()
    {
        if (!CanSaveList)
            return;

        IsSaved = true;
    }

    private void ReopenScriptures()
    {
        if (!CanReopenScriptures)
            return;

        IsSaved = false;
        if (IsMinimized)
            _onExpandRequested?.Invoke(this);
    }

    private void UpdateFilteredOptions()
    {
        var filtered = _allMiracles;

        var allowedAlignments = _domainService.GetAllowedMiracleAlignments(
            _getAlignment(),
            Draft.Entries ?? new List<MiracleListEntryDraft>(),
            lockTrueNeutral: true);
        filtered = filtered
            .Where(m => allowedAlignments.Contains(_domainService.NormalizeAlignmentToken(m.alignment)))
            .ToList();

        if (SelectedSphereFilters.Count > 0)
        {
            var sphereSet = SelectedSphereFilters
                .Select(NormalizeToken)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(m =>
            {
                var label = MapSphereLabel(m.sphere);
                if (IsUniversalSphere(label))
                    return true;
                return sphereSet.Contains(NormalizeToken(label));
            }).ToList();
        }

        var advanced = SelectedAdvancedFilters.Any(x => x.Equals("Advanced", StringComparison.OrdinalIgnoreCase));
        var handbook = SelectedAdvancedFilters.Any(x => x.Equals("Handbook", StringComparison.OrdinalIgnoreCase));
        if (advanced ^ handbook)
            filtered = filtered.Where(m => m.isAdvanced == advanced).ToList();

        var dict = new Dictionary<string, MiracleOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var miracle in filtered)
        {
            if (string.IsNullOrWhiteSpace(miracle?.name))
                continue;

            var option = new MiracleOption(
                miracle.name ?? string.Empty,
                miracle.power,
                miracle.alignment ?? string.Empty,
                MapSphereLabel(miracle.sphere),
                miracle.isAdvanced);

            var label = $"{miracle.name} ({miracle.power})";
            if (!dict.ContainsKey(label))
                dict[label] = option;
        }

        FilteredOptions = dict;
    }

    private string MapSphereLabel(string raw)
    {
        var cleaned = StripMajorMinorPrefix(raw);
        var key = NormalizeToken(cleaned);
        if (_sphereLookup.TryGetValue(key, out var label))
            return label;

        return cleaned?.Trim() ?? string.Empty;
    }

    private static bool IsUniversalSphere(string? label)
        => NormalizeToken(label) == "universal";

    private static string StripMajorMinorPrefix(string? raw)
    {
        var text = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.StartsWith("Major", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Minor", StringComparison.OrdinalIgnoreCase))
        {
            if (text.Length <= 5)
                return string.Empty;

            var trimmed = text.Substring(5).TrimStart(' ', ':', '-');
            return trimmed.Trim();
        }

        return text;
    }

    private static string NormalizeToken(string? value)
    {
        var text = value ?? string.Empty;
        var chars = text.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private Dictionary<string, string> BuildSphereLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sphere in Enum.GetValues<SpiritualSpheres>())
        {
            var label = StripMajorMinorPrefix(EnumDisplayFormatter.Format(sphere));
            var key = NormalizeToken(label);
            if (!lookup.ContainsKey(key))
                lookup[key] = label;
        }
        return lookup;
    }

    private void UpdateValidation()
    {
        var totals = _domainService.ComputeMiraclePointTotals(Entries.Select(e => e.Draft));
        var advancedSpheres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Draft.Name))
                continue;

            if (entry.Draft.IsAdvanced)
            {
                var sphere = MapSphereLabel(entry.Draft.Sphere);
                if (!string.IsNullOrWhiteSpace(sphere))
                    advancedSpheres.Add(sphere);
            }
        }

        GoodPoints = totals.Good;
        EvilPoints = totals.Evil;
        NeutralPoints = totals.Neutral;
        TotalPoints = totals.Total;
        AdvancedPoints = totals.Advanced;

        Raise(nameof(UnselectedPoints));
        Raise(nameof(GoodWidth));
        Raise(nameof(NeutralWidth));
        Raise(nameof(EvilWidth));
        Raise(nameof(UnselectedWidth));

        var hasMixed = totals.Good > 0 && totals.Evil > 0;

        var messages = new List<string>();
        if (hasMixed)
            messages.Add("Cannot mix Good and Evil miracles in the same list.");
        if (IsScriptures)
        {
            ScripturesAllowed = _domainService.GetScriptureTablesReached(_getPoints());
            if (ScripturesUsed > ScripturesAllowed)
                messages.Add($"Exceeds scriptures allowed ({ScripturesAllowed}).");

            ShowNeutralAlignmentChoice = false;
        }
        else
        {
            ScripturesAllowed = 0;

            var overTotal = TotalPoints > MaxListPower;
            var overAdvanced = AdvancedPoints > MaxAdvancedPower;
            var tooManyAdvancedSpheres = advancedSpheres.Count > 1;
            var needsNeutralChoice = totals.Good <= 10 && totals.Evil <= 10 && (totals.Good > 0 || totals.Evil > 0);
            var hasNeutralMismatch = needsNeutralChoice && string.IsNullOrWhiteSpace(Draft.NeutralAlignmentChoice);
            var neutralChoiceMismatch = false;
            if (needsNeutralChoice && !string.IsNullOrWhiteSpace(Draft.NeutralAlignmentChoice))
            {
                var choice = _domainService.NormalizeAlignmentToken(Draft.NeutralAlignmentChoice);
                neutralChoiceMismatch = (totals.Good > 0 && choice != "good") || (totals.Evil > 0 && choice != "evil");
            }

            if (SourceName == string.Empty)
            {
                if (overTotal)
                    messages.Add($"Exceeds {MaxListPower} spirit limit.");
                if (overAdvanced)
                    messages.Add($"Advanced miracles exceed {MaxAdvancedPower} spirit limit.");
                if (tooManyAdvancedSpheres)
                    messages.Add("Advanced miracles must be from a single Sphere.");
                if (hasNeutralMismatch)
                    messages.Add("Pick Good or Evil alignment for this neutral list.");
                if (neutralChoiceMismatch)
                    messages.Add("Neutral alignment choice must match selected aligned miracles.");
            }

            ShowNeutralAlignmentChoice = needsNeutralChoice;
        }

        ValidationMessage = string.Join(" ", messages);
        HasValidationError = messages.Count > 0;

        Raise(nameof(EffectiveAlignment));
        Raise(nameof(CanSaveList));
        Raise(nameof(CanAddSelected));
        UpdateScripturesVisuals();
        RaiseScripturesProgressProperties();
        _onValidationChanged();
    }

    private void RaiseScripturesProgressProperties()
    {
        Raise(nameof(ScripturesUsed));
        Raise(nameof(ScripturesRemaining));
        Raise(nameof(ScripturesProgressText));
        Raise(nameof(ScripturesUnusedText));
        Raise(nameof(ScripturesUsedRatio));
        Raise(nameof(ScripturesRemainingRatio));
        Raise(nameof(HasUsedScriptures));
        Raise(nameof(ScripturesUsedWidth));
        Raise(nameof(ScripturesUnusedWidth));
        Raise(nameof(HasScripturesSegments));
    }

    private void UpdateScripturesVisuals()
    {
        RebuildScriptureSegments();

        var alignmentToken = ResolveScripturesAlignmentToken();
        switch (alignmentToken)
        {
            case "good":
                ScripturesFillColor = Colors.White;
                ScripturesFillStrokeColor = Color.FromArgb("#374151");
                break;
            case "evil":
                ScripturesFillColor = Color.FromArgb("#111827");
                ScripturesFillStrokeColor = Color.FromArgb("#374151");
                break;
            default:
                ScripturesFillColor = Color.FromArgb("#4B5563");
                ScripturesFillStrokeColor = Color.FromArgb("#374151");
                break;
        }
    }

    private void RebuildScriptureSegments()
    {
        ScripturesSegments.Clear();
        if (!IsScriptures || ScripturesAllowed <= 0)
            return;

        var selectedEntries = Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Draft.Name))
            .ToList();

        var good = selectedEntries.Count(entry =>
            _domainService.NormalizeAlignmentToken(entry.Draft.Alignment) == "good");
        var evil = selectedEntries.Count(entry =>
            _domainService.NormalizeAlignmentToken(entry.Draft.Alignment) == "evil");
        var neutral = Math.Max(0, selectedEntries.Count - good - evil);
        var remaining = ScripturesRemaining;

        if (good > 0)
            ScripturesSegments.Add(new ScriptureSegmentVm(good, Colors.White));
        if (neutral > 0)
            ScripturesSegments.Add(new ScriptureSegmentVm(neutral, Color.FromArgb("#6B7280")));
        if (evil > 0)
            ScripturesSegments.Add(new ScriptureSegmentVm(evil, Color.FromArgb("#111827")));
        if (remaining > 0)
            ScripturesSegments.Add(new ScriptureSegmentVm(remaining, Color.FromArgb("#80D1D5DB"), isUnselected: true));
    }

    private string ResolveScripturesAlignmentToken()
    {
        var selectedEntries = Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Draft.Name))
            .ToList();
        if (selectedEntries.Count == 0)
            return "neutral";

        var hasGood = selectedEntries.Any(e => _domainService.NormalizeAlignmentToken(e.Draft.Alignment) == "good");
        var hasEvil = selectedEntries.Any(e => _domainService.NormalizeAlignmentToken(e.Draft.Alignment) == "evil");
        if (hasGood && !hasEvil)
            return "good";
        if (hasEvil && !hasGood)
            return "evil";

        return "neutral";
    }

    public void RefreshExternalLimits()
    {
        UpdateFilteredOptions();
        UpdateValidation();
    }

    public bool IsAlignmentCompatible(Alignment? alignment)
    {
        if (HasValidationError)
            return false;

        if (!alignment.HasValue)
            return true;

        return _domainService.AreMiracleEntriesAlignmentCompatible(alignment, Entries.Select(e => e.Draft));
    }
}

public sealed class MiracleEntryVm : INotifyPropertyChanged
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

    public MiracleListEntryDraft Draft { get; }

    public string DisplayText
        => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : $"{Draft.Name} ({Draft.Power})";
    public string NameText => Draft.Name ?? string.Empty;
    public string PowerText => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : Draft.Power.ToString();

    public bool HasMiracle => !string.IsNullOrWhiteSpace(Draft.Name);
    private int _rowIndex;
    public Color RowBackgroundColor => (_rowIndex % 2) == 0 ? Colors.White : Color.FromArgb("#FAF8F3");

    private MiracleOption? _selectedMiracle;
    public MiracleOption? SelectedMiracle
    {
        get => _selectedMiracle;
        set
        {
            if (!Set(ref _selectedMiracle, value)) return;

            if (value == null)
            {
                Draft.Name = string.Empty;
                Draft.Power = 0;
                Draft.Alignment = string.Empty;
                Draft.Sphere = string.Empty;
                Draft.IsAdvanced = false;
            }
            else
            {
                Draft.Name = value.Value.Name;
                Draft.Power = value.Value.Power;
                Draft.Alignment = value.Value.Alignment;
                Draft.Sphere = value.Value.Sphere;
                Draft.IsAdvanced = value.Value.IsAdvanced;
            }

            Raise(nameof(DisplayText));
            Raise(nameof(NameText));
            Raise(nameof(PowerText));
            Raise(nameof(HasMiracle));
            _onChanged();
        }
    }

    public MiracleEntryVm(MiracleListEntryDraft draft, Action onChanged)
    {
        Draft = draft;
        _onChanged = onChanged;
    }

    public void SetRowIndex(int rowIndex)
    {
        if (_rowIndex == rowIndex)
            return;

        _rowIndex = rowIndex;
        Raise(nameof(RowBackgroundColor));
    }
}

public readonly record struct MiracleOption(string Name, int Power, string Alignment, string Sphere, bool IsAdvanced);
