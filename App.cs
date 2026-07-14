using System;
using System.Reflection;
using Autodesk.Revit.UI;

namespace TypeCatalogMVP
{
    public class App : IExternalApplication
    {
        // Stable, hard-coded pane id (any fixed GUID works).
        public static readonly DockablePaneId PaneId =
            new DockablePaneId(new Guid("D6E9F1A2-3B4C-4D5E-8F60-1122334455AA"));

        // Session-lived singletons. The WPF control + VM are created once at startup and
        // reused for the whole Revit session (dockable panes must be registered in OnStartup).
        internal static CatalogViewModel ViewModel = null!;
        internal static ExternalEvent ExternalEvent = null!;
        internal static RequestHandler Handler = null!;

        public Result OnStartup(UIControlledApplication app)
        {
            Handler = new RequestHandler();
            ExternalEvent = ExternalEvent.Create(Handler);
            ViewModel = new CatalogViewModel(ExternalEvent, Handler);
            Handler.ViewModel = ViewModel; // back-ref so Execute() can push results to the VM

            var view = new CatalogView { DataContext = ViewModel };
            var provider = new CatalogPaneProvider(view);
            app.RegisterDockablePane(PaneId, "Type Catalog", provider);

            BuildRibbon(app);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;

        private static void BuildRibbon(UIControlledApplication app)
        {
            const string tab = "Type Catalog";
            try { app.CreateRibbonTab(tab); } catch { /* already exists */ }

            var panel = app.CreateRibbonPanel(tab, "Catalog");
            var asmPath = Assembly.GetExecutingAssembly().Location;
            var btn = new PushButtonData(
                "ShowCatalog", "Catalog", asmPath, "TypeCatalogMVP.ShowPaneCommand")
            {
                ToolTip = "Open the type catalog and place a loaded family type."
            };
            panel.AddItem(btn);
        }
    }
}
