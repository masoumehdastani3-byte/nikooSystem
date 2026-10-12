using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class PayController : Controller
    {
        private readonly ZarinPalService _zarinPal = new ZarinPalService();

        [HttpGet]
        [NoIndex]
        public ActionResult Start(string token)
        {
            var payment = SiteStore.GetHubPaymentByToken(token);
            if (payment == null)
                return HttpNotFound();

            if (payment.Status == HubPaymentStatus.Paid)
                return RedirectToAction("Done", new { token = payment.Token, ok = 1 });

            if (payment.ExpiresAt < DateTime.Now && payment.Status == HubPaymentStatus.Pending)
            {
                payment.Status = HubPaymentStatus.Expired;
                payment.PaymentMessage = "مهلت پرداخت به پایان رسیده است.";
                SiteStore.UpdateHubPayment(payment);
            }

            if (payment.Status != HubPaymentStatus.Pending)
                return RedirectToAction("Done", new { token = payment.Token, ok = 0 });

            var model = new PayStartViewModel
            {
                Payment = payment,
                CustomerName = payment.CustomerName,
                CustomerMobile = payment.CustomerMobile,
                CustomerEmail = payment.CustomerEmail
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Start(string token, PayStartViewModel model)
        {
            var payment = SiteStore.GetHubPaymentByToken(token);
            if (payment == null)
                return HttpNotFound();

            if (payment.Status != HubPaymentStatus.Pending || payment.ExpiresAt < DateTime.Now)
                return RedirectToAction("Done", new { token, ok = 0 });

            model.Payment = payment;

            if (!ModelState.IsValid)
                return View(model);

            payment.CustomerName = model.CustomerName;
            payment.CustomerMobile = model.CustomerMobile;
            payment.CustomerEmail = model.CustomerEmail;
            SiteStore.UpdateHubPayment(payment);

            return await RedirectToGateway(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Confirm(string token)
        {
            var payment = SiteStore.GetHubPaymentByToken(token);
            if (payment == null)
                return HttpNotFound();

            if (payment.Status != HubPaymentStatus.Pending || payment.ExpiresAt < DateTime.Now)
                return RedirectToAction("Done", new { token, ok = 0 });

            if (PaymentHubService.NeedsCustomerInfo(payment))
                return RedirectToAction("Start", new { token });

            return await RedirectToGateway(payment);
        }

        private async Task<ActionResult> RedirectToGateway(HubPayment payment)
        {
            var callback = PaymentHubService.HubCallbackUrl;
            var result = await _zarinPal.RequestPaymentAsync(
                payment.Amount,
                payment.Description ?? ("پرداخت " + payment.Token),
                payment.CustomerEmail,
                payment.CustomerMobile,
                payment.Token,
                callback);

            if (!result.Success)
            {
                payment.Status = HubPaymentStatus.Failed;
                payment.PaymentMessage = result.Message;
                SiteStore.UpdateHubPayment(payment);
                TempData["Error"] = result.Message;
                return RedirectToAction("Start", new { token = payment.Token });
            }

            payment.Authority = result.Authority;
            SiteStore.UpdateHubPayment(payment);
            return Redirect(result.PaymentUrl);
        }

        [HttpGet]
        public async Task<ActionResult> Callback(string orderId, string Authority, string Status)
        {
            // orderId carries hub payment token (see ZarinPalService callback query)
            var token = orderId;
            var payment = SiteStore.GetHubPaymentByToken(token);
            if (payment == null)
                return RedirectToAction("Done", new { token = token ?? "", ok = 0 });

            var client = SiteStore.GetPaymentClient(payment.AppId);
            if (payment.Status == HubPaymentStatus.Paid)
                return Redirect(BuildReturnOrDone(payment, client, true));

            var verify = await _zarinPal.VerifyCallbackAsync(payment.Authority, Authority, Status, payment.Amount);
            if (verify.Forged)
                return RedirectToAction("Done", new { token = payment.Token, ok = 0 });
            if (!verify.Success)
            {
                payment.Status = HubPaymentStatus.Failed;
                payment.PaymentMessage = verify.Message;
                SiteStore.UpdateHubPayment(payment);
                await PaymentHubService.TryNotifyAsync(payment, client);
                return Redirect(BuildReturnOrDone(payment, client, false));
            }

            payment.Status = HubPaymentStatus.Paid;
            payment.RefId = verify.RefId;
            payment.PaidAt = DateTime.Now;
            payment.PaymentMessage = verify.Message;
            SiteStore.UpdateHubPayment(payment);
            Notifications.HubPayment(payment, client);
            await PaymentHubService.TryNotifyAsync(payment, client);
            return Redirect(BuildReturnOrDone(payment, client, true));
        }

        private static string BuildReturnOrDone(HubPayment payment, PaymentClient client, bool ok)
        {
            if (client != null && !string.IsNullOrWhiteSpace(payment.ReturnUrl))
                return PaymentHubService.BuildSignedReturnUrl(payment, client);

            return "/Pay/Done?token=" + Uri.EscapeDataString(payment.Token) + "&ok=" + (ok ? "1" : "0");
        }

        [HttpGet]
        [NoIndex]
        public ActionResult Done(string token, int? ok)
        {
            var payment = SiteStore.GetHubPaymentByToken(token);
            ViewBag.Ok = ok == 1 || (payment != null && payment.Status == HubPaymentStatus.Paid);

            if (payment != null && !string.IsNullOrWhiteSpace(payment.ReturnUrl))
            {
                var client = SiteStore.GetPaymentClient(payment.AppId);
                if (client != null)
                    ViewBag.SignedReturnUrl = PaymentHubService.BuildSignedReturnUrl(payment, client);
            }

            return View(payment);
        }

        [HttpGet]
        public ActionResult Guide()
        {
            ViewBag.Title = "راهنمای اتصال پرداخت";
            ViewBag.Nav = "";
            ViewBag.BaseUrl = PaymentHubService.BaseUrl;
            Seo.Of(HttpContext).Description = "راهنمای اتصال سایت یا اپلیکیشن به درگاه پرداخت مرکزی نیکو سیستم: ساخت پرداخت با API، بازگشت از درگاه و استعلام وضعیت، با نمونه کد.";
            return View();
        }
    }
}
