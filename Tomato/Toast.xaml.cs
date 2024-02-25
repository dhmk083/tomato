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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Tomato {
    /// <summary>
    /// Interaction logic for Toast.xaml
    /// </summary>
    public partial class Toast : Window {
        bool closeAnimated = false;

        public double Progress { 
            set {
                //progress.Value = value;
            } 
        }

        public Toast() {
            InitializeComponent();

            Closing += Toast_Closing;
        }

        private void Toast_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (!closeAnimated) {
                e.Cancel = true;
                
                var a = new DoubleAnimation {
                    From = 0,
                    To = 500,
                    Duration = new Duration(TimeSpan.FromMilliseconds(800)),
                };
                Storyboard.SetTarget(a, panel);
                Storyboard.SetTargetProperty(a, new PropertyPath("(Canvas.Left)"));
                var s = new Storyboard();
                s.Children.Add(a);
                s.Completed += (_s, _e) => {
                    closeAnimated = true;
                    Close();
                };
                s.Begin();
            }
        }

        private void CloseStoryboard_Completed(object sender, EventArgs e) {
            Close();
        }
    }
}
