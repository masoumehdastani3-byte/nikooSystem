using System.Web.Mvc;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// «دامنه اختصاصی»: the owner enters the domain, follows the DNS guide and sees the automatic check; support's part (binding
    /// and SSL) is requested by <see cref="CustomDomains"/> and shown here as a step.
    /// </summary>
    public partial class ShopAdminController
    {
        [HttpGet]
        public ActionResult Domain()
        {
            ViewBag.StoreUrl = TenantProvisioningService.DefaultStoreUrl(CompanyCode, Request);
            ViewBag.IsDemo = TenantStore.IsDemo(CompanyCode);
            ViewBag.Check = CustomDomains.LastCheck(Tenant);
            return View(Tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Domain(string customDomain, bool remove = false)
        {
            if (Tenant == null) return ToAction("Index");
            var error = SaveCustomDomain(remove ? "" : customDomain);
            if (error != null) return Fail(error, "Domain");

            switch (Tenant.DomainStatus())
            {
                case DomainState.None:
                    return Ok("دامنه اختصاصی حذف شد؛ سایت با آدرس رایگان در دسترس است.", "Domain");
                case DomainState.WaitingSupport:
                    return Ok("دامنه ذخیره شد و DNS آن درست است؛ پشتیبانی نیکو برای فعال‌سازی SSL خبردار شد.", "Domain");
                default:
                    return Ok("دامنه ذخیره شد. حالا رکوردهای DNS جدول پایین را در پنل خرید دامنه بسازید.", "Domain");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DomainRecheck()
        {
            if (Tenant == null || Tenant.DomainStatus() == DomainState.None) return ToAction("Domain");
            var result = CustomDomains.Check(Tenant);
            if (Tenant.DomainStatus() == DomainState.Active)
                return Ok("بررسی شد؛ دامنه فعال است.", "Domain");
            return result.RootOk
                ? Ok("DNS دامنه درست است؛ پشتیبانی نیکو برای فعال‌سازی SSL خبردار شده است.", "Domain")
                : Fail("دامنه هنوز به سرور نیکو اشاره نمی‌کند؛ موارد زیر «وضعیت دامنه» را اصلاح کنید یا اگر تازه تغییر داده‌اید کمی صبر کنید.", "Domain");
        }
    }
}
