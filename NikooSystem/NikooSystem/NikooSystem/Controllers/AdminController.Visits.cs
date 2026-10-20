using System;
using System.Collections.Generic;
using System.Web.Mvc;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// «آمار بازدید»: visits of Nikoo's own site, of all the customers' sites together and the stores ranked by visits;
    /// <c>?site=CODE</c> shows the report of one store.
    /// </summary>
    public partial class AdminController
    {
        [HttpGet]
        public ActionResult Visits(GridQuery query, string site = null)
        {
            if (!string.IsNullOrWhiteSpace(site))
            {
                var tenant = TenantStore.GetTenant(site.Trim());
                if (tenant == null) return HttpNotFound();
                ViewBag.Store = tenant;
                return View("StoreVisits", VisitStats.Report(tenant.CompanyCode));
            }

            ViewBag.SiteVisits = VisitStats.Report(VisitStats.MainSite);
            ViewBag.StoreVisits = VisitStats.Report(null);
            return View(query.Slice(VisitStats.StoreRanking(),
                s => s.Name + " " + s.CompanyCode,
                new Dictionary<string, Func<StoreVisitRow, object>>
                {
                    ["name"] = s => s.Name,
                    ["today"] = s => s.TodayViews,
                    ["month"] = s => s.MonthViews,
                    ["visitors"] = s => s.MonthVisitors
                }, "month", true));
        }

        /// <summary>The IP addresses that visited Nikoo's site, or the store <c>?site=CODE</c>.</summary>
        [HttpGet]
        public ActionResult VisitIps(GridQuery query, string site = null, string period = null)
        {
            var code = VisitStats.MainSite;
            if (!string.IsNullOrWhiteSpace(site))
            {
                var tenant = TenantStore.GetTenant(site.Trim());
                if (tenant == null) return HttpNotFound();
                ViewBag.Store = tenant;
                code = tenant.CompanyCode;
            }

            var days = VisitStats.IpPeriodDays(period);
            var ips = VisitStats.Ips(code, query, days);
            ViewBag.IpTotals = VisitStats.IpTotals(code, query, days);
            return View(ips);
        }
    }
}
