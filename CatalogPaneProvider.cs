using System.Windows;
using Autodesk.Revit.UI;

namespace TypeCatalogMVP
{
    public class CatalogPaneProvider : IDockablePaneProvider
    {
        private readonly FrameworkElement _element;

        public CatalogPaneProvider(FrameworkElement element) => _element = element;

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = _element;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Tabbed,
                TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette
            };
        }
    }
}
