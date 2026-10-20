using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>The payment hub: the apps allowed to take payments through it and their transactions.</summary>
    public partial class AdminController
    {
        public ActionResult PaymentApps(GridQuery query)
        {
            var stats = SiteStore.HubStatsByApp();
            ViewBag.Stats = stats;
            return View(query.Slice(SiteStore.GetPaymentClients(),
                c => c.Name + " " + c.AppId + " " + string.Join(" ", c.AllowedReturnHosts ?? new string[0]),
                new Dictionary<string, Func<PaymentClient, object>>
                {
                    ["name"] = c => c.Name,
                    ["app"] = c => c.AppId,
                    ["count"] = c => Stat(stats, c.AppId).PaidCount,
                    ["amount"] = c => Stat(stats, c.AppId).PaidAmount
                }, "name"));
        }

        private static HubAppStats Stat(Dictionary<string, HubAppStats> stats, string appId)
        {
            HubAppStats stat;
            return stats != null && stats.TryGetValue(appId ?? "", out stat) ? stat : new HubAppStats();
        }

        [HttpGet]
        public ActionResult EditPaymentApp(string id)
        {
            if (string.IsNullOrEmpty(id))
                return View(new PaymentClientForm { IsNew = true });

            var client = SiteStore.GetPaymentClient(id);
            if (client == null) return HttpNotFound();
            return View(new PaymentClientForm
            {
                AppId = client.AppId,
                Name = client.Name,
                ApiKey = client.ApiKey,
                AllowedHosts = string.Join("\n", client.AllowedReturnHosts ?? new string[0])
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPaymentApp(PaymentClientForm form)
        {
            form.AppId = (form.AppId ?? "").Trim().ToLowerInvariant();
            var hosts = (form.AllowedHosts ?? "")
                .Split(new[] { '\n', '\r', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeReturnHost)
                .Where(h => h.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            PaymentClient existing = null;
            if (!form.IsNew)
            {
                existing = SiteStore.GetPaymentClient(form.AppId);
                if (existing == null) return HttpNotFound();
            }
            else if (ModelState.IsValidField("AppId") && SiteStore.GetPaymentClient(form.AppId) != null)
            {
                ModelState.AddModelError("AppId", "اپلیکیشنی با این شناسه وجود دارد.");
            }

            if (!ModelState.IsValid)
            {
                form.ApiKey = existing?.ApiKey;
                return View(form);
            }

            if (form.IsNew)
            {
                var client = new PaymentClient { AppId = form.AppId, Name = form.Name.Trim(), ApiKey = SiteStore.NewApiKey(), AllowedReturnHosts = hosts };
                SiteStore.AddPaymentClient(client);
                TempData["Ok"] = "اپلیکیشن ساخته شد. کلید API را در تنظیمات پروژه مقصد قرار دهید.";
            }
            else
            {
                existing.Name = form.Name.Trim();
                existing.AllowedReturnHosts = hosts;
                SiteStore.UpdatePaymentClient(existing);
                TempData["Ok"] = "اپلیکیشن ذخیره شد.";
            }
            return RedirectToAction("EditPaymentApp", new { id = form.AppId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegenerateApiKey(string id)
        {
            var client = SiteStore.GetPaymentClient(id);
            if (client == null) return HttpNotFound();
            client.ApiKey = SiteStore.NewApiKey();
            SiteStore.UpdatePaymentClient(client);
            TempData["Ok"] = "کلید API جدید ساخته شد؛ کلید قبلی دیگر کار نمی‌کند و باید در پروژه مقصد جایگزین شود.";
            return RedirectToAction("EditPaymentApp", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeletePaymentApp(string id)
        {
            SiteStore.DeletePaymentClient(id);
            TempData["Ok"] = "اپلیکیشن حذف شد؛ تراکنش‌های قبلی آن در فهرست تراکنش‌ها می‌مانند.";
            return RedirectToAction("PaymentApps");
        }

        public ActionResult HubPayments(GridQuery query, string app = null, HubPaymentStatus? status = null)
        {
            ViewBag.Apps = SiteStore.GetPaymentClients();
            return View(SiteStore.HubPaymentGrid(query, app, status));
        }

        /// <summary>A host name as the hub compares it (no scheme, path or port); "*.example.ir" allows every subdomain.</summary>
        private static string NormalizeReturnHost(string input)
        {
            var host = (input ?? "").Trim().ToLowerInvariant();
            var scheme = host.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0) host = host.Substring(scheme + 3);
            var cut = host.IndexOfAny(new[] { '/', '?', '#', ':' });
            if (cut >= 0) host = host.Substring(0, cut);
            return host.TrimEnd('.');
        }

        public ActionResult HubPayment(int id)
        {
            var payment = SiteStore.GetHubPayment(id);
            if (payment == null) return HttpNotFound();
            ViewBag.App = SiteStore.GetPaymentClient(payment.AppId);
            return View(payment);
        }
    }
}
