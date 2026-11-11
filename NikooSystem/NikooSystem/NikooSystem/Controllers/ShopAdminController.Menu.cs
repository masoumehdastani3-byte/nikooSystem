using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public partial class ShopAdminController
    {
        [HttpGet]
        [ForKinds(SiteKinds.Menu)]
        public ActionResult MenuQr()
        {
            ViewBag.MenuUrl = Seo.StoreUrl(Tenant, Request);
            return View();
        }

        [HttpGet]
        [ForKinds(SiteKinds.Menu)]
        public ActionResult WaiterCalls()
        {
            return View(TenantStore.GetWaiterCalls(CompanyCode));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Menu)]
        public ActionResult CloseWaiterCall(int id)
        {
            TenantStore.CloseWaiterCall(CompanyCode, id);
            return RedirectToAction("WaiterCalls", new { company = CompanyCode });
        }
    }
}
