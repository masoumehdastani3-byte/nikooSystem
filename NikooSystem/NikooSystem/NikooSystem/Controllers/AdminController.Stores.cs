using System;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>The stores built on the platform: trials, subscriptions and support actions.</summary>
    public partial class AdminController
    {
        public ActionResult Stores(GridQuery query, string status = null, string type = null)
        {
            return View(TenantStore.TenantGrid(query, status, type));
        }

        public ActionResult Store(string id)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            ViewBag.Settings = TenantStore.GetSettings(store.CompanyCode);
            ViewBag.Counts = TenantStore.TenantCounts(store.CompanyCode);
            ViewBag.Orders = SiteStore.OrdersForStore(store.CompanyCode);
            ViewBag.NewPassword = TempData["NewPassword"] as string;
            ViewBag.Storage = StorageQuota.Usage(store.CompanyCode);
            ViewBag.StoragePurchases = StorageQuota.Purchases(store.CompanyCode);
            return View(store);
        }

        /// <summary>Adds (or, with a negative amount, takes back) upload space: a gift, or space paid for outside the gateway.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GrantStorage(string id, int megabytes, string note)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            megabytes = Math.Max(-1024 * 1024, Math.Min(1024 * 1024, megabytes));
            var granted = StorageQuota.Grant(store.CompanyCode, megabytes, note);
            if (granted > 0) TempData["Ok"] = StorageQuota.FormatMb(granted) + " به فضای «" + store.Name + "» اضافه شد.";
            else if (granted < 0) TempData["Ok"] = StorageQuota.FormatMb(-granted) + " از فضای اضافه «" + store.Name + "» کم شد.";
            else TempData["Error"] = "تغییری داده نشد؛ مقدار را وارد کنید (فضای پایه کم نمی‌شود).";
            return RedirectToAction("Store", new { id = store.CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CleanStoreStorage(string id)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            var freed = StorageQuota.FreeUnused(store.CompanyCode);
            TempData["Ok"] = freed > 0 ? StorageQuota.Format(freed) + " از فایل‌های بی‌استفاده پاک شد." : "فایل بی‌استفاده‌ای پیدا نشد.";
            return RedirectToAction("Store", new { id = store.CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStore(string id, string name, string ownerName, string ownerMobile, string ownerEmail, string customDomain)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();

            var domain = TenantProvisioningService.NormalizeDomain(customDomain);
            var domainError = TenantProvisioningService.ValidateDomain(store.CompanyCode, domain);
            if (string.IsNullOrWhiteSpace(name))
                TempData["Error"] = "نام فروشگاه الزامی است.";
            else if (!string.IsNullOrWhiteSpace(ownerMobile) && !System.Text.RegularExpressions.Regex.IsMatch(ownerMobile.Trim(), @"^09\d{9}$"))
                TempData["Error"] = "شماره موبایل مالک معتبر نیست (مثال: 09121234567).";
            else if (domainError != null)
                TempData["Error"] = domainError;
            else
            {
                store.Name = name.Trim();
                store.OwnerName = (ownerName ?? "").Trim();
                store.OwnerMobile = (ownerMobile ?? "").Trim();
                store.OwnerEmail = (ownerEmail ?? "").Trim();
                TenantStore.UpdateTenant(store);
                CustomDomains.Change(store, domain);
                TempData["Ok"] = "اطلاعات فروشگاه ذخیره شد.";
            }
            return RedirectToAction("Store", new { id = store.CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExtendTrial(string id, int days = 7)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            if (store.IsPaid || TenantStore.IsDemo(store.CompanyCode))
            {
                TempData["Error"] = "این فروشگاه آزمایشی نیست؛ برای تمدید، اشتراک ثبت کنید.";
                return RedirectToAction("Store", new { id });
            }
            days = Math.Max(1, Math.Min(365, days));
            TenantStore.ExtendTrial(store, days);
            TempData["Ok"] = "دوره آزمایشی " + days.ToFa() + " روز تمدید شد؛ پایان: " + store.TrialEndsAt.Value.ToShamsiText();
            return RedirectToAction("Store", new { id = store.CompanyCode });
        }

        /// <summary>Records months of subscription paid outside the gateway (card to card, cash) or given as a gift.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GrantSubscription(string id, int months, string note)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            if (TenantStore.IsDemo(store.CompanyCode))
            {
                TempData["Error"] = "سایت‌های دمو اشتراک ندارند.";
                return RedirectToAction("Store", new { id });
            }
            var order = Subscriptions.Grant(store, months, note);
            if (order == null) TempData["Error"] = "مدت اشتراک معتبر نیست.";
            else TempData["Ok"] = "اشتراک " + Subscriptions.PeriodName(months) + " برای «" + store.Name + "» ثبت شد؛ پایان اشتراک: " + order.SubscriptionEndsAt?.ToShamsiText();
            return RedirectToAction("Store", new { id = store.CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleStore(string id)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            store.IsActive = !store.IsActive;
            TenantStore.UpdateTenant(store);
            TempData["Ok"] = store.IsActive ? "فروشگاه دوباره فعال شد." : "فروشگاه تعلیق شد؛ سایت و پنل آن تا فعال‌سازی دوباره در دسترس نیست.";
            return RedirectToAction("Store", new { id = store.CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetStorePassword(string id)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            var password = RandomPassword();
            TenantStore.SetStoreAdminPassword(store.CompanyCode, password);
            TempData["NewPassword"] = password;
            TempData["Ok"] = "رمز پنل فروشگاه عوض شد؛ رمز جدید را همین حالا به مالک بدهید (دوباره نمایش داده نمی‌شود).";
            return RedirectToAction("Store", new { id = store.CompanyCode });
        }

        /// <summary>Opens the store's own panel for support, without its password.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EnterStore(string id)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            if (!TenantStore.IsLive(store))
            {
                TempData["Error"] = "فروشگاه فعال نیست؛ ابتدا آن را فعال یا تمدید کنید.";
                return RedirectToAction("Store", new { id });
            }
            Session["shop_admin_" + store.CompanyCode] = true;
            return RedirectToAction("Index", "ShopAdmin", new { company = store.CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteStore(string id, string confirmCode)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            if (TenantStore.IsDemo(store.CompanyCode))
            {
                TempData["Error"] = "سایت‌های دمو حذف نمی‌شوند.";
                return RedirectToAction("Store", new { id });
            }
            if (!string.Equals((confirmCode ?? "").Trim(), store.CompanyCode, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "برای حذف، کد فروشگاه (" + store.CompanyCode + ") را دقیق وارد کنید.";
                return RedirectToAction("Store", new { id });
            }
            TenantStore.DeleteStore(store.CompanyCode);
            TempData["Ok"] = "فروشگاه «" + store.Name + "» و همه داده‌هایش حذف شد.";
            return RedirectToAction("Stores");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PurgeExpiredStores()
        {
            var count = TenantProvisioningService.PurgeExpiredTrials();
            TempData["Ok"] = count == 0 ? "فروشگاه آزمایشی منقضی‌ای نبود." : count.ToFa() + " فروشگاه آزمایشی منقضی حذف شد.";
            return RedirectToAction("Stores");
        }

        private static string RandomPassword()
        {
            const string chars = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = new byte[10];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }
    }
}
