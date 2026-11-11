using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>شارژ اعتبار پیامک پاپوس، مشاهده موجودی و سابقه ارسال‌ها.</summary>
    public partial class ShopAdminController
    {
        [HttpGet]
        public ActionResult Sms()
        {
            ViewBag.IsDemo = TenantStore.IsDemo(CompanyCode);
            ViewBag.Ready = SmsService.IsReady;
            ViewBag.Packages = SmsService.Packages();
            ViewBag.Purchases = SmsService.Purchases(CompanyCode);
            ViewBag.Logs = SmsService.Recent(CompanyCode);
            ViewBag.Credit = SmsService.CreditOf(CompanyCode);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> BuySms(long credit)
        {
            if (TenantStore.IsDemo(CompanyCode))
                return Fail("سایت‌های نمونه اعتبار پیامک جداگانه نمی‌خرند؛ در سایت خودتان از همین صفحه شارژ کنید.", "Sms");
            if (!SmsService.IsReady)
                return Fail("سامانه پیامک هنوز توسط نیکو فعال نشده است.", "Sms");

            var purchase = SmsService.StartPurchase(CompanyCode, credit);
            if (purchase == null) return Fail("این بسته پیدا نشد.", "Sms");

            var callback = Url.Action("SmsPaid", "ShopAdmin", new { company = CompanyCode }, Request.Url.Scheme);
            var pay = await new ZarinPalService().RequestPaymentAsync(
                purchase.Amount,
                "شارژ " + purchase.Credit.ToToman() + " اعتبار پیامک " + (CurrentSettings?.StoreName ?? CompanyCode),
                Tenant?.OwnerEmail,
                Tenant?.OwnerMobile,
                purchase.Id.ToString(),
                callback);
            if (!pay.Success)
            {
                SmsService.FailPurchase(purchase, pay.Message);
                return Fail("اتصال به درگاه پرداخت انجام نشد: " + pay.Message, "Sms");
            }
            SmsService.SetAuthority(purchase, pay.Authority);
            return Redirect(pay.PaymentUrl);
        }

        /// <summary>بازگشت از درگاه؛ بدون نیاز به نشست پنل تا پرداخت از دست نرود.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> SmsPaid(int? orderId, string Authority, string Status)
        {
            var purchase = orderId.HasValue ? SmsService.GetPurchase(orderId.Value) : null;
            if (purchase == null || !string.Equals(purchase.CompanyCode, CompanyCode, StringComparison.OrdinalIgnoreCase))
                return Fail("خرید اعتبار پیامک پیدا نشد.", "Sms");
            if (purchase.Status == OrderStatus.Paid)
                return Ok(purchase.Credit.ToToman() + " به اعتبار پیامک شما اضافه شده است.", "Sms");

            var verify = await new ZarinPalService().VerifyCallbackAsync(purchase.Authority, Authority, Status, purchase.Amount);
            if (verify.Forged) return Fail("اطلاعات بازگشت از درگاه معتبر نبود.", "Sms");
            if (!verify.Success)
            {
                SmsService.FailPurchase(purchase, verify.Message);
                return Fail("پرداخت انجام نشد و اعتباری اضافه نشد: " + verify.Message, "Sms");
            }

            if (SmsService.CompletePurchase(purchase.Id, verify.RefId, verify.Message))
                NikooSystem.Services.Notifications.SmsPurchase(SmsService.GetPurchase(purchase.Id) ?? purchase);
            return Ok("پرداخت انجام شد و " + purchase.Credit.ToToman() + " به اعتبار پیامک شما اضافه شد. از این پس تغییر رمز و صورتحساب خرید برای مشتریان پیامک می‌شود.", "Sms");
        }

        /// <summary>ارسال یک پیامک آزمایشی به شماره دلخواه (معمولاً موبایل مالک) برای اطمینان از اتصال.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> TestSms(string mobile)
        {
            if (TenantStore.IsDemo(CompanyCode))
                return Fail("ارسال آزمایشی در سایت نمونه غیرفعال است.", "Sms");
            if (!SmsService.IsReady)
                return Fail("سامانه پیامک هنوز توسط نیکو فعال نشده است.", "Sms");

            mobile = string.IsNullOrWhiteSpace(mobile) ? Tenant?.OwnerMobile : mobile;
            var result = await SmsService.SendTestAsync(CompanyCode, mobile, CurrentSettings?.StoreName ?? BaleNotifier.SiteName(CompanyCode));
            return result.Success
                ? Ok("پیامک آزمایشی به " + BookingTime.NormalizeMobile(mobile) + " فرستاده شد (هزینه " + result.Cost.ToToman() + ").", "Sms")
                : Fail("پیامک آزمایشی فرستاده نشد: " + result.Error, "Sms");
        }
    }
}
