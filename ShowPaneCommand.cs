using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TypeCatalogMVP
{
    [Transaction(TransactionMode.Manual)]
    public class ShowPaneCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var pane = commandData.Application.GetDockablePane(App.PaneId);
            pane.Show();
            App.ViewModel.RequestRefresh(); // populate from the active document
            return Result.Succeeded;
        }
    }
}
