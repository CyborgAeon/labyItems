# Primus map and monthly world statistics

Open **Primus map and world stats** in the toolset navigation menu. It is public reference content, available independently of member calendar access.

## Architecture

`PrimusMapView` loads the original overlay as a raw `MauiAsset` with logical name `primus-overlay.svg`. It uses MAUI's built-in WebView and the native browser's SVG implementation rather than adding SkiaSharp. This follows the repository's no-new-packages UI rule and preserves Bézier curves, inherited transforms, IDs and metadata used for selection. No NuGet packages were added.

One parent SVG group applies pan and zoom to the background and overlay together. The viewport fits initially, clamps zoom to 1–12 times fit, anchors pinch zoom at the fingers' midpoint, bounds panning, and preserves the center and relative zoom on resize. Pointer state suppresses selection after dragging or pinching. Exact `isPointInFill` checks use each path's inverse screen matrix. Locations win overlapping hits, followed by the territory with the smallest bounding area, then ordinal SVG ID. Bounding boxes order hits; they never decide containment.

Selection highlights the exact path using its SVG fill and stroke colours and shows a small native name tooltip anchored above the tap. Only the tooltip’s information glyph opens the region overview. Pan, zoom, reset, empty taps and clearing selection dismiss the tooltip. Dismissing the overview preserves the WebView and its viewport. The map page owns the large translucent primary-red banner over the splash image, with the toolset burger as its only navigation button. A three-column icon summary shows the selected area’s highest deity/order, administration and dragon values. The original artwork uses the full page width in its original 4:3 proportions; native controls for zoom, fit, place, month and filtering sit below it. A selected-place card presents its name and key statistics side by side. The statistics modal has a compact name/date heading and close button. Statistics use `ListItemRowView` under unframed subheadings with `ChevronIconView` and `AnimatedExpandView`; the redundant full-details route has been removed. The item wallet’s legacy text caret also uses these shared animation controls now. Content security policy blocks network resources. All scripts and data are local.

`IWorldRegionRepository` separates map metadata and monthly statistics from rendering. `WorldRegionRepository` reads reference data only from `account-vaults/public.db`, never from an active member database. Startup seeds that database from trusted bundled SQL in one transaction. There is no member-derived cache or personal data in the map.

## Town filters

The filter overlay follows Global Search: a centered bordered panel, scrollable fields and fixed Cancel/Reset/Search actions over a scrim. Cancel discards pending edits; Reset clears the pending form; Search applies it. Name matching ignores case. Score ranges include their endpoints; equal limits request an exact score, and blank limits impose no restriction. Administration, a selected dragon and a selected deity/order can be combined with AND. The “Highest published value” option compares the maximum within that category. Missing historical values never match a numeric range as zero.

Hiring Hall filtering uses the published **Volksmight score**. Council office-holder assignments (including Helgenmyr) and separate building levels are not supplied by the monthly PDF. As agreed, those remain unknown and are not exposed as vacancy/level filters. A zero Volksmight score does not imply an unassigned council position.

Filters apply to the selected month and are reapplied when it changes. Matching published places appear in the bounded `CollectionView` beneath the map header and their explicitly mapped SVG paths receive highlights. Selecting a result focuses its path; an unmapped place still has a statistics card without selecting an unrelated territory. No personal data or persistent filter cache is introduced.

## Data and mappings

`WorldStatistic` uses the composite primary key `(Year, Month, RegionId, Category, Name)` and stores `Value`, display `Ordinal`, and PDF `SourcePage`. Month and year are checked. A complete monthly seed replaces only its own year and month; other historical months remain. The UI defaults to the newest available period and can select historical periods. Runtime downloads and latest-data synchronization have deliberately not been added.

The November 2026 PDF contains **586 values for 20 places across 16 pages**. All places have eight administration values and fourteen dragon values. Deity/order collections are preserved separately for each place, with their actual entries and values. The import matches numeric cells by PDF coordinates, not unreliable PDF text order. The two-column pages are split by column centers.

`Resources/Raw/world-map/regions.json` lists all **70 SVG paths (36 territories, 34 locations)**. Its `Id` is the exact SVG path ID; `StatisticsId` explicitly maps that path to the source place name. For example, `loc-rodanne` maps to source spelling `Rodane`, and `loc-mordreds-rest` maps to `Mordreds Rest`. No fuzzy matching occurs at runtime.

Twenty paths have explicit statistics mappings to fifteen published places. Combined territories such as Alguz & Gileenim, Gothiel & Eartholme, Mordred's Rest & Scarlene, and Bulaslavia & Thimon are not assigned one constituent town's values. Their `RelatedStatisticsIds` explicitly list available constituent places, displayed separately in the modal without aggregating them. Sellaville territory paths similarly link to its published town statistics. Individual location paths and the place picker also open those records. The five published places without a confirmed individual SVG match are Bulaslavia, Lu-Chang, Jia Muryo, Ghalabhad and Khanniabad. All twenty places remain accessible through the picker. Unmapped paths display a clear unavailable-data message.

## Original map artwork

The updated `Resources/Images/primus.svg` embeds the original 2048 × 1536 PNG. Its seventy selectable path geometries match `primus-overlay.svg` exactly. The separately supplied `Resources/Images/primus-base.png` is also 2048 × 1536 and provides the original artwork (SHA-256 `be59824858fb89cdfaf65ff7a291ca63bf4d5d4c42b499a6fab9a4b3ebc2eb79`). It is bundled as a raw asset with logical name `primus-map.png`, avoiding MAUI image conversion and its restrictions on hyphenated image names.

The renderer places this image behind the polygons in the same coordinate system and hides unselected polygon fills/outlines over the illustration. The supplied image files remain unchanged on disk and are excluded from `MauiImage` processing. A development utility can extract the embedded original PNG without changing its pixels, if needed:

```sh
python3 tools/worldstats/extract_background.py \
  labyItems/Resources/Images/primus.svg /tmp/primus-original.png
```

## Adding another month

For a PDF with the same layout, on macOS:

```sh
swiftc -module-cache-path /tmp/primus-swift-cache -framework PDFKit -framework AppKit \
  tools/worldstats/extract-layout.swift -o /tmp/extract-world-layout
/tmp/extract-world-layout /path/to/monthly-stats.pdf /tmp/month-layout.json /tmp/month-preview.png
python3 tools/worldstats/import_stats.py /tmp/month-layout.json \
  --year 2026 --month 12 \
  --output labyItems/Resources/Raw/world-map/stats/2026-12.sql
```

Add `"2026-12"` to `Resources/Raw/world-map/stats-index.json`, leaving existing entries in place. Review values against the PDF, especially multi-column pages, and update explicit mappings if place names change. Commit the SQL and index. Startup imports every indexed month, and the picker orders periods newest first. The importer rejects unexpected non-numeric cells and incorrect administration/dragon counts; a changed PDF layout requires updating the extractor rather than guessing.

## Verification

- Android: `dotnet build labyItems/labyItems.csproj -f net10.0-android -t:Compile --no-restore` passed. Existing package advisory warnings remain.
- Focused C# tests: three `WorldRegionRepositoryTests`, two `WorldStatisticsSummaryFormatterTests` and four `WorldMapFilterEvaluatorTests` passed, covering highest-stat selection, composite-place attribution, missing values, idempotent import, historical retention, source values, explicit mappings and differing deity collections. The runner requires a permitted local socket.
- `node --test tools/worldstats/viewport.test.cjs`: four tests passed (round trips, limits, midpoint anchoring, clamping and resize).
- `python3 -m unittest discover -s tools/worldstats -p '*_test.py'`: two tests passed (invalid extraction rejection and month-specific replacement).
- `python3 tools/worldstats/test_browser.py`: twenty-two headless Chrome checks passed, including concavity, Bézier geometry, inherited transforms, overlap ordering, tap suppression, tooltip bridge coordinates, selection preserving the viewport, filtered highlights and result focus. Requires installed macOS Chrome and permission to launch its subprocesses.
- Changed XAML and project XML parse successfully; `git diff --check` passed.
- iOS compile attempted with `-p:UseIosWorkload=true`: blocked by missing `net10.0-ios` restore target. The installed workloads do not include iOS.
- Android/iOS device gesture behavior, phone-width layout, large text, modal dismissal, app lifecycle and alignment on devices remain unverified. Browser checks do not establish native device behavior.

## File inventory

Created:

- `labyItems/Controls/PrimusMapView.cs`, `WorldStatisticsView.cs`
- `labyItems/Models/WorldMap/WorldRegion.cs`, `WorldStatistic.cs`, `WorldStatsPeriod.cs`, `WorldStatisticsGroup.cs`, `WorldMapFilter.cs`, `WorldStatisticFilter.cs`, `WorldTownResult.cs`
- `labyItems/Services/IWorldRegionRepository.cs`, `WorldRegionRepository.cs`
- `labyItems/Pages/WorldMap/PrimusMapPage.xaml`, `PrimusMapPage.xaml.cs`, `RegionDetailsPage.cs`
- `labyItems/Resources/Raw/world-map/regions.json`, `stats-index.json`, `stats/2026-11.sql`, `viewport.js`, `map.js`
- `labyItems/Helpers/WorldStatisticsSummaryFormatter.cs`, `WorldMapFilterEvaluator.cs`
- `tests/labyItems.Tests/WorldRegionRepositoryTests.cs`, `WorldStatisticsSummaryFormatterTests.cs`, `WorldMapFilterEvaluatorTests.cs`
- `tools/worldstats/extract_background.py`, `extract-layout.swift`, `import_stats.py`, `import_stats_test.py`, `viewport.test.cjs`, `test_browser.py`
- This document.

Modified:

- `labyItems/labyItems.csproj`: raw asset packaging and image-processing exclusions.
- `labyItems/Services/AccountStorageCoordinator.cs`: trusted public-table seed.
- `labyItems/Controls/ToolsetNavigationMenu.xaml` and `.xaml.cs`: public map entry.
- `tests/labyItems.Tests/labyItems.Tests.csproj`: links repository and model source for tests.

The user's existing `dotnet-install.sh`, glyph catalogue and toolset-menu edits were preserved. The new area SVGs use underscore MAUI aliases; missing SVG root closers were added to area-b, area-c and area-d so resource processing can compile them.
