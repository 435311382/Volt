using System;
using System.Threading;
using System.Windows.Forms;

namespace Volt
{
    public static class Program
    {
        private const string MutexName = "Volt.SingleInstance.v1";
        private const string ShowEventName = "Volt.ShowEvent.v1";

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool showOnStart = false;
            foreach (string a in args)
            {
                if (string.Equals(a, "--show", StringComparison.OrdinalIgnoreCase)) showOnStart = true;
            }

            bool created;
            using (Mutex mutex = new Mutex(true, MutexName, out created))
            {
                if (!created)
                {
                    // 已经有一份在跑：叫它把浮窗弹出来，然后自己退出。
                    // 这就是「再次双击快捷方式没反应」的修复。
                    try
                    {
                        using (EventWaitHandle ev = EventWaitHandle.OpenExisting(ShowEventName))
                        {
                            ev.Set();
                        }
                    }
                    catch { }
                    return;
                }

                using (EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
                {
                    MainForm form = new MainForm();
                    form.ShowOnStart = showOnStart;
                    form.AttachShowEvent(showEvent);
                    Application.Run(form);
                }
            }
        }
    }
}
