using Microsoft.Maui.Graphics;
using labyItems.Models.Enums;

namespace labyItems.Helpers;

public static class MagicColourExtensions
{
    public static Color ToColour(this MagicColours colour)
        => colour switch
        {
            MagicColours.Red => Color.FromArgb("#EF4444"),
            MagicColours.Blue => Color.FromArgb("#3B82F6"),
            MagicColours.Green => Color.FromArgb("#10B981"),
            MagicColours.Brown => Color.FromArgb("#8B5E3C"),
            MagicColours.White => Color.FromArgb("#F3F4F6"),
            MagicColours.Black => Color.FromArgb("#111827"),
            MagicColours.Grey => Color.FromArgb("#9CA3AF"),
            MagicColours.Gold => Color.FromArgb("#D4AF37"),
            MagicColours.Bronze => Color.FromArgb("#CD7F32"),
            MagicColours.Silver => Color.FromArgb("#C0C0C0"),
            MagicColours.Ivory => Color.FromArgb("#F5F5DC"),
            MagicColours.Ebony => Color.FromArgb("#2F1B0C"),
            MagicColours.Jade => Color.FromArgb("#00A86B"),
            MagicColours.Onyx => Color.FromArgb("#353839"),
            _ => Color.FromArgb("#6B7280")
        };
}
