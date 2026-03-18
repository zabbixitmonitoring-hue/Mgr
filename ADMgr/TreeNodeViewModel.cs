using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ADMgr.Classes;

namespace ADMgr
{
    public class TreeNodeViewModel : INotifyPropertyChanged
    {
        public ADContentBase Data { get; }
        public string DisplayName => Data?.name ?? string.Empty;

        private ObservableCollection<TreeNodeViewModel> _children;
        public ObservableCollection<TreeNodeViewModel> Children
        {
            get
            {
                if (_children == null)
                    _children = new ObservableCollection<TreeNodeViewModel>();
                return _children;
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                    if (_isExpanded) EnsureChildrenLoaded();
                }
            }
        }

        private bool _hasDummy;
        public bool HasDummyChild => _hasDummy;

        public TreeNodeViewModel(ADContentBase data)
        {
            Data = data;
            // if organizational unit with children, add dummy child to enable expand arrow
            if (data is ADMgr.Classes.ADOrganizationalUnit ou)
            {
                try
                {
                    // do not enumerate children now; just indicate presence
                    if (ou.content != null && ou.content.Count > 0)
                    {
                        _children = new ObservableCollection<TreeNodeViewModel>();
                        _children.Add(null); // dummy
                        _hasDummy = true;
                    }
                }
                catch { }
            }
        }

        private void EnsureChildrenLoaded()
        {
            if (!_hasDummy) return;
            try
            {
                _children.Clear();
                if (Data is ADMgr.Classes.ADOrganizationalUnit ou && ou.content != null)
                {
                    foreach (var child in ou.content)
                        _children.Add(new TreeNodeViewModel(child));
                }
            }
            catch { }
            finally
            {
                _hasDummy = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
