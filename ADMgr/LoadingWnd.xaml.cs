using System;
using System.Windows;
using System.Windows.Threading;

namespace ADMgr
{
    public partial class LoadingWnd : Window
    {
        public Boolean isCancel;

        public static Action TotalProgressIncDelegate;

        public static Action CurrentProgressIncDelegate;

        public LoadingWnd(String _title)
        {
            InitializeComponent();

            Title = _title;
            pbTotalProgress.Value = 0;

            TotalProgressIncDelegate = delegate()
            {
                pbTotalProgress.Value++;
                pbTotalProgress.UpdateLayout();
                tbTotalProgress.Text = ((Int32)(pbTotalProgress.Value / pbTotalProgress.Maximum * 100)).ToString() + "%";
                //tbTotalProgress.Text = pbTotalProgress.Value.ToString() + "/" + pbTotalProgress.Maximum.ToString();
                tbTotalProgress.UpdateLayout();
            };

            CurrentProgressIncDelegate = delegate()
            {
                pbCurrentProgress.Value++;
                pbCurrentProgress.UpdateLayout();
                tbCurrentProgress.Text = ((Int32)(pbCurrentProgress.Value / pbCurrentProgress.Maximum * 100)).ToString() + "%";
                //tbCurrentProgress.Text = pbCurrentProgress.Value.ToString() + "/" + pbCurrentProgress.Maximum.ToString();
                tbCurrentProgress.UpdateLayout();
            };

            isCancel = false;
            Show();
        }

        public bool IsCancel { get; set; }

        public void TotalProgressInc()
        {
            if (TotalProgressIncDelegate != null)
            {
                Dispatcher.Invoke(DispatcherPriority.Background, TotalProgressIncDelegate);
                pbCurrentProgress.Value = 0;
            }
        }

        public void CurrentProgressInc()
        {
            if (CurrentProgressIncDelegate != null)
                Dispatcher.Invoke(DispatcherPriority.Background, CurrentProgressIncDelegate);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (pbTotalProgress.Value < pbTotalProgress.Maximum)
                isCancel = true;
        }
    }
}