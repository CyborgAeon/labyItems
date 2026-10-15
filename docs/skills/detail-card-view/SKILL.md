---
name: detail-card-view
description: Create, change, or review MAUI DetailCardView pages in labyItems, including their summary grids, shared rows, expandable sections, navigation, typography, and data projection. Use for any *DetailCardView.xaml or related detail-card page work; do not use for search-result cards alone.
---

# Detail Card Views

Match the established detail-page language rather than designing an isolated card. Inspect the closest current peers before editing, normally:

- `Pages/ClassCard/ClassDetailCardView.xaml(.cs)` for summary facts, progression tables, powers, collapsible lists, and linked rows.
- `Pages/RaceCard/RaceDetailCardView.xaml(.cs)` for subtype selection, post-8th tables, available-class rows, and long descriptions.
- `Pages/MiracleCard/MiracleDetailCardView.xaml(.cs)` for thin-divider fact grids and expandable text.
- `Pages/GuildCard/GuildDetailCardView.xaml(.cs)` for alignment notation and grouped linked lists.

Reuse shared controls in `labyItems/Controls`; refine a shared control when duplicated detail views need the same behavior.

## Page structure

- Use `DetailPageHeader` for the containing page's navigation/header treatment.
- Put the entity name first, its muted category/bracket/type subtitle beneath it, and its semantic icon on the right unless the existing family deliberately differs.
- When a source book exists, use `BookDirectory` and `SourceBookPdfLauncher`; never hard-code a PDF filename or path.
- Present key facts as an open grid with thin `SurfaceBorderColor` dividers. Do not wrap each fact in a separate bordered card.
- Let facts take only the space their content needs. Prefer three equal columns for short facts; make optional groups complete rows so hidden content does not leave holes.
- Use `KeyInfoLabelStyle`, `KeyInfoValueStyle`, `SectionHeaderLabelStyle`, `MutedTextColor`, and existing surface resources instead of page-local typography or colours.
- Format domain shorthand through helpers such as `NotationHelper`; do not duplicate notation rules in a view.

## Tabular and expandable content

- Render ordinary table results with `ListItemRowView`. Header and row grids must use matching column widths, padding, and `ColumnSpacing` so values sit directly under their headings.
- Set text alignment explicitly for every column. Headers and their values must agree.
- Keep row typography uniform: regular weight, the shared row font sizes, and consistent colour/opacity unless a field has a semantic reason to differ.
- Alternate rows between white and `#FAF8F3`, beginning with white. Assign the colour after the final sort/filter so striping follows displayed order.
- Sort rows by the domain's primary ordering before display (for example level ascending, or evolution table ascending with name as a stable tie-breaker).
- Reserve the final narrow column for the information glyph. Bind the row itself as `ActionCommandParameter`, and resolve the destination through the existing lookup/service path.
- Use `AnimatedExpandView` with `ChevronIconView` for collapsed sections. Copy the direction and animation settings from a working current detail view; do not introduce a second caret animation.
- Keep potentially large sections collapsed or virtualized where appropriate. Never eagerly create a broad catalogue of unrelated rows inside a page `ScrollView`; filter the data to the entity first.

## Selection and text behavior

- Compact selections such as race subtype belong in the summary grid. Show a single-line, tail-truncated selected value and make the whole cell tappable; open the established action-sheet/modal selector rather than displaying a platform-default `Picker` inline.
- Descriptions longer than four rendered lines start collapsed with an ellipsis and muted `... more` text. Tapping anywhere in that text area toggles expansion. Measure rendered overflow when layout width matters; do not rely only on character count.
- Omit absent optional fields instead of displaying empty headings. Use an em dash only for mandatory facts whose value is unavailable.

## Data and navigation safeguards

- Project JSON/dictionary data into explicit row view models before binding. Preserve numeric level/table ordering and handle missing collections as empty collections.
- Treat service/model collections as nullable at UI boundaries. Never call LINQ operators such as `Count()`, `Any()`, or `Where()` on a possibly null collection.
- For subtype or other selection-dependent sections, recompute only the affected rows and raise the associated visibility properties.
- Avoid loading all abilities and filtering through generic availability if the section promises entity-specific results. Require a positive matching race/class/guild dimension as appropriate.
- Catch and log recoverable reference-data failures with `RuntimeLog`; do not silently turn navigation failures into an application crash.

## Verification

Before finishing:

1. Compare the rendered XAML structure and shared-control settings with the closest peer detail view.
2. Test the row projector/filter independently for ordering, exclusions, subtype/selection changes, and null collections.
3. Exercise every information button and back navigation path.
4. Check header/value alignment, alternating rows, hidden optional sections, collapsed/expanded states, and long text at a phone width.
5. Run `dotnet build labyItems/labyItems.csproj -f net10.0-android -t:Compile --no-restore` and the focused tests.
