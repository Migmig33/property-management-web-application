using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web;
using static WebApplication1.Services.NotificationChecker;

namespace WebApplication1.Services
{
    public class SmsScheduler
    {
        // ============================================================
        // TESTING:    TimeSpan.FromSeconds(15)
        // PRODUCTION: TimeSpan.FromHours(24)
        // ============================================================
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

        // Wait a moment after app start so the first request isn't competing
        // with the scan for the connection pool.
        private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(10);

        // false = actually send. Set true to log "Preview" rows only.
        private const bool DRY_RUN = false;

        private static Timer _timer;
        private static int _running;   // 0 = idle, 1 = a scan is in progress

        public static DateTime? LastRunAt { get; private set; }
        public static string LastRunSummary { get; private set; }

        public static void Start()
        {
            if (_timer != null) return;   // guard against a double start

            _timer = new Timer(Tick, null, StartDelay, Interval);
            Debug.WriteLine("[SmsScheduler] started, interval = " + Interval);
        }

        public static void Stop()
        {
            if (_timer == null) return;

            _timer.Dispose();
            _timer = null;
            Debug.WriteLine("[SmsScheduler] stopped");
        }

        private static void Tick(object state)
        {
            // If the previous scan is still going, skip this tick rather than
            // stacking overlapping runs.
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                Debug.WriteLine("[SmsScheduler] previous run still going, skipping");
                return;
            }

            try
            {
                var run = NotificationScanner.Run(DRY_RUN);

                LastRunAt = DateTime.Now;
                LastRunSummary =
                    "leases=" + run.expiringLeases +
                    " payments=" + run.duePayments +
                    " messages=" + run.results.Count;

                Debug.WriteLine("[SmsScheduler] " + LastRunSummary);

                foreach (var r in run.results)
                {
                    Debug.WriteLine("  " + r.status + " - " + r.tenant +
                                    " (" + r.type + ") " + (r.error ?? ""));
                }
            }
            catch (Exception ex)
            {
                // Never let an exception escape — it would kill the timer
                // and take the whole app pool down with it.
                LastRunSummary = "ERROR: " + ex.Message;
                Debug.WriteLine("[SmsScheduler] " + LastRunSummary);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }



    }
}