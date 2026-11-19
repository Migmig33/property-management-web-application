using Mysqlx;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using WebApplication1.Filter;
using WebApplication1.Helper;
using WebApplication1.Models.Context;
using WebApplication1.Models.Tables;
using WebApplication1.Services;


namespace WebApplication1.Controllers
{
    [RateLimit(sec = 10, requests = 5)]

    public class TenantPortalController : Controller
    {
            [HttpGet]
            public JsonResult GetCurrentTenantData()
            {
            // 1. Get logged-in Tenant ID (e.g., from Session)
            int currentTid = Convert.ToInt32(Session["LoggedInTid"]);


                if (currentTid == 0)
                {
                    return Json(new { success = false, message = "Not authenticated" }, JsonRequestBehavior.AllowGet);
                }

            // 2. Fetch data from DB inside a using block
            using (var connect = new DB_Context())
            {
                var tenantData = connect.tenant.FirstOrDefault(t => t.Tid == currentTid);
                if (tenantData == null)
                    return Json(new { success = false }, JsonRequestBehavior.AllowGet);

                var unitData = connect.unit.FirstOrDefault(u => u.Uid == tenantData.unitId);
                var coOccupants = connect.co_occupant.Where(c => c.Tid == currentTid).ToList();

                return Json(new
                {
                    success = true,
                    tenant = new
                    {
                        tenantData.Tid,
                        tenantData.tenantNumber,
                        tenantData.unitId,
                        tenantData.name,
                        tenantData.email,
                        tenantData.phone,
                        tenantData.address,
                        tenantData.occupation,
                        tenantData.occupancyTypeId,
                        tenantData.isTerminated,
                        leaseStart = tenantData.leaseStart.ToString("MMM dd, yyyy"),
                        leaseEnd = tenantData.leaseEnd.ToString("MMM dd, yyyy")
                    },
                    unitName = unitData?.unitName ?? "Unassigned",
                    coOccupants = coOccupants
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]

        public JsonResult GetMaintenanceRequests(int tid)
        {
            // Make sure the requested tid matches the logged-in user for security
            int currentTid = Convert.ToInt32(Session["LoggedInTid"]);
            if (tid != currentTid)
            {
                return Json(null, JsonRequestBehavior.AllowGet);
            }

            using (var connect = new DB_Context())
            {
                var requests = connect.maintenance_request
                                  .Where(m => m.Tid == tid)
                                  .OrderByDescending(m => m.reportedDate)
                                  .ToList() // 1. Execute query and pull to memory
                                  .Select(m => new
                                  {
                                      // 2. Map properties
                                      id = m.id,
                                      Tid = m.Tid,
                                      Uid = m.Uid,
                                      category = m.category,
                                      description = m.description,
                                      status = m.status,

                                      // 3. Convert Dates to standard readable strings!
                                      // "yyyy-MM-dd" gives you "2023-10-25"
                                      // Use "yyyy-MM-ddTHH:mm:ss" if you also need the exact time
                                      reportedDate = m.reportedDate.ToString("yyyy-MM-dd"),
                                      resolvedDate = m.resolvedDate.HasValue
                                                     ? m.resolvedDate.Value.ToString("yyyy-MM-dd")
                                                     : null,
                                     scheduledDate = m.scheduledDate.HasValue ? m.resolvedDate.Value.ToString("yyyy-MM-dd") : null
                                  });

                return Json(requests, JsonRequestBehavior.AllowGet);
            }
        }

            [HttpPost]
            public JsonResult SubmitMaintenanceRequest(maintenance_request req)
            {
                try
                {
                    // Validate security
                    int currentTid = Convert.ToInt32(Session["LoggedInTid"]);
                    if (req.Tid != currentTid)
                    {
                        return Json(new { success = false, message = "Unauthorized." });
                    }

                    // Force server-side dates/statuses to prevent client manipulation
                    req.reportedDate = DateTime.Now;
                    req.status = "Pending";

                    using (var connect = new DB_Context())
                    {
                        connect.maintenance_request.Add(req);
                        connect.SaveChanges();
                    }

                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = ex.Message });
                }
            }

        public JsonResult GetPayments(int tid)
        {
            int currentTid = Convert.ToInt32(Session["LoggedInTid"]);
            if (tid != currentTid)
                return Json(null, JsonRequestBehavior.AllowGet);

            using (var connect = new DB_Context())
            {
                var payments = connect.payment
                    .Where(p => p.Tid == tid)
                    .OrderBy(p => p.dueDate)
                    .ToList()
                    .Select(p => new
                    {
                        p.id,
                        p.Tid,
                        p.Uid,
                        p.amount,
                        p.billingPeriod,
                        dueDate = p.dueDate.ToString("MMM dd, yyyy"),
                        paidDate = p.paidDate.HasValue ? p.paidDate.Value.ToString("MMM dd, yyyy") : null,
                        status = p.status,  // computed from getter
                        month = p.billingPeriod // what the frontend shows as the row title
                    })
                    .ToList();

                return Json(payments, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult SaveTenantProfileChangeRequest(tenant_profile_change_request data)
        {
            try
            {
                if (data == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid request data."
                    }, JsonRequestBehavior.AllowGet);
                }

                using (var connect = new DB_Context())
                {
                    int currentTenantId = Convert.ToInt32(Session["LoggedInTid"]);

                    var currentTenant = connect.tenant
                        .FirstOrDefault(t => t.Tid == currentTenantId);

                    if (currentTenant == null)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Tenant not found."
                        }, JsonRequestBehavior.AllowGet);
                    }

                    // Get the tenant's latest profile change request.
                    var latestTenantRequest = connect.tenant_profile_change_request
                        .Where(x => x.Tid == currentTenantId)
                        .OrderByDescending(x => x.requestedAt)
                        .ThenByDescending(x => x.id)
                        .FirstOrDefault();

                    // If the latest request is still pending, update that record.
                    if (latestTenantRequest != null &&
                        string.Equals(
                            latestTenantRequest.status,
                            "Pending",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        latestTenantRequest.requestedEmail = data.requestedEmail;
                        latestTenantRequest.requestedPhone = data.requestedPhone;
                        latestTenantRequest.requestedOccupation = data.requestedOccupation;
                        latestTenantRequest.requestedAddress = data.requestedAddress;
                        latestTenantRequest.reason = data.reason;

                        // Keep the request pending.
                        latestTenantRequest.status = "Pending";

                        connect.SaveChanges();

                        return Json(new
                        {
                            success = true,
                            message = "Your pending profile change request has been updated."
                        }, JsonRequestBehavior.AllowGet);
                    }

                    // If there is no request or the latest one was accepted/declined,
                    // create a new request record.
                    var tenantRequest = new tenant_profile_change_request
                    {
                        Tid = currentTenantId,
                        requestedEmail = data.requestedEmail,
                        requestedPhone = data.requestedPhone,
                        requestedOccupation = data.requestedOccupation,
                        requestedAddress = data.requestedAddress,
                        reason = data.reason,
                        status = "Pending",
                        requestedAt = DateTime.Now
                    };

                    connect.tenant_profile_change_request.Add(tenantRequest);
                    connect.SaveChanges();
                    Session.Remove("VerifiedTenantPhone");
                    AuditLogger.Log(
                        "user",
                        "info",
                        "Change Profile Request " + currentTenant.name,
                        CurrentTenantName()
                    );

                    // Notify property managers about the new request.
                    bool notifStatus = true;

                    var existingPropertyManager = connect.admin
                        .Where(a => a.role == 2)
                        .ToList();

                    foreach (var manager in existingPropertyManager)
                    {
                        string message =
                            $"Hello {manager.name},\n\n" +
                            $"{currentTenant.name} has submitted a request to change their profile information.\n\n" +
                            $"Reason for the request: {data.reason}\n\n" +
                            "Please review the request and approve or decline it when you have a moment.\n\n" +
                            "Thank you!";

                        var phone = SmsServices.NormalizePhone(manager.phone);

                        if (string.IsNullOrWhiteSpace(phone))
                        {
                            notifStatus = false;
                            continue;
                        }

                        string error = SmsServices.SendSms(phone, message);

                        if (error != null)
                        {
                            notifStatus = false;
                        }
                    }

                    return Json(new
                    {
                        success = true,
                        message = notifStatus
                            ? "Profile change request successfully submitted."
                            : "Request was submitted, but some notifications could not be sent."
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ErrorHandling(ex)
                }, JsonRequestBehavior.AllowGet);
            }
        }
        public JsonResult GetMySmsNotifications()
        {
            if (!int.TryParse(
                Convert.ToString(Session["LoggedInTid"] ?? 17),
                out int currentTid) || currentTid <= 0)
            {
                return Json(
                    new { success = false, message = "Unauthorized." },
                    JsonRequestBehavior.AllowGet
                );
            }

            using (var db = new DB_Context())
            {
                var logs = db.sms_log
                    .Where(x =>
                        x.Tid == currentTid &&
                        (
                            x.type == "Lease Expiry" ||
                            x.type == "Payment Due" ||
                            x.type == "Payment Failed" ||
                            x.type == "TenantProfileRequest"
                        ))
                    .Select(x => new
                    {
                        id = x.id,
                        type = x.type,
                        message = x.message,
                        sentAt = x.sentAt
                    })
                    .ToList();

                // Remove duplicate notifications with identical types and messages.
                var notifications = logs
                    .GroupBy(x => new { x.type, x.message })
                    .Select(g => g
                        .OrderByDescending(x => x.sentAt)
                        .ThenByDescending(x => x.id)
                        .FirstOrDefault())
                    .OrderByDescending(x => x.sentAt)
                    .Select(x => new
                    {
                        id = x.id,
                        type = x.type,
                        message = x.message,
                        sentAt = x.sentAt.ToString("MMM d, yyyy h:mm tt")
                    })
                    .ToList();

                return Json(
                    notifications,
                    JsonRequestBehavior.AllowGet
                );
            }
        }
        public JsonResult GetMyProfileChangeRequests()
        {
            if (!int.TryParse(
                Convert.ToString(Session["LoggedInTid"]),
                out int currentTid) || currentTid <= 0)
            {
                return Json(
                    new { success = false, message = "Unauthorized." },
                    JsonRequestBehavior.AllowGet
                );
            }

            using (var db = new DB_Context())
            {
                var requests = db.tenant_profile_change_request
                    .Where(r => r.Tid == currentTid)
                    .OrderByDescending(r => r.requestedAt)
                    .ToList()
                    .Select(r => new
                    {
                        id = r.id,
                        requestedEmail = r.requestedEmail,
                        requestedPhone = r.requestedPhone,
                        requestedOccupation = r.requestedOccupation,
                        requestedAddress = r.requestedAddress,
                        reason = r.reason,

                        status = string.IsNullOrWhiteSpace(r.status)
                            ? "pending"
                            : r.status.Trim().ToLowerInvariant(),

                        requestedAt = r.requestedAt.ToString("MMM d, yyyy h:mm tt"),

                        reviewedAt = r.reviewedAt.HasValue
                            ? r.reviewedAt.Value.ToString("MMM d, yyyy h:mm tt")
                            : null,

                        reviewNotes = r.reviewNotes
                    })
                    .ToList();

                return Json(
                    new
                    {
                        success = true,
                        data = requests
                    },
                    JsonRequestBehavior.AllowGet
                );
            }
        }
        //Helper
        public string ErrorHandling(Exception ex)
        {
            var errorMessage = $@"
            ===== ERROR DETAILS =====
            Message        : {ex.Message}
            Type           : {ex.GetType().FullName}
            Source         : {ex.Source}
            Stack Trace    : {ex.StackTrace}
            Inner Exception: {(ex.InnerException != null ? ex.InnerException.Message : "None")}
            Timestamp      : {DateTime.Now:yyyy-MM-dd HH:mm:ss}
            =========================";

            return errorMessage;
        }
        private string CurrentTenantName()
        {
            string email = Session["VerifyEmail"] as string;
            if (string.IsNullOrEmpty(email)) return "System";
            using (var connect = new DB_Context())
            {
                var a = connect.tenant.FirstOrDefault(x => x.email == email);
                return a != null ? a.name : email;
            }
        }
        private string CurrentManagerName()
        {
            string email = Session["VerifyEmail"] as string;
            if (string.IsNullOrEmpty(email)) return "System";
            using (var connect = new DB_Context())
            {
                var a = connect.admin.FirstOrDefault(x => x.email == email);
                return a != null ? a.name : email;
            }
        }
        private string HashPhoneVerificationCode(string code, string phone)
        {
            using (var sha = SHA256.Create())
            {
                var input = code + "|" + phone + "|" + Session.SessionID;
                var bytes = Encoding.UTF8.GetBytes(input);
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }
        [HttpPost]
        public JsonResult SendPhoneVerificationCode(string phone)
        {
            try
            {
                var normalizedPhone = SmsServices.NormalizePhone(phone);

                if (string.IsNullOrWhiteSpace(normalizedPhone))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please enter a valid phone number."
                    });
                }

                // Prevent repeated SMS requests within 60 seconds.
                var lastSent = Session["TenantPhoneOtpLastSentAt"] as DateTime?;

                if (lastSent.HasValue &&
                    (DateTime.UtcNow - lastSent.Value).TotalSeconds < 60)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please wait 60 seconds before requesting another code."
                    });
                }

                // Invalidate previous verification.
                Session.Remove("VerifiedTenantPhone");

                // Generate a secure six-digit OTP.
                byte[] randomBytes = new byte[4];

                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomBytes);
                }

                int randomNumber = BitConverter.ToInt32(randomBytes, 0) & 0x7fffffff;
                string code = (randomNumber % 900000 + 100000).ToString();

                string message =
                    "GREEN RESIDENCES: Your phone verification code is " + code +
                    ". This code expires in 5 minutes. Do not share this code with anyone.";

                var error = SmsServices.SendSms(normalizedPhone, message);

                if (error != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Unable to send the SMS verification code."
                    });
                }

                // Store OTP details in the server-side session.
                Session["TenantPhoneOtpHash"] =
                    HashPhoneVerificationCode(code, normalizedPhone);

                Session["TenantPhoneOtpPhone"] = normalizedPhone;
                Session["TenantPhoneOtpExpiry"] = DateTime.UtcNow.AddMinutes(5);
                Session["TenantPhoneOtpAttempts"] = 0;
                Session["TenantPhoneOtpLastSentAt"] = DateTime.UtcNow;

                return Json(new
                {
                    success = true,
                    message = "Verification code sent successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ErrorHandling(ex)
                });
            }
        }
        [HttpPost]
        public JsonResult VerifyPhoneVerificationCode(string phone, string code)
        {
            try
            {
                var normalizedPhone = SmsServices.NormalizePhone(phone);

                if (string.IsNullOrWhiteSpace(normalizedPhone) ||
                    string.IsNullOrWhiteSpace(code))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid verification details."
                    });
                }

                string storedPhone = Session["TenantPhoneOtpPhone"] as string;
                string storedHash = Session["TenantPhoneOtpHash"] as string;
                DateTime? expiry = Session["TenantPhoneOtpExpiry"] as DateTime?;

                if (storedPhone == null || storedHash == null || !expiry.HasValue)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please request a new verification code."
                    });
                }

                if (!string.Equals(storedPhone, normalizedPhone, StringComparison.Ordinal))
                {
                    return Json(new
                    {
                        success = false,
                        message = "The verification code was sent to a different phone number."
                    });
                }

                if (DateTime.UtcNow > expiry.Value)
                {
                    Session.Remove("TenantPhoneOtpHash");
                    Session.Remove("TenantPhoneOtpPhone");
                    Session.Remove("TenantPhoneOtpExpiry");

                    return Json(new
                    {
                        success = false,
                        message = "Your verification code has expired. Please request a new one."
                    });
                }

                int attempts = Convert.ToInt32(
                    Session["TenantPhoneOtpAttempts"] ?? 0);

                if (attempts >= 5)
                {
                    Session.Remove("TenantPhoneOtpHash");
                    Session.Remove("TenantPhoneOtpPhone");
                    Session.Remove("TenantPhoneOtpExpiry");

                    return Json(new
                    {
                        success = false,
                        message = "Too many incorrect attempts. Please request a new code."
                    });
                }

                Session["TenantPhoneOtpAttempts"] = attempts + 1;

                string submittedHash =
                    HashPhoneVerificationCode(code, normalizedPhone);

                if (!string.Equals(storedHash, submittedHash, StringComparison.Ordinal))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Incorrect verification code. Please try again."
                    });
                }

                // Mark this exact phone number as verified on the server.
                Session["VerifiedTenantPhone"] = normalizedPhone;

                Session.Remove("TenantPhoneOtpHash");
                Session.Remove("TenantPhoneOtpPhone");
                Session.Remove("TenantPhoneOtpExpiry");
                Session.Remove("TenantPhoneOtpAttempts");

                return Json(new
                {
                    success = true,
                    message = "Phone number successfully verified."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ErrorHandling(ex)
                });
            }
        }
    }
}