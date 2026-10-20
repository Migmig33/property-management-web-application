using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WebApplication1.Helper;
using WebApplication1.Models.Context;
using WebApplication1.Models.Tables;

namespace WebApplication1.Services
{
    public class NotificationChecker
    {
        public class NotificationResult
        {
            public string tenant { get; set; }
            public string phone { get; set; }
            public string type { get; set; }
            public string status { get; set; }
            public string error { get; set; }
            public string message { get; set; }
        }

        public class NotificationRun
        {
            public int expiringLeases { get; set; }
            public int duePayments { get; set; }
            public List<NotificationResult> results { get; set; }
        }

        /// <summary>
        /// The lease/payment scan. Lives here rather than in the controller so
        /// the admin button and the background timer run identical code.
        /// </summary>
        public class NotificationScanner
        {
            public const int SMS_COOLDOWN_DAYS = 7;
            public const int LEASE_WARN_DAYS = 45;
            public const int PAYMENT_WARN_DAYS = 15;

            public static NotificationRun Run(bool dryRun)
            {
                using (var connect = new DB_Context())
                {
                    var today = DateTime.Today;
                    var leaseCutoff = today.AddDays(LEASE_WARN_DAYS);
                    var paymentCutoff = today.AddDays(PAYMENT_WARN_DAYS);
                    var cooldownSince = DateTime.Now.AddDays(-SMS_COOLDOWN_DAYS);

                    var run = new NotificationRun { results = new List<NotificationResult>() };

                    var recent = connect.sms_log
                        .Where(s => s.sentAt >= cooldownSince)
                        .Select(s => new { s.Tid, s.type })
                        .ToList();

                    // ---- 1. Leases expiring within 45 days ----
                    var expiring = connect.tenant
                        .Where(t => t.isTerminated != 1
                                    && t.leaseEnd >= today
                                    && t.leaseEnd <= leaseCutoff)
                        .ToList();

                    var pms = connect.admin
                        .Where(a => a.role == 2).ToList();

                    run.expiringLeases = expiring.Count;

                    foreach (var t in expiring)
                    {
                        if (recent.Any(r => r.Tid == t.Tid && r.type == "Lease Expiry"))
                        {
                            run.results.Add(Skipped(t.name, "Lease Expiry"));
                            continue;
                        }

                        int daysLeft = (int)(t.leaseEnd - today).TotalDays;

                        string message =
                            $"Hi {t.name}, your lease at Green Residences ends on " +
                            $"{t.leaseEnd:MMM dd, yyyy} ({daysLeft} day{(daysLeft == 1 ? "" : "s")} left). " +
                            "Please contact the office to renew. Thank you!";

                        run.results.Add(Dispatch(connect, t.Tid, t.name, t.phone,
                                                 "Lease Expiry", message, dryRun));
                        AuditLogger.Log("settings", "info", "Notified " + t.name + " about their expiring lease", "System");

                        bool pmAlreadySent = recent.Any(r => r.Tid == t.Tid && r.type == "Lease Expiry (PM)");
                        if (!pmAlreadySent)
                        {
                            foreach (var pm in pms)
                            {
                               
                               


                                string pmmessage =
                                    $"Hi {pm.name}, {t.name}'s lease at Green Residences ends on " +
                                     $"{t.leaseEnd:MMM dd, yyyy} ({daysLeft} day{(daysLeft == 1 ? "" : "s")} left). " +
                                    "Please contact the tenant to negotiate the lease. Thank you!";

                                run.results.Add(Dispatch(connect, t.Tid, pm.name, pm.phone,
                                                         "Lease Expiry (PM)", pmmessage, dryRun));

                            }
                            AuditLogger.Log("settings", "info", "Notified property managers about " + t.name + "'s expiring lease", "System");

                        }


                    }
                   

                    // ---- 2. Unpaid rent due within 15 days ----
                    var dueSoon = (from p in connect.payment
                                   join t in connect.tenant on p.Tid equals t.Tid
                                   where p.paidDate == null
                                         && p.dueDate >= today
                                         && p.dueDate <= paymentCutoff
                                         && t.isTerminated != 1
                                   select new { p, t })
                                  .ToList();

                    run.duePayments = dueSoon.Count;

                    foreach (var row in dueSoon)
                    {
                        if (recent.Any(r => r.Tid == row.t.Tid && r.type == "Payment Due"))
                        {
                            run.results.Add(Skipped(row.t.name, "Payment Due"));
                            continue;
                        }

                        int daysLeft = (int)(row.p.dueDate - today).TotalDays;

                        string message =
                            $"Hi {row.t.name}, your rent of PHP {row.p.amount:N2} for " +
                            $"{row.p.billingPeriod} is due on {row.p.dueDate:MMM dd, yyyy} " +
                            $"({daysLeft} day{(daysLeft == 1 ? "" : "s")} left). " +
                            "Please settle on or before the due date. Thank you!";

                        run.results.Add(Dispatch(connect, row.t.Tid, row.t.name, row.t.phone,
                                                 "Payment Due", message, dryRun));

                        AuditLogger.Log("settings", "info", "Notified " + row.t.name + " about upcoming rent due", "System");


                    }

                    connect.SaveChanges();
                    return run;
                }
            }

            private static NotificationResult Dispatch(DB_Context connect, int tid, string name,
                                                       string rawPhone, string type,
                                                       string message, bool dryRun)
            {
                string phone = SmsServices.NormalizePhone(rawPhone);

                if (phone == null)
                {
                    connect.sms_log.Add(new sms_log
                    {
                        Tid = tid,
                        message = message,
                        type = type,
                        status = "Invalid Number",
                        sentAt = DateTime.Now
                    });

                    return new NotificationResult
                    {
                        tenant = name,
                        phone = rawPhone,
                        type = type,
                        status = "Invalid Number",
                        message = message
                    };
                }

                string status;
                string error = null;

                if (dryRun)
                {
                    status = "Preview";
                }
                else
                {
                    error = SmsServices.SendSms(phone, message);
                    status = (error == null) ? "Sent" : "Failed";
                }

                connect.sms_log.Add(new sms_log
                {
                    Tid = tid,
                    message = message,
                    type = type,
                    status = status,
                    sentAt = DateTime.Now
                });

                return new NotificationResult
                {
                    tenant = name,
                    phone = phone,
                    type = type,
                    status = status,
                    error = error,
                    message = message
                };
            }

            private static NotificationResult Skipped(string name, string type)
            {
                return new NotificationResult
                {
                    tenant = name,
                    type = type,
                    status = "Skipped",
                    error = "Already notified recently"
                };
            }
        }
    }
}