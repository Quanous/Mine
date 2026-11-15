using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HD
{
    // 디데이 추가/수정 시트 + 삭제 확인
    public partial class MainWindow
    {
        EventItem editing;          // null 이면 새로 추가
        string sheetImage;          // 지금 붙어 있는 저장된 사진 파일명
        string sheetNewPath;        // 방금 고른(아직 저장 전) 사진 경로
        EventItem confirming;

        static readonly Brush ErrB = Hex("#FF6B78");
        const string PhotoFilter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.tif;*.tiff;*.ico|모든 파일|*.*";

        void WireSheet()
        {
            SheetBackdrop.MouseLeftButtonDown += (s, e) => CloseSheet();
            BtnCancel.Click += (s, e) => CloseSheet();
            BtnSave.Click += (s, e) => SaveSheet();
            BtnToday.Click += (s, e) => SetDate(DateTime.Today);
            BtnRemovePic.Click += (s, e) => { sheetImage = null; sheetNewPath = null; UpdatePic(); };

            PicBox.MouseLeftButtonUp += (s, e) => PickImageForSheet();
            PicBox.DragOver += (s, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            PicBox.Drop += Pic_Drop;

            foreach (TextBox tb in new[] { YearBox, MonthBox, DayBox })
            {
                tb.PreviewTextInput += (s, e) => e.Handled = !e.Text.All(char.IsDigit);
                tb.PreviewMouseWheel += (s, e) => { Step((TextBox)s, e.Delta > 0 ? 1 : -1); e.Handled = true; };
                tb.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Up) { Step((TextBox)s, 1); e.Handled = true; }
                    else if (e.Key == Key.Down) { Step((TextBox)s, -1); e.Handled = true; }
                };
                tb.TextChanged += (s, e) => UpdatePreview();
            }

            ConfirmBackdrop.MouseLeftButtonDown += (s, e) => CloseConfirm();
            ConfirmCancel.Click += (s, e) => CloseConfirm();
            ConfirmOk.Click += (s, e) => DoDelete();
        }

        // ---------- 날짜 입력 ----------
        void SetDate(DateTime dt)
        {
            YearBox.Text = dt.Year.ToString(Inv);
            MonthBox.Text = dt.Month.ToString("00", Inv);
            DayBox.Text = dt.Day.ToString("00", Inv);
        }

        void Step(TextBox tb, int delta)
        {
            int v;
            bool isYear = tb == YearBox;
            if (!int.TryParse(tb.Text, out v)) v = isYear ? DateTime.Today.Year : 1;
            int min = isYear ? 1900 : 1;
            int max = isYear ? 2999 : (tb == MonthBox ? 12 : 31);
            v = Math.Max(min, Math.Min(max, v + delta));
            tb.Text = isYear ? v.ToString(Inv) : v.ToString("00", Inv);
            tb.CaretIndex = tb.Text.Length;
        }

        bool ReadDate(out DateTime dt)
        {
            dt = DateTime.Today;
            int y, m, d;
            if (!int.TryParse(YearBox.Text, out y) || !int.TryParse(MonthBox.Text, out m) || !int.TryParse(DayBox.Text, out d))
                return false;
            if (y < 1900 || y > 2999 || m < 1 || m > 12 || d < 1) return false;
            if (d > DateTime.DaysInMonth(y, m)) return false;
            dt = new DateTime(y, m, d);
            return true;
        }

        void UpdatePreview()
        {
            DateTime dt;
            if (!ReadDate(out dt))
            {
                PreviewText.Text = "올바른 날짜를 입력해 주세요";
                PreviewText.Foreground = ErrB;
                return;
            }
            int days = (int)(dt - DateTime.Today).TotalDays;
            string label = days > 0 ? "D-" + days.ToString("N0", Inv) : days == 0 ? "D-DAY" : "D+" + (-days).ToString("N0", Inv);
            PreviewText.Text = label + "    ·    " + FormatDate(dt);
            PreviewText.Foreground = days > 0 ? AccentB : days == 0 ? GoldB : MutedB;
        }

        // ---------- 사진 ----------
        void UpdatePic()
        {
            ImageSource src = null;
            if (sheetNewPath != null) src = Img.LoadFile(sheetNewPath, 340);
            else if (!string.IsNullOrEmpty(sheetImage)) src = Img.LoadStored(sheetImage, 340);

            bool has = src != null;
            PicImage.Fill = has ? MakeBrush(src) : null;
            PicImage.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            PicHint.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
            PicRing.StrokeDashArray = has ? new DoubleCollection() : new DoubleCollection { 3, 3 };
            PicRing.Stroke = has ? Hex("#40FFFFFF") : Hex("#55FFFFFF");
            BtnRemovePic.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            PicCaption.Text = has ? "사진을 바꾸려면 클릭" : "사진 선택 (클릭하거나 끌어다 놓기)";
            PicCaption.Foreground = Hex("#7A7A86");
        }

        void SetPickedImage(string path)
        {
            if (Img.LoadFile(path, 64) == null)
            {
                PicCaption.Text = "이 파일은 사진으로 열 수 없어요";
                PicCaption.Foreground = ErrB;
                return;
            }
            sheetNewPath = path;
            UpdatePic();
        }

        void PickImageForSheet()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog { Title = "사진 선택", Filter = PhotoFilter };
            if (dlg.ShowDialog(this) == true) SetPickedImage(dlg.FileName);
        }

        void Pic_Drop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) SetPickedImage(files[0]);
            e.Handled = true;
        }

        // ---------- 시트 열기/저장/닫기 ----------
        void OpenSheet(EventItem ev)
        {
            HideMenu();
            editing = ev;
            sheetImage = ev != null ? ev.image : null;
            sheetNewPath = null;

            SheetTitle.Text = ev == null ? "디데이 추가" : "디데이 수정";
            BtnSave.Content = ev == null ? "추가" : "저장";
            NameBox.Text = ev != null ? ev.name : "";

            DateTime dt = DateTime.Today, tmp;
            if (ev != null && TryDate(ev.date, out tmp)) dt = tmp;
            SetDate(dt);
            UpdatePic();
            UpdatePreview();

            SheetLayer.Visibility = Visibility.Visible;
            FadeIn(SheetLayer, 170);
            SlideUp(SheetShift, 230);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Keyboard.Focus(NameBox);
                NameBox.CaretIndex = NameBox.Text.Length;
            }), DispatcherPriority.Input);
        }

        void CloseSheet()
        {
            SheetLayer.Visibility = Visibility.Collapsed;
            editing = null;
        }

        void SaveSheet()
        {
            string name = NameBox.Text.Trim();
            DateTime dt;
            if (name.Length == 0) { Keyboard.Focus(NameBox); Shake(SheetShift); return; }
            if (!ReadDate(out dt)) { Shake(SheetShift); return; }

            string img = sheetImage;
            string oldImg = editing != null ? editing.image : null;
            if (sheetNewPath != null)
            {
                string stored = Img.Import(sheetNewPath);
                if (stored != null) img = stored;
            }
            string date = dt.ToString("yyyy-MM-dd", Inv);

            if (editing == null)
                data.events.Add(new EventItem { name = name, date = date, image = img });
            else
            {
                editing.name = name; editing.date = date; editing.image = img;
            }
            if (!string.IsNullOrEmpty(oldImg) && oldImg != img) Img.Delete(oldImg);

            Store.Save(data);
            RefreshView();
            CloseSheet();
        }

        static void Shake(TranslateTransform t)
        {
            DoubleAnimationUsingKeyFrames a = new DoubleAnimationUsingKeyFrames();
            double[] xs = { 0, -9, 9, -6, 6, -3, 0 };
            for (int i = 0; i < xs.Length; i++)
                a.KeyFrames.Add(new LinearDoubleKeyFrame(xs[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * 45))));
            t.BeginAnimation(TranslateTransform.XProperty, a);
        }

        // ---------- 삭제 확인 ----------
        void OpenConfirm(EventItem it)
        {
            HideMenu();
            confirming = it;
            ConfirmMsg.Text = "'" + it.name + "' 디데이를 삭제해요.\n삭제하면 되돌릴 수 없어요.";
            ConfirmLayer.Visibility = Visibility.Visible;
            FadeIn(ConfirmLayer, 150);
            SlideUp(ConfirmShift, 210);
        }

        void CloseConfirm()
        {
            ConfirmLayer.Visibility = Visibility.Collapsed;
            confirming = null;
        }

        void DoDelete()
        {
            if (confirming == null) return;
            EventItem it = confirming;
            data.events.Remove(it);
            Img.Delete(it.image);
            Store.Save(data);
            CloseConfirm();
            RefreshView();
        }
    }
}
