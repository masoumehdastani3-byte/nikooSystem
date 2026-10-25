using System.Linq;
using System.Web.Mvc;
using NikooSystem.Filters;
using NikooSystem.Helpers;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Free online tools on the main site (/tools).</summary>
    [CountVisits]
    public class ToolsController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Nav = "tools";
            var site = SiteContentStore.Settings;
            var seo = Seo.Of(HttpContext);
            seo.Title = "ابزارهای رایگان آنلاین" + SeoPages.Separator + site.SiteName;
            seo.Description = "ابزارهای رایگان نیکو: مبدل تاریخ شمسی و میلادی، محاسبه سن، ساخت QR کد و فشرده‌سازی تصویر؛ بدون ثبت‌نام و در مرورگر.";
            seo.Canonical = seo.Url(Url.RouteUrl("Tools"));
            seo.Schemas.Add(SeoPages.Breadcrumbs(seo,
                SeoPages.Crumb("خانه", "/"),
                SeoPages.Crumb("ابزارهای رایگان", null)));
            return View(SiteTools.All);
        }

        public ActionResult Show(string slug)
        {
            var tool = SiteTools.Get(slug);
            if (tool == null) return HttpNotFound();

            ViewBag.Nav = "tools";
            var site = SiteContentStore.Settings;
            var seo = Seo.Of(HttpContext);
            seo.Title = tool.SeoTitle + SeoPages.Separator + site.SiteName;
            seo.Description = tool.Description;
            seo.Canonical = seo.Url(Url.RouteUrl("Tool", new { slug = tool.Slug }));
            seo.Schemas.Add(SeoPages.Breadcrumbs(seo,
                SeoPages.Crumb("خانه", "/"),
                SeoPages.Crumb("ابزارهای رایگان", Url.RouteUrl("Tools")),
                SeoPages.Crumb(tool.Name, null)));
            seo.Schemas.Add(SeoPages.FreeWebTool(seo, tool.Name, tool.Description, seo.Canonical));
            if (tool.Faqs != null)
                seo.Schemas.Add(SeoPages.Faq(tool.Faqs));
            return View(tool);
        }
    }
}
