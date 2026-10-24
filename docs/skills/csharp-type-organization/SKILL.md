---
name: csharp-type-organization
description: Organize C# declarations in the labyItems solution when creating, changing, reviewing, or relocating classes, records, structs, interfaces, and enums.
---

# C# Type Organization

Keep each top-level class, record, struct, interface, or enum in its own `.cs` file named after the type. Do not append supporting top-level declarations to a page, view model, service, or another type's file.

Private nested types may remain with their owning type when they are implementation details that are not used outside it. A type used by multiple owners, exposed through a public or internal contract, or independently meaningful belongs in its own file.

Place related data and UI contract types in a domain folder under `labyItems/Models`, using a namespace that matches the folder. For example, shared global-search options, enums, filter state, and result view models belong in `labyItems/Models/Search` and use `labyItems.Models.Search`. Keep behavior-owning pages, controls, services, and primary view models in their existing architectural folders.

When separating existing declarations:

1. Preserve accessibility and behavior unless the requested design requires a change.
2. Update namespaces and imports at every use site, including tests and XAML type references.
3. Update object initializers to match the separated model's current composition instead of restoring obsolete convenience properties.
4. Build the solution and fix all compile errors caused by the move.

During reviews and audits, report pre-existing multi-type files outside the requested change rather than adding new violations. Improve touched files when the split is safe and in scope.
