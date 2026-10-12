using System;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;
using NikooSystem.Helpers;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// The store's guide pages: «سئو و گوگل» (what is automatic, the store's own checklist and Search Console steps) and
    /// «اتصال به ترب» (the Torob product API: settings, the address to register and a preview of what Torob receives).
    /// </summary>
    public partial class ShopAdminController
    {
        [HttpGet]
        public ActionResult SeoGuide()
        {
            var seo = Seo.Of(HttpContext);
            ViewBag.Home = seo.Url("/Shop/" + CompanyCode);
            ViewBag.Sitemap = seo.Url("/Shop/" + CompanyCode + "/sitemap.xml");
            ViewBag.Robots = seo.Url("/Shop/" + CompanyCode + "/robots.txt");
            ViewBag.IsDemo = TenantStore.IsDemo(CompanyCode);
            ViewBag.WithoutImage = Kind.Sells
                ? TenantStore.GetProducts(CompanyCode).Count(p => p.IsActive && (p.Images == null || !p.Images.Any()))
                : 0;
            return View(CurrentSettings);
        }

        [HttpGet]
        public ActionResult Torob()
        {
            if (!Kind.IsShop) return ToAction("Index");
            var company = CompanyCode;
            var domain = Tenant?.ActiveDomain();
            ViewBag.ApiOnDomain = domain == null ? null : "https://" + domain + "/torob_api/v3/products";
            ViewBag.ApiWithCode = Seo.SiteUrl(Request) + "/torob_api/v3/" + company + "/products";
            ViewBag.Preview = TorobCatalogService.Preview(company, Request);
            ViewBag.IsDemo = TenantStore.IsDemo(company);
            return View(CurrentSettings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Torob(bool enableTorob, string publicStoreBaseUrl, string webhookToken, bool removeToken = false)
        {
            if (!Kind.IsShop) return ToAction("Index");
            if (TenantStore.IsDemo(CompanyCode))
                return Fail("فروشگاه‌های نمایشی به ترب وصل نمی‌شوند؛ این امکان را در فروشگاه خودتان فعال کنید.", "Torob");

            publicStoreBaseUrl = (publicStoreBaseUrl ?? "").Trim().TrimEnd('/');
            if (publicStoreBaseUrl.Length > 0)
            {
                Uri uri;
                if (!Uri.TryCreate(publicStoreBaseUrl, UriKind.Absolute, out uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    return Fail("آدرس صفحه اول فروشگاه باید کامل و با http:// یا https:// شروع شود.", "Torob");
            }

            var settings = TenantStore.GetSettings(CompanyCode);
            settings.EnableTorob = enableTorob;
            settings.PublicStoreBaseUrl = publicStoreBaseUrl.Length == 0 ? null : publicStoreBaseUrl;
            if (removeToken) settings.TorobWebhookBearerToken = null;
            else if (!string.IsNullOrWhiteSpace(webhookToken)) settings.TorobWebhookBearerToken = webhookToken.Trim();
            TenantStore.SaveSettings(settings);
            return Ok(enableTorob ? "اتصال به ترب فعال و ذخیره شد." : "تنظیمات ترب ذخیره شد؛ API ترب خاموش است.", "Torob");
        }

        /// <summary>The JSON of the first page Torob receives, for the owner or Torob's support to check.</summary>
        [HttpGet]
        public ActionResult TorobPreview()
        {
            if (!Kind.IsShop) return HttpNotFound();
            return Content(JsonConvert.SerializeObject(TorobCatalogService.Preview(CompanyCode, Request), Formatting.Indented), "application/json");
        }
    }
}
