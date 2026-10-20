using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// «دامنه‌های مشتریان»: support's side of connecting stores' own domains — the server IP customers are given, the DNS
    /// state of every domain and the binding and SSL steps support does before marking a domain active.
    /// </summary>
    public partial class AdminController
    {
        public ActionResult Domains(GridQuery query)
        {
            ViewBag.ServerIpSetting = SiteContentStore.Settings.ServerIp;
            var all = CustomDomains.WithDomains();
            ViewBag.WaitingSupport = all.Count(t => t.DomainStatus() == DomainState.WaitingSupport);
            ViewBag.WaitingDns = all.Count(t => t.DomainStatus() == DomainState.WaitingDns);
            ViewBag.Active = all.Count(t => t.DomainStatus() == DomainState.Active);
            return View(query.Slice(all,
                t => t.Name + " " + t.CompanyCode + " " + t.CustomDomain,
                new Dictionary<string, Func<TenantInfo, object>>
                {
                    ["name"] = t => t.Name,
                    ["domain"] = t => t.CustomDomain,
                    ["status"] = t => DomainRank(t.DomainStatus()),
                    ["checked"] = t => CustomDomains.LastCheck(t) == null ? (DateTime?)null : CustomDomains.LastCheck(t).CheckedAt,
                    ["set"] = t => t.DomainSetAt
                }, "status"));
        }

        /// <summary>Waiting for SSL first, then DNS, then active — the order support works through.</summary>
        private static int DomainRank(DomainState state)
        {
            switch (state)
            {
                case DomainState.WaitingSupport: return 0;
                case DomainState.WaitingDns: return 1;
                case DomainState.Active: return 2;
                default: return 3;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveServerIp(string serverIp)
        {
            var ips = (serverIp ?? "").Split(new[] { ',', ' ', ';', '\n', '\r', '،' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(ip => ip.Trim()).Distinct().ToList();
            var invalid = ips.FirstOrDefault(ip => !IPAddress.TryParse(ip, out var parsed) || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork);
            if (invalid != null)
            {
                TempData["Error"] = "«" + invalid + "» یک IPv4 معتبر نیست (نمونه: 185.10.20.30).";
                return RedirectToAction("Domains");
            }
            SiteContentStore.SaveServerIp(string.Join(", ", ips));
            TempData["Ok"] = ips.Count == 0
                ? "IP پاک شد؛ مقدار Web.config (اگر باشد) به مشتریان نشان داده می‌شود."
                : "IP سرور ذخیره شد و از این پس در راهنمای اتصال دامنه به مشتریان نشان داده می‌شود.";
            return RedirectToAction("Domains");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckDomain(string id)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            var result = CustomDomains.Check(store);
            if (result == null) TempData["Error"] = "این فروشگاه دامنه‌ای ثبت نکرده است.";
            else if (result.RootOk) TempData["Ok"] = "DNS دامنه " + result.Domain + " درست است" + (result.WwwOk ? "." : "؛ www هنوز تنظیم نشده است.");
            else TempData["Error"] = "دامنه " + result.Domain + " هنوز به سرور اشاره نمی‌کند: " + string.Join(" ", result.Problems);
            return RedirectToAction("Domains");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ActivateDomain(string id)
        {
            var store = TenantStore.GetTenantForAdmin(id);
            if (store == null) return HttpNotFound();
            if (string.IsNullOrWhiteSpace(store.CustomDomain))
            {
                TempData["Error"] = "این فروشگاه دامنه‌ای ثبت نکرده است.";
                return RedirectToAction("Domains");
            }
            CustomDomains.Activate(store);
            TempData["Ok"] = "دامنه " + store.CustomDomain + " فعال شد و به مالک «" + store.Name + "» خبر داده شد.";
            return RedirectToAction("Domains");
        }
    }
}
