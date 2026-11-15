using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HD
{
    // 앱 안에서 직접 그리는 우클릭 메뉴 (윈도우 기본 메뉴 대신 → 디자인 통일)
    public partial class MainWindow
    {
        class MenuEntry
        {
            public string Text;
            public string Icon;
            public Action Action;
            public bool Danger;
            public bool Checked;
            public bool Sep;
        }

        const string IcoEdit = "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z";
        const string IcoDelete = "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z";
        const string IcoImage = "M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z";
        const string IcoAdd = "M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z";
        const string IcoPin = "M16,9V4l1,0c0.55,0,1-0.45,1-1v0c0-0.55-0.45-1-1-1H7C6.45,2,6,2.45,6,3v0c0,0.55,0.45,1,1,1l1,0v5c0,1.66-1.34,3-3,3h0v2h5.97v7l1,1l1-1v-7H19v-2h0C17.34,12,16,10.66,16,9z";
        const string IcoPower = "M13 3h-2v10h2V3zm4.83 2.17l-1.42 1.42C17.99 7.86 19 9.81 19 12c0 3.87-3.13 7-7 7s-7-3.13-7-7c0-2.19 1.01-4.14 2.58-5.42L6.17 5.17C4.23 6.82 3 9.26 3 12c0 4.97 4.03 9 9 9s9-4.03 9-9c0-2.74-1.23-5.18-3.17-6.83z";
        const string IcoCheck = "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z";
        const string IcoNoImage = "M19 5v11.17l2 2V5c0-1.1-.9-2-2-2H5.83l2 2H19zM2.81 2.81L1.39 4.22 3 5.83V19c0 1.1.9 2 2 2h13.17l1.61 1.61 1.41-1.41L2.81 2.81zM5 19V7.83l6.37 6.37-.87 1.3-2-2.5L5 19z";

        void WireMenu()
        {
            MenuLayer.MouseDown += (s, e) => { if (ReferenceEquals(e.OriginalSource, MenuLayer)) HideMenu(); };
            Scroller.PreviewMouseRightButtonUp += Scroller_RightUp;
            Scroller.PreviewMouseLeftButtonDown += Scroller_LeftDown;
            TitleBar.MouseRightButtonUp += (s, e) => { ShowBgMenu(e.GetPosition(Stage)); e.Handled = true; };
        }

        // ---------- 어느 카드 위에서 눌렀는지 찾기 ----------
        static DependencyObject ParentOf(DependencyObject o)
        {
            if (o is Visual || o is System.Windows.Media.Media3D.Visual3D) return VisualTreeHelper.GetParent(o);
            return LogicalTreeHelper.GetParent(o);
        }

        static EventVM FindVM(DependencyObject o)
        {
            while (o != null)
            {
                FrameworkElement fe = o as FrameworkElement;
                if (fe != null && fe.DataContext is EventVM) return (EventVM)fe.DataContext;
                o = ParentOf(o);
            }
            return null;
        }

        void Scroller_RightUp(object sender, MouseButtonEventArgs e)
        {
            EventVM vm = FindVM(e.OriginalSource as DependencyObject);
            Point p = e.GetPosition(Stage);
            if (vm != null) ShowCardMenu(vm, p); else ShowBgMenu(p);
            e.Handled = true;
        }

        void Scroller_LeftDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            EventVM vm = FindVM(e.OriginalSource as DependencyObject);
            if (vm == null) return;
            OpenSheet(vm.Item);
            e.Handled = true;
        }

        // ---------- 메뉴 내용 ----------
        void ShowCardMenu(EventVM vm, Point p)
        {
            EventItem it = vm.Item;
            List<MenuEntry> list = new List<MenuEntry>();
            list.Add(new MenuEntry { Text = "수정", Icon = IcoEdit, Action = () => OpenSheet(it) });
            list.Add(new MenuEntry { Text = "사진 변경", Icon = IcoImage, Action = () => ChangeImage(it) });
            if (!string.IsNullOrEmpty(it.image))
                list.Add(new MenuEntry { Text = "사진 제거", Icon = IcoNoImage, Action = () => RemoveImage(it) });
            list.Add(new MenuEntry { Sep = true });
            list.Add(new MenuEntry { Text = "삭제", Icon = IcoDelete, Danger = true, Action = () => OpenConfirm(it) });
            ShowMenu(p, list);
        }

        void ShowBgMenu(Point p)
        {
            List<MenuEntry> list = new List<MenuEntry>();
            list.Add(new MenuEntry { Text = "디데이 추가", Icon = IcoAdd, Action = () => OpenSheet(null) });
            list.Add(new MenuEntry { Text = "항상 위에 표시", Icon = IcoPin, Checked = data.pin, Action = TogglePin });
            list.Add(new MenuEntry { Sep = true });
            list.Add(new MenuEntry { Text = "HD 종료", Icon = IcoPower, Action = Close });
            ShowMenu(p, list);
        }

        void ChangeImage(EventItem it)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog { Title = "사진 선택", Filter = PhotoFilter };
            if (dlg.ShowDialog(this) != true) return;
            string stored = Img.Import(dlg.FileName);
            if (stored == null) return;
            string old = it.image;
            it.image = stored;
            if (!string.IsNullOrEmpty(old)) Img.Delete(old);
            Store.Save(data);
            RefreshView();
        }

        void RemoveImage(EventItem it)
        {
            string old = it.image;
            it.image = "";
            Img.Delete(old);
            Store.Save(data);
            RefreshView();
        }

        // ---------- 메뉴 그리기 ----------
        void ShowMenu(Point p, List<MenuEntry> entries)
        {
            MenuItems.Children.Clear();
            foreach (MenuEntry m in entries)
            {
                if (m.Sep)
                {
                    MenuItems.Children.Add(new Border { Height = 1, Background = Hex("#26FFFFFF"), Margin = new Thickness(8, 5, 8, 5) });
                    continue;
                }
                MenuItems.Children.Add(BuildMenuItem(m));
            }

            MenuLayer.Visibility = Visibility.Visible;
            MenuCard.Margin = new Thickness(0);
            MenuCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size sz = MenuCard.DesiredSize;
            double x = Math.Max(10, Math.Min(p.X, Stage.ActualWidth - sz.Width - 10));
            double y = Math.Max(10, Math.Min(p.Y, Stage.ActualHeight - sz.Height - 10));
            MenuCard.Margin = new Thickness(x, y, 0, 0);
            FadeIn(MenuCard, 120);
        }

        void HideMenu()
        {
            MenuLayer.Visibility = Visibility.Collapsed;
        }

        UIElement BuildMenuItem(MenuEntry m)
        {
            Brush fg = m.Danger ? Hex("#FF5A68") : Hex("#ECECF1");

            Border row = new Border
            {
                CornerRadius = new CornerRadius(9),
                Height = 40,
                Padding = new Thickness(11, 0, 12, 0),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Path ico = new Path
            {
                Data = Geometry.Parse(m.Icon),
                Fill = fg,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock tb = new TextBlock
            {
                Text = m.Text,
                FontSize = 14.5,
                Foreground = fg,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 18, 0)
            };
            Grid.SetColumn(tb, 1);
            g.Children.Add(ico);
            g.Children.Add(tb);

            if (m.Checked)
            {
                Path chk = new Path
                {
                    Data = Geometry.Parse(IcoCheck),
                    Fill = AccentB,
                    Width = 14,
                    Height = 14,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(chk, 2);
                g.Children.Add(chk);
            }

            row.Child = g;
            row.MouseEnter += (s, e) => row.Background = Hex("#2AFFFFFF");
            row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (s, e) =>
            {
                HideMenu();
                if (m.Action != null) m.Action();
                e.Handled = true;
            };
            return row;
        }
    }
}
