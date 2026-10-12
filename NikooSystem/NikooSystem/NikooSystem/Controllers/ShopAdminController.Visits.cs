using System.Web.Mvc;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>«آمار بازدید»: the store's daily and monthly visits.</summary>
    public partial class ShopAdminController
    {
        [HttpGet]
        public ActionResult Visits()
        {
            ViewBag.IsDemo = TenantStore.IsDemo(CompanyCode);
            return View(VisitStats.Report(CompanyCode));
        }
    }
}
