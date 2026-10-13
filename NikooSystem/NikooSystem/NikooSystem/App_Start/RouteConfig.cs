using System.Web.Mvc;
using System.Web.Routing;

namespace NikooSystem
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(name: "SeoRobots", url: "robots.txt", defaults: new { controller = "Seo", action = "Robots" });
            routes.MapRoute(name: "SeoSitemap", url: "sitemap.xml", defaults: new { controller = "Seo", action = "Sitemap" });
            routes.MapRoute(name: "SeoSitemapPages", url: "sitemap/pages", defaults: new { controller = "Seo", action = "Pages" });
            routes.MapRoute(name: "SeoStoreRobots", url: "Shop/{company}/robots.txt", defaults: new { controller = "Seo", action = "StoreRobots" });
            routes.MapRoute(name: "SeoStoreSitemap", url: "Shop/{company}/sitemap.xml", defaults: new { controller = "Seo", action = "StoreSitemap" });

            routes.MapRoute(
                name: "PayStart",
                url: "Pay/Start/{token}",
                defaults: new { controller = "Pay", action = "Start" }
            );

            routes.MapRoute(
                name: "PayCallback",
                url: "Pay/Callback",
                defaults: new { controller = "Pay", action = "Callback" }
            );

            routes.MapRoute(
                name: "PayGuide",
                url: "Pay/Guide",
                defaults: new { controller = "Pay", action = "Guide" }
            );

            routes.MapRoute(
                name: "ApiPaymentsCreate",
                url: "api/payments/create",
                defaults: new { controller = "ApiPayments", action = "Create" }
            );

            routes.MapRoute(
                name: "ApiPaymentsStatus",
                url: "api/payments/status/{token}",
                defaults: new { controller = "ApiPayments", action = "Status" }
            );

            routes.MapRoute(
                name: "ShopProduct",
                url: "Shop/{company}/Product/{slug}",
                defaults: new { controller = "Shop", action = "Product" }
            );

            routes.MapRoute(
                name: "ShopPage",
                url: "Shop/{company}/Page/{slug}",
                defaults: new { controller = "Shop", action = "Page" }
            );

            routes.MapRoute(
                name: "ShopPost",
                url: "Shop/{company}/Post/{slug}",
                defaults: new { controller = "Shop", action = "Post" }
            );

            routes.MapRoute(
                name: "Shop",
                url: "Shop/{company}/{action}/{id}",
                defaults: new { controller = "Shop", action = "Index", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ShopAdmin",
                url: "ShopAdmin/{company}/{action}/{id}",
                defaults: new { controller = "ShopAdmin", action = "Index", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "ShopAccount",
                url: "ShopAccount/{company}/{action}/{id}",
                defaults: new { controller = "ShopAccount", action = "Login", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "TorobApiByCompany",
                url: "torob_api/v3/{company}/products",
                defaults: new { controller = "TorobApi", action = "Products" }
            );

            routes.MapRoute(
                name: "TorobApi",
                url: "torob_api/v3/products",
                defaults: new { controller = "TorobApi", action = "Products", company = UrlParameter.Optional }
            );

            routes.MapRoute(name: "Solution", url: "{slug}", defaults: new { controller = "Solutions", action = "Show" },
                constraints: new { slug = Helpers.ProductLandings.SlugPattern });

            routes.MapRoute(name: "Blog", url: "blog", defaults: new { controller = "Blog", action = "Index" });
            routes.MapRoute(name: "BlogPost", url: "blog/{slug}", defaults: new { controller = "Blog", action = "Post" });

            routes.MapRoute(
                name: "Demo",
                url: "Demo/{slug}",
                defaults: new { controller = "Demo", action = "Index" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
