using System;
using System.Configuration;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using NikooSystem.Services;

namespace NikooSystem
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static System.Threading.Timer _trialCleanupTimer;
        private static System.Threading.Timer _balePollTimer;

        protected void Application_Start()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            _trialCleanupTimer = new System.Threading.Timer(_ =>
            {
                try { TenantProvisioningService.PurgeExpiredTrials(); }
                catch { }
                try { Subscriptions.SendReminders(); }
                catch { }
                try { CustomDomains.CheckPending(); }
                catch { }
                try { CartStore.PurgeOlderThan(DateTime.Now.AddDays(-30)); }
                catch { }
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));

            // replies to support tickets written in Bale arrive even when no panel page is open
            _balePollTimer = new System.Threading.Timer(_ => BaleNotifier.PollInBackground(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5));
        }

        protected void Application_End()
        {
            try { VisitStats.Flush(); }
            catch { }
        }

        protected void Application_BeginRequest()
        {
            if (RedirectToSiteUrl()) return;

            var path = Request.Path ?? "";
            if (path.StartsWith("/Assets", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Content", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Scripts", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Uploads", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Shop", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/ShopAdmin", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/ShopAccount", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Pay", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/torob_api", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var tenant = TenantStore.ResolveByHost(Request.Url.Host);
            if (tenant == null) return;

            if (string.Equals(path, CustomDomains.ProbePath, StringComparison.OrdinalIgnoreCase))
            {
                Response.ContentType = "text/plain";
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Write(CustomDomains.ProbeAnswer(tenant.CompanyCode));
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (path == "/" || string.IsNullOrWhiteSpace(path.Trim('/')))
            {
                Context.RewritePath("~/Shop/" + tenant.CompanyCode);
                return;
            }

            Context.RewritePath("~/Shop/" + tenant.CompanyCode + "/" + path.TrimStart('/'));
        }

        private static readonly Uri SiteUrl = ParseSiteUrl();

        private static Uri ParseSiteUrl()
        {
            Uri uri;
            var configured = (ConfigurationManager.AppSettings["Seo.SiteUrl"] ?? "").Trim();
            return Uri.TryCreate(configured, UriKind.Absolute, out uri) ? uri : null;
        }

        /// <summary>
        /// http:// and the www or bare form of the main domain get a 301 to the Seo.SiteUrl address, so search engines see one site.
        /// Stores' own domains and subdomains are left alone, and only GET and HEAD are moved, so forms and payment callbacks keep their body.
        /// </summary>
        private bool RedirectToSiteUrl()
        {
            var site = SiteUrl;
            if (site == null || (Request.HttpMethod != "GET" && Request.HttpMethod != "HEAD")) return false;
            var host = Request.Url.Host.ToLowerInvariant();
            var bare = site.Host.StartsWith("www.", StringComparison.Ordinal) ? site.Host.Substring(4) : site.Host;
            if (host != bare && host != "www." + bare) return false;

            // behind a CDN that ends TLS, the request reaches IIS over http and the original scheme is in this header
            var forwarded = Request.Headers["X-Forwarded-Proto"];
            var scheme = string.IsNullOrEmpty(forwarded) ? Request.Url.Scheme : forwarded.Split(',')[0].Trim().ToLowerInvariant();
            if (host == site.Host && scheme == site.Scheme) return false;

            Response.RedirectPermanent(site.GetLeftPart(UriPartial.Authority) + Request.RawUrl, false);
            Context.ApplicationInstance.CompleteRequest();
            return true;
        }
    }
}
