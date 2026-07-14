# Type Catalog MVP (Revit 2026)

Dockable pane that lists the loadable family types already in the active document, grouped by
category, with search and thumbnails. Click a card → Revit's native placement loop.

## Build

1. Confirm the Revit DLL paths in `TypeCatalogMVP.csproj` match your install
   (default: `C:\Program Files\Autodesk\Revit 2026\`).
2. `dotnet build -c Debug` (or build in VS 2022 / Rider). Output is `net8.0-windows`, x64.

## Deploy

1. Copy the build output DLL somewhere stable, e.g. `C:\RevitAddins\TypeCatalogMVP\`.
2. Edit `TypeCatalogMVP.addin` so `<Assembly>` is the FULL path to that DLL
   (or just drop the `.addin` next to the DLL and use a relative name).
3. Copy `TypeCatalogMVP.addin` to:
   `C:\ProgramData\Autodesk\Revit\Addins\2026\`
4. Start Revit → **Type Catalog** ribbon tab → **Catalog** button. Open a project with some
   families loaded, hit **Refresh**.

## Gotchas to watch on first run

- **System.Drawing.Common version.** The csproj references it compile-time only
  (`ExcludeAssets=runtime`) to avoid clashing with Revit's own copy. If you get a load error,
  align the version to whatever Revit 2026 ships.
- **`.addin` Assembly path / AddInId.** Must be a real path; the GUID must be unique per machine.
- **Empty catalog.** The list only shows *loadable* family types (`FamilySymbol`). System types
  (walls/floors/roofs) are intentionally out of scope for v0 — they can't use
  `PromptForFamilyInstancePlacement` anyway.
- **Missing thumbnails.** `GetPreviewImage` can return null for some families; those show the
  empty grey box. Expected, not a bug.

## Architecture (one thing that matters)

The pane is modeless, so it can't call the Revit API directly. Every API touch goes through
`RequestHandler : IExternalEventHandler`. The pane sets a request (`Refresh` / `Place`) and
raises the `ExternalEvent`; Revit calls `Execute` back inside a valid context.

## Next increment (v0.1): type-swap

Add a `RequestType.Swap` mode: read `uidoc.Selection`, keep only `FamilyInstance`s, and on a
card click set their symbol to the chosen `FamilySymbol` inside a transaction. The only real
work is the compatibility rule (same category / same family?) and how to handle a mixed or
empty selection.
