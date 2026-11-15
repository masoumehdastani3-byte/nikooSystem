using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Filters;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>ثبت‌نام خودکار و ساخت فروشگاه آزمایشی بدون نیاز به پشتیبان</summary>
    [CountVisits]
    public class SignupController : Controller
    {
        private const int MaxSignupsPerHour = 3;
        private static readonly Dictionary<string, List<DateTime>> SignupsByIp = new Dictionary<string, List<DateTime>>();

        [HttpGet]
        public ActionResult Index(string type, string template)
        {
            ViewBag.Nav = "signup";
            Seo.Of(HttpContext).Description = "ساخت رایگان فروشگاه اینترنتی، سایت خبری یا شرکتی و سامانه نوبت‌دهی آنلاین در چند دقیقه؛ بدون نیاز به برنامه‌نویسی، با درگاه پرداخت و پشتیبانی.";
            var chosen = StoreTemplates.All.FirstOrDefault(t => t.Id == template);
            var resolvedType = type;
            if (string.IsNullOrWhiteSpace(resolvedType))
                resolvedType = GuessTypeFromReferrer(Request?.UrlReferrer);
            ViewBag.PrefilledStoreType = !string.IsNullOrWhiteSpace(resolvedType);
            return View(new StoreSignupVm { StoreType = Helpers.StoreTypes.Get(resolvedType).Id, Template = chosen?.Id });
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
            {
                AlignStoreNameErrors(ModelState, model.StoreType);
                return View(model);
            }

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

            Notifications.Signup(tenant);
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
        [NoIndex]
        public ActionResult Login(string company = null)
        {
            ViewBag.Nav = "signup";
            company = (company ?? "").Trim();
            if (string.IsNullOrEmpty(company))
                company = (Request.Cookies["nk_last_company"]?.Value ?? "").Trim();

            if (!string.IsNullOrEmpty(company))
            {
                var tenant = TenantStore.GetTenant(company);
                if (tenant != null)
                    return RedirectToAction("Login", "ShopAdmin", new { company = tenant.CompanyCode });
            }
            return View(new OwnerLoginVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(string mobile)
        {
            ViewBag.Nav = "signup";
            mobile = BookingTime.NormalizeMobile(mobile);
            if (mobile == null)
            {
                ModelState.AddModelError("", "شماره موبایل معتبر نیست.");
                return View("Login", new OwnerLoginVm());
            }

            var stores = TenantStore.GetTenants()
                .Where(t => TenantStore.IsLive(t) && string.Equals(BookingTime.NormalizeMobile(t.OwnerMobile), mobile, StringComparison.Ordinal))
                .ToList();
            if (!stores.Any())
            {
                ModelState.AddModelError("", "با این شماره، سایت فعالی پیدا نشد. اگر دوره آزمایشی تمام شده، از لینک ورود همان سایت رمز را بازیابی کنید.");
                return View("Login", new OwnerLoginVm { Mobile = mobile });
            }

            var temporary = PasswordHelper.GenerateTemporaryPassword();
            var hash = PasswordHelper.Hash(temporary);
            var previous = new Dictionary<StoreSettings, string>();
            foreach (var store in stores)
            {
                var settings = TenantStore.GetSettings(store.CompanyCode);
                if (settings == null) continue;
                previous[settings] = settings.AdminPasswordHash;
                settings.AdminPasswordHash = hash;
                TenantStore.SaveSettings(settings);
            }
            if (previous.Count == 0)
            {
                ModelState.AddModelError("", "تنظیمات سایت پیدا نشد.");
                return View("Login", new OwnerLoginVm { Mobile = mobile });
            }

            var sms = await SendOwnerResetSms(stores, mobile, temporary);
            var baleError = sms.Success ? null : await SendOwnerResetBale(stores, mobile, temporary);
            if (!sms.Success && baleError != null)
            {
                foreach (var item in previous)
                {
                    item.Key.AdminPasswordHash = item.Value;
                    TenantStore.SaveSettings(item.Key);
                }
                ModelState.AddModelError("", "ارسال رمز ممکن نشد. پیامک: " + (sms.Error ?? "ناموفق")
                    + (string.IsNullOrEmpty(baleError) ? "" : " — بله: " + baleError));
                return View("Login", new OwnerLoginVm { Mobile = mobile });
            }

            TempData["Ok"] = sms.Success
                ? (stores.Count == 1 ? "رمز موقت مدیر پیامک شد." : "رمز موقت برای همه سایت‌های این موبایل پیامک شد.")
                : "رمز موقت در بله ارسال شد.";
            return RedirectToAction("Login");
        }

        private static async Task<SmsSendResult> SendOwnerResetSms(IList<TenantInfo> stores, string mobile, string temporary)
        {
            var names = string.Join("، ", stores.Select(t => string.IsNullOrWhiteSpace(t.Name) ? t.CompanyCode : t.Name).Take(3));
            if (stores.Count > 3) names += " و …";
            var text = new StringBuilder()
                .AppendLine("بازیابی رمز عبور")
                .AppendLine((stores.Count > 1 ? "سایت‌ها: " : "سایت: ") + names)
                .AppendLine("رمز موقت: " + temporary)
                .Append("پس از ورود، رمز را عوض کنید.")
                .ToString();

            SmsSendResult last = SmsSendResult.Fail("ارسال پیامک ممکن نشد.");
            foreach (var store in stores)
            {
                last = await SmsService.SendAsync(store.CompanyCode, mobile, text, SmsService.KindPassword, "pwd-" + store.CompanyCode);
                if (last.Success) return last;
                if (last.Error == null || last.Error.IndexOf("اعتبار", StringComparison.Ordinal) < 0)
                    return last;
            }
            return last;
        }

        /// <summary>وقتی پیامک نرود، رمز را به اولین سایتِ این مالک که بله وصل دارد می‌فرستد. null یعنی ارسال شد.</summary>
        private static async Task<string> SendOwnerResetBale(IList<TenantInfo> stores, string mobile, string temporary)
        {
            string last = "هیچ حساب بله‌ای برای این سایت‌ها وصل نشده است.";
            foreach (var store in stores)
            {
                if (BaleNotifier.Recipients(store.CompanyCode).Count == 0) continue;
                var text = "🔐 بازیابی رمز مدیر\n"
                    + "سایت: " + (string.IsNullOrWhiteSpace(store.Name) ? store.CompanyCode : store.Name) + "\n"
                    + "موبایل مالک: " + mobile + "\n"
                    + "رمز موقت جدید: " + temporary + "\n"
                    + "این رمز برای همه سایت‌های همین موبایل است. بعد از ورود عوضش کنید.";
                last = await BaleNotifier.SendToRecipientsAsync(store.CompanyCode, text);
                if (last == null) return null;
            }
            return last;
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

        private static void AlignStoreNameErrors(ModelStateDictionary state, string storeType)
        {
            var noun = StoreTypes.Get(storeType).NameNoun;
            if (string.IsNullOrWhiteSpace(noun) || noun == "فروشگاه") return;
            foreach (var key in new[] { "StoreName", "StoreCode" })
            {
                var entry = state[key];
                if (entry == null || entry.Errors.Count == 0) continue;
                var messages = entry.Errors.Select(e => (e.ErrorMessage ?? "").Replace("فروشگاه", noun)).ToList();
                entry.Errors.Clear();
                foreach (var message in messages)
                    if (!string.IsNullOrEmpty(message))
                        state.AddModelError(key, message);
            }
        }

        private static string GuessTypeFromReferrer(Uri referrer)
        {
            var path = (referrer?.AbsolutePath ?? "").Trim('/');
            if (string.IsNullOrEmpty(path) || path.Contains("/")) return null;
            return ProductLandings.Get(path)?.MainType;
        }
    }
}
