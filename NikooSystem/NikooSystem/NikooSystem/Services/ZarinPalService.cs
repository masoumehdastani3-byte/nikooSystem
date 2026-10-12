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
    public class ZarinPalPaymentResult
    {
        public bool Success { get; set; }
        public string Authority { get; set; }
        public string PaymentUrl { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
    }

    public class ZarinPalVerifyResult
    {
        public bool Success { get; set; }
        public long RefId { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }

        /// <summary>
        /// The callback did not carry the authority issued for this payment (a forged or mixed-up link):
        /// the payment must be left as it is rather than marked failed.
        /// </summary>
        public bool Forged { get; set; }
    }

    public class ZarinPalService
    {
        private readonly string _merchantId;
        private readonly bool _sandbox;
        private readonly string _callbackUrl;

        public ZarinPalService()
            : this(null, null)
        {
        }

        public ZarinPalService(string merchantId, bool? sandbox)
        {
            _merchantId = string.IsNullOrWhiteSpace(merchantId)
                ? (ConfigurationManager.AppSettings["ZarinPal.MerchantId"] ?? "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx")
                : merchantId;
            _sandbox = sandbox ?? string.Equals(ConfigurationManager.AppSettings["ZarinPal.Sandbox"], "true", StringComparison.OrdinalIgnoreCase);
            _callbackUrl = ConfigurationManager.AppSettings["ZarinPal.CallbackUrl"];
        }

        private string ApiBase => _sandbox
            ? "https://sandbox.zarinpal.com/pg/v4/payment/"
            : "https://payment.zarinpal.com/pg/v4/payment/";

        private string StartPayBase => _sandbox
            ? "https://sandbox.zarinpal.com/pg/StartPay/"
            : "https://www.zarinpal.com/pg/StartPay/";

        public async Task<ZarinPalPaymentResult> RequestPaymentAsync(long amountToman, string description, string email, string mobile, string orderId)
        {
            return await RequestPaymentAsync(amountToman, description, email, mobile, orderId, null);
        }

        public async Task<ZarinPalPaymentResult> RequestPaymentAsync(long amountToman, string description, string email, string mobile, string orderId, string callbackUrl)
        {
            // ZarinPal expects amount in Rials
            var amountRial = amountToman * 10;
            var baseCallback = string.IsNullOrWhiteSpace(callbackUrl) ? _callbackUrl : callbackUrl;
            var callback = baseCallback + (orderId != null
                ? (baseCallback.Contains("?") ? "&" : "?") + "orderId=" + Uri.EscapeDataString(orderId)
                : "");

            var payload = new Dictionary<string, object>
            {
                { "merchant_id", _merchantId },
                { "amount", amountRial },
                { "callback_url", callback },
                { "description", description ?? "پرداخت سفارش نیکو سیستم آفرین" },
                { "metadata", new Dictionary<string, string>
                    {
                        { "email", email ?? "" },
                        { "mobile", mobile ?? "" }
                    }
                }
            };

            try
            {
                using (var client = new HttpClient())
                {
                    var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(ApiBase + "request.json", content);
                    var body = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(body);
                    var data = json["data"];
                    var errors = json["errors"];

                    if (data != null && data.Type != JTokenType.Null && data["authority"] != null)
                    {
                        var authority = data["authority"].ToString();
                        return new ZarinPalPaymentResult
                        {
                            Success = true,
                            Authority = authority,
                            PaymentUrl = StartPayBase + authority,
                            Code = data["code"]?.Value<int>() ?? 100,
                            Message = "OK"
                        };
                    }

                    var message = "خطا در اتصال به درگاه پرداخت";
                    if (errors != null && errors.Type != JTokenType.Null)
                        message = errors.ToString();

                    return new ZarinPalPaymentResult { Success = false, Message = message };
                }
            }
            catch (Exception ex)
            {
                return new ZarinPalPaymentResult
                {
                    Success = false,
                    Message = "خطا در ارتباط با درگاه پرداخت: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Checks the gateway's return to a callback URL: the authority must be the one stored when the payment was
        /// started, the payer must not have cancelled, and the gateway must confirm the stored amount.
        /// </summary>
        public async Task<ZarinPalVerifyResult> VerifyCallbackAsync(string storedAuthority, string authority, string status, long amountToman)
        {
            if (string.IsNullOrEmpty(storedAuthority) || !string.Equals(authority, storedAuthority, StringComparison.Ordinal))
                return new ZarinPalVerifyResult { Forged = true, Message = "اطلاعات بازگشت از درگاه معتبر نیست." };
            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                return new ZarinPalVerifyResult { Message = "پرداخت توسط کاربر لغو شد یا ناموفق بود." };
            return await VerifyPaymentAsync(storedAuthority, amountToman);
        }

        public async Task<ZarinPalVerifyResult> VerifyPaymentAsync(string authority, long amountToman)
        {
            var amountRial = amountToman * 10;
            var payload = new Dictionary<string, object>
            {
                { "merchant_id", _merchantId },
                { "amount", amountRial },
                { "authority", authority }
            };

            try
            {
                using (var client = new HttpClient())
                {
                    var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(ApiBase + "verify.json", content);
                    var body = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(body);
                    var data = json["data"];

                    if (data != null && data.Type != JTokenType.Null)
                    {
                        var code = data["code"]?.Value<int>() ?? 0;
                        if (code == 100 || code == 101)
                        {
                            return new ZarinPalVerifyResult
                            {
                                Success = true,
                                RefId = data["ref_id"]?.Value<long>() ?? 0,
                                Code = code,
                                Message = code == 101 ? "قبلاً تأیید شده" : "پرداخت موفق"
                            };
                        }

                        return new ZarinPalVerifyResult
                        {
                            Success = false,
                            Code = code,
                            Message = data["message"]?.ToString() ?? "پرداخت ناموفق"
                        };
                    }

                    return new ZarinPalVerifyResult
                    {
                        Success = false,
                        Message = json["errors"]?.ToString() ?? "خطا در تأیید پرداخت"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ZarinPalVerifyResult
                {
                    Success = false,
                    Message = "خطا در تأیید پرداخت: " + ex.Message
                };
            }
        }
    }
}
