using System.Text.Json;
using System.Xml.Linq;
using labyItems.Services;
using labyItems.Helpers;
using System.Globalization;
using Microsoft.Maui.Layouts;

namespace labyItems.Controls;

public sealed class PrimusMapView : ContentView
{
    private readonly WebView browser = new();
    private bool loaded;
    private readonly AbsoluteLayout canvas = new();
    private readonly Label selectionName = new() { VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Border tooltip;
    private readonly Dictionary<string, string> regionNames = new();
    private string? selectedId;
    public event EventHandler<string>? RegionDetailsRequested;

    public PrimusMapView()
    {
        AbsoluteLayout.SetLayoutBounds(browser, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(browser, AbsoluteLayoutFlags.All);
        canvas.Add(browser);
        var info = new ImageButton
        {
            Source = new FontImageSource { FontFamily = FontAwesomeGlyphs.SolidFamily, Glyph = FontAwesomeGlyphs.InfoCircle, Size = 22, Color = Color.FromArgb(CssColor("Primary", "#530000")) },
            WidthRequest = 44, HeightRequest = 44, BackgroundColor = Colors.Transparent
        };
        SemanticProperties.SetDescription(info, "Open selected place details");
        info.Clicked += (_, _) => { if (selectedId is not null) RegionDetailsRequested?.Invoke(this, selectedId); };
        var contents = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        contents.Add(selectionName); contents.Add(info, 1);
        tooltip = new Border { Content = contents, Padding = new Thickness(12, 4), BackgroundColor = Colors.White, Stroke = Color.FromArgb(CssColor("SurfaceBorderColor", "#E5E7EB")), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }, IsVisible = false, ZIndex = 10 };
        canvas.Add(tooltip);
        Content = canvas;
        SizeChanged += (_, _) => tooltip.IsVisible = false;
        browser.Navigating += OnNavigating;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("primus://hide", StringComparison.Ordinal))
        {
            e.Cancel = true;
            tooltip.IsVisible = false;
        }
        else if (e.Url.StartsWith("primus://select/", StringComparison.Ordinal))
        {
            e.Cancel = true;
            var uri = new Uri(e.Url);
            selectedId = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            if (!regionNames.TryGetValue(selectedId, out var name)) return;
            selectionName.Text = name;
            var coordinates = uri.Query.TrimStart('?').Split('&').Select(part => part.Split('=')).Where(parts => parts.Length == 2).ToDictionary(parts => parts[0], parts => parts[1]);
            double Coordinate(string key) => coordinates.TryGetValue(key, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? Math.Clamp(number, 0, 1) : .5;
            var width = Math.Min(280, Math.Max(0, canvas.Width - 16));
            var x = Math.Clamp(Coordinate("x") * canvas.Width - width / 2, 8, Math.Max(8, canvas.Width - width - 8));
            var y = Math.Clamp(Coordinate("y") * canvas.Height - 86, 8, Math.Max(8, canvas.Height - 86));
            AbsoluteLayout.SetLayoutBounds(tooltip, new Rect(x, y, width, AbsoluteLayout.AutoSize));
            tooltip.IsVisible = true;
        }
        else if (!e.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                 && !e.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                 && !e.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
    }

    public async Task LoadAsync()
    {
        if (loaded) return;
        var text = await ServiceHelper.ReadPackageTextAsync("primus-overlay.svg");
        var document = XDocument.Parse(text);
        // Only consume the trusted geometric subset. Metadata and external resources do not execute.
        foreach (var element in document.Descendants().Where(e => e.Name.LocalName is "metadata" or "script" or "image" or "foreignObject").ToList()) element.Remove();
        foreach (var attribute in document.Descendants().Attributes().Where(a => a.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase) || a.Name.LocalName is "href").ToList()) attribute.Remove();
        foreach (var path in document.Descendants().Where(e => e.Attribute("data-type") is not null))
            regionNames[(string)path.Attribute("id")!] = (string?)path.Attribute("data-name") ?? (string)path.Attribute("id")!;
        var background = "";
        if (await FileSystem.AppPackageFileExistsAsync("primus-map.png"))
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("primus-map.png");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            background = "data:image/png;base64," + Convert.ToBase64String(buffer.ToArray());
        }
        var primary = CssColor("Primary", "#530000");
        var mutedSurface = CssColor("SurfaceMutedBackgroundColor", "#F3F4F6");
        var surfaceBorder = CssColor("SurfaceBorderColor", "#E5E7EB");
        var viewport = await ServiceHelper.ReadPackageTextAsync("world-map/viewport.js");
        var script = await ServiceHelper.ReadPackageTextAsync("world-map/map.js");
        browser.Source = new HtmlWebViewSource { Html = $$"""
            <!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'">
            <style>
            body{margin:0;background:{{mutedSurface}};font-family:Georgia,serif}#map{position:fixed;inset:0;touch-action:none}svg{width:100%;height:100%;touch-action:none}
            .artwork .region,.artwork .loc{fill-opacity:0;stroke-opacity:0}.chosen{fill-opacity:.38!important;stroke-opacity:1!important;stroke-width:3!important;vector-effect:non-scaling-stroke}
            nav{position:fixed;bottom:12px;left:12px;display:flex;gap:8px}button{min-width:44px;min-height:44px;border:1px solid {{surfaceBorder}};background:white;border-radius:8px;color:{{primary}};font:16px Georgia}
            </style></head><body><div id="map">{{document.Root}}</div>
            <nav aria-label="Map controls"><button id="plus" aria-label="Zoom in">+</button><button id="minus" aria-label="Zoom out">−</button><button id="fit">Fit</button><button id="clear">Clear selection</button></nav>
            <script>const background={{JsonSerializer.Serialize(background)}};{{viewport}}
            {{script}}</script></body></html>
            """ };
        loaded = true;
    }

    private static string CssColor(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return $"#{(byte)(color.Red * 255):X2}{(byte)(color.Green * 255):X2}{(byte)(color.Blue * 255):X2}";
        return fallback;
    }
}
