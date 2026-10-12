using System.Web.Mvc;
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
        public ActionResult Visits(string site = null)
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
            return View(VisitStats.StoreRanking());
        }
    }
}
