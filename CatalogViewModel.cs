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

        public ObservableCollection<FamilyTypeItem> Items { get; } = new();
        public ICollectionView ItemsView { get; }

        private string _search = "";
        public string Search
        {
            get => _search;
            set { _search = value; OnPropertyChanged(); ItemsView.Refresh(); }
        }

        public ICommand PlaceCommand { get; }
        public ICommand RefreshCommand { get; }

        public CatalogViewModel(ExternalEvent externalEvent, RequestHandler handler)
        {
            _externalEvent = externalEvent;
            _handler = handler;

            ItemsView = CollectionViewSource.GetDefaultView(Items);
            ItemsView.GroupDescriptions.Add(
                new PropertyGroupDescription(nameof(FamilyTypeItem.Category)));
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
            if (string.IsNullOrWhiteSpace(_search)) return true;
            if (o is not FamilyTypeItem it) return false;

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
            ItemsView.Refresh();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
