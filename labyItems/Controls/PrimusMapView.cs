using System.Text.Json;
using System.Xml.Linq;
using labyItems.Services;

namespace labyItems.Controls;

public sealed class PrimusMapView : ContentView
{
    private readonly WebView browser = new();
    private bool loaded;
    public event EventHandler<string>? RegionSelected;

    public PrimusMapView()
    {
        Content = browser;
        browser.Navigating += OnNavigating;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // The only allowed navigation is a canceled, local selection notification.
        if (e.Url.StartsWith("primus://select/", StringComparison.Ordinal))
        {
            e.Cancel = true;
            RegionSelected?.Invoke(this, Uri.UnescapeDataString(e.Url["primus://select/".Length..]));
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
            .artwork .region,.artwork .loc{fill-opacity:0;stroke-opacity:0}.chosen{fill:{{primary}}!important;fill-opacity:.4!important;stroke:{{primary}}!important;stroke-opacity:1!important;stroke-width:4!important;vector-effect:non-scaling-stroke}
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
