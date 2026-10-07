using System;
using System.Web.Mvc;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class DemoController : Controller
    {
        public ActionResult Index(string slug)
        {
            string company;
            switch ((slug ?? "").ToLowerInvariant())
            {
                case "fashion":
                    company = "FASHION";
                    break;
                case "grocery":
                    company = "GROCERY";
                    break;
                case "digital":
                    company = "DIGITAL";
                    break;
                default:
                    return HttpNotFound();
            }

            TenantStore.EnsureTenantSeed(company);
            return RedirectToAction("Index", "Shop", new { company });
        }
    }
}
