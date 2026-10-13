using System.Linq;
using System.Web.Mvc;
using NikooSystem.Filters;
using NikooSystem.Helpers;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>The landing page of each product group (/online-shop, /course-website, ...; see <see cref="ProductLandings"/>).</summary>
    [CountVisits]
    public class SolutionsController : Controller
    {
        public ActionResult Show(string slug)
        {
            var page = ProductLandings.Get(slug);
            if (page == null) return HttpNotFound();

            var site = SiteContentStore.Settings;
            var active = SiteStore.GetProducts().Where(p => p.IsActive).ToList();
            if (page.Kind == null)
            {
                ViewBag.Products = active.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Id).ToList();
            }
            else
            {
                var productId = TenantProvisioningService.SubscriptionProductId(page.MainType);
                ViewBag.Plan = productId.HasValue ? SiteStore.GetProduct(productId.Value) : null;
                ViewBag.Products = active
                    .Where(p => !string.IsNullOrEmpty(p.DemoSlug) && page.Types.Contains(StoreTypes.Get(p.DemoSlug).Id))
                    .OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Id)
                    .ToList();
            }
            ViewBag.Posts = SiteBlogStore.Related(page.Keywords, 3);
            ViewBag.Nav = "products";

            var seo = Seo.Of(HttpContext);
            seo.Title = ProductLandings.Fill(page.SeoTitle, site.SiteName);
            seo.Description = ProductLandings.Fill(page.Description, site.SiteName);
            seo.Image = page.Image;
            seo.Canonical = seo.Url(Url.RouteUrl("Solution", new { slug = page.Slug }));
            seo.Schemas.Add(SeoPages.Breadcrumbs(seo,
                SeoPages.Crumb("خانه", "/"),
                SeoPages.Crumb("محصولات", Url.Action("Index", "Products")),
                SeoPages.Crumb(page.Name, null)));
            seo.Schemas.Add(SeoPages.Faq(page.Faqs.Select(f => new Models.SiteFaq
            {
                Question = f.Question,
                Answer = ProductLandings.Fill(f.Answer, site.SiteName)
            })));
            return View(page);
        }
    }
}
