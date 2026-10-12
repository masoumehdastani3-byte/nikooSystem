using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using NikooSystem.Models;

namespace NikooSystem.Services
{
    public static class PaymentHubService
    {
        public static string BaseUrl
        {
            get
            {
                var request = HttpContext.Current?.Request;
                if (request != null)
                {
                    var host = request.Url.Host;
                    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                        host == "127.0.0.1")
                    {
                        return request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/');
                    }
                }

                var configured = ConfigurationManager.AppSettings["PaymentHub.BaseUrl"];
                if (!string.IsNullOrWhiteSpace(configured))
                    return configured.TrimEnd('/');

                if (request != null)
                    return request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/');

                return "https://nikoooo.ir";
            }
        }

        public static string HubCallbackUrl
        {
            get
            {
                var request = HttpContext.Current?.Request;
                if (request != null)
                {
                    var host = request.Url.Host;
                    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                        host == "127.0.0.1")
                    {
                        return BaseUrl + "/Pay/Callback";
                    }
                }

                var configured = ConfigurationManager.AppSettings["PaymentHub.CallbackUrl"];
                if (!string.IsNullOrWhiteSpace(configured))
                    return configured;

                return BaseUrl + "/Pay/Callback";
            }
        }

        public static PaymentClient Authenticate(string appId, string apiKey)
        {
            var client = SiteStore.GetPaymentClient(appId);
            if (client == null) return null;
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            if (!string.Equals(client.ApiKey, apiKey, StringComparison.Ordinal))
                return null;
            return client;
        }

        public static bool IsReturnUrlAllowed(PaymentClient client, string returnUrl)
        {
            if (client == null || string.IsNullOrWhiteSpace(returnUrl))
                return false;

            Uri uri;
            if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            var hosts = client.AllowedReturnHosts ?? new string[0];
            if (hosts.Length == 0)
                return true;

            return hosts.Any(h =>
                string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase) ||
                (h.StartsWith("*.") && uri.Host.EndsWith(h.Substring(1), StringComparison.OrdinalIgnoreCase)));
        }

        public static bool NeedsCustomerInfo(HubPayment payment)
        {
            return string.IsNullOrWhiteSpace(payment.CustomerName)
                || string.IsNullOrWhiteSpace(payment.CustomerMobile)
                || string.IsNullOrWhiteSpace(payment.CustomerEmail);
        }

        public static string BuildSignedReturnUrl(HubPayment payment, PaymentClient client)
        {
            var status = payment.Status == HubPaymentStatus.Paid ? "paid" :
                         payment.Status == HubPaymentStatus.Failed ? "failed" : "pending";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var refId = payment.RefId?.ToString() ?? "";
            var externalRef = payment.ExternalRef ?? "";
            var payload = string.Join("|", new[]
            {
                payment.Token,
                status,
                payment.Amount.ToString(),
                externalRef,
                refId,
                timestamp
            });
            var signature = ComputeHmac(client.ApiKey, payload);

            var separator = payment.ReturnUrl.Contains("?") ? "&" : "?";
            return payment.ReturnUrl + separator +
                   "token=" + Uri.EscapeDataString(payment.Token) +
                   "&status=" + status +
                   "&amount=" + payment.Amount +
                   "&externalRef=" + Uri.EscapeDataString(externalRef) +
                   "&refId=" + Uri.EscapeDataString(refId) +
                   "&timestamp=" + timestamp +
                   "&signature=" + Uri.EscapeDataString(signature);
        }

        public static string ComputeHmac(string secret, string payload)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret ?? "")))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload ?? ""));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// پروژه‌های دیگر با همین فرمول امضا را روی ReturnUrl / NotifyUrl بررسی می‌کنند.
        /// </summary>
        public static bool VerifySignature(
            string apiKey,
            string token,
            string status,
            string amount,
            string externalRef,
            string refId,
            string timestamp,
            string signature,
            int maxAgeSeconds = 3600)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(signature))
                return false;

            long ts;
            if (!long.TryParse(timestamp, out ts))
                return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - ts) > maxAgeSeconds)
                return false;

            var payload = string.Join("|", new[]
            {
                token ?? "",
                status ?? "",
                amount ?? "",
                externalRef ?? "",
                refId ?? "",
                timestamp ?? ""
            });
            var expected = ComputeHmac(apiKey, payload);
            return FixedTimeEquals(expected, signature);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static async Task TryNotifyAsync(HubPayment payment, PaymentClient client)
        {
            if (payment == null || client == null || string.IsNullOrWhiteSpace(payment.NotifyUrl))
                return;
            if (payment.NotifySent)
                return;

            var status = payment.Status == HubPaymentStatus.Paid ? "paid" :
                         payment.Status == HubPaymentStatus.Failed ? "failed" : "pending";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var refId = payment.RefId?.ToString() ?? "";
            var externalRef = payment.ExternalRef ?? "";
            var payload = string.Join("|", new[]
            {
                payment.Token,
                status,
                payment.Amount.ToString(),
                externalRef,
                refId,
                timestamp
            });
            var signature = ComputeHmac(client.ApiKey, payload);

            var body = JsonConvert.SerializeObject(new
            {
                token = payment.Token,
                status,
                amount = payment.Amount,
                externalRef,
                refId = payment.RefId,
                timestamp,
                signature,
                message = payment.PaymentMessage
            });

            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                    wc.Encoding = Encoding.UTF8;
                    await wc.UploadStringTaskAsync(payment.NotifyUrl, "POST", body);
                    payment.NotifySent = true;
                    SiteStore.UpdateHubPayment(payment);
                }
            }
            catch
            {
                // webhook failure must not block user redirect
            }
        }
    }
}
