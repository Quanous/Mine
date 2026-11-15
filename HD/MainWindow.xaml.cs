using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HD
{
    // 화면에 보여줄 한 줄
    public class EventVM
    {
        public EventItem Item { get; set; }
        public string Name { get; set; }
        public string DateText { get; set; }
        public string Prefix { get; set; }
        public string Number { get; set; }
        public string Remain { get; set; }
        public string Initial { get; set; }
        public int Days { get; set; }
        public Brush NumBrush { get; set; }
        public ImageSource Avatar { get; set; }
        public Visibility AvatarVisibility { get; set; }
        public Visibility InitialVisibility { get; set; }
    }

    public partial class MainWindow : Window
    {
        AppData data;
        readonly string[] args;
        readonly string shotsDir;
        readonly bool demo;
        DispatcherTimer clock;
        DateTime lastDay = DateTime.Today;
        ImageSource defaultArt;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] Weekdays = { "일", "월", "화", "수", "목", "금", "토" };
        static readonly Brush AccentB = Hex("#E60012");
        static readonly Brush GoldB = Hex("#FFC247");
        static readonly Brush PastB = Hex("#6E6E79");
        static readonly Brush MutedB = Hex("#B6B6C0");
        static readonly Brush TintB = Hex("#26E60012");

        public static SolidColorBrush Hex(string hex)
        {
            SolidColorBrush b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        static ImageBrush MakeBrush(ImageSource src)
        {
            ImageBrush b = new ImageBrush(src) { Stretch = Stretch.UniformToFill };
            RenderOptions.SetBitmapScalingMode(b, BitmapScalingMode.HighQuality);
            return b;
        }

        public MainWindow(string[] args)
        {
            this.args = args ?? new string[0];
            int si = Array.IndexOf(this.args, "--shots");
            if (si >= 0 && si + 1 < this.args.Length) shotsDir = this.args[si + 1];
            demo = this.args.Contains("--demo");
            if (demo) Store.UseTempRoot();

            InitializeComponent();

            data = demo ? Demo.Build() : Store.Load();

            ImageSource logo = Img.LoadPack("Assets/logo.png", 0);
            if (logo != null)
            {
                Icon = logo;
                LogoBox.Background = MakeBrush(logo);
            }
            defaultArt = Img.LoadPack("Assets/hero.png", 0);
            if (defaultArt != null) EmptyArt.Fill = MakeBrush(defaultArt);

            RestoreWindowBounds();
            WireEvents();
            RefreshView();

            clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            clock.Tick += (s, e) => { if (DateTime.Today != lastDay) RefreshView(); };
            clock.Start();

            Loaded += (s, e) =>
            {
                FadeIn(Stage, 380);
                if (shotsDir != null) RunShots(shotsDir);
            };
        }

        // ---------- 창 위치/크기 ----------
        void RestoreWindowBounds()
        {
            // 작은 화면에서는 창이 화면 밖으로 넘치지 않게 높이를 줄임
            double maxH = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 16);
            if (Height > maxH) Height = maxH;

            if (data.ww >= MinWidth && data.wh >= MinHeight)
            {
                Width = data.ww;
                Height = Math.Min(data.wh, maxH);
                double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
                double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
                if (data.wx > vl - Width + 140 && data.wx < vl + vw - 140 && data.wy > vt - 10 && data.wy < vt + vh - 90)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = data.wx;
                    Top = data.wy;
                }
            }
            Topmost = data.pin;
            UpdatePinVisual();
        }

        void SaveWindowBounds()
        {
            if (demo) return;
            if (WindowState == WindowState.Normal)
            {
                data.wx = Left; data.wy = Top; data.ww = Width; data.wh = Height;
            }
            Store.Save(data);
        }

        void UpdatePinVisual()
        {
            BtnPin.Foreground = data.pin ? AccentB : MutedB;
            BtnPin.Background = data.pin ? TintB : Brushes.Transparent;
        }

        void TogglePin()
        {
            data.pin = !data.pin;
            Topmost = data.pin;
            UpdatePinVisual();
            if (!demo) Store.Save(data);
        }

        public void BringToFront()
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Show();
            Activate();
            Topmost = true;
            Topmost = data.pin;
        }

        // ---------- 이벤트 연결 ----------
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        void WireEvents()
        {
            BtnClose.Click += (s, e) => Close();
            BtnMin.Click += (s, e) => WindowState = WindowState.Minimized;
            BtnPin.Click += (s, e) => TogglePin();
            BtnAdd.Click += (s, e) => OpenSheet(null);
            EmptyAddBtn.Click += (s, e) => OpenSheet(null);

            TitleBar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Left) return;
                try { DragMove(); } catch { }
            };

            foreach (Rectangle r in new[] { RL, RR, RT, RB, RTL, RTR, RBL, RBR })
                r.MouseLeftButtonDown += ResizeHandle_Down;

            Closing += (s, e) => SaveWindowBounds();
            PreviewKeyDown += Window_PreviewKeyDown;

            // 최대화는 막음 (투명한 그림자 여백까지 늘어나서 어색해짐)
            StateChanged += (s, e) => { if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal; };

            WireMenu();
            WireSheet();
        }

        void ResizeHandle_Down(object sender, MouseButtonEventArgs e)
        {
            int dir = int.Parse((string)((FrameworkElement)sender).Tag, Inv);
            SendMessage(new WindowInteropHelper(this).Handle, 0x112, (IntPtr)(0xF000 + dir), IntPtr.Zero);
            e.Handled = true;
        }

        void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool menu = MenuLayer.Visibility == Visibility.Visible;
            bool confirm = ConfirmLayer.Visibility == Visibility.Visible;
            bool sheet = SheetLayer.Visibility == Visibility.Visible;

            if (e.Key == Key.Escape)
            {
                if (menu) HideMenu();
                else if (confirm) CloseConfirm();
                else if (sheet) CloseSheet();
                else return;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Keyboard.FocusedElement is TextBox)
            {
                if (sheet) { SaveSheet(); e.Handled = true; }
            }
            else if (e.Key == Key.Enter && confirm)
            {
                DoDelete();
                e.Handled = true;
            }
            else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control && !menu && !confirm && !sheet)
            {
                OpenSheet(null);
                e.Handled = true;
            }
        }

        // ---------- 날짜 계산/표시 ----------
        static bool TryDate(string s, out DateTime dt)
        {
            return DateTime.TryParseExact(s, "yyyy-MM-dd", Inv, DateTimeStyles.None, out dt);
        }

        static string FormatDate(DateTime dt)
        {
            return dt.ToString("yyyy.MM.dd", Inv) + " (" + Weekdays[(int)dt.DayOfWeek] + ")";
        }

        static string FirstChar(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "D";
            return StringInfo.GetNextTextElement(name.Trim()).ToUpperInvariant();
        }

        EventVM MakeVM(EventItem it, DateTime dt, DateTime today)
        {
            int days = (int)(dt.Date - today).TotalDays;
            EventVM vm = new EventVM { Item = it, Name = it.name, Days = days, DateText = FormatDate(dt) };
            if (days > 0)
            {
                vm.Prefix = "D-"; vm.Number = days.ToString("N0", Inv);
                vm.Remain = days.ToString("N0", Inv) + "일 남음"; vm.NumBrush = AccentB;
            }
            else if (days == 0)
            {
                vm.Prefix = ""; vm.Number = "D-DAY";
                vm.Remain = "바로 오늘이에요"; vm.NumBrush = GoldB;
            }
            else
            {
                int a = -days;
                vm.Prefix = "D+"; vm.Number = a.ToString("N0", Inv);
                vm.Remain = a.ToString("N0", Inv) + "일 지남"; vm.NumBrush = PastB;
            }
            vm.Initial = FirstChar(it.name);
            ImageSource av = Img.LoadStored(it.image, 168);
            vm.Avatar = av;
            vm.AvatarVisibility = av != null ? Visibility.Visible : Visibility.Collapsed;
            vm.InitialVisibility = av != null ? Visibility.Collapsed : Visibility.Visible;
            return vm;
        }

        // ---------- 화면 갱신 ----------
        void RefreshView()
        {
            DateTime today = DateTime.Today;
            lastDay = today;

            List<EventVM> all = new List<EventVM>();
            foreach (EventItem it in data.events)
            {
                DateTime dt;
                if (!TryDate(it.date, out dt)) continue;
                all.Add(MakeVM(it, dt, today));
            }

            List<EventVM> up = all.Where(v => v.Days >= 0).OrderBy(v => v.Days).ThenBy(v => v.Name).ToList();
            List<EventVM> past = all.Where(v => v.Days < 0).OrderByDescending(v => v.Days).ThenBy(v => v.Name).ToList();

            EventVM hero = null;
            if (up.Count > 0) { hero = up[0]; up.RemoveAt(0); }
            else if (past.Count > 0) { hero = past[0]; past.RemoveAt(0); }

            bool empty = hero == null;
            EmptyPanel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            HeroHost.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            if (hero != null) ShowHero(hero);

            UpcomingList.ItemsSource = up;
            UpcomingPanel.Visibility = up.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpcomingLabel.Text = "다가오는 일정  ·  " + up.Count;

            PastList.ItemsSource = past;
            PastPanel.Visibility = past.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            PastLabel.Text = "지난 일정  ·  " + past.Count;
        }

        void ShowHero(EventVM v)
        {
            HeroHost.DataContext = v;
            HeroName.Text = v.Name;
            HeroChip.Text = v.Days > 0 ? "다가오는 디데이" : v.Days == 0 ? "오늘이에요!" : "지난 디데이";
            HeroPrefix.Text = v.Prefix;
            HeroNum.Text = v.Number;
            HeroNum.FontSize = v.Days == 0 ? 58 : (v.Number.Length > 5 ? 62 : 90);
            HeroNumber.Foreground = v.Days < 0 ? Hex("#B4B4BF") : v.NumBrush;
            HeroDate.Text = v.DateText + "  ·  " + v.Remain;

            ImageSource big = Img.LoadStored(v.Item.image, 1100);
            if (big != null)
            {
                HeroBg.Background = MakeBrush(big);
            }
            else if (defaultArt != null)
            {
                // 기본 아트: 좌우 반전해서 얼굴이 오른쪽(글자 반대편)에 오도록
                ImageBrush b = MakeBrush(defaultArt);
                b.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
                b.Viewbox = new Rect(0.04, 0.20, 0.92, 0.54);
                b.RelativeTransform = new ScaleTransform(-1, 1, 0.5, 0.5);
                HeroBg.Background = b;
            }
        }

        // ---------- 애니메이션 도우미 ----------
        static void FadeIn(UIElement el, int ms)
        {
            DoubleAnimation a = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            el.BeginAnimation(OpacityProperty, a);
        }

        static void SlideUp(TranslateTransform t, int ms)
        {
            DoubleAnimation a = new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            t.BeginAnimation(TranslateTransform.YProperty, a);
        }
    }
}
