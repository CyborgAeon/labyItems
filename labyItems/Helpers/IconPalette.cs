using System;

namespace labyItems.Helpers;

public static class IconPalette
{
    private static readonly string[] PastelIcons =
    {
        "#FEC5BB",
        "#FAE1DD",
        "#F8EDEB",
        "#E8E8E4",
        "#D8E2DC",
        "#ECE4DB",
        "#FFE5D9",
        "#FFD7BA"
    };

    public static string PickRandomIconPastel()
        => PastelIcons[Random.Shared.Next(PastelIcons.Length)];
}
