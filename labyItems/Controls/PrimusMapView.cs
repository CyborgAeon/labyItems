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
    private bool ready;
    private string? pendingSelection;
    private bool pendingFocus;
    private string matchingIds = "null";
#if ANDROID
    private Android.Webkit.WebView? nativeBrowser;
#endif
    private readonly AbsoluteLayout canvas = new();
    private readonly Label selectionName = new() { VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Border tooltip;
    private readonly Dictionary<string, string> regionNames = new();
    private string? selectedId;
    public event EventHandler<string>? RegionDetailsRequested;
    public event EventHandler<string>? RegionSelectionChanged;

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
#if ANDROID
        browser.HandlerChanging += (_, _) =>
        {
            if (nativeBrowser is not null) nativeBrowser.Touch -= OnNativeTouch;
            nativeBrowser = null;
        };
        browser.HandlerChanged += (_, _) =>
        {
            nativeBrowser = browser.Handler?.PlatformView as Android.Webkit.WebView;
            if (nativeBrowser is not null) nativeBrowser.Touch += OnNativeTouch;
        };
#endif
        browser.Navigating += OnNavigating;
        browser.Navigated += async (_, e) =>
        {
            if (e.Result != WebNavigationResult.Success || e.Url.StartsWith("primus:", StringComparison.OrdinalIgnoreCase)) return;
            ready = true;
            try
            {
                await browser.EvaluateJavaScriptAsync($"setMatchingRegions({matchingIds})");
                if (pendingSelection is not null)
                    await browser.EvaluateJavaScriptAsync($"selectRegion({JsonSerializer.Serialize(pendingSelection)}, {pendingFocus.ToString().ToLowerInvariant()})");
            }
            catch (Exception ex) { RuntimeLog.Write("WORLD_MAP", "Failed preparing map selection.", ex); }
        };
    }

#if ANDROID
    private void OnNativeTouch(object? sender, Android.Views.View.TouchEventArgs e)
    {
        // Keep the surrounding results list from taking over map pan/pinch gestures.
        if (e.Event?.ActionMasked == Android.Views.MotionEventActions.Down)
            nativeBrowser?.Parent?.RequestDisallowInterceptTouchEvent(true);
        else if (e.Event?.ActionMasked is Android.Views.MotionEventActions.Up or Android.Views.MotionEventActions.Cancel)
            nativeBrowser?.Parent?.RequestDisallowInterceptTouchEvent(false);
        e.Handled = false;
    }
#endif

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
            RegionSelectionChanged?.Invoke(this, selectedId);
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
        ready = false;
        browser.Source = new HtmlWebViewSource { Html = $$"""
            <!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'">
            <style>
            body{margin:0;background:{{mutedSurface}};font-family:Georgia,serif}#map{position:fixed;inset:0;touch-action:none}svg{display:block;width:100%;height:100%;touch-action:none}
            .artwork .region,.artwork .loc{fill-opacity:0;stroke-opacity:0}.chosen{fill-opacity:.38!important;stroke-opacity:1!important;stroke-width:3!important;vector-effect:non-scaling-stroke}
            .matching:not(.chosen){fill-opacity:.2!important;stroke-opacity:.8!important;vector-effect:non-scaling-stroke}
            nav{display:none;position:fixed;bottom:12px;left:12px;display:flex;gap:8px}button{min-width:44px;min-height:44px;border:1px solid {{surfaceBorder}};background:white;border-radius:8px;color:{{primary}};font:16px Georgia}
            </style></head><body><div id="map">{{document.Root}}</div>
            <nav aria-label="Map controls"><button id="plus" aria-label="Zoom in">+</button><button id="minus" aria-label="Zoom out">−</button><button id="fit">Fit</button><button id="clear">Clear selection</button></nav>
            <script>const background={{JsonSerializer.Serialize(background)}};{{viewport}}
            {{script}}</script></body></html>
            """ };
        loaded = true;
    }

    public async Task SelectRegionAsync(string id, bool focus)
    {
        tooltip.IsVisible = false;
        pendingSelection = id;
        pendingFocus = focus;
        if (ready) await browser.EvaluateJavaScriptAsync($"selectRegion({JsonSerializer.Serialize(id)}, {focus.ToString().ToLowerInvariant()})");
    }

    public async Task SetMatchingRegionsAsync(IEnumerable<string>? ids)
    {
        matchingIds = JsonSerializer.Serialize(ids?.ToArray());
        if (ready) await browser.EvaluateJavaScriptAsync($"setMatchingRegions({matchingIds})");
    }

    public Task ZoomInAsync() => InvokeControlAsync("plus");
    public Task ZoomOutAsync() => InvokeControlAsync("minus");
    public Task FitAsync() => InvokeControlAsync("fit");
    private async Task InvokeControlAsync(string id)
    {
        tooltip.IsVisible = false;
        if (ready) await browser.EvaluateJavaScriptAsync($"document.getElementById('{id}').click()");
    }

    private static string CssColor(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return $"#{(byte)(color.Red * 255):X2}{(byte)(color.Green * 255):X2}{(byte)(color.Blue * 255):X2}";
        return fallback;
    }
}
