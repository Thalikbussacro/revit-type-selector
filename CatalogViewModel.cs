using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.UI;

namespace TypeCatalogMVP
{
    public class CatalogViewModel : INotifyPropertyChanged
    {
        private readonly ExternalEvent _externalEvent;
        private readonly RequestHandler _handler;

        // Pseudo-category shown as the first pill; means "no category filter".
        public const string AllCategories = "All";

        public ObservableCollection<FamilyTypeItem> Items { get; } = new();
        public ICollectionView ItemsView { get; }

        // Category pills, rebuilt from the loaded items. Always starts with "All".
        public ObservableCollection<string> Categories { get; } = new() { AllCategories };

        private string _search = "";
        public string Search
        {
            get => _search;
            set { _search = value; OnPropertyChanged(); ItemsView.Refresh(); }
        }

        private string _selectedCategory = AllCategories;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value ?? AllCategories;
                OnPropertyChanged();
                ItemsView.Refresh();
            }
        }

        public ICommand PlaceCommand { get; }
        public ICommand RefreshCommand { get; }

        public CatalogViewModel(ExternalEvent externalEvent, RequestHandler handler)
        {
            _externalEvent = externalEvent;
            _handler = handler;

            ItemsView = CollectionViewSource.GetDefaultView(Items);
            ItemsView.Filter = FilterItem;

            PlaceCommand = new RelayCommand(p =>
            {
                if (p is FamilyTypeItem item)
                {
                    _handler.SetPlace(item.SymbolId);
                    _externalEvent.Raise();
                }
            });

            RefreshCommand = new RelayCommand(_ => RequestRefresh());
        }

        private bool FilterItem(object o)
        {
            if (o is not FamilyTypeItem it) return false;

            // Category pill filter (All = no restriction).
            if (_selectedCategory != AllCategories
                && !string.Equals(it.Category, _selectedCategory, StringComparison.Ordinal))
                return false;

            // Text search, applied within the selected category.
            if (string.IsNullOrWhiteSpace(_search)) return true;

            var q = _search.Trim();
            return it.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || it.FamilyName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || it.Category.Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        public void RequestRefresh()
        {
            _handler.SetRefresh();
            _externalEvent.Raise();
        }

        // Called from RequestHandler.Execute (Revit main thread == the WPF UI thread here).
        public void SetItems(IEnumerable<FamilyTypeItem> items)
        {
            Items.Clear();
            foreach (var i in items
                         .OrderBy(x => x.Category)
                         .ThenBy(x => x.FamilyName)
                         .ThenBy(x => x.Name))
            {
                Items.Add(i);
            }

            RebuildCategories();
            ItemsView.Refresh();
        }

        private void RebuildCategories()
        {
            var cats = Items.Select(i => i.Category)
                            .Where(c => !string.IsNullOrEmpty(c))
                            .Distinct()
                            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                            .ToList();

            Categories.Clear();
            Categories.Add(AllCategories);
            foreach (var c in cats) Categories.Add(c);

            // Keep the current pill if it still exists, otherwise fall back to "All".
            if (!Categories.Contains(_selectedCategory))
            {
                _selectedCategory = AllCategories;
                OnPropertyChanged(nameof(SelectedCategory));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
