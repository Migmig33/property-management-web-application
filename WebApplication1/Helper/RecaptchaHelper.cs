
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WebApplication1.Helper
{
    public static class RecaptchaHelper
    {
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private const string VerifyUrl =
            "https://www.google.com/recaptcha/api/siteverify";

        public static async Task<string> VerifyAsync(
            string token,
            string expectedHostname)
        {
            try
            {
                // 1. Validate CAPTCHA token
                if (string.IsNullOrWhiteSpace(token))
                {
                    return "CAPTCHA token is missing.";
                }

                // 2. Get Secret Key from configuration
                string secret = ConfigurationManager
                    .AppSettings["RecaptchaSecretKey"];

                if (string.IsNullOrWhiteSpace(secret))
                {
                    return "RecaptchaSecretKey is missing from appsettings.local.config.";
                }

                // 3. Prepare Google's SiteVerify request
                var parameters = new[]
                {
                    new KeyValuePair<string, string>(
                        "secret", secret),

                    new KeyValuePair<string, string>(
                        "response", token)
                };

                using (var content =
                    new FormUrlEncodedContent(parameters))
                {
                    // 4. Send POST request to Google
                    using (var response = await client.PostAsync(
                        VerifyUrl, content))
                    {
                        string json =
                            await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            return "Google API HTTP error: " +
                                (int)response.StatusCode;
                        }

                        // 5. Read Google's JSON response
                        var serializer = new JavaScriptSerializer();

                        var result = serializer.Deserialize<
                            Dictionary<string, object>>(json);

                        if (result == null)
                        {
                            return "Invalid Google verification response.";
                        }

                        // 6. Check success
                        object successValue;

                        bool success =
                            result.TryGetValue(
                                "success", out successValue) &&
                            successValue is bool &&
                            (bool)successValue;

                        if (!success)
                        {
                            // Get Google's actual error codes
                            object errorValue;

                            if (result.TryGetValue(
                                "error-codes", out errorValue))
                            {
                                var errors = errorValue as object[];

                                if (errors != null && errors.Length > 0)
                                {
                                    string errorCodes =
                                        string.Join(", ", errors);

                                    return "Google CAPTCHA rejected: " +
                                        errorCodes;
                                }
                            }

                            return "Google CAPTCHA verification failed.";
                        }

                        // 7. Verify hostname
                        object hostnameValue;

                        string hostname =
                            result.TryGetValue(
                                "hostname", out hostnameValue)
                            ? Convert.ToString(hostnameValue)
                            : "";

                        if (string.IsNullOrWhiteSpace(hostname))
                        {
                            return "Google did not return a hostname.";
                        }

                        if (!string.Equals(
                            hostname,
                            expectedHostname,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return "Hostname mismatch. Expected: " +
                                expectedHostname +
                                " | Received: " + hostname;
                        }

                        // 8. CAPTCHA successfully verified
                        return null;
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                Trace.TraceError("[reCAPTCHA] Timeout: " + ex);

                return "Google CAPTCHA verification timed out.";
            }
            catch (Exception ex)
            {
                Trace.TraceError("[reCAPTCHA] Error: " + ex);

                return "CAPTCHA server error: " +
                    ex.GetType().Name;
            }
        }
    }
}
