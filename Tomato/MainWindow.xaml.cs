using System;
using System.Drawing;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;

namespace Tomato {
    class Settings {
        public int Interval { get; set; } = 60 * 20;
        public int ToastDuration { get; set; } = 5;
        public bool BeepStart { get; set; } = true;
        public bool BeepEnd { get; set; } = false;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        readonly SoundPlayer snd = new SoundPlayer(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/notify.wav")).Stream);

        readonly Settings settings;

        public MainWindow() {
            InitializeComponent();

            {
                var yaml = ":";

                try {
                    yaml = File.ReadAllText("Tomato.yaml");
                }
                catch {

                }

                settings = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build()
                    .Deserialize<Settings>(yaml);
            }

            {
                var desktop = SystemParameters.WorkArea;
                Left = desktop.Right - Width;
                Top = desktop.Bottom - Height;
            }            

            {
                var timer = new DispatcherTimer();
                timer.Tick += (s, e) => {
                    ShowToast();
                };
                timer.Interval = TimeSpan.FromSeconds(settings.Interval);
                timer.Start();

                {
                    var icon = new NotifyIcon();
                    icon.Visible = true;
                    using (var res = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/watch.ico")).Stream) {
                        icon.Icon = new Icon(res);
                    }
                    icon.ContextMenuStrip = new ContextMenuStrip();
                    icon.ContextMenuStrip.Items.Add("pause", null, (s, e) => {
                        var toggleTimer = (ToolStripItem)s;

                        if (toggleTimer.Text == "pause") {
                            timer.Stop();
                            toggleTimer.Text = "resume";
                            overlay.Visibility = Visibility.Visible;
                        }
                        else {
                            timer.Start();
                            toggleTimer.Text = "pause";
                            overlay.Visibility = Visibility.Collapsed;
                        }
                    });
                    icon.ContextMenuStrip.Items.Add("exit", null, (s, e) => { Close(); });
                }
            }
        }

        private void Window_Deactivated(object sender, EventArgs e) {
            Topmost = true;
        }

        void BeepCooled() {
            Task.Factory.StartNew(() => {
                Console.Beep(3000, 200);
                Console.Beep(2500, 200);
            });
        }

        void ShowToast() {
            if (settings.BeepStart) snd.Play();

            var toast = new Toast();
            toast.Left = SystemParameters.WorkArea.Right - toast.Width;
            toast.Top = 200;
            toast.Show();

            var timer = new DispatcherTimer();
            var cur = 0;
            var max = (double)settings.ToastDuration;
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) => {
                cur++;
                toast.Progress = cur / max;

                if (cur == max) {
                    toast.Close();
                    timer.Stop();
                    if (settings.BeepEnd) BeepCooled();
                }
            };
            timer.Start();
        }
    }
}
