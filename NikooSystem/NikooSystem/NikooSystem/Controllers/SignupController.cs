using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>ثبت‌نام خودکار و ساخت فروشگاه آزمایشی بدون نیاز به پشتیبان</summary>
    public class SignupController : Controller
    {
        private const int MaxSignupsPerHour = 3;
        private static readonly Dictionary<string, List<DateTime>> SignupsByIp = new Dictionary<string, List<DateTime>>();

        [HttpGet]
        public ActionResult Index(string type)
        {
            ViewBag.Nav = "signup";
            return View(new StoreSignupVm { StoreType = Helpers.StoreTypes.Get(type).Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(StoreSignupVm model)
        {
            ViewBag.Nav = "signup";
            if (!string.IsNullOrEmpty(model.Website))
                return RedirectToAction("Index", "Home");

            model.StoreType = Helpers.StoreTypes.Get(model.StoreType).Id;
            model.StoreCode = TenantProvisioningService.NormalizeCode(model.StoreCode);

            if (ModelState.IsValidField("StoreCode"))
            {
                var codeError = TenantProvisioningService.ValidateCode(model.StoreCode);
                if (codeError != null) ModelState.AddModelError("StoreCode", codeError);
            }

            if (ModelState.IsValidField("Mobile") && TenantProvisioningService.HasActiveTrial(model.Mobile))
                ModelState.AddModelError("Mobile", "با این شماره یک فروشگاه آزمایشی فعال دارید؛ از «ورود فروشندگان» وارد شوید.");

            if (!ModelState.IsValid)
                return View(model);

            if (!TryRegisterAttempt(Request.UserHostAddress))
            {
                ModelState.AddModelError("", "تعداد درخواست‌های ساخت فروشگاه زیاد است؛ یک ساعت دیگر دوباره تلاش کنید.");
                return View(model);
            }

            TenantInfo tenant;
            try
            {
                tenant = TenantProvisioningService.CreateTrialStore(model);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }

            Session["shop_admin_" + tenant.CompanyCode] = true;
            TempData["Welcome"] = true;
            return RedirectToAction("Setup", "ShopAdmin", new { company = tenant.CompanyCode });
        }

        [HttpGet]
        public JsonResult CheckCode(string code)
        {
            var normalized = TenantProvisioningService.NormalizeCode(code);
            var error = TenantProvisioningService.ValidateCode(normalized);
            return Json(new
            {
                ok = error == null,
                message = error ?? "این آدرس آزاد است.",
                preview = TenantProvisioningService.StorePreviewUrl(normalized)
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Login()
        {
            ViewBag.Nav = "signup";
            return View(new OwnerLoginVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(OwnerLoginVm model)
        {
            ViewBag.Nav = "signup";
            if (!ModelState.IsValid) return View(model);

            var stores = TenantStore.GetTenants()
                .Where(t => TenantStore.IsLive(t) && string.Equals(t.OwnerMobile, model.Mobile, StringComparison.Ordinal))
                .Where(t =>
                {
                    var s = TenantStore.GetSettings(t.CompanyCode);
                    return s != null && PasswordHelper.Verify(model.Password, s.AdminPasswordHash);
                })
                .ToList();

            if (!stores.Any())
            {
                ModelState.AddModelError("", "شماره موبایل یا رمز عبور اشتباه است.");
                return View(model);
            }

            foreach (var t in stores)
                Session["shop_admin_" + t.CompanyCode] = true;

            if (stores.Count == 1)
                return RedirectToAction("Index", "ShopAdmin", new { company = stores[0].CompanyCode });

            return View("ChooseStore", stores);
        }

        private static bool TryRegisterAttempt(string ip)
        {
            ip = ip ?? "unknown";
            var now = DateTime.Now;
            lock (SignupsByIp)
            {
                List<DateTime> list;
                if (!SignupsByIp.TryGetValue(ip, out list))
                    SignupsByIp[ip] = list = new List<DateTime>();
                list.RemoveAll(t => t < now.AddHours(-1));
                if (list.Count >= MaxSignupsPerHour) return false;
                list.Add(now);
                return true;
            }
        }
    }
}
