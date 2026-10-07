using System.Web.Mvc;
using System.Web.Routing;

namespace NikooSystem
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

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
