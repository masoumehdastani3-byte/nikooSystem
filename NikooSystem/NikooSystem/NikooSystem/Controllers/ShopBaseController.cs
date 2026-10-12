using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>The action (or every action of the controller) is for the store's signed-in admin; others go to the admin login.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ShopAdminOnlyAttribute : Attribute { }

    /// <summary>The action is for a signed-in customer of the store; others go to the customer login.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ShopCustomerOnlyAttribute : Attribute { }

    /// <summary>The action exists only on these kinds of site (<see cref="SiteKinds"/>); on others it is not found.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ForKindsAttribute : Attribute
    {
        public ForKindsAttribute(params string[] kinds)
        {
            Kinds = kinds;
        }

        public string[] Kinds { get; }
    }

    public abstract class ShopBaseController : Controller
    {
        protected string CompanyCode { get; private set; }
        protected StoreSettings CurrentSettings { get; private set; }
        protected TenantInfo Tenant { get; private set; }
        /// <summary>Physical shop, digital products or content site; decides which features exist.</summary>
        protected SiteKindInfo Kind { get; private set; }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var routeCompany = filterContext.RouteData.Values["company"] as string;
            CompanyCode = TenantStore.ResolveCompanyCode(Request, routeCompany);

            if (string.IsNullOrEmpty(CompanyCode))
            {
                filterContext.Result = HttpNotFound();
                return;
            }

            if (Requires<ShopAdminOnlyAttribute>(filterContext) && !IsShopAdmin)
            {
                filterContext.Result = ToAction("Login", "ShopAdmin");
                return;
            }
            if (Requires<ShopCustomerOnlyAttribute>(filterContext) && CurrentCustomer == null)
            {
                filterContext.Result = ToLogin();
                return;
            }

            Tenant = TenantStore.GetTenant(CompanyCode);
            Kind = SiteKinds.Of(Tenant);
            if (!AllowedFor(filterContext, Kind.Id))
            {
                filterContext.Result = HttpNotFound();
                return;
            }

            CurrentSettings = TenantStore.GetSettings(CompanyCode);
            if (CurrentSettings == null)
            {
                TenantStore.EnsureTenantSeed(CompanyCode);
                CurrentSettings = TenantStore.GetSettings(CompanyCode);
            }

            ApplyTemplatePreview();

            if (Tenant != null)
            {
                var seo = Seo.Of(HttpContext);
                Seo.ForStore(seo, Tenant, Request);
                seo.SiteName = CurrentSettings?.StoreName;
                seo.GoogleVerification = CurrentSettings?.GoogleVerification;
                if (!string.IsNullOrEmpty(CurrentSettings?.EnamadMeta))
                    seo.Meta.Add(new KeyValuePair<string, string>("enamad", CurrentSettings.EnamadMeta));
            }

            ViewBag.CompanyCode = CompanyCode;
            ViewBag.Settings = CurrentSettings;
            ViewBag.Tenant = Tenant;
            ViewBag.Kind = Kind;
            ViewBag.Menus = TenantStore.GetMenus(CompanyCode).Where(m => m.IsActive).ToList();
            ViewBag.CartCount = GetCart().Sum(x => x.Quantity);

            if (Tenant != null && Tenant.IsSubscriptionLocked() && !OpenWhenClosed(filterContext.ActionDescriptor.ActionName))
            {
                filterContext.Result = WhenClosed();
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        /// <summary>Actions that still work while the site is closed for an unpaid subscription.</summary>
        protected virtual bool OpenWhenClosed(string action) => false;

        /// <summary>
        /// What a closed site shows: «temporarily unavailable» with 503, so search engines keep its pages for when it reopens.
        /// </summary>
        protected virtual ActionResult WhenClosed()
        {
            Response.StatusCode = 503;
            Response.TrySkipIisCustomErrors = true;
            Response.AppendHeader("Retry-After", "86400");
            return View("~/Views/Shop/Closed.cshtml");
        }

        /// <summary>
        /// ?previewTemplate=id renders the store with another template and its default colours,
        /// without saving anything. Used by the template picker in the admin panel.
        /// </summary>
        private void ApplyTemplatePreview()
        {
            if (!(this is ShopController) || Request.HttpMethod != "GET") return;
            var previewId = Request.QueryString["previewTemplate"];
            if (CurrentSettings == null || string.IsNullOrWhiteSpace(previewId)) return;

            var template = StoreTemplates.All.FirstOrDefault(t => t.Id == previewId);
            if (template == null) return;

            ViewBag.SavedTemplate = CurrentSettings.LayoutStyle;
            // settings may be cached and shared, so preview works on a copy
            var copy = CurrentSettings.Clone();
            copy.LayoutStyle = template.Id;
            copy.PrimaryColor = template.PrimaryColor;
            copy.SecondaryColor = template.SecondaryColor;
            CurrentSettings = copy;
            ViewBag.PreviewTemplate = template.Id;
        }

        private const string CartCookie = "nikoo_cart";

        protected List<StoreCartItem> GetCart()
        {
            return CartStore.Get(Request.Cookies[CartCookie]?.Value, CompanyCode);
        }

        protected void SaveCart(List<StoreCartItem> cart)
        {
            var cartId = Request.Cookies[CartCookie]?.Value;
            if (!CartStore.IsValidCartId(cartId))
            {
                if (cart == null || cart.Count == 0) return;
                cartId = CartStore.NewCartId();
            }
            // refreshed on every change so an active cart outlives the 30-day cleanup
            Response.Cookies.Add(new HttpCookie(CartCookie, cartId)
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection,
                Expires = DateTime.Now.AddDays(30)
            });
            CartStore.Save(cartId, CompanyCode, cart);
        }

        protected int? CurrentCustomerId
        {
            get { return Session["shop_cust_" + CompanyCode] as int?; }
            set
            {
                Session["shop_cust_" + CompanyCode] = value;
                _customerLoaded = false;
            }
        }

        private StoreCustomer _customer;
        private bool _customerLoaded;

        /// <summary>The signed-in customer of this store, or null.</summary>
        protected StoreCustomer CurrentCustomer
        {
            get
            {
                if (!_customerLoaded)
                {
                    _customer = TenantStore.GetCustomer(CompanyCode, CurrentCustomerId);
                    _customerLoaded = true;
                }
                return _customer;
            }
        }

        protected bool IsShopAdmin
        {
            get { return Session["shop_admin_" + CompanyCode] as bool? == true; }
            set { Session["shop_admin_" + CompanyCode] = value; }
        }

        private static bool Requires<TAttribute>(ActionExecutingContext context) where TAttribute : Attribute
        {
            var action = context.ActionDescriptor;
            if (action.IsDefined(typeof(AllowAnonymousAttribute), true)) return false;
            return action.IsDefined(typeof(TAttribute), true) || action.ControllerDescriptor.IsDefined(typeof(TAttribute), true);
        }

        private static bool AllowedFor(ActionExecutingContext context, string kind)
        {
            var action = context.ActionDescriptor;
            return action.GetCustomAttributes(typeof(ForKindsAttribute), true)
                .Concat(action.ControllerDescriptor.GetCustomAttributes(typeof(ForKindsAttribute), true))
                .Cast<ForKindsAttribute>()
                .All(a => a.Kinds.Contains(kind));
        }

        /// <summary>
        /// Back to the list the admin came from, with its page, search and sort, when the request was posted from it;
        /// otherwise to <paramref name="action"/>.
        /// </summary>
        protected ActionResult BackTo(string action)
        {
            var referrer = Request.UrlReferrer;
            var listPath = Url.Action(action, new { company = CompanyCode });
            if (referrer != null && Request.Url != null && referrer.Authority == Request.Url.Authority
                && string.Equals(referrer.AbsolutePath, listPath, StringComparison.OrdinalIgnoreCase))
                return Redirect(referrer.PathAndQuery);
            return ToAction(action);
        }

        protected ActionResult OkBack(string message, string action)
        {
            Ok(message);
            return BackTo(action);
        }

        /// <summary>The customer login, coming back to the current page (GET requests) after signing in.</summary>
        protected RedirectToRouteResult ToLogin()
        {
            return Request.HttpMethod == "GET"
                ? ToAction("Login", "ShopAccount", new { returnUrl = Request.RawUrl })
                : ToAction("Login", "ShopAccount");
        }

        /// <summary>Redirect to an action of this store (the company route value is filled in).</summary>
        protected RedirectToRouteResult ToAction(string action, object routeValues = null)
        {
            return ToAction(action, null, routeValues);
        }

        protected RedirectToRouteResult ToAction(string action, string controller, object routeValues = null)
        {
            var values = new RouteValueDictionary(routeValues) { ["company"] = CompanyCode };
            return controller == null ? RedirectToAction(action, values) : RedirectToAction(action, controller, values);
        }

        /// <summary>Success message shown once on the next page (see _FlashMessages).</summary>
        protected void Ok(string message)
        {
            TempData["Ok"] = message;
        }

        /// <summary>Error message shown once on the next page (see _FlashMessages).</summary>
        protected void Fail(string message)
        {
            TempData["Error"] = message;
        }

        protected RedirectToRouteResult Ok(string message, string action, object routeValues = null)
        {
            Ok(message);
            return ToAction(action, routeValues);
        }

        protected RedirectToRouteResult Fail(string message, string action, object routeValues = null)
        {
            Fail(message);
            return ToAction(action, routeValues);
        }
    }
}
