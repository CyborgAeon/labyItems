using labyItems.Models.Enums;

namespace labyItems.Helpers;

public static class MagicColourOppositionRules
{
    private static readonly IReadOnlyDictionary<MagicColours, MagicColours> Opposites =
        new Dictionary<MagicColours, MagicColours>
        {
            [MagicColours.Red] = MagicColours.Green,
            [MagicColours.Green] = MagicColours.Red,
            [MagicColours.Brown] = MagicColours.Blue,
            [MagicColours.Blue] = MagicColours.Brown,
            [MagicColours.White] = MagicColours.Black,
            [MagicColours.Black] = MagicColours.White,
            [MagicColours.Gold] = MagicColours.Bronze,
            [MagicColours.Bronze] = MagicColours.Gold,
            [MagicColours.Ivory] = MagicColours.Ebony,
            [MagicColours.Ebony] = MagicColours.Ivory,
            [MagicColours.Jade] = MagicColours.Onyx,
            [MagicColours.Onyx] = MagicColours.Jade
        };

    public static bool AreOpposites(MagicColours first, MagicColours second)
        => Opposites.TryGetValue(first, out var opposite) && opposite == second;

    public static bool TryGetOpposite(MagicColours colour, out MagicColours opposite)
        => Opposites.TryGetValue(colour, out opposite);
}
