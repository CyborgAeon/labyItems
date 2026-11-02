using System.Text.Json;
using System.Text.RegularExpressions;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

public abstract partial class MpCalculatorPageBase
{
    internal async Task RestoreLegacyItem(Item item)
    {
        using var document = JsonDocument.Parse(item.PayloadJson);
        var root = document.RootElement;
        var savedRows = new List<(string Id, string Text)>();
        if (root.TryGetProperty("breakdown", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rows.EnumerateArray())
                savedRows.Add(((row.TryGetProperty("Id", out var rowId) ? rowId : row.GetProperty("id")).GetString() ?? string.Empty,
                    (row.TryGetProperty("Text", out var rowText) ? rowText : row.GetProperty("text")).GetString() ?? string.Empty));
        }
        else
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item.PayloadJson);
            foreach (var ability in payload?.Item?.Abilities ?? new List<CalcResult>())
                savedRows.Add((string.Empty, ability.Summary));
        }
        if (savedRows.Count == 0)
            throw new InvalidOperationException("This older item does not contain editable monster-point settings.");

        var spells = (await BuildSpellLookupAsync()).Values.ToList();
        var miracles = (await BuildMiracleLookupAsync()).Values.ToList();
        var evocations = (await FetchEvocationOptionsAsync(string.Empty)).Values.ToList();
        var neuros = (await BuildNeuroLookupAsync()).Values.ToList();
        foreach (var row in savedRows)
        {
            var text = Regex.Replace(row.Text, @"\s*=\s*-?\d+\s*$", string.Empty).Trim();
            var id = row.Id.Length > 0 ? row.Id : InferLegacyRowId(text);
            var usesMatch = Regex.Match(text, @" x(?<count>\d+)$");
            var uses = usesMatch.Success ? int.Parse(usesMatch.Groups["count"].Value) : 1;
            var label = usesMatch.Success ? text[..usesMatch.Index] : text;
            if (id.StartsWith("spell-", StringComparison.Ordinal))
            {
                var option = spells.FirstOrDefault(option => label == $"Spell: {FormatSpellLabel(option)}");
                if (string.IsNullOrWhiteSpace(option.Name)) throw MissingLegacyOption(text);
                SpellSelections.Add(new(option, FormatSpellLabel(option), OnSpellSelectionUsesChanged, uses));
            }
            else if (id.StartsWith("miracle-", StringComparison.Ordinal))
            {
                var option = miracles.FirstOrDefault(option => label == $"Miracle: {FormatMiracleLabel(option)}");
                if (string.IsNullOrWhiteSpace(option.Name)) throw MissingLegacyOption(text);
                MiracleSelections.Add(new(option, FormatMiracleLabel(option), OnMiracleSelectionUsesChanged, uses));
            }
            else if (id.StartsWith("evocation-", StringComparison.Ordinal))
            {
                var option = evocations.FirstOrDefault(option => label == $"Evocation: {option.Name}");
                if (string.IsNullOrWhiteSpace(option.Name)) throw MissingLegacyOption(text);
                EvocationSelections.Add(new(option, FormatEvocationLabel(option), OnEvocationSelectionUsesChanged, uses));
            }
            else if (id.StartsWith("neuro-", StringComparison.Ordinal))
            {
                var option = neuros.FirstOrDefault(option => label == $"Neuronic: {FormatNeuroLabel(option)}");
                if (string.IsNullOrWhiteSpace(option.Name)) throw MissingLegacyOption(text);
                NeuroSelections.Add(new(option, FormatNeuroLabel(option), OnNeuroSelectionUsesChanged, uses));
            }
            else switch (id)
            {
                case "life":
                    var lifeIndex = LifeOptions.Keys.ToList().FindIndex(key => text == $"Life {key}");
                    if (lifeIndex < 0) throw MissingLegacyOption(text);
                    LifeSliderControl.SelectedIndex = lifeIndex;
                    break;
                case "pac": PacChecked = true; break;
                case "dac": DacChecked = true; break;
                case "mac": MacChecked = true; break;
                case "sac": SacChecked = true; break;
                case "repel-ge":
                    RepelGoodEvilTarget = RepelGoodEvilOptions.FirstOrDefault(target => label == $"Repel {TrimChipLabel(target)}");
                    if (RepelGoodEvilTarget == null) throw MissingLegacyOption(text);
                    RepelGoodEvilCount = uses;
                    break;
                case "repel-life": RepelLifeCount = uses; break;
                case "apprentice":
                    ApprenticeType = ApprenticeTypeOptions.FirstOrDefault(type => label.StartsWith($"Apprentice crafted {TrimChipLabel(type)}", StringComparison.Ordinal));
                    if (ApprenticeType == null) throw MissingLegacyOption(text);
                    if (IsApprenticeWeapon)
                    {
                        var weapon = Enum.GetValues<WeaponType>().Cast<WeaponType?>().FirstOrDefault(value =>
                            label.EndsWith($"({EnumDisplayFormatter.FormatName(value!.Value.ToString())})", StringComparison.Ordinal));
                        if (!weapon.HasValue) throw MissingLegacyOption(text);
                        SelectedWeapon = weapon;
                    }
                    break;
                default:
                    if (!RestoreLegacyCustomSetting(text, label, uses))
                        throw MissingLegacyOption(text);
                    break;
            }
        }
        RefreshSelectionRowStyles(SpellSelections);
        RefreshSelectionRowStyles(MiracleSelections);
        RefreshSelectionRowStyles(EvocationSelections);
        RefreshSelectionRowStyles(NeuroSelections);
        OnPropertyChanged(nameof(HasSpellSelections));
        OnPropertyChanged(nameof(HasMiracleSelections));
        OnPropertyChanged(nameof(HasEvocationSelections));
        OnPropertyChanged(nameof(HasNeuroSelections));
        if (this is MpBasicPage basic)
        {
            basic.ApprenticeShieldChecked = TrimChipLabel(ApprenticeType) == "Shield";
            basic.ApprenticeWeaponChecked = TrimChipLabel(ApprenticeType) == "Weapon";
        }
        Recalculate();
    }

    private static string InferLegacyRowId(string text)
    {
        if (text.StartsWith("Spell:")) return "spell-legacy";
        if (text.StartsWith("Miracle:")) return "miracle-legacy";
        if (text.StartsWith("Evocation:")) return "evocation-legacy";
        if (text.StartsWith("Neuronic:")) return "neuro-legacy";
        if (text.StartsWith("Life ")) return "life";
        if (text == "+1 PAC") return "pac";
        if (text == "+1 DAC") return "dac";
        if (text == "+1 MAC") return "mac";
        if (text == "+1 SAC") return "sac";
        if (text.StartsWith("Repel Life")) return "repel-life";
        if (text.StartsWith("Repel ")) return "repel-ge";
        if (text.StartsWith("Apprentice crafted ")) return "apprentice";
        return "custom";
    }

    private bool RestoreLegacyCustomSetting(string text, string label, int count)
    {
        // These properties are shared by the crew/themeday variants. Match the
        // displayed options, including their emoji prefixes, rather than storing
        // an unrecognised value that would silently remove a contribution.
        if (text == "+2 MAC") return SetLegacyProperty("Mac2Checked", true);
        if (text == "+2 SAC") return SetLegacyProperty("Sac2Checked", true);
        if (text == "Weapon apprentice status") return SetLegacyProperty("WeaponApprenticeStatus", true);
        if (text == "Shield apprentice status") return SetLegacyProperty("ShieldApprenticeStatus", true);
        if (label.EndsWith(" status bag", StringComparison.Ordinal))
            return SetLegacyChoice("SelectedBagType", "StatusBagOptions", label[..^11]);

        var details = Regex.Match(label, @"^(?<kind>.+?) (?<object>weapon|shield|power store|store) \((?<details>.*)\)$");
        if (details.Success)
        {
            var kind = details.Groups["kind"].Value;
            var parts = details.Groups["details"].Value.Split(',', StringSplitOptions.TrimEntries);
            var objectType = details.Groups["object"].Value;
            if (objectType == "weapon")
            {
                var property = GetType().GetProperty("SelectedWeaponKind") != null ? "SelectedWeaponKind" : "SelectedWeaponPowerType";
                var options = property == "SelectedWeaponKind" ? "WeaponTypeOptions" : "WeaponPowerOptions";
                if (!SetLegacyChoice(property, options, kind)) return false;
                foreach (var part in parts)
                    if (!SetLegacyEnum("SelectedWeaponType", part)
                        && !SetLegacyEnum("SelectedWeaponColour", part)
                        && !SetLegacyEnum("SelectedWeaponAlignment", part)) return false;
                return true;
            }
            if (objectType == "shield")
                return SetLegacyChoice("SelectedShieldType", "ShieldTypeOptions", kind)
                    && (SetLegacyEnum("SelectedShieldColour", parts[0]) || SetLegacyEnum("SelectedShieldAlignment", parts[0]));
            var prefix = kind switch { "Spirit" => "Spirit", "Magic" => "Magic", "Earthpower" => "Earthpower", _ => "" };
            var selection = prefix switch { "Spirit" => "SelectedSpiritSphere", "Magic" => "SelectedMagicStoreColour", _ => "SelectedEarthpowerField" };
            return prefix.Length > 0 && SetLegacyEnum(selection, parts[0])
                && SetLegacyProperty(prefix == "Earthpower" ? "EarthpowerStorePoints" : prefix + "PowerStorePoints", count);
        }
        if (GetType().GetProperty("CpOptions")?.GetValue(this) is IEnumerable<string> cpOptions)
        {
            var option = cpOptions.FirstOrDefault(option => label == option || label.StartsWith(option + " (", StringComparison.Ordinal));
            if (option != null)
            {
                SetLegacyProperty("SelectedCpOption", option);
                if (label.Length > option.Length)
                    SetLegacyProperty("CpLocationText", label[(option.Length + 2)..^1]);
                return true;
            }
        }
        return false;
    }

    private bool SetLegacyProperty(string name, object value)
    {
        var property = GetType().GetProperty(name);
        if (property?.CanWrite != true) return false;
        property.SetValue(this, value);
        return true;
    }

    private bool SetLegacyChoice(string name, string optionsName, string value)
    {
        if (GetType().GetProperty(optionsName)?.GetValue(this) is not IEnumerable<string> options) return false;
        var selected = options.FirstOrDefault(option => string.Equals(TrimChipLabel(option), value, StringComparison.Ordinal));
        return selected != null && SetLegacyProperty(name, selected);
    }

    private bool SetLegacyEnum(string name, string value)
    {
        var property = GetType().GetProperty(name);
        var enumType = Nullable.GetUnderlyingType(property?.PropertyType ?? typeof(object));
        if (property?.CanWrite != true || enumType?.IsEnum != true) return false;
        foreach (var candidate in Enum.GetValues(enumType))
        {
            if (EnumDisplayFormatter.FormatName(candidate.ToString()!) != value) continue;
            property.SetValue(this, candidate);
            return true;
        }
        return false;
    }

    private static InvalidOperationException MissingLegacyOption(string text) =>
        new($"The saved setting '{text}' could not be restored. The original item has not been changed.");
}
