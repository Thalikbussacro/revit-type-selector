using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.UI;

namespace TypeCatalogMVP
{
    public partial class CatalogView : UserControl
    {
        public CatalogView()
        {
            InitializeComponent();
            ApplyTheme();
        }

        /// <summary>
        /// Picks a neutral brush set that matches Revit's current light/dark theme and injects
        /// it into the control's resources, where the XAML picks it up via DynamicResource.
        /// </summary>
        public void ApplyTheme()
        {
            bool dark;
            try { dark = UIThemeManager.CurrentTheme == UITheme.Dark; }
            catch { dark = false; } // theme API unavailable: default to light

            if (dark)
                SetBrushes(
                    bg: "#2C2B29", card: "#363532", fg: "#E6E3DC", muted: "#9B968D",
                    line: "#454340", accent: "#5A9FE0", accentFg: "#FFFFFF",
                    pill: "#3A3936", hover: "#33445A", thumb: "#F3F2EF");
            else
                SetBrushes(
                    bg: "#F4F3F1", card: "#FFFFFF", fg: "#232323", muted: "#7A766E",
                    line: "#E2E0DB", accent: "#2F6FB0", accentFg: "#FFFFFF",
                    pill: "#E9E7E2", hover: "#EEF4FB", thumb: "#F7F7F6");
        }

        private void SetBrushes(string bg, string card, string fg, string muted, string line,
                                string accent, string accentFg, string pill, string hover, string thumb)
        {
            Resources["CatBg"] = Brush(bg);
            Resources["CatCard"] = Brush(card);
            Resources["CatFg"] = Brush(fg);
            Resources["CatMuted"] = Brush(muted);
            Resources["CatLine"] = Brush(line);
            Resources["CatAccent"] = Brush(accent);
            Resources["CatAccentFg"] = Brush(accentFg);
            Resources["CatPill"] = Brush(pill);
            Resources["CatHover"] = Brush(hover);
            Resources["CatThumb"] = Brush(thumb);
        }

        private static SolidColorBrush Brush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
