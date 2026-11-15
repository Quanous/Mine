using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HD
{
    // 개발용: "HD.exe --demo --shots 폴더" 로 실행하면 화면을 PNG 로 저장하고 종료 (빌드 서버에서 화면 확인용)
    public partial class MainWindow
    {
        async void RunShots(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                WriteEnv(dir);
                await Task.Delay(1300);
                Capture(dir, "01-main");

                var backup = data.events.ToList();
                EventItem withImg = data.events.FirstOrDefault(e => !string.IsNullOrEmpty(e.image)) ?? data.events[0];

                // 새 디데이 시트 (빈 상태 / 채운 상태)
                OpenSheet(null);
                await Task.Delay(500);
                Capture(dir, "02-add-empty");
                NameBox.Text = "100일 기념일";
                SetDate(DateTime.Today.AddDays(37));
                if (Demo.PhotoPath != null) SetPickedImage(Demo.PhotoPath);
                await Task.Delay(300);
                Capture(dir, "03-add-filled");
                CloseSheet();

                // 수정 시트
                OpenSheet(withImg);
                await Task.Delay(500);
                Capture(dir, "04-edit");
                CloseSheet();

                // 카드 메뉴 / 배경 메뉴 / 삭제 확인
                ShowCardMenu(new EventVM { Item = withImg }, new Point(190, 430));
                await Task.Delay(350);
                Capture(dir, "05-menu-card");
                HideMenu();
                ShowBgMenu(new Point(150, 600));
                await Task.Delay(350);
                Capture(dir, "06-menu-bg");
                HideMenu();
                OpenConfirm(withImg);
                await Task.Delay(450);
                Capture(dir, "07-confirm");
                CloseConfirm();

                // 오늘이 디데이일 때
                data.events.Add(new EventItem { name = "오늘의 약속", date = DateTime.Today.ToString("yyyy-MM-dd"), image = "" });
                RefreshView();
                await Task.Delay(400);
                Capture(dir, "08-today");
                data.events = backup.ToList();

                // 지난 일정만 있을 때
                data.events = backup.Where(e => TryPast(e)).ToList();
                RefreshView();
                await Task.Delay(400);
                Capture(dir, "09-past-only");

                // 좁은 창
                data.events = backup.ToList();
                RefreshView();
                Width = MinWidth;
                Height = 640;
                await Task.Delay(500);
                Capture(dir, "10-narrow");

                // 비어 있을 때
                data.events = new System.Collections.Generic.List<EventItem>();
                RefreshView();
                await Task.Delay(400);
                Capture(dir, "11-empty");
            }
            catch (Exception ex)
            {
                File.WriteAllText(System.IO.Path.Combine(dir, "shots-error.txt"), ex.ToString());
            }
            finally
            {
                Application.Current.Shutdown();
            }
        }

        static bool TryPast(EventItem e)
        {
            DateTime dt;
            return TryDate(e.date, out dt) && dt.Date < DateTime.Today;
        }

        void Capture(string dir, string name)
        {
            Root.UpdateLayout();
            double w = Root.ActualWidth, h = Root.ActualHeight, scale = 2.0;
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)Math.Ceiling(w * scale), (int)Math.Ceiling(h * scale),
                96 * scale, 96 * scale, PixelFormats.Pbgra32);

            DrawingVisual bg = new DrawingVisual();
            using (DrawingContext dc = bg.RenderOpen())
                dc.DrawRectangle(Hex("#3A3A42"), null, new Rect(0, 0, w, h));
            rtb.Render(bg);
            rtb.Render(Root);

            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(System.IO.Path.Combine(dir, name + ".png"))) enc.Save(fs);
        }

        void WriteEnv(string dir)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string fam in new[] { "Segoe UI Variable Text", "Segoe UI", "Malgun Gothic", "Bahnschrift" })
                sb.AppendLine(fam + ": " + Fonts.SystemFontFamilies.Any(f => f.Source == fam));
            sb.AppendLine("OS: " + Environment.OSVersion + "   CLR: " + Environment.Version);
            sb.AppendLine("DPI scale: " + VisualTreeHelper.GetDpi(this).DpiScaleX);
            sb.AppendLine("Window: " + ActualWidth + " x " + ActualHeight);
            File.WriteAllText(System.IO.Path.Combine(dir, "env.txt"), sb.ToString());
        }
    }

    // 데모용 샘플 데이터
    public static class Demo
    {
        public static string PhotoPath;

        public static AppData Build()
        {
            DateTime t = DateTime.Today;
            string photo = null;
            try
            {
                PhotoPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hd_demo_photo.png");
                BitmapSource src = Img.LoadPack("Assets/hero.png", 0);
                PngBitmapEncoder enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(src));
                using (FileStream fs = File.Create(PhotoPath)) enc.Save(fs);
                photo = Img.Import(PhotoPath);
            }
            catch (Exception ex) { Store.LogError(ex); }

            AppData d = new AppData();
            Func<int, string> day = n => t.AddDays(n).ToString("yyyy-MM-dd");
            d.events.Add(new EventItem { name = "토익 시험", date = day(14), image = "" });
            d.events.Add(new EventItem { name = "크리스마스", date = day(40), image = photo });
            d.events.Add(new EventItem { name = "제주도 여름 휴가", date = day(112), image = "" });
            d.events.Add(new EventItem { name = "엄마 생일", date = day(203), image = photo });
            d.events.Add(new EventItem { name = "대학 입학", date = day(-412), image = "" });
            d.events.Add(new EventItem { name = "우리의 첫 만남", date = day(-128), image = photo });
            return d;
        }
    }
}
