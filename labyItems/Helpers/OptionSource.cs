using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Controls.Pickers;
using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Pages.Characters;

public enum SlotOptionMode
{
    PlainPicker,
    WardPactEnum,
    EnumPicker,
    DictionarySearch
}


public interface IOptionSource
{
    SlotOptionMode Mode { get; }
    Type? EnumType { get; }
    ILookupService? LookupService { get; }

    IReadOnlyList<string> GetOptionNames();
    void ApplyToSlot(SpecialisationSlotVm slot, string? preselected);
}

public sealed class PlainPickerSource : IOptionSource
{
    private readonly IReadOnlyList<string> _options;

    public PlainPickerSource(IEnumerable<string> optionNames)
        => _options = optionNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public SlotOptionMode Mode => SlotOptionMode.PlainPicker;
    public Type? EnumType => null;
    public ILookupService? LookupService => null;
    public IReadOnlyList<string> GetOptionNames() => _options;

    public void ApplyToSlot(SpecialisationSlotVm slot, string? preselected)
    {
        slot.SetOptionsSource(() => _options);
        if (!string.IsNullOrWhiteSpace(preselected))
            slot.SelectedOption = preselected;
    }
}

public sealed class WardPactSource : IOptionSource
{
    public SlotOptionMode Mode => SlotOptionMode.WardPactEnum;
    public Type? EnumType => typeof(StandardWardPacts);
    public ILookupService? LookupService => null;
    public IReadOnlyList<string> GetOptionNames() => Array.Empty<string>();
    public void ApplyToSlot(SpecialisationSlotVm slot, string? preselected)
    {
        if (!string.IsNullOrWhiteSpace(preselected))
        {
            var match = WardPactOptions.Standard
                .FirstOrDefault(k => string.Equals(k.Key, preselected, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(match.Key))
                slot.SelectedWardPact = match.Value;
        }
    }
}

public sealed class EnumPickerSource<TEnum> : IOptionSource where TEnum : struct, Enum
{
    private readonly IReadOnlyList<TEnum> _allowed;
    private readonly IReadOnlyList<string> _labels;

    public EnumPickerSource(IEnumerable<TEnum>? allowed = null)
    {
        _allowed = (allowed?.ToList() ?? Enum.GetValues<TEnum>().ToList())
            .Distinct()
            .ToList();

        _labels = _allowed
            .Select(EnumDisplayFormatter.Format)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public SlotOptionMode Mode => SlotOptionMode.EnumPicker;
    public Type? EnumType => typeof(TEnum);
    public ILookupService? LookupService => null;

    public IReadOnlyList<string> GetOptionNames() => _labels;

    public void ApplyToSlot(SpecialisationSlotVm slot, string? preselected)
    {
        if (typeof(TEnum) == typeof(MagicColours))
        {
            slot.ConfigureMagicColours(_allowed.Cast<MagicColours>());
            slot.SetOptionsSource(() => slot.MagicColourOptionNames);
            if (!string.IsNullOrWhiteSpace(preselected))
            {
                var match = _allowed.Cast<MagicColours?>()
                    .FirstOrDefault(m => m.HasValue
                        && string.Equals(EnumDisplayFormatter.Format(m.Value), preselected, StringComparison.OrdinalIgnoreCase));
                if (match.HasValue)
                    slot.SelectedMagicColour = match.Value;
            }
        }
        else if (typeof(TEnum) == typeof(VivomancerColours))
        {
            slot.ConfigureVivomancerColours(_allowed.Cast<VivomancerColours>());
            slot.SetOptionsSource(() => slot.VivomancerColourOptionNames);
            if (!string.IsNullOrWhiteSpace(preselected))
            {
                var match = _allowed.Cast<VivomancerColours?>()
                    .FirstOrDefault(v => v.HasValue
                        && string.Equals(EnumDisplayFormatter.Format(v.Value), preselected, StringComparison.OrdinalIgnoreCase));
                if (match.HasValue)
                    slot.SelectedVivomancerColour = match.Value;
            }
        }
        else
        {
            slot.SetOptionsSource(() => _labels);
            if (!string.IsNullOrWhiteSpace(preselected))
                slot.SelectedOption = preselected;
        }
    }
}

public sealed class DictionarySearchSource : IOptionSource
{
    private readonly IReadOnlyList<string> _fallbackOptions;

    public DictionarySearchSource(ILookupService service, IEnumerable<string> fallbackOptions)
    {
        LookupService = service;
        _fallbackOptions = fallbackOptions
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public SlotOptionMode Mode => SlotOptionMode.DictionarySearch;
    public Type? EnumType => null;
    public ILookupService LookupService { get; }
    ILookupService? IOptionSource.LookupService => LookupService;
    public IReadOnlyList<string> GetOptionNames() => _fallbackOptions;
    public void ApplyToSlot(SpecialisationSlotVm slot, string? preselected)
    {
        slot.SetOptionsSource(() => _fallbackOptions);
        if (!string.IsNullOrWhiteSpace(preselected))
            slot.SelectedOption = preselected;
    }
}
