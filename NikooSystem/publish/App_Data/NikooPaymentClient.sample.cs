// نمونه کلاینت برای پروژه‌های دیگر (.NET)
// این فایل را در پروژه خود کپی کنید و AppId / ApiKey را تنظیم کنید.
// وابستگی: Newtonsoft.Json

using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;

public class NikooPaymentClient
{
    private readonly string _baseUrl;
    private readonly string _appId;
    private readonly string _apiKey;

    public NikooPaymentClient(string baseUrl, string appId, string apiKey)
    {
        _baseUrl = (baseUrl ?? "https://nikoooo.ir").TrimEnd('/');
        _appId = appId;
        _apiKey = apiKey;
    }

    public async Task<string> CreatePaymentAsync(
        long amountToman,
        string returnUrl,
        string description = null,
        string externalRef = null,
        string notifyUrl = null,
        string customerName = null,
        string customerMobile = null,
        string customerEmail = null)
    {
        var payload = new JObject
        {
            ["amount"] = amountToman,
            ["returnUrl"] = returnUrl,
            ["description"] = description,
            ["externalRef"] = externalRef,
            ["notifyUrl"] = notifyUrl,
            ["customerName"] = customerName,
            ["customerMobile"] = customerMobile,
            ["customerEmail"] = customerEmail
        };

        using (var http = new HttpClient())
        {
            http.DefaultRequestHeaders.Add("X-App-Id", _appId);
            http.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            var response = await http.PostAsync(_baseUrl + "/api/payments/create", content);
            var body = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(body);
            if (json["success"]?.Value<bool>() != true)
                throw new Exception(json["message"]?.ToString() ?? "خطا در ساخت پرداخت");
            return json["paymentUrl"].ToString();
        }
    }

    public bool VerifyReturn(HttpRequestBase request, out string status, out string token, out string refId)
    {
        token = request["token"];
        status = request["status"];
        refId = request["refId"];
        var amount = request["amount"];
        var externalRef = request["externalRef"] ?? "";
        var timestamp = request["timestamp"];
        var signature = request["signature"];

        long ts;
        if (!long.TryParse(timestamp, out ts))
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - ts) > 3600)
            return false;

        var payload = string.Join("|", new[]
        {
            token ?? "",
            status ?? "",
            amount ?? "",
            externalRef,
            refId ?? "",
            timestamp ?? ""
        });

        return FixedTimeEquals(HmacSha256(_apiKey, payload), signature);
    }

    private static string HmacSha256(string secret, string data)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret ?? "")))
        {
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data ?? ""));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
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
}

/* مثال استفاده در پروژه دیگر:

var client = new NikooPaymentClient("https://nikoooo.ir", "my-shop", "your-secret-key");
var paymentUrl = await client.CreatePaymentAsync(
    amountToman: 150000,
    returnUrl: "https://myshop.ir/payment/return",
    description: "سفارش ۱۲۳",
    externalRef: "ORD-123",
    notifyUrl: "https://myshop.ir/payment/notify");

return Redirect(paymentUrl);

// در Action بازگشت:
string status, token, refId;
if (!client.VerifyReturn(Request, out status, out token, out refId) || status != "paid")
    return View("Failed");
return View("Success");
*/
