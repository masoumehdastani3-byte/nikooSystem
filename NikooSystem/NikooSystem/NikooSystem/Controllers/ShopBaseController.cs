using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;
using Newtonsoft.Json;

namespace NikooSystem.Controllers
{
    public abstract class ShopBaseController : Controller
    {
        protected string CompanyCode { get; private set; }
        protected StoreSettings CurrentSettings { get; private set; }
        protected TenantInfo Tenant { get; private set; }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var routeCompany = filterContext.RouteData.Values["company"] as string;
            CompanyCode = TenantStore.ResolveCompanyCode(Request, routeCompany);

            if (string.IsNullOrEmpty(CompanyCode))
            {
                filterContext.Result = HttpNotFound();
                return;
            }

            Tenant = TenantStore.GetTenant(CompanyCode);
            CurrentSettings = TenantStore.GetSettings(CompanyCode);
            if (CurrentSettings == null)
            {
                TenantStore.EnsureTenantSeed(CompanyCode);
                CurrentSettings = TenantStore.GetSettings(CompanyCode);
            }

            ApplyTemplatePreview();

            ViewBag.CompanyCode = CompanyCode;
            ViewBag.Settings = CurrentSettings;
            ViewBag.Tenant = Tenant;
            ViewBag.Menus = TenantStore.GetMenus(CompanyCode).Where(m => m.IsActive).ToList();
            ViewBag.CartCount = GetCart().Sum(x => x.Quantity);
            base.OnActionExecuting(filterContext);
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

            // settings may be cached and shared, so preview works on a copy
            var copy = JsonConvert.DeserializeObject<StoreSettings>(JsonConvert.SerializeObject(CurrentSettings));
            copy.LayoutStyle = template.Id;
            copy.PrimaryColor = template.PrimaryColor;
            copy.SecondaryColor = template.SecondaryColor;
            CurrentSettings = copy;
            ViewBag.PreviewTemplate = template.Id;
        }

        protected string CartSessionKey => "shop_cart_" + CompanyCode;

        protected List<StoreCartItem> GetCart()
        {
            var json = Session[CartSessionKey] as string;
            if (string.IsNullOrEmpty(json)) return new List<StoreCartItem>();
            return JsonConvert.DeserializeObject<List<StoreCartItem>>(json) ?? new List<StoreCartItem>();
        }

        protected void SaveCart(List<StoreCartItem> cart)
        {
            Session[CartSessionKey] = JsonConvert.SerializeObject(cart);
        }

        protected int? CurrentCustomerId
        {
            get { return Session["shop_cust_" + CompanyCode] as int?; }
            set { Session["shop_cust_" + CompanyCode] = value; }
        }

        protected bool IsShopAdmin
        {
            get { return Session["shop_admin_" + CompanyCode] as bool? == true; }
            set { Session["shop_admin_" + CompanyCode] = value; }
        }

        protected ActionResult RequireAdmin()
        {
            if (!IsShopAdmin)
                return RedirectToAction("Login", "ShopAdmin", new { company = CompanyCode });
            return null;
        }
    }
}
