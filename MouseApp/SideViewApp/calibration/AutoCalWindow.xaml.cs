using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SideViewApp
{
    /// <summary>
    /// Interaction logic for AutoCalWindow.xaml
    /// </summary>
    public partial class AutoCalWindow : Window
    {
        private bool closing = false;
        public bool succeeded = false;

        public AutoCalWindow()
        {
            InitializeComponent();
        }

        private void Window_LostFocus(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key==Key.Space)
            {
                this.Close();
            }
        }

        public void runAutoCal()
        {

        }

        public void runManualCal()
        {

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closing = true;
        }

        private void Window_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!closing)
            {
                this.Close();
            }
        }
    }
}
