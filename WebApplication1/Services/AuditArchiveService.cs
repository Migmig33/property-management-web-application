using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using WebApplication1.Helper;
using WebApplication1.Models.Context;

namespace WebApplication1.Services
{
    public static class AuditArchiveService
    {
        public static DateTime? LastRunAt { get; private set; }
        public static string LastRunSummary { get; private set; }

        private const int BatchSize = 5000;
        private static readonly object RunLock = new object();

        // Reads AuditRetentionYears from web.config, defaults to 1
        private static int RetentionYears
        {
            get
            {
                int years;
                string raw = ConfigurationManager.AppSettings["AuditRetentionYears"];
                return int.TryParse(raw, out years) && years > 0 ? years : 1;
            }
        }

        // Marks audit entries older than the retention period as archived.
        // dryRun = true only counts what WOULD be archived.
        public static int Run(bool dryRun = false)
        {
            // Prevents two runs from overlapping inside the same app process
            if (!Monitor.TryEnter(RunLock))
            {
                LastRunSummary = "Skipped: an archive run is already in progress.";
                return 0;
            }

            try
            {
                var watch = Stopwatch.StartNew();
                int years = RetentionYears;
                DateTime cutoff = DateTime.Now.AddYears(-years);
                int archived = 0;

                if (dryRun)
                {
                    using (var db = new DB_Context())
                    {
                        archived = db.audit_log.Count(x => x.is_archived == 0 && x.createdAt < cutoff);
                    }
                }
                else
                {
                    // One short context per batch keeps memory use and locks small
                    while (true)
                    {
                        using (var db = new DB_Context())
                        {
                            var batch = db.audit_log
                                .Where(x => x.is_archived == 0 && x.createdAt < cutoff)
                                .OrderBy(x => x.logId)
                                .Take(BatchSize)
                                .ToList();

                            if (batch.Count == 0) break;

                            foreach (var row in batch)
                                row.is_archived = 1;   // the only column this job changes

                            db.SaveChanges();
                            archived += batch.Count;

                            if (batch.Count < BatchSize) break;
                        }
                    }
                }

                watch.Stop();
                LastRunAt = DateTime.Now;
                LastRunSummary = (dryRun ? "[dry run] " : "") + archived +
                                 " audit entries older than " + years +
                                 " year(s), took " + watch.ElapsedMilliseconds + " ms";

                if (!dryRun && archived > 0)
                    AuditLogger.Log("settings", "info",
                        "Archived " + archived + " audit log entries", "System");

                return archived;
            }
            finally
            {
                Monitor.Exit(RunLock);
            }
        }
    }
}