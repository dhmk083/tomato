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

namespace Tomato {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        [StructLayout(LayoutKind.Sequential)]
        struct LastInputInfo {
            public int cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LastInputInfo info);

        readonly MediaPlayer snd = new MediaPlayer();

        public MainWindow() {
            InitializeComponent();

            {
                var desktop = SystemParameters.WorkArea;
                Left = desktop.Right - Width;
                Top = desktop.Bottom - Height;
            }            

            {
                // all time in sec
                var acc = 0;
                var timeStep = 1; // X sec timer ticks
                var range = 15 * 60; // time in seconds before overload
                var holdTime = 3 * 60; // do not decrement acc after inactivity for X sec
                var decSpeed = 30; // after overload decrement within X sec
                var toastPeriod = range / 5; // show toast every X sec while incrementing

                var fired = false;
                var firedTime = 0;
                var prevInputTime = 0;
                var nextToastTime = 0;
                var cooling = false;
                Window blinky = null;

                var timer = new DispatcherTimer();
                timer.Tick += (s, e) => {
                    var info = new LastInputInfo();
                    info.cbSize = Marshal.SizeOf(info);
                    var ok = GetLastInputInfo(ref info);
                    if (!ok) {
                        System.Windows.Forms.MessageBox.Show("GetLastInputInfo failed", "Tomato");
                        throw new Exception("GetLastInputInfo failed");
                    }

                    var lastInputTime = (int)(info.dwTime / 1000);
                    var currentTime = Environment.TickCount / 1000;

                    var lastInputDelta = currentTime - lastInputTime;

                    if (lastInputDelta < timeStep) {
                        if (lastInputTime - prevInputTime < holdTime) {
                            acc += lastInputTime - prevInputTime;
                        }
                        else {
                            acc += timeStep;
                        }

                        cooling = false;
                    }

                    var decThreshold = cooling ? timeStep : holdTime;

                    if (lastInputDelta >= decThreshold) {
                        if (cooling) {
                            acc -= decSpeed;
                        }
                        else {
                            acc -= timeStep;
                        }
                    }

                    if (acc <= 0) {
                        prevInputTime = 0;
                    }
                    else {
                        prevInputTime = lastInputTime;
                    }

                    var isOverloaded = acc > range;

                    acc = Math.Max(0, Math.Min(range, acc));

                    var ratio = acc / (double)range;
                    progress.Value = Math.Max(0, Math.Min(1.0, ratio));

                    if (ratio >= 1.0 && !fired) {
                        fired = true;
                        firedTime = currentTime;
                        isOverloaded = true;

                        blinky = new BlinkyScreen();
                        blinky.Show();
                    }

                    if (fired && ratio < 1.0) {
                        fired = false;
                        blinky.Close();
                        blinky = null;
                    }

                    if (isOverloaded && currentTime != firedTime && ((currentTime - firedTime) % 5) == 0) {
                        Beep();
                    }

                    if (fired && !cooling) {
                        cooling = true;
                    }

                    if (ratio == 0 && cooling) {
                        cooling = false;
                        BeepCooled();
                    }

                    if (!cooling && acc >= nextToastTime) {
                        ShowToast();
                    }

                    nextToastTime = ((int)Math.Floor(acc / (float)toastPeriod) + 1) * toastPeriod;
                };
                timer.Interval = TimeSpan.FromSeconds(timeStep);
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

            {
                var leaveTimer = new DispatcherTimer();
                leaveTimer.Interval = TimeSpan.FromSeconds(0.5);
                leaveTimer.Tick += (s, e) => {
                    var mouseScreen = Control.MousePosition;
                    var windowScreen = new System.Windows.Point(Left, Top);

                    var isOutside = !new Rect(Left, Top, Width, Height).Contains(mouseScreen.X, mouseScreen.Y);

                    if (isOutside) {
                        Opacity = 1;
                        leaveTimer.Stop();
                    }
                };

                MouseEnter += (s, e) => Opacity = 0;
                MouseLeave += (s, e) => leaveTimer.Start();
            }

            snd.Open(new Uri("notify.mp3", UriKind.Relative));
        }

        private void Window_Deactivated(object sender, EventArgs e) {
            Topmost = true;
        }

        void Beep() {
            Task.Factory.StartNew(() => {
                Console.Beep(2400, 200);
                Console.Beep(2400, 200);
                System.Threading.Thread.Sleep(400);
                Console.Beep(2400, 200);
                Console.Beep(2400, 200);
            });
        }

        void BeepCooled() {
            Task.Factory.StartNew(() => {
                Console.Beep(3000, 200);
                Console.Beep(2500, 200);
            });
        }


        void ShowToast() {
            snd.Position = TimeSpan.Zero;
            snd.Play();

            var toast = new Toast();
            toast.Left = SystemParameters.WorkArea.Right - toast.Width;
            toast.Top = 200;
            toast.Show();

            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(15);
            timer.Tick += (s, e) => toast.Close();
            timer.Start();
        }


        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);


        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }


        private void Window_Loaded(object sender, RoutedEventArgs e) {
            const int GWL_EXSTYLE = -20;
            const int WS_EX_TOOLWINDOW = 0x00000080;

            var wndHelper = new WindowInteropHelper(this);
            var exStyle = (int)GetWindowLongPtr(wndHelper.Handle, GWL_EXSTYLE) | WS_EX_TOOLWINDOW;

            SetWindowLongPtr(wndHelper.Handle, GWL_EXSTYLE, (IntPtr)exStyle);
        }
    }
}
