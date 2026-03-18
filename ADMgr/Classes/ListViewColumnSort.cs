using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ADMgr.ListViewColumnSort
{
    public class GridViewSort
    {
        #region Attached properties

        public static ICommand GetCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(CommandProperty);
        }

        public static void SetCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(CommandProperty, value);
        }

        // Using a DependencyProperty as the backing store for Command.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached(
                "Command",
                typeof(ICommand),
                typeof(GridViewSort),
                new UIPropertyMetadata(
                    null,
                    (o, e) =>
                    {
                        ItemsControl listView = o as ItemsControl;
                        if (listView != null)
                        {
                            if (!GetAutoSort(listView)) // Don't change click handler if AutoSort enabled
                            {
                                if (e.OldValue != null && e.NewValue == null)
                                {
                                    listView.RemoveHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ColumnHeader_Click));
                                }
                                if (e.OldValue == null && e.NewValue != null)
                                {
                                    listView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ColumnHeader_Click));
                                }
                            }
                        }
                    }
                )
            );

        public static Boolean GetAutoSort(DependencyObject obj)
        {
            return (Boolean)obj.GetValue(AutoSortProperty);
        }

        public static void SetAutoSort(DependencyObject obj, Boolean value)
        {
            obj.SetValue(AutoSortProperty, value);
        }

        // Using a DependencyProperty as the backing store for AutoSort.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AutoSortProperty =
            DependencyProperty.RegisterAttached(
                "AutoSort",
                typeof(Boolean),
                typeof(GridViewSort),
                new UIPropertyMetadata(
                    false,
                    (o, e) =>
                    {
                        ListView listView = o as ListView;
                        if (listView != null)
                        {
                            if (GetCommand(listView) == null) // Don't change click handler if a command is set
                            {
                                Boolean oldValue = (Boolean)e.OldValue;
                                Boolean newValue = (Boolean)e.NewValue;
                                if (oldValue && !newValue)
                                {
                                    listView.RemoveHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ColumnHeader_Click));
                                }
                                if (!oldValue && newValue)
                                {
                                    listView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ColumnHeader_Click));
                                }
                            }
                        }
                    }
                )
            );

        public static string GetPropertyName(DependencyObject obj)
        {
            return (String)obj.GetValue(PropertyNameProperty);
        }

        public static void SetPropertyName(DependencyObject obj, String value)
        {
            obj.SetValue(PropertyNameProperty, value);
        }

        // Using a DependencyProperty as the backing store for PropertyName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PropertyNameProperty =
            DependencyProperty.RegisterAttached(
                "PropertyName",
                typeof(String),
                typeof(GridViewSort),
                new UIPropertyMetadata(null)
            );

        #endregion

        #region Column header click event handler

        private static void ColumnHeader_Click(Object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader headerClicked = e.OriginalSource as GridViewColumnHeader;
            if (headerClicked != null)
            {
                String propertyName = GetPropertyName(headerClicked.Column);
                if (!String.IsNullOrEmpty(propertyName))
                {
                    ListView listView = GetAncestor<ListView>(headerClicked);
                    if (listView != null)
                    {
                        ICommand command = GetCommand(listView);
                        if (command != null)
                        {
                            if (command.CanExecute(propertyName))
                                command.Execute(propertyName);
                        }
                        else if (GetAutoSort(listView))
                            ApplySort(listView.Items, propertyName);
                    }
                }
            }
        }

        #endregion

        #region Helper methods

        public static T GetAncestor<T>(DependencyObject reference) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(reference);
            while (!(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            if (parent != null)
                return (T)parent;
            else
                return null;
        }

        public static void ApplySort(ICollectionView view, String propertyName)
        {
            ListSortDirection direction = ListSortDirection.Ascending;

            if (view.SortDescriptions.Count > 0)
            {
                SortDescription currentSort = view.SortDescriptions[0];
                if (currentSort.PropertyName == propertyName)
                    direction = currentSort.Direction == ListSortDirection.Ascending 
                        ? direction = ListSortDirection.Descending 
                        : direction = ListSortDirection.Ascending;
                view.SortDescriptions.Clear();
            }

            if (!String.IsNullOrEmpty(propertyName))
            {
                if (propertyName == "marksAverage")
                {
                    view.SortDescriptions.Add(new SortDescription("isOrphan", ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription("marksAverage", ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription("isManyChildren", ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription("maths", ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription("lang", ListSortDirection.Descending));
                }
                else
                    view.SortDescriptions.Add(new SortDescription(propertyName, direction));
            }
        }

        #endregion
    }
}