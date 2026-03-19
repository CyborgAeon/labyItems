namespace Microsoft.Maui.Graphics;

public readonly struct Color
{
    private readonly string _hex;

    private Color(string hex)
    {
        _hex = hex ?? string.Empty;
    }

    public static Color FromArgb(string hex)
        => new(hex);

    public override string ToString() => _hex;
}
