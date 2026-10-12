using System.Web.Mvc;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class DemoController : Controller
    {
        public ActionResult Index(string slug)
        {
            string company;
            if (!TenantStore.DemoBySlug.TryGetValue(slug ?? "", out company))
                return HttpNotFound();

            TenantStore.EnsureTenantSeed(company);
            return RedirectToAction("Index", "Shop", new { company });
        }
    }
}
