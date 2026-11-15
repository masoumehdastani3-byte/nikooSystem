using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public partial class ShopAdminController
    {
        /// <summary>Printable Code 128 labels built from each product's code (SKU).</summary>
        [HttpGet]
        [ForKinds(SiteKinds.Shop)]
        public ActionResult ProductBarcodes(int? id)
        {
            ViewBag.FocusId = id;
            return View(TenantStore.GetProducts(CompanyCode).OrderBy(p => p.Title).ToList());
        }
    }
}
