using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>«فضای ذخیره‌سازی»: how much of the store's upload space is used, buying more, and clearing unused files.</summary>
    public partial class ShopAdminController
    {
        [HttpGet]
        public ActionResult Storage()
        {
            ViewBag.IsDemo = TenantStore.IsDemo(CompanyCode);
            ViewBag.Packages = StorageQuota.Packages();
            ViewBag.Purchases = StorageQuota.Purchases(CompanyCode);
            ViewBag.ReturnUrl = TempData["StorageReturn"] as string;
            return View(StorageQuota.Usage(CompanyCode));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> BuyStorage(int gb)
        {
            if (TenantStore.IsDemo(CompanyCode))
                return Fail("سایت‌های نمونه فضای جداگانه نمی‌خرند؛ در سایت خودتان از همین صفحه حجم بخرید.", "Storage");

            var purchase = StorageQuota.StartPurchase(CompanyCode, gb);
            if (purchase == null) return Fail("این بسته پیدا نشد.", "Storage");

            var callback = Url.Action("StoragePaid", "ShopAdmin", new { company = CompanyCode }, Request.Url.Scheme);
            var pay = await new ZarinPalService().RequestPaymentAsync(
                purchase.Amount,
                "خرید " + StorageQuota.FormatMb(purchase.Megabytes) + " فضای سایت " + (CurrentSettings?.StoreName ?? CompanyCode),
                Tenant?.OwnerEmail,
                Tenant?.OwnerMobile,
                purchase.Id.ToString(),
                callback);
            if (!pay.Success)
            {
                StorageQuota.FailPurchase(purchase, pay.Message);
                return Fail("اتصال به درگاه پرداخت انجام نشد: " + pay.Message, "Storage");
            }
            StorageQuota.SetAuthority(purchase, pay.Authority);
            return Redirect(pay.PaymentUrl);
        }

        /// <summary>The gateway's return; open without the panel session so a lost session cannot lose a payment.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> StoragePaid(int? orderId, string Authority, string Status)
        {
            var purchase = orderId.HasValue ? StorageQuota.GetPurchase(orderId.Value) : null;
            if (purchase == null || !string.Equals(purchase.CompanyCode, CompanyCode, StringComparison.OrdinalIgnoreCase))
                return Fail("خرید فضا پیدا نشد.", "Storage");
            if (purchase.Status == OrderStatus.Paid)
                return Ok(StorageQuota.FormatMb(purchase.Megabytes) + " به فضای سایت شما اضافه شده است.", "Storage");

            var verify = await new ZarinPalService().VerifyCallbackAsync(purchase.Authority, Authority, Status, purchase.Amount);
            if (verify.Forged) return Fail("اطلاعات بازگشت از درگاه معتبر نبود.", "Storage");
            if (!verify.Success)
            {
                StorageQuota.FailPurchase(purchase, verify.Message);
                return Fail("پرداخت انجام نشد و فضایی اضافه نشد: " + verify.Message, "Storage");
            }

            if (StorageQuota.CompletePurchase(purchase.Id, verify.RefId, verify.Message))
                NikooSystem.Services.Notifications.StoragePurchase(StorageQuota.GetPurchase(purchase.Id) ?? purchase);
            return Ok("پرداخت انجام شد و " + StorageQuota.FormatMb(purchase.Megabytes) + " به فضای سایت شما اضافه شد. حالا می‌توانید دوباره تصویر و فایل بارگذاری کنید.", "Storage");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CleanStorage()
        {
            var freed = StorageQuota.FreeUnused(CompanyCode);
            return freed > 0
                ? Ok(StorageQuota.Format(freed) + " از فایل‌های بی‌استفاده پاک شد.", "Storage")
                : Ok("فایل بی‌استفاده‌ای پیدا نشد؛ همه فایل‌ها در سایت به کار رفته‌اند.", "Storage");
        }

        /// <summary>
        /// An upload refused for lack of space sends the owner to the purchase page instead of back to the form; a form shown
        /// again with its errors stays, so what was typed is not lost (the panel's banner then offers the purchase).
        /// </summary>
        protected override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            base.OnActionExecuted(filterContext);
            if (!StorageQuota.WasBlocked(HttpContext) || string.IsNullOrEmpty(CompanyCode)) return;

            string back = null;
            var redirect = filterContext.Result as RedirectResult;
            var routeRedirect = filterContext.Result as RedirectToRouteResult;
            if (redirect != null) back = redirect.Url;
            else if (routeRedirect != null) back = Url.RouteUrl(routeRedirect.RouteName, routeRedirect.RouteValues);
            else return;

            if (!string.IsNullOrEmpty(back) && Url.IsLocalUrl(back)) TempData["StorageReturn"] = back;
            filterContext.Result = ToAction("Storage");
        }
    }
}
