using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NikooSystem.Services
{
    /// <summary>
    /// کلاینت REST سامانه پیامکی پاپوس (<c>https://my.pappus.ir/api/v1</c>).
    /// احراز هویت با هدر <c>x-api-key</c>؛ مبالغ API به ریال است.
    /// </summary>
    public static class PappusSmsClient
    {
        public const string DefaultBaseUrl = "https://my.pappus.ir/api/v1";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public static string ApiKey
        {
            get
            {
                var fromSettings = SiteContentStore.Settings?.PappusApiKey;
                if (!string.IsNullOrWhiteSpace(fromSettings)) return fromSettings.Trim();
                var fromConfig = ConfigurationManager.AppSettings["Pappus.ApiKey"];
                return string.IsNullOrWhiteSpace(fromConfig) ? null : fromConfig.Trim();
            }
        }

        public static string Sender
        {
            get
            {
                var fromSettings = SiteContentStore.Settings?.PappusSender;
                if (!string.IsNullOrWhiteSpace(fromSettings)) return fromSettings.Trim();
                var fromConfig = ConfigurationManager.AppSettings["Pappus.Sender"];
                return string.IsNullOrWhiteSpace(fromConfig) ? null : fromConfig.Trim();
            }
        }

        public static string BaseUrl
        {
            get
            {
                var configured = ConfigurationManager.AppSettings["Pappus.BaseUrl"];
                return string.IsNullOrWhiteSpace(configured) ? DefaultBaseUrl : configured.Trim().TrimEnd('/');
            }
        }

        public static bool IsConfigured => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Sender);

        public static async Task<PappusBalance> GetBalanceAsync()
        {
            var json = await GetAsync("/balance").ConfigureAwait(false);
            return new PappusBalance
            {
                Success = json.Value<bool?>("success") == true,
                BalanceRial = json.Value<long?>("balance") ?? 0,
                Currency = json.Value<string>("currency") ?? "IRR",
                Message = json.Value<string>("message")
            };
        }

        /// <summary>هزینه را بدون ارسال حساب می‌کند؛ <paramref name="type"/> مطابق مستندات پاپوس (۰ عادی، ۱ رمز یکبار، ۲ تراکنشی، …).</summary>
        public static async Task<PappusEstimate> EstimateAsync(string to, string text, int type = 0)
        {
            var json = await CallSendAsync("/estimate", to, text, type, null).ConfigureAwait(false);
            return ParseEstimate(json);
        }

        public static async Task<PappusSendResult> SendAsync(string to, string text, int type = 0, string reference = null)
        {
            var json = await CallSendAsync("/send", to, text, type, reference).ConfigureAwait(false);
            var result = new PappusSendResult();
            FillEstimate(result, json);
            result.JobId = json.Value<long?>("jobId");
            result.Status = json.Value<string>("status");
            return result;
        }

        private static async Task<JObject> CallSendAsync(string path, string to, string text, int type, string reference)
        {
            if (string.IsNullOrEmpty(ApiKey))
                return JObject.FromObject(new { success = false, message = "کلید API پاپوس تنظیم نشده است." });
            if (string.IsNullOrEmpty(Sender))
                return JObject.FromObject(new { success = false, message = "خط فرستنده پاپوس تنظیم نشده است." });
            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(text))
                return JObject.FromObject(new { success = false, message = "شماره گیرنده یا متن پیام خالی است." });

            var body = new Dictionary<string, object>
            {
                ["from"] = Sender,
                ["to"] = to.Trim(),
                ["text"] = text,
                ["type"] = type
            };
            if (!string.IsNullOrWhiteSpace(reference)) body["ref"] = reference.Trim();
            return await PostAsync(path, body).ConfigureAwait(false);
        }

        private static PappusEstimate ParseEstimate(JObject json)
        {
            var result = new PappusEstimate();
            FillEstimate(result, json);
            return result;
        }

        private static void FillEstimate(PappusEstimate result, JObject json)
        {
            result.Success = json.Value<bool?>("success") == true;
            result.Message = json.Value<string>("message");
            result.Count = json.Value<int?>("count") ?? 0;
            result.Parts = json.Value<int?>("parts") ?? 0;
            result.CostRial = json.Value<long?>("cost") ?? 0;
            result.BalanceRial = json.Value<long?>("balance");
            result.Enough = json.Value<bool?>("enough");
            if (!result.Success && string.IsNullOrEmpty(result.Message))
                result.Message = "ارسال پیامک انجام نشد.";
        }

        private static async Task<JObject> GetAsync(string path)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path))
            {
                ApplyKey(request);
                using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                    return await ReadAsync(response).ConfigureAwait(false);
            }
        }

        private static async Task<JObject> PostAsync(string path, object body)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path))
            {
                ApplyKey(request);
                request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                    return await ReadAsync(response).ConfigureAwait(false);
            }
        }

        private static void ApplyKey(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("x-api-key", ApiKey ?? "");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
        }

        private static async Task<JObject> ReadAsync(HttpResponseMessage response)
        {
            var raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            try
            {
                return JObject.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            }
            catch (JsonException)
            {
                return JObject.FromObject(new
                {
                    success = false,
                    status = (int)response.StatusCode,
                    message = "پاسخ نامعتبر از پاپوس (HTTP " + (int)response.StatusCode + ")."
                });
            }
        }
    }

    public class PappusBalance
    {
        public bool Success { get; set; }
        public long BalanceRial { get; set; }
        public string Currency { get; set; }
        public string Message { get; set; }
    }

    public class PappusEstimate
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int Count { get; set; }
        public int Parts { get; set; }
        public long CostRial { get; set; }
        public long? BalanceRial { get; set; }
        public bool? Enough { get; set; }
    }

    public class PappusSendResult : PappusEstimate
    {
        public long? JobId { get; set; }
        public string Status { get; set; }
    }
}
