using System.Linq;
using System.Web.Mvc;
using NikooSystem.Filters;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
    [NoIndex]
    [CountVisits]
    public class ShopAccountController : ShopBaseController
    {
        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new StoreLoginVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(StoreLoginVm model, string returnUrl)
        {
            // Username field = mobile
            var customer = TenantStore.GetCustomerByMobile(CompanyCode, model.Username);
            if (customer == null || !PasswordHelper.Verify(model.Password, customer.PasswordHash))
            {
                ModelState.AddModelError("", "موبایل یا رمز عبور اشتباه است.");
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }
            CurrentCustomerId = customer.Id;
            return AfterSignIn(returnUrl);
        }

        [HttpGet]
        public ActionResult Register(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new StoreRegisterVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(StoreRegisterVm model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValid) return View(model);
            if (TenantStore.GetCustomerByMobile(CompanyCode, model.Mobile) != null)
            {
                ModelState.AddModelError("", "این موبایل قبلاً ثبت شده است.");
                return View(model);
            }

            var customer = TenantStore.SaveCustomer(CompanyCode, new StoreCustomer
            {
                FullName = model.FullName,
                Mobile = model.Mobile,
                Email = model.Email,
                Address = model.Address,
                PasswordHash = PasswordHelper.Hash(model.Password)
            });
            CurrentCustomerId = customer.Id;
            Ok("ثبت‌نام انجام شد.");
            return AfterSignIn(returnUrl);
        }

        /// <summary>Back to the page that asked for a login (only addresses of this site), else the customer's home.</summary>
        private ActionResult AfterSignIn(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return ToAction(Kind.IsDigital ? "Library" : "Orders");
        }

        public ActionResult Logout()
        {
            CurrentCustomerId = null;
            return ToAction("Index", "Shop");
        }

        [ShopCustomerOnly]
        public ActionResult Orders(int page = 1)
        {
            ViewBag.Customer = CurrentCustomer;
            return View(TenantStore.CustomerOrders(CompanyCode, CurrentCustomer.Id, page));
        }

        [ShopCustomerOnly]
        public ActionResult Profile()
        {
            return View(CurrentCustomer);
        }

        [HttpPost]
        [ShopCustomerOnly]
        [ValidateAntiForgeryToken]
        public ActionResult Profile(StoreCustomer model, string newPassword)
        {
            var customer = CurrentCustomer;
            customer.FullName = model.FullName;
            customer.Email = model.Email;
            customer.Address = model.Address;
            if (!string.IsNullOrWhiteSpace(newPassword))
                customer.PasswordHash = PasswordHelper.Hash(newPassword);
            TenantStore.SaveCustomer(CompanyCode, customer);
            return Ok("پروفایل به‌روز شد.", "Profile");
        }

        // ---- Bought books, courses and files

        /// <summary>کتابخانه من: every digital product of the customer's paid orders.</summary>
        [ShopCustomerOnly]
        public ActionResult Library()
        {
            var owned = TenantStore.OwnedProductIds(CompanyCode, CurrentCustomer.Id);
            var products = TenantStore.GetProductsByIds(CompanyCode, owned).Where(p => p.IsDigital()).OrderBy(p => p.Title).ToList();
            ViewBag.Customer = CurrentCustomer;
            return View(products);
        }

        /// <summary>The course player. Free lessons are open to everyone; the rest need the course to be bought.</summary>
        [HttpGet]
        public ActionResult Learn(int id, string lesson = null)
        {
            var course = TenantStore.GetProduct(CompanyCode, id);
            if (course == null || !course.IsActive || course.ProductType != StoreProductType.Course) return HttpNotFound();

            var owned = TenantStore.OwnsProduct(CompanyCode, CurrentCustomerId, course.Id);
            var current = course.Lessons.FirstOrDefault(l => l.Id == lesson)
                ?? course.Lessons.FirstOrDefault(l => owned || l.IsFree)
                ?? course.Lessons.FirstOrDefault();
            if (current != null && !owned && !current.IsFree)
            {
                if (CurrentCustomer == null) return ToLogin();
                Fail("برای دیدن این جلسه ابتدا دوره را تهیه کنید.");
                return ToAction("Product", "Shop", new { slug = course.Slug });
            }

            ViewBag.Owned = owned;
            ViewBag.Lesson = current;
            ViewBag.File = current == null ? null : course.Files.FirstOrDefault(f => f.Id == current.FileId);
            return View(course);
        }

        /// <summary>
        /// Sends a protected file: in full as a download, or inline with range support for the lesson player. Buyers get every
        /// file of the product; others only files of free lessons.
        /// </summary>
        [HttpGet]
        public ActionResult Media(int id, string file, bool download = false)
        {
            var product = TenantStore.GetProduct(CompanyCode, id);
            var stored = product?.Files.FirstOrDefault(f => f.Id == file);
            if (stored == null) return HttpNotFound();

            var owned = TenantStore.OwnsProduct(CompanyCode, CurrentCustomerId, product.Id);
            var free = product.IsActive && product.Lessons.Any(l => l.IsFree && l.FileId == stored.Id);
            if (!owned && !free) return CurrentCustomer == null ? (ActionResult)ToLogin() : new HttpStatusCodeResult(403);

            var path = DigitalFileService.PathOf(CompanyCode, stored);
            if (path == null) return HttpNotFound();
            DigitalFileService.Send(HttpContext, path, stored, download);
            return new EmptyResult();
        }
    }
}
