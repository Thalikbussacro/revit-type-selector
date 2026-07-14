using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
                .Select(s => new FamilyTypeItem
                {
                    SymbolId = s.Id,
                    Name = s.Name,
                    FamilyName = s.FamilyName,
                    Category = s.Category?.Name ?? "Uncategorized",
                    Thumbnail = TryGetPreview(s)
                })
                .ToList();

            ViewModel.SetItems(items);
        }

        private static BitmapSource? TryGetPreview(FamilySymbol symbol)
        {
            try
            {
                using var bmp = symbol.GetPreviewImage(new Size(96, 96));
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

        private static void PlaceSymbol(UIDocument uidoc, Document doc, ElementId symbolId)
        {
            if (doc.GetElement(symbolId) is not FamilySymbol symbol) return;

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
                // e.g. a type that can't be placed this way (needs a specific host)
                TaskDialog.Show("Type Catalog", $"Could not place this type:\n{ex.Message}");
            }
        }

        public string GetName() => "Type Catalog Request Handler";
    }
}
