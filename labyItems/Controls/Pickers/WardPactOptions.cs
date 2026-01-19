using labyItems.Models.Enums;

namespace labyItems.Controls.Pickers;

public static class WardPactOptions
{
    public static Dictionary<string, StandardWardPacts> Standard { get; } = new()
    {
        ["Undead"] = StandardWardPacts.Undead,
        ["Salamanders"] = StandardWardPacts.Salamanders,
        ["Shades"] = StandardWardPacts.Shades,
        ["Sprites"] = StandardWardPacts.Sprites,
        ["Sylphs"] = StandardWardPacts.Sylphs,
        ["Gnomes"] = StandardWardPacts.Gnomes,
        ["Undines"] = StandardWardPacts.Undines,

        ["Supernatural Power (add in your notes)"] = StandardWardPacts.SupernaturalPower,

        ["Fire-Elves"] = StandardWardPacts.FireElves,
        ["Air-Elves"] = StandardWardPacts.AirElves,
        ["earth-Elves"] = StandardWardPacts.EarthElves,
        ["Aquatic-Elves"] = StandardWardPacts.AquaticElves,
        ["Light-Elves"] = StandardWardPacts.LightElves,
        ["dark-Elves"] = StandardWardPacts.DarkElves,
        ["drowe-Elves"] = StandardWardPacts.DroweElves,
        ["winter-Elves"] = StandardWardPacts.WinterElves,
        ["twilight-Elves"] = StandardWardPacts.TwilightElves,
        ["wood-Elves"] = StandardWardPacts.WoodElves,
    };

    public static string LabelFor(StandardWardPacts value)
    {
        foreach (var kvp in Standard)
        {
            if (EqualityComparer<StandardWardPacts>.Default.Equals(kvp.Value, value))
                return kvp.Key;
        }

        return value.ToString();
    }
}
