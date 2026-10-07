using System;
using System.IO;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class ApiPaymentsController : Controller
    {
        [AcceptVerbs(HttpVerbs.Post | HttpVerbs.Options)]
        public ActionResult Create()
        {
            AddCorsHeaders();
            if (string.Equals(Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(204);

            var appId = Request.Headers["X-App-Id"] ?? Request["appId"];
            var apiKey = Request.Headers["X-Api-Key"] ?? Request["apiKey"];
            var client = PaymentHubService.Authenticate(appId, apiKey);
            if (client == null)
                return JsonError(401, "احراز هویت نامعتبر است.");

            var request = ReadCreateRequest();
            if (request == null)
                return JsonError(400, "بدنه درخواست خالی یا نامعتبر است.");

            if (request.Amount < 1000)
                return JsonError(400, "مبلغ باید حداقل ۱۰۰۰ تومان باشد.");

            if (string.IsNullOrWhiteSpace(request.ReturnUrl))
                return JsonError(400, "ReturnUrl الزامی است.");

            if (!PaymentHubService.IsReturnUrlAllowed(client, request.ReturnUrl))
                return JsonError(400, "ReturnUrl مجاز نیست. دامنه را در payment-clients.json اضافه کنید.");

            if (!string.IsNullOrWhiteSpace(request.NotifyUrl))
            {
                Uri notifyUri;
                if (!Uri.TryCreate(request.NotifyUrl, UriKind.Absolute, out notifyUri) ||
                    (notifyUri.Scheme != Uri.UriSchemeHttp && notifyUri.Scheme != Uri.UriSchemeHttps))
                {
                    return JsonError(400, "NotifyUrl نامعتبر است.");
                }
            }

            var hours = 2;
            int.TryParse(System.Configuration.ConfigurationManager.AppSettings["PaymentHub.ExpireHours"], out hours);
            if (hours <= 0) hours = 2;

            var payment = new HubPayment
            {
                AppId = client.AppId,
                ExternalRef = request.ExternalRef,
                Amount = request.Amount,
                Description = string.IsNullOrWhiteSpace(request.Description)
                    ? ("پرداخت " + (request.ExternalRef ?? "سفارش"))
                    : request.Description,
                ReturnUrl = request.ReturnUrl,
                NotifyUrl = request.NotifyUrl,
                CustomerName = request.CustomerName,
                CustomerMobile = request.CustomerMobile,
                CustomerEmail = request.CustomerEmail,
                Status = HubPaymentStatus.Pending,
                ExpiresAt = DateTime.Now.AddHours(hours)
            };

            payment = JsonStore.AddHubPayment(payment);
            var paymentUrl = PaymentHubService.BaseUrl + "/Pay/Start/" + payment.Token;

            return JsonOk(new
            {
                success = true,
                token = payment.Token,
                paymentId = payment.Id,
                paymentUrl,
                amount = payment.Amount,
                expiresAt = payment.ExpiresAt
            });
        }

        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Options)]
        public ActionResult Status(string token)
        {
            AddCorsHeaders();
            if (string.Equals(Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(204);

            var appId = Request.Headers["X-App-Id"] ?? Request["appId"];
            var apiKey = Request.Headers["X-Api-Key"] ?? Request["apiKey"];
            var client = PaymentHubService.Authenticate(appId, apiKey);
            if (client == null)
                return JsonError(401, "احراز هویت نامعتبر است.");

            var payment = JsonStore.GetHubPaymentByToken(token);
            if (payment == null || !string.Equals(payment.AppId, client.AppId, StringComparison.OrdinalIgnoreCase))
                return JsonError(404, "پرداخت پیدا نشد.");

            var status = payment.Status == HubPaymentStatus.Paid ? "paid" :
                         payment.Status == HubPaymentStatus.Failed ? "failed" :
                         payment.Status == HubPaymentStatus.Expired ? "expired" : "pending";

            return JsonOk(new
            {
                success = true,
                token = payment.Token,
                status,
                amount = payment.Amount,
                externalRef = payment.ExternalRef,
                refId = payment.RefId,
                message = payment.PaymentMessage,
                paidAt = payment.PaidAt,
                expiresAt = payment.ExpiresAt
            });
        }

        private CreatePaymentRequest ReadCreateRequest()
        {
            if (Request.ContentType != null &&
                Request.ContentType.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (Request.InputStream.CanSeek)
                    Request.InputStream.Position = 0;

                using (var reader = new StreamReader(Request.InputStream, Encoding.UTF8))
                {
                    var body = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(body)) return null;
                    return JsonConvert.DeserializeObject<CreatePaymentRequest>(body);
                }
            }

            return new CreatePaymentRequest
            {
                Amount = ParseLong(Request["amount"]),
                Description = Request["description"],
                ReturnUrl = Request["returnUrl"],
                NotifyUrl = Request["notifyUrl"],
                ExternalRef = Request["externalRef"],
                CustomerName = Request["customerName"],
                CustomerMobile = Request["customerMobile"],
                CustomerEmail = Request["customerEmail"]
            };
        }

        private static long ParseLong(string value)
        {
            long n;
            return long.TryParse(value, out n) ? n : 0;
        }

        private void AddCorsHeaders()
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-App-Id, X-Api-Key";
            Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        }

        private ActionResult JsonOk(object data)
        {
            Response.ContentType = "application/json; charset=utf-8";
            return Content(JsonConvert.SerializeObject(data), "application/json", Encoding.UTF8);
        }

        private ActionResult JsonError(int code, string message)
        {
            Response.StatusCode = code;
            Response.TrySkipIisCustomErrors = true;
            Response.ContentType = "application/json; charset=utf-8";
            return Content(
                JsonConvert.SerializeObject(new { success = false, message }),
                "application/json",
                Encoding.UTF8);
        }
    }
}
