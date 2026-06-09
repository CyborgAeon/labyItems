using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace labyItems.Helpers;

public enum CharacterActionKind
{
    ReviewFullDetails,
    EditCharacter,
    AdvanceCharacter,
    ViewBattleboard,
    ExportBattleboard,
    ViewMakeSheet,
    DeleteCharacter
}

public static class CharacterActionOrderHelper
{
    public const string ReviewStyleId = "character-action-review";
    public const string EditStyleId = "character-action-edit";
    public const string AdvanceStyleId = "character-action-advance";
    public const string BattleboardStyleId = "character-action-battleboard";
    public const string MakeSheetStyleId = "character-action-makesheet";
    public const string ExportStyleId = "character-action-export";
    public const string DeleteStyleId = "character-action-delete";

    private static readonly IReadOnlyList<CharacterActionKind> CanonicalOrder =
    [
        CharacterActionKind.ReviewFullDetails,
        CharacterActionKind.EditCharacter,
        CharacterActionKind.AdvanceCharacter,
        CharacterActionKind.ViewBattleboard,
        CharacterActionKind.ExportBattleboard,
        CharacterActionKind.ViewMakeSheet,
        CharacterActionKind.DeleteCharacter
    ];

    private static readonly IReadOnlyDictionary<CharacterActionKind, int> OrderLookup =
        CanonicalOrder
            .Select((action, index) => (action, index))
            .ToDictionary(x => x.action, x => x.index);

    private static readonly IReadOnlyDictionary<string, CharacterActionKind> StyleIdLookup =
        new Dictionary<string, CharacterActionKind>(StringComparer.Ordinal)
        {
            [ReviewStyleId] = CharacterActionKind.ReviewFullDetails,
            [EditStyleId] = CharacterActionKind.EditCharacter,
            [AdvanceStyleId] = CharacterActionKind.AdvanceCharacter,
            [BattleboardStyleId] = CharacterActionKind.ViewBattleboard,
            [ExportStyleId] = CharacterActionKind.ExportBattleboard,
            [MakeSheetStyleId] = CharacterActionKind.ViewMakeSheet,
            [DeleteStyleId] = CharacterActionKind.DeleteCharacter
        };

    public static IReadOnlyList<CharacterActionKind> GetOrderedActions(IEnumerable<CharacterActionKind> availableActions)
    {
        return (availableActions ?? Enumerable.Empty<CharacterActionKind>())
            .Distinct()
            .OrderBy(GetOrderIndex)
            .ToList();
    }

    public static IReadOnlyList<IView> GetOrderedViewsByStyleId(IEnumerable<IView> views)
    {
        return (views ?? Enumerable.Empty<IView>())
            .OrderBy(GetViewOrderIndex)
            .ToList();
    }

    public static bool TryResolveStyleId(string? styleId, out CharacterActionKind action)
    {
        if (string.IsNullOrWhiteSpace(styleId))
        {
            action = default;
            return false;
        }

        return StyleIdLookup.TryGetValue(styleId.Trim(), out action);
    }

    public static int GetOrderIndex(CharacterActionKind action)
        => OrderLookup.TryGetValue(action, out var index) ? index : int.MaxValue;

    private static int GetViewOrderIndex(IView? view)
    {
        if (view is not VisualElement element)
            return int.MaxValue;

        return TryResolveStyleId(element.StyleId, out var action)
            ? GetOrderIndex(action)
            : int.MaxValue;
    }
}
