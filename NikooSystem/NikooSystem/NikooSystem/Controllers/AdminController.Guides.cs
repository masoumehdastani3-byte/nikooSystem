using System;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>«راهنمای سئو»: what is automatic, the main site's checklist and how each customer site reaches Google and Torob.</summary>
    public partial class AdminController
    {
        [HttpGet]
        public ActionResult SeoGuide()
        {
            var site = Seo.SiteUrl(Request);
            ViewBag.SiteUrlSetting = (ConfigurationManager.AppSettings["Seo.SiteUrl"] ?? "").Trim();
            ViewBag.SiteUrl = site;
            ViewBag.Stores = TenantStore.GetTenants()
                .Where(t => TenantStore.IsLive(t) && !TenantStore.IsDemo(t.CompanyCode))
                .OrderBy(t => t.CompanyCode)
                .Select(t =>
                {
                    var root = Seo.StoreUrl(t, Request);
                    var settings = TenantStore.GetSettings(t.CompanyCode);
                    var kind = SiteKinds.Of(t);
                    return new SeoGuideStore
                    {
                        Code = t.CompanyCode,
                        Name = settings?.StoreName ?? t.Name,
                        Kind = kind.Name,
                        Root = root,
                        Sitemap = root + "/sitemap.xml",
                        InMainSitemap = root.StartsWith(site + "/Shop/", StringComparison.OrdinalIgnoreCase),
                        HasVerification = !string.IsNullOrEmpty(settings?.GoogleVerification),
                        Torob = kind.IsShop ? settings?.EnableTorob == true : (bool?)null
                    };
                })
                .ToList();
            return View(SiteContentStore.Settings);
        }
    }
}

namespace NikooSystem.Models
{
    /// <summary>A customer site on the SEO guide: where its sitemap is and whether its owner did their part.</summary>
    public sealed class SeoGuideStore
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Root { get; set; }
        public string Sitemap { get; set; }
        /// <summary>Lives under /Shop/CODE of the main domain, so the main sitemap lists it; otherwise it is submitted on its own domain.</summary>
        public bool InMainSitemap { get; set; }
        public bool HasVerification { get; set; }
        /// <summary>Torob switched on; null for sites that are not shops.</summary>
        public bool? Torob { get; set; }
    }
}
