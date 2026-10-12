using System;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Filters;
using NikooSystem.Helpers;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    [CountVisits]
    public class ProductsController : Controller
    {
        public ActionResult Index(string category = null)
        {
            var products = SiteStore.GetProducts().Where(p => p.IsActive).AsEnumerable();
            if (!string.IsNullOrWhiteSpace(category))
                products = products.Where(p => p.Category == category);

            ViewBag.Category = category;
            ViewBag.Categories = SiteStore.GetProducts().Where(p => p.IsActive).Select(p => p.Category).Distinct().ToList();
            var list = products.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Title).ToList();

            var seo = Seo.Of(HttpContext);
            seo.Description = "محصولات " + SiteContentStore.Settings.SiteName + ": " + string.Join("، ", list.Take(6).Select(p => p.Title))
                + " — با دموی آنلاین، پشتیبانی و راه‌اندازی سریع.";
            seo.Canonical = seo.Url(Url.Action("Index", "Products")
                + (string.IsNullOrWhiteSpace(category) ? "" : "?category=" + Uri.EscapeDataString(category)));
            return View(list);
        }

        public ActionResult Details(int id)
        {
            var product = SiteStore.GetProduct(id);
            if (product == null || !product.IsActive)
                return HttpNotFound();

            var related = SiteStore.GetProducts()
                .Where(p => p.IsActive && p.Id != id && p.Category == product.Category)
                .Take(3)
                .ToList();
            ViewBag.Related = related;

            var site = SiteContentStore.Settings;
            var seo = Seo.Of(HttpContext);
            seo.Title = SeoPages.SiteProductTitle(product, site);
            seo.Description = SeoPages.SiteProductDescription(product);
            seo.Image = product.ImageUrl;
            seo.Type = "product";
            seo.Price = product.Price;
            seo.Canonical = seo.Url(Url.Action("Details", "Products", new { id }));
            seo.Schemas.Add(SeoPages.SiteProduct(seo, product, site));
            seo.Schemas.Add(SeoPages.Breadcrumbs(seo,
                SeoPages.Crumb("خانه", "/"),
                SeoPages.Crumb("محصولات", Url.Action("Index", "Products")),
                SeoPages.Crumb(product.Title, null)));
            return View(product);
        }
    }
}
