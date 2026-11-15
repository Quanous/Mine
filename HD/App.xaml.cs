using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace HD
{
    public partial class App : Application
    {
        static Mutex mutex;
        const string MutexName = "Local\\HD_DDay_App_Single";
        const string ActivateName = "Local\\HD_DDay_App_Activate";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, ex) =>
            {
                Store.LogError(ex.Exception);
                ex.Handled = true;
            };

            string[] args = e.Args ?? new string[0];
            bool demo = args.Contains("--demo");

            EventWaitHandle activate = null;
            if (!demo)
            {
                bool created;
                mutex = new Mutex(true, MutexName, out created);
                if (!created)
                {
                    // 이미 실행 중이면 그 창을 앞으로 가져오고 종료
                    try { EventWaitHandle.OpenExisting(ActivateName).Set(); } catch { }
                    Shutdown();
                    return;
                }
                activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateName);
            }

            MainWindow win = new MainWindow(args);
            MainWindow = win;
            win.Show();

            if (activate != null)
            {
                Thread t = new Thread(() =>
                {
                    while (true)
                    {
                        activate.WaitOne();
                        win.Dispatcher.BeginInvoke(new Action(win.BringToFront));
                    }
                });
                t.IsBackground = true;
                t.Start();
            }
        }
    }
}
