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
        private static uint update_period = 2500;                 // Update period in ms
        private static uint save_period = 1000 * 5;               // Save & trim period in ms
        private static uint life_time = 1000 * 60 * 60 * 24;      // Storage time in ms
        private static bool debug = true;                         // Put runtime information into stdout

        private const string SYSTEM_IDLE_PROCESS_NAME = "Idle Process";

        private static RuntimeStorage storage = new RuntimeStorage ();

        private static string GenerateReport(RuntimeStorage seq) =>
             string.Join("\n---\n", seq.Select(x =>
               string.Format(
                   "{0}\n{1}",
                   $"{x.Key} - {x.Value.msElapsed}ms - {x.Value.msElapsed / 1000 / 60}m",
                   string.Join("\n", x.Value.titles.Select(t => $"    {t}"))
               )
            ));

        public static uint ParseString2uint(string s, uint @default) =>  uint.TryParse(s, out uint result) ? result : @default;

        private static void SaveReport(string data) => File.WriteAllText(string.Format("{0}/wmonitord_activity_{1}.log",
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), DateTime.Today.ToString("yyyy_MM_dd")), data);

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
            if (args.Length >= 1 && args[0]=="d")
            {
                debug = true;
            }
            if (args.Length >= 2)
            {/*ms*/
                update_period = ParseString2uint(args[1], update_period);
            }
            if (args.Length >= 3)
            { /*ms*/
                save_period = ParseString2uint(args[2], save_period); 
            }
            if (args.Length >= 4)
            { /*h*/
                life_time = ParseString2uint(args[3], 24) * 60 * 60 * 1000;
            }

            if (!debug)
            {
                Wutils.HideConsoleWindow();
            }

            static void exeptionCallback(Exception e)
            {
                if (debug)
                    Debug(e.Message);
            }

            new System.Threading.Timer(CreateLock ( (e) =>
            {
                var (title, filename, pid) = Wutils.GetCurrentActiveWindowData();                

                if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(title))
                {
                    filename = SYSTEM_IDLE_PROCESS_NAME;
                }

                if (!storage.ContainsKey(filename))
                {
                    storage[filename] = new WindowActivityInformation();
                }

                DateTimeOffset now = DateTimeOffset.Now;

                storage[filename].titles.Add(title);
                storage[filename].msElapsed += update_period;
                storage[filename].lastTimeUpdated = now.ToUnixTimeMilliseconds();

                if (debug)
                {
                    Debug($"{now:T} -- {filename} -> {storage[filename].msElapsed}ms");
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
