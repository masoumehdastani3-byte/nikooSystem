using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Filters;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// Panel of the main Nikoo site. This file: login, dashboard, the site's products and their orders;
    /// stores, payment hub and site content are in the other AdminController.*.cs files.
    /// </summary>
    [AdminAuthorize]
    [NoIndex]
    public partial class AdminController : Controller
    {
        [AllowAnonymous]
        [HttpGet]
        public ActionResult Login()
        {
            if (Session["AdminUser"] != null)
                return RedirectToAction("Index");
            return View(new LoginViewModel());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (SiteContentStore.CheckAdminLogin(model.Username, model.Password))
            {
                Session["AdminUser"] = model.Username;
                if (Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
                    return Redirect(returnUrl);
                return RedirectToAction("Index");
            }

            ModelState.AddModelError("", "نام کاربری یا رمز عبور اشتباه است.");
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        public ActionResult Index()
        {
            var products = SiteStore.GetProducts();
            var orders = SiteStore.GetOrders();
            var paid = orders.Where(IsPaid).ToList();
            var monthAgo = DateTime.Now.AddDays(-30);

            var stores = TenantStore.GetTenants().Where(t => !TenantStore.IsDemo(t.CompanyCode)).ToList();
            var liveTrials = stores.Where(t => t.IsTrial && !t.IsPaid && !t.IsTrialExpired()).ToList();
            var hub = SiteStore.HubStatsByApp().Values.ToList();
            var today = DateTime.Today;

            var model = new AdminDashboardViewModel
            {
                ProductCount = products.Count,
                ActiveProductCount = products.Count(p => p.IsActive),
                OrderCount = orders.Count,
                PaidOrderCount = paid.Count,
                PendingOrderCount = orders.Count(o => o.Status == OrderStatus.PendingPayment),
                TotalRevenue = paid.Sum(o => o.Amount),
                MonthRevenue = paid.Where(o => (o.PaidAt ?? o.CreatedAt) >= monthAgo).Sum(o => o.Amount),
                RecentOrders = orders.Take(6).ToList(),

                StoreCount = stores.Count,
                TrialStoreCount = liveTrials.Count,
                PaidStoreCount = stores.Count(t => t.IsPaid && !t.IsSubscriptionExpired()),
                ExpiredStoreCount = stores.Count(t => t.IsTrialExpired()),
                NewStoresThisWeek = stores.Count(t => t.CreatedAt >= today.AddDays(-6)),
                RecentStores = stores.OrderByDescending(t => t.CreatedAt).Take(6).ToList(),
                ExpiringTrials = liveTrials.Where(t => t.TrialEndsAt <= DateTime.Now.AddDays(3)).OrderBy(t => t.TrialEndsAt).ToList(),
                RenewalsDue = stores.Where(t => t.IsActive && t.IsRenewalDue()).OrderBy(t => t.SubscriptionEndsAt).ToList(),

                UnreadMessageCount = SiteContentStore.UnreadMessageCount(),
                RecentMessages = SiteContentStore.RecentMessages(5),

                PaymentAppCount = SiteStore.GetPaymentClients().Count,
                HubPaidAmount = hub.Sum(h => h.PaidAmount),
                HubPaidCount = hub.Sum(h => h.PaidCount),

                SignupsByDay = Enumerable.Range(0, 14)
                    .Select(i => today.AddDays(i - 13))
                    .Select(day => new KeyValuePair<DateTime, int>(day, stores.Count(t => t.CreatedAt.Date == day)))
                    .ToList(),

                SiteVisits = VisitStats.Report(VisitStats.MainSite, "بازدید سایت نیکو"),
                StoreVisits = VisitStats.Report(null, "بازدید سایت‌های مشتریان")
            };
            return View(model);
        }

        private static bool IsPaid(Order o) =>
            o.Status == OrderStatus.Paid || o.Status == OrderStatus.Processing || o.Status == OrderStatus.Completed;

        // ---- Products of the main site

        public ActionResult Products(GridQuery query)
        {
            return View(SiteStore.ProductGrid(query));
        }

        [HttpGet]
        public ActionResult CreateProduct()
        {
            return View(new Product { IsActive = true, Category = "فروشگاه آنلاین" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateProduct(Product model, HttpPostedFileBase imageFile)
        {
            var saved = TrySaveImage(imageFile);
            NormalizeProduct(model);
            if (!ModelState.IsValid)
                return View(model);

            model.ImageUrl = saved ?? model.ImageUrl ?? "/Assets/img/portfolio/portfolio-1.jpg";
            SiteStore.AddProduct(model);
            TempData["Ok"] = "محصول با موفقیت اضافه شد.";
            return RedirectToAction("Products");
        }

        [HttpGet]
        public ActionResult EditProduct(int id)
        {
            var product = SiteStore.GetProduct(id);
            if (product == null) return HttpNotFound();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProduct(Product model, HttpPostedFileBase imageFile)
        {
            var saved = TrySaveImage(imageFile);
            NormalizeProduct(model);
            if (!ModelState.IsValid)
                return View(model);

            var existing = SiteStore.GetProduct(model.Id);
            if (existing == null) return HttpNotFound();

            if (!string.IsNullOrEmpty(saved))
                model.ImageUrl = saved;
            else if (string.IsNullOrEmpty(model.ImageUrl))
                model.ImageUrl = existing.ImageUrl;

            SiteStore.UpdateProduct(model);
            TempData["Ok"] = "محصول به‌روزرسانی شد.";
            return RedirectToAction("Products");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteProduct(int id)
        {
            var product = SiteStore.GetProduct(id);
            if (product != null && SiteStore.GetOrders().Any(o => o.ProductId == id))
            {
                product.IsActive = false;
                SiteStore.UpdateProduct(product);
                TempData["Ok"] = "این محصول سفارش ثبت‌شده دارد؛ به جای حذف، غیرفعال شد.";
                return RedirectToAction("Products");
            }
            SiteStore.DeleteProduct(id);
            TempData["Ok"] = "محصول حذف شد.";
            return RedirectToAction("Products");
        }

        /// <param name="field">active | featured</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleProduct(int id, string field)
        {
            var product = SiteStore.GetProduct(id);
            if (product == null) return HttpNotFound();
            if (field == "featured") product.IsFeatured = !product.IsFeatured;
            else product.IsActive = !product.IsActive;
            SiteStore.UpdateProduct(product);
            TempData["Ok"] = "«" + product.Title + "» " + (field == "featured"
                ? (product.IsFeatured ? "پیشنهادی شد." : "از پیشنهادی‌ها خارج شد.")
                : (product.IsActive ? "فعال شد." : "غیرفعال شد."));
            return RedirectToAction("Products");
        }

        private void NormalizeProduct(Product model)
        {
            model.Title = model.Title?.Trim();
            model.Code = string.IsNullOrWhiteSpace(model.Code) ? null : model.Code.Trim();
            if (model.Code != null && SiteStore.GetProducts().Any(p => p.Id != model.Id && string.Equals(p.Code, model.Code, StringComparison.OrdinalIgnoreCase)))
                ModelState.AddModelError("Code", "این کد محصول قبلاً استفاده شده است.");
            model.Category = string.IsNullOrWhiteSpace(model.Category) ? null : model.Category.Trim();
            model.SeoTitle = string.IsNullOrWhiteSpace(model.SeoTitle) ? null : model.SeoTitle.Trim();
            model.SeoDescription = string.IsNullOrWhiteSpace(model.SeoDescription) ? null : model.SeoDescription.Trim();
            model.DemoSlug = string.IsNullOrWhiteSpace(model.DemoSlug) ? null : model.DemoSlug.Trim().ToLowerInvariant();
            if (model.DemoSlug != null && !TenantStore.DemoBySlug.ContainsKey(model.DemoSlug))
                ModelState.AddModelError("DemoSlug", "برای این نوع، سایت دمو وجود ندارد.");
        }

        // ---- Orders of the main site (purchases of products and store subscriptions)

        public ActionResult Orders(GridQuery query, OrderStatus? status = null)
        {
            return View(SiteStore.OrderGrid(query, status));
        }

        public ActionResult OrderDetails(int id)
        {
            var order = SiteStore.GetOrder(id);
            if (order == null) return HttpNotFound();
            ViewBag.Store = string.IsNullOrEmpty(order.CompanyCode) ? null : TenantStore.GetTenantForAdmin(order.CompanyCode);
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateOrderStatus(int id, OrderStatus status)
        {
            var order = SiteStore.GetOrder(id);
            if (order == null) return HttpNotFound();
            order.Status = status;
            if (IsPaid(order) && !order.PaidAt.HasValue) order.PaidAt = DateTime.Now;
            SiteStore.UpdateOrder(order);

            var message = "وضعیت سفارش به‌روزرسانی شد.";
            if (IsPaid(order) && !string.IsNullOrEmpty(order.CompanyCode) && Subscriptions.Apply(order.Id))
            {
                var store = TenantStore.GetTenantForAdmin(order.CompanyCode);
                message += " اشتراک «" + store?.Name + "» تا " + store?.SubscriptionEndsAt?.ToShamsiText() + " تمدید شد.";
            }
            TempData["Ok"] = message;
            return RedirectToAction("OrderDetails", new { id });
        }

        /// <summary>The saved image's URL, or null when no file was sent; an invalid file becomes a form error.</summary>
        private string TrySaveImage(HttpPostedFileBase file, string field = "imageFile")
        {
            try
            {
                return ImageService.SaveSiteImage(file);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(field, ex.Message);
                return null;
            }
        }
    }
}
