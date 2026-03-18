using System;
using System.Windows;

namespace ADMgr
{
    /// <summary>
    /// Логика взаимодействия для Messagegood.xaml
    /// </summary>
    public partial class Messagegood : Window
    {
        public Messagegood(String _messagegood)
        {
            InitializeComponent();

            TbMessage.Text = _messagegood;


            ShowDialog();
        }

        private void bnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
