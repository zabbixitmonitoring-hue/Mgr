using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ADMgr
{
    public partial class Barcode39Char : UserControl
    {
        private const Double ratioCoeff = 1.5;

        public Barcode39Char()
        {
            InitializeComponent();
        }

        public Barcode39Char(Char _ch)
        {
            InitializeComponent();

            switch (_ch)
            {
                case '0':
                    SetBarsWidth("NNNWWNWNN");
                    break;
                case '1':
                    SetBarsWidth("WNNWNNNNW");
                    break;
                case '2':
                    SetBarsWidth("NNWWNNNNW");
                    break;
                case '3':
                    SetBarsWidth("WNWWNNNNN");
                    break;
                case '4':
                    SetBarsWidth("NNNWWNNNW");
                    break;
                case '5':
                    SetBarsWidth("WNNWWNNNN");
                    break;
                case '6':
                    SetBarsWidth("NNWWWNNNN");
                    break;
                case '7':
                    SetBarsWidth("NNNWNNWNW");
                    break;
                case '8':
                    SetBarsWidth("WNNWNNWNN");
                    break;
                case '9':
                    SetBarsWidth("NNWWNNWNN");
                    break;
                case '*':
                    SetBarsWidth("NWNNWNWNN");
                    break;
            }

            tbChar.Text = _ch.ToString();
        }

        private void SetBarsWidth(String _widths)
        {
            if (_widths.Length == 9)
            {
                bar1.Width = (_widths[0] == 'N' ? 1 : 3) * ratioCoeff;
                bar2.Width = (_widths[1] == 'N' ? 1 : 3) * ratioCoeff;
                bar3.Width = (_widths[2] == 'N' ? 1 : 3) * ratioCoeff;
                bar4.Width = (_widths[3] == 'N' ? 1 : 3) * ratioCoeff;
                bar5.Width = (_widths[4] == 'N' ? 1 : 3) * ratioCoeff;
                bar6.Width = (_widths[5] == 'N' ? 1 : 3) * ratioCoeff;
                bar7.Width = (_widths[6] == 'N' ? 1 : 3) * ratioCoeff;
                bar8.Width = (_widths[7] == 'N' ? 1 : 3) * ratioCoeff;
                bar9.Width = (_widths[8] == 'N' ? 1 : 3) * ratioCoeff;
                bar10.Width = 1 * ratioCoeff;
            }
        }
    }
}
