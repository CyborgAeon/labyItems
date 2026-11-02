---
name: maui-xaml-style
description: Create, change, or audit MAUI XAML pages, controls, and resource dictionaries in labyItems using shared styling and built-in MAUI APIs.
---

# MAUI XAML style

Use the installed .NET 10 MAUI APIs and existing repository controls. Do not install or include external packages. Inspect the closest page in the same feature family before choosing layout dimensions; calculator, calendar, search, and detail pages have distinct established treatments.

## Surfaces

Use `Border` for outlined containers in both XAML and C#. Do not introduce `Frame`. Keep one content child, using a layout when multiple children are needed.

The shared styles live in `labyItems/Resources/Styles/Styles.xaml`:

| Style | Purpose | Padding | Shape |
| --- | --- | --- | --- |
| `CardBorderStyle` | General card; preserves migrated card defaults | 20 | RoundRectangle 8 |
| `DetailCardBorderStyle` | Detail content card | 16 | RoundRectangle 12 |
| `SectionCardBorderStyle` | Grouped form/section card | 12 | RoundRectangle 12 |
| `SelectableCardBorderStyle` | Card with its own content padding | 0 | RoundRectangle 12 |
| `MetaChipBorderStyle` | Muted metadata chip | 10,4 | RoundRectangle 14 |
| `TagPillBorderStyle` | Transparent tag pill | 8,2 | RoundRectangle 999 |

These keyed styles inherit from `CardBorderStyle`. The implicit `Border` style deliberately remains rectangular, with a one-unit gray stroke and no card background or padding. Use a keyed card style explicitly when needed; changing the implicit style would also change existing pickers, overlays, dividers, and navigation surfaces.

For migration, map `BorderColor` to `Stroke`, and `CornerRadius="12"` to `StrokeShape="RoundRectangle 12"`. Remove `HasShadow="False"`; use an explicit `Shadow` for intentional elevation. Update property elements, trigger target types and setters, ancestor bindings, C# casts, and ancestor lookup code together. Button and shape `CornerRadius` properties are valid and should remain.

## Colour and typography

Reuse `Resources/Styles/Colors.xaml`: `Primary` for primary actions, `AccentMaroonColor` for selection emphasis, `SurfaceBorderColor` for subtle outlines/dividers, `SurfaceMutedBackgroundColor` for secondary surfaces, and `MutedTextColor`/`SubtleTextColor` for secondary text. Keep semantic domain colours (for example calendar roles and tier badges) distinct. Promote repeated colours to semantic resources when editing their feature; avoid unrelated palette rewrites.

The global text family is Libre Caslon Text, with existing bold/italic style triggers. Body text defaults to 14; page/entity headings commonly use 28 bold, list titles 18, and metadata 12–13. Prefer `SectionHeaderLabelStyle`, `KeyInfoLabelStyle`, and `KeyInfoValueStyle` over duplicating their setters. Preserve font scaling and allow wrapping; constrain/truncate only deliberately compact summaries.

Use `StaticResource` for fixed design tokens. Use `DynamicResource` or `AppThemeBinding` when a value actually changes with runtime theme. The current palette is predominantly light; dark-theme support needs a coordinated palette and contrast review, not isolated colour inversions.

## Layout and interaction

Use `Grid` for aligned rows/columns and remaining-space allocation, and `VerticalStackLayout`/`HorizontalStackLayout` for simple flows. Prefer explicit `*` grid tracks over obsolete `*AndExpand` options when modifying that layout. Avoid redundant single-child layouts. Common local spacing is 8–12 between controls, 2–4 between related text, and 16–20 around page content; match the feature's peer rather than forcing one value everywhere.

Reuse `DetailPageHeader`, `ListItemRowView`, `SearchFilterBar`, `ExpandableSectionCard`, `AnimatedExpandView`, `ChevronIconView`, and existing footer/selection controls where applicable. Follow `../detail-card-view/SKILL.md` for detail pages and `../unified-glyph-icons/SKILL.md` for icons.

Keep actionable hit areas at least 44 by 44 where practical, as established by global button styles; compact glyph artwork can sit inside a larger hit area. Prefer buttons for standalone actions. Give icon-only actions meaningful `SemanticProperties.Description`; preserve keyboard focus and screen-reader reading order. Keep decorative icons from repeating adjacent text.

Prefer a bounded `CollectionView` for large lists. Do not nest it inside a `ScrollView` or replace it with an eagerly populated bindable layout. Small, bounded inline sections may use `BindableLayout`. Add accurate `x:DataType` at page/template binding boundaries when the binding context is known; account for `Source` and context changes rather than copying an ancestor type into unrelated templates.

## Verification

Parse all changed XAML, resolve renamed resource keys, and search XAML/C# for obsolete `Frame` references. Compile with `dotnet build labyItems/labyItems.csproj -f net10.0-android -t:Compile --no-restore`. For surface/interaction changes, check phone-width layout, large text, selection, expansion, navigation, and light/dark contrast on available devices; compilation alone does not validate rendering.

For the audited baseline and outstanding inconsistencies, read [the XAML audit](references/xaml-audit.md). Official references: [Border](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/border?view=net-maui-10.0), [performance and layouts](https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance?view=net-maui-10.0), [accessibility](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility?view=net-maui-10.0).
