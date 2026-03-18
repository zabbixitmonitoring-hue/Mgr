using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ADMgr
{
    public partial class SearchResultsWindow : Window
    {
        public SearchResultsWindow(List<SearchResultInfo> results)
        {
            InitializeComponent();

            // register converter resource (also declared in XAML but ensure type available)
            this.Resources["ByteArrayToImageConverter"] = new ByteArrayToImageConverter();

            lbResults.ItemsSource = results;

            bnOk.Click += (s, e) => { OnOk(); };
            bnCancel.Click += (s, e) => { this.DialogResult = false; this.Close(); };
        }

        private void OnOk()
        {
            if (lbResults.SelectedItem == null)
            {
                MessageBox.Show("Выберите элемент.", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sel = lbResults.SelectedItem as SearchResultInfo;
            if (sel != null)
            {
                if (!String.IsNullOrEmpty(sel.EntryPath))
                {
                    var main = Application.Current.MainWindow as MainWindow;
                    if (main != null)
                    {
                        List<Classes.ADContentBase> resultPath = null;
                        try
                        {
                            foreach (var item in App.activeDirectoryContent)
                            {
                                List<Classes.ADContentBase> path = new List<Classes.ADContentBase>();
                                if (main.TryFindPathToEntry(item, sel.EntryPath, path, out resultPath))
                                    break;
                            }
                        }
                        catch { resultPath = null; }

                        if (resultPath != null && resultPath.Count > 0)
                        {
                            main.SelectPathInTreeView(resultPath);
                            this.DialogResult = true;
                            this.Close();
                            return;
                        }
                        else
                        {
                            MessageBox.Show("Объект найден в AD, но не загружен в дереве. Попробуйте обновить содержимое.", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Объект найден в AD, но отсутствует путь.", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
        }
    }

    public class ByteArrayToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                byte[] bytes = value as byte[];
                if (bytes == null || bytes.Length == 0) return null;
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                return bmp;
            }
            catch { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
