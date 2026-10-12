using System;
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
    }
}
