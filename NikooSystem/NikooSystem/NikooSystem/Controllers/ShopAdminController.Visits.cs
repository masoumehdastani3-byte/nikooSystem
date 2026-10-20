using System.Web.Mvc;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>«آمار بازدید»: the store's daily and monthly visits, and the IP addresses of its visitors.</summary>
    public partial class ShopAdminController
    {
        [HttpGet]
        public ActionResult Visits()
        {
            ViewBag.IsDemo = TenantStore.IsDemo(CompanyCode);
            return View(VisitStats.Report(CompanyCode));
        }

        [HttpGet]
        public ActionResult VisitIps(GridQuery query, string period = null)
        {
            var days = VisitStats.IpPeriodDays(period);
            var ips = VisitStats.Ips(CompanyCode, query, days);
            ViewBag.IpTotals = VisitStats.IpTotals(CompanyCode, query, days);
            return View(ips);
        }
    }
}
