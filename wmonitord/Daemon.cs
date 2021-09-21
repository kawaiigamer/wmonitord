using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace wmonitord
{
    using RuntimeStorage = Dictionary<string, WindowActivityInformation>;

    class Daemon
    {
        private static long update_period = 2500;                  // Update period in ms
        private static long save_period = 1000 * 60;               // Save & trim period in ms
        private static long life_time = 1000 * 60 * 60 * 24;       // Storage time in ms
        private static bool debug = false;                         // Put runtime information into stdout
        private static long debug_display_period = 1000 * 60 * 5;  // Put runtime information for last period in ms

        private static RuntimeStorage storage = new RuntimeStorage();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private static string GenerateReport(RuntimeStorage seq) =>
             string.Join("\n---\n", seq.Select(x =>
               string.Format(
                   "{0}\n{1}",
                   $"{x.Key} - {x.Value.msElapsed / 1000 / 60 / 60}h",
                   string.Join("\n", x.Value.titles.Select(t => $"    {t}"))
               )
            ));

        private static void SaveReport(string data) => File.WriteAllText(string.Format("{0}/{1}.log",
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), DateTime.Today.ToString("MM.dd")), data);

        private static void Debug(string msg) => Console.WriteLine(msg);

        private static RuntimeStorage TrimStorage(RuntimeStorage seq, long deprecatePeriod)
        {
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return seq.Where(e => (now - e.Value.lastTimeUpdated) < deprecatePeriod)
                     .ToDictionary(e => e.Key, e => e.Value);
        }

        private static TimerCallback CreateLock(Action<object?> f, object l, Action<Exception> exeptionCallback, int timeout = 1000) => (e) =>
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(l, timeout, ref lockTaken);
                if (lockTaken)
                {
                    f(e);
                }
                else
                {
                    return;
                }
            }
            catch (Exception exp)
            {
                exeptionCallback(exp);
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(l);
                }
            }
        };

        static async Task Main(string[] args)
        {
            const int SW_HIDE = 0;
            IntPtr handle = GetConsoleWindow();
            if (!debug)
            {
               ShowWindow(handle, SW_HIDE);
            }

            Action<Exception> exeptionCallback = (e) =>
            {
                if (debug)
                    Debug(e.Message);
            };

            new System.Threading.Timer(CreateLock ( (e) =>
            {
                var current = Wutils.GetCurrentActiveWindowData();                

                if (string.IsNullOrEmpty(current.filename) || string.IsNullOrEmpty(current.title))
                {
                    return;
                }

                if (!storage.ContainsKey(current.filename))
                {
                    storage[current.filename] = new WindowActivityInformation();
                }

                long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                storage[current.filename].titles.Add(current.title);
                storage[current.filename].msElapsed += update_period;
                storage[current.filename].lastTimeUpdated = now;

                if (debug)
                {
                    Debug(GenerateReport(TrimStorage(storage, debug_display_period)));
                }
                
            }, storage, exeptionCallback), null, 0, update_period);

            new System.Threading.Timer(CreateLock( (e) =>
            {
                SaveReport(GenerateReport(storage));
                storage = TrimStorage(storage, life_time);
            }, storage, exeptionCallback), null, 0, save_period);

            await Task.Delay(Timeout.Infinite, new CancellationToken()).ConfigureAwait(false);
        }
    }
}
