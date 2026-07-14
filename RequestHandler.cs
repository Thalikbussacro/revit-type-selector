using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TypeCatalogMVP
{
    public enum RequestType { None, Refresh, Place }

    /// <summary>
    /// Everything here runs inside a valid Revit API context (Revit calls Execute on its main thread).
    /// The pane only ever sets a request + Raise()s the ExternalEvent; it never calls the API directly.
    /// </summary>
    public class RequestHandler : IExternalEventHandler
    {
        private readonly object _lock = new();
        private RequestType _request = RequestType.None;
        private ElementId? _placeTarget;

        public CatalogViewModel ViewModel { get; set; } = null!;

        public void SetRefresh()
        {
            lock (_lock) _request = RequestType.Refresh;
        }

        public void SetPlace(ElementId symbolId)
        {
            lock (_lock) { _request = RequestType.Place; _placeTarget = symbolId; }
        }

        public void Execute(UIApplication app)
        {
            RequestType req;
            ElementId? target;
            lock (_lock)
            {
                req = _request;
                target = _placeTarget;
                _request = RequestType.None;
            }

            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) return; // no open document
            var doc = uidoc.Document;

            switch (req)
            {
                case RequestType.Refresh:
                    RefreshCatalog(doc);
                    break;
                case RequestType.Place:
                    if (target != null) PlaceSymbol(uidoc, doc, target);
                    break;
            }
        }

        private void RefreshCatalog(Document doc)
        {
            // CONCERN: synchronous over every FamilySymbol + preview render. Fine for typical
            // projects; on a doc with thousands of types this briefly blocks Revit. If that
            // bites, move preview generation to lazy/on-scroll or a background pass.
            var items = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(IsPlaceableByPoint) // hide types PromptForFamilyInstancePlacement can't handle
                .Select(s => new FamilyTypeItem
                {
                    SymbolId = s.Id,
                    Name = s.Name,
                    FamilyName = s.FamilyName,
                    Category = EnglishCategory(s.Category),
                    Thumbnail = TryGetPreview(s)
                })
                .ToList();

            ViewModel.SetItems(items);
        }

        // Category.Name is localized to Revit's UI language ("Janelas"), so instead we derive
        // the canonical English name from the BuiltInCategory enum: OST_PlumbingFixtures ->
        // "Plumbing Fixtures". Falls back to the localized name for non-built-in categories.
        private static string EnglishCategory(Category? category)
        {
            if (category == null) return "Uncategorized";
            try
            {
                var bic = category.BuiltInCategory;
                if (bic != BuiltInCategory.INVALID)
                {
                    var name = bic.ToString();
                    if (name.StartsWith("OST_")) name = name.Substring(4);
                    return Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ");
                }
            }
            catch { /* older API or odd category: fall back below */ }
            return category.Name;
        }

        private static BitmapSource? TryGetPreview(FamilySymbol symbol)
        {
            try
            {
                using var bmp = symbol.GetPreviewImage(new Size(128, 128));
                if (bmp == null) return null;

                // Encode to PNG then load into WPF — avoids the GetHbitmap/DeleteObject GDI-leak dance.
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze(); // cross-thread safe
                return img;
            }
            catch
            {
                return null; // placeholder box shows in the UI
            }
        }

        // PromptForFamilyInstancePlacement only accepts point-placed families. Curve-based,
        // view-based (annotation/detail), structural line-based and adaptive families make it
        // throw "Modification of the document is forbidden", so they're excluded up front.
        private static bool IsPlaceableByPoint(FamilySymbol symbol) =>
            symbol.Family.FamilyPlacementType switch
            {
                FamilyPlacementType.OneLevelBased => true,
                FamilyPlacementType.OneLevelBasedHosted => true,
                FamilyPlacementType.TwoLevelsBased => true,
                FamilyPlacementType.WorkPlaneBased => true,
                _ => false
            };

        // Placement is only enabled in model views. Sheets, schedules, legends and drafting
        // views disable the command ("The command to place an instance ... is disabled").
        private static bool ViewSupportsPlacement(View? view) =>
            view is not null && view.ViewType switch
            {
                ViewType.FloorPlan => true,
                ViewType.CeilingPlan => true,
                ViewType.EngineeringPlan => true,
                ViewType.AreaPlan => true,
                ViewType.ThreeD => true,
                ViewType.Section => true,
                ViewType.Elevation => true,
                ViewType.Detail => true,
                _ => false
            };

        private static void PlaceSymbol(UIDocument uidoc, Document doc, ElementId symbolId)
        {
            if (doc.GetElement(symbolId) is not FamilySymbol symbol) return;

            if (!IsPlaceableByPoint(symbol))
            {
                TaskDialog.Show("Type Catalog",
                    $"'{symbol.Name}' is a {symbol.Family.FamilyPlacementType} family and can't be " +
                    "placed by point from the catalog. Only point-placed model families are supported.");
                return;
            }

            var activeView = uidoc.ActiveView;
            if (!ViewSupportsPlacement(activeView))
            {
                TaskDialog.Show("Type Catalog",
                    $"Can't place '{symbol.Name}' in the current view ({activeView?.ViewType}).\n\n" +
                    "Open a model view first — a floor plan, ceiling plan, 3D, section or elevation — " +
                    "then try again. Families can't be placed on sheets, schedules or legends.");
                return;
            }

            try
            {
                if (!symbol.IsActive)
                {
                    using var t = new Transaction(doc, "Activate type");
                    t.Start();
                    symbol.Activate();
                    t.Commit();
                    doc.Regenerate();
                }

                // Native placement loop: hosting, rotation, repeat-place — all handled by Revit.
                uidoc.PromptForFamilyInstancePlacement(symbol);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // user pressed Esc to end placement — normal, swallow it
            }
            catch (Exception ex)
            {
                // e.g. a type that can't be placed this way (needs a specific host, or the
                // active view disables placement). Surface the diagnostic context so we can tell.
                var view = uidoc.ActiveView;
                TaskDialog.Show("Type Catalog",
                    $"Could not place '{symbol.Name}':\n{ex.Message}\n\n" +
                    $"Placement type: {symbol.Family.FamilyPlacementType}\n" +
                    $"Category: {symbol.Category?.Name ?? "?"}\n" +
                    $"Active view: {view?.ViewType} — {view?.Name}");
            }
        }

        public string GetName() => "Type Catalog Request Handler";
    }
}
