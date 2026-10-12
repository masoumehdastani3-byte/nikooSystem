using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Web.UI;
using System.Xml.Linq;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// robots.txt and sitemap.xml of the main site and of every site. On a site's own domain /robots.txt and /sitemap.xml
    /// arrive here as /Shop/CODE/... (see Global.asax), so each domain gets its own files.
    /// </summary>
    public class SeoController : Controller
    {
        private static readonly XNamespace Sm = "http://www.sitemaps.org/schemas/sitemap/0.9";
        private static readonly XNamespace Img = "http://www.google.com/schemas/sitemap-image/1.1";
        private const int MaxUrls = 45000;

        /// <summary>Pages of sites that are not for search results; on a site's own domain they have no /Shop/CODE prefix.</summary>
        private static readonly string[] PrivateStorePages =
        {
            "Cart", "Checkout", "PaymentCallback", "OrderDone", "BookingDays", "BookingSlots", "BookingCallback", "Appointment", "Track"
        };

        [OutputCache(Duration = 1800, VaryByParam = "none", VaryByHeader = "Host", Location = OutputCacheLocation.Server)]
        public ActionResult Robots()
        {
            var site = Seo.SiteUrl(Request);
            var lines = new List<string>
            {
                "User-agent: *",
                "Disallow: /Admin",
                "Disallow: /ShopAdmin/",
                "Disallow: /ShopAccount/",
                "Disallow: /Checkout/",
                "Disallow: /Pay/Start/",
                "Disallow: /Pay/Done",
                "Disallow: /Pay/Callback",
                "Disallow: /api/",
                "Disallow: /torob_api/",
                "Disallow: /Signup/Login",
                "Disallow: /*previewTemplate="
            };
            lines.AddRange(PrivateStorePages.Select(p => "Disallow: /Shop/*/" + p));
            lines.Add("");
            lines.Add("Sitemap: " + site + "/sitemap.xml");
            return Text(lines);
        }

        [OutputCache(Duration = 1800, VaryByParam = "none", VaryByHeader = "Host", Location = OutputCacheLocation.Server)]
        public ActionResult StoreRobots(string company)
        {
            var tenant = TenantStore.GetTenant(company);
            if (tenant == null) return HttpNotFound();
            var seo = StoreSeo(tenant);
            var prefix = "/Shop/" + tenant.CompanyCode + "/";
            var lines = new List<string> { "User-agent: *" };
            if (TenantStore.IsDemo(tenant.CompanyCode))
            {
                lines.Add("Disallow: /");
                return Text(lines);
            }
            lines.AddRange(new[] { "Disallow: /ShopAdmin/", "Disallow: /ShopAccount/", "Disallow: /*previewTemplate=" });
            foreach (var page in PrivateStorePages)
            {
                lines.Add("Disallow: /" + page);
                lines.Add("Disallow: " + prefix + page);
            }
            lines.Add("");
            lines.Add("Sitemap: " + seo.Url(prefix + "sitemap.xml"));
            return Text(lines);
        }

        /// <summary>The main sitemap: the main site's pages and the sites that live under /Shop/CODE of this domain.</summary>
        [OutputCache(Duration = 1800, VaryByParam = "none", VaryByHeader = "Host", Location = OutputCacheLocation.Server)]
        public ActionResult Sitemap()
        {
            var site = Seo.SiteUrl(Request);
            var maps = new List<string> { site + Url.Action("Pages", "Seo") };
            foreach (var tenant in TenantStore.GetTenants().Where(t => TenantStore.IsLive(t) && !t.IsSubscriptionLocked() && !TenantStore.IsDemo(t.CompanyCode)))
            {
                var root = Seo.StoreUrl(tenant, Request);
                if (root.StartsWith(site + "/Shop/", StringComparison.OrdinalIgnoreCase))
                    maps.Add(root + "/sitemap.xml");
            }
            var index = new XElement(Sm + "sitemapindex", maps.Select(m => new XElement(Sm + "sitemap", new XElement(Sm + "loc", m))));
            return Xml(index);
        }

        [OutputCache(Duration = 1800, VaryByParam = "none", VaryByHeader = "Host", Location = OutputCacheLocation.Server)]
        public ActionResult Pages()
        {
            var site = Seo.SiteUrl(Request);
            var main = new SeoInfo { BaseUrl = site };
            var urls = new List<XElement>
            {
                UrlEntry(site + "/"),
                UrlEntry(site + Url.Action("Index", "Products")),
                UrlEntry(site + Url.Action("Index", "Signup")),
                UrlEntry(site + Url.Action("About", "Home")),
                UrlEntry(site + Url.Action("Contact", "Home")),
                UrlEntry(site + Url.Action("Guide", "Pay")),
                UrlEntry(site + Url.Action("BuyGuide", "Home")),
                UrlEntry(site + Url.Action("Terms", "Home")),
                UrlEntry(site + Url.Action("Privacy", "Home"))
            };
            foreach (var product in SiteStore.GetProducts().Where(p => p.IsActive).OrderBy(p => p.Id))
                urls.Add(UrlEntry(site + Url.Action("Details", "Products", new { id = product.Id }), product.CreatedAt,
                    Images(main.Asset, product.ImageUrl)));
            return UrlSet(urls);
        }

        [OutputCache(Duration = 1800, VaryByParam = "none", VaryByHeader = "Host", Location = OutputCacheLocation.Server)]
        public ActionResult StoreSitemap(string company)
        {
            var tenant = TenantStore.GetTenant(company);
            if (tenant == null || TenantStore.IsDemo(tenant.CompanyCode) || tenant.IsSubscriptionLocked()) return HttpNotFound();
            var code = tenant.CompanyCode;
            var kind = SiteKinds.Of(tenant);
            var seo = StoreSeo(tenant);
            Func<string, string> asset = seo.Asset;
            string ShopUrl(string action) => seo.Url(Url.Action(action, "Shop", new { company = code }));

            var urls = new List<XElement> { UrlEntry(seo.Url(Url.Action("Index", "Shop", new { company = code }))) };
            var categories = TenantStore.GetCategories(code).Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToList();

            if (kind.IsContent)
            {
                urls.Add(UrlEntry(ShopUrl("Blog")));
                urls.AddRange(categories.Select(c => UrlEntry(ShopUrl("Blog") + "?categoryId=" + c.Id, null, Images(asset, c.ImageUrl))));
                urls.AddRange(TenantStore.TopPosts(code, MaxUrls).Select(p => UrlEntry(
                    seo.Url(Url.RouteUrl("ShopPost", new { company = code, slug = p.Slug })), p.UpdatedAt ?? p.PublishedAt,
                    Images(asset, p.CoverImageUrl))));
            }
            else if (kind.IsBooking)
            {
                urls.Add(UrlEntry(ShopUrl("Services")));
                urls.Add(UrlEntry(ShopUrl("Book")));
            }
            else
            {
                urls.Add(UrlEntry(ShopUrl("Catalog")));
                urls.AddRange(categories.Select(c => UrlEntry(ShopUrl("Catalog") + "?categoryId=" + c.Id, null, Images(asset, c.ImageUrl))));
                urls.AddRange(TenantStore.GetCollections(code).Where(c => c.IsActive).OrderBy(c => c.SortOrder)
                    .Select(c => UrlEntry(ShopUrl("Catalog") + "?collectionId=" + c.Id)));
                urls.AddRange(TenantStore.GetProducts(code).Where(p => p.IsActive).OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt).Select(p => UrlEntry(
                    seo.Url(Url.RouteUrl("ShopProduct", new { company = code, slug = p.Slug })), p.UpdatedAt ?? p.CreatedAt,
                    Images(asset, (p.Images ?? new List<StoreProductImage>()).OrderBy(i => i.SortOrder).Select(i => i.LargeUrl ?? i.ThumbUrl).ToArray()))));
            }

            urls.AddRange(TenantStore.GetPages(code).Where(p => p.IsPublished).Select(p => UrlEntry(
                seo.Url(Url.RouteUrl("ShopPage", new { company = code, slug = p.Slug })), p.UpdatedAt)));
            if (TenantStore.GetGallery(code).Any()) urls.Add(UrlEntry(ShopUrl("Gallery")));
            urls.Add(UrlEntry(ShopUrl("Contact")));
            return UrlSet(urls.Take(MaxUrls));
        }

        private SeoInfo StoreSeo(TenantInfo tenant)
        {
            var seo = new SeoInfo();
            Seo.ForStore(seo, tenant, Request);
            return seo;
        }

        private static IEnumerable<XElement> Images(Func<string, string> absolute, params string[] urls) =>
            urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().Take(10)
                .Select(u => new XElement(Img + "image", new XElement(Img + "loc", absolute(u))));

        private static XElement UrlEntry(string loc, DateTime? modified = null, IEnumerable<XElement> images = null) =>
            new XElement(Sm + "url",
                new XElement(Sm + "loc", loc),
                modified.HasValue ? new XElement(Sm + "lastmod", SeoPages.Iso(modified.Value)) : null,
                images);

        private ActionResult UrlSet(IEnumerable<XElement> urls) =>
            Xml(new XElement(Sm + "urlset", new XAttribute(XNamespace.Xmlns + "image", Img), urls));

        private ActionResult Xml(XElement root)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            return Content(doc.Declaration + Environment.NewLine + doc.Root, "application/xml", Encoding.UTF8);
        }

        private ActionResult Text(IEnumerable<string> lines) =>
            Content(string.Join("\n", lines) + "\n", "text/plain", Encoding.UTF8);
    }
}
