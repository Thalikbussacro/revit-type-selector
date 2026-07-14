using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;

namespace TypeCatalogMVP
{
    public class FamilyTypeItem
    {
        public ElementId SymbolId { get; set; } = ElementId.InvalidElementId;
        public string Name { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string Category { get; set; } = "";
        public BitmapSource? Thumbnail { get; set; }
    }
}
