---
name: unified-glyph-icons
description: Add, change, or review glyph icons in labyItems while keeping every glyph code centralized in FontAwesomeGlyphs and referenced consistently from C# and XAML.
---

# Unified Glyph Icons

Use `labyItems/Helpers/FontAwesomeGlyphs.cs` as the single source of truth for glyph characters and the Font Awesome font-family name.

## Rules

- Never add a raw Font Awesome Unicode value, numeric XML entity, or HTML `<i>` element to XAML or C#.
- Reuse an existing semantic constant when it represents the same action or concept.
- When a glyph is new, add one clearly named constant to `FontAwesomeGlyphs` first. Avoid code-point names unless the existing icon's meaning genuinely cannot be determined.
- In C#, reference `FontAwesomeGlyphs.ConstantName` and import `labyItems.Helpers`.
- In XAML, declare `xmlns:helpers="clr-namespace:labyItems.Helpers"` and bind with `{x:Static helpers:FontAwesomeGlyphs.ConstantName}`.
- Keep `FontFamily` set to `FontAwesomeGlyphs.SolidFamily` in C# or `FASolid` in existing XAML styles. Do not introduce a second font-family registry.
- Dynamic icon selection must return centralized constants rather than raw glyph strings.
- Preserve accessibility descriptions and existing button behavior when changing an icon reference.

## Audit

Before finishing, search both XAML and C# for uncatalogued glyphs:

```sh
rg -n '&#x[0-9A-Fa-f]+;|\\u[fF][0-9a-fA-F]{3,5}' labyItems \
  -g '*.xaml' -g '*.cs' -g '!FontAwesomeGlyphs.cs'
```

The only acceptable matches are documented non-Font-Awesome Unicode symbols that cannot use the icon font. Build at least one application target after changing XAML references.
