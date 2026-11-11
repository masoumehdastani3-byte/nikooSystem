using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            var mobile = BookingTime.NormalizeMobile(model.Username);
            var customer = mobile == null ? null : TenantStore.GetCustomerByMobile(CompanyCode, mobile);
            if (customer == null || string.IsNullOrEmpty(customer.PasswordHash) || !PasswordHelper.Verify(model.Password, customer.PasswordHash))
            {
                ModelState.AddModelError("", "موبایل یا رمز عبور اشتباه است. اگر فقط سفارش داده‌اید و رمز ندارید، از «ارسال رمز موقت» استفاده کنید.");
                ViewBag.ReturnUrl = returnUrl;
                model.Username = mobile ?? model.Username;
                return View(model);
            }
            TenantStore.ClaimGuestOrders(CompanyCode, customer);
            CurrentCustomerId = customer.Id;
            return AfterSignIn(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(string mobile, string returnUrl)
        {
            mobile = BookingTime.NormalizeMobile(mobile);
            ViewBag.ReturnUrl = returnUrl;
            if (mobile == null)
            {
                ModelState.AddModelError("", "شماره موبایل معتبر نیست.");
                return View("Login", new StoreLoginVm());
            }

            var customer = TenantStore.GetCustomerByMobile(CompanyCode, mobile);
            if (customer == null)
            {
                // Older guest checkouts left orders without an account — recover from those orders.
                string createdPassword;
                customer = TenantStore.RecoverGuestCheckout(CompanyCode, mobile, out createdPassword);
                if (customer == null || string.IsNullOrEmpty(createdPassword))
                {
                    ModelState.AddModelError("", "مشتری با این موبایل پیدا نشد. اگر هنوز ثبت‌نام نکرده‌اید، از لینک ثبت‌نام استفاده کنید.");
                    return View("Login", new StoreLoginVm { Username = mobile });
                }
                var site = BaleNotifier.SiteName(CompanyCode);
                var send = await SmsService.SendPasswordAsync(CompanyCode, mobile, createdPassword, site);
                if (send.Success)
                {
                    Ok("حساب شما ساخته شد و رمز موقت پیامک شد. سفارش‌های قبلی همین موبایل هم به حسابتان وصل شد.");
                    return ToAction("Login", new { returnUrl });
                }
                ModelState.AddModelError("", "حساب ساخته شد ولی پیامک نرفت. رمز موقت: " + createdPassword
                    + (string.IsNullOrWhiteSpace(send.Error) ? "" : " — " + send.Error));
                return View("Login", new StoreLoginVm { Username = mobile });
            }
            var temporary = PasswordHelper.GenerateTemporaryPassword();
            customer.PasswordHash = PasswordHelper.Hash(temporary);
            TenantStore.SaveCustomer(CompanyCode, customer);
            TenantStore.ClaimGuestOrders(CompanyCode, customer);

            var siteName = BaleNotifier.SiteName(CompanyCode);
            var sms = await SmsService.SendPasswordAsync(CompanyCode, mobile, temporary, siteName);
            if (sms.Success)
            {
                Ok("رمز موقت جدید پیامک شد.");
                return ToAction("Login", new { returnUrl });
            }

            if (customer.BaleChatId.HasValue && customer.BaleChatId > 0)
            {
                var text = "🔐 بازیابی رمز ورود مشتری\n"
                    + "فروشگاه: " + siteName + "\n"
                    + "موبایل: " + mobile + "\n"
                    + "رمز موقت جدید: " + temporary + "\n"
                    + "بعد از ورود، رمز را عوض کنید.";
                var error = await BaleNotifier.SendDirectAsync(CompanyCode, customer.BaleChatId.Value, text);
                if (error == null)
                {
                    Ok("رمز موقت جدید در بله ارسال شد.");
                    return ToAction("Login", new { returnUrl });
                }
                ModelState.AddModelError("", "ارسال رمز ممکن نشد. پیامک: " + sms.Error + " — بله: " + error);
                return View("Login", new StoreLoginVm { Username = mobile });
            }

            ModelState.AddModelError("", sms.Error
                ?? "برای بازیابی رمز، اعتبار پیامک فروشگاه را شارژ کنید یا شناسه چت بله را در پروفایل ثبت کنید.");
            return View("Login", new StoreLoginVm { Username = mobile });
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
            var mobile = BookingTime.NormalizeMobile(model.Mobile);
            if (mobile == null)
            {
                ModelState.AddModelError("", "شماره موبایل معتبر نیست؛ مثلاً ۰۹۱۲۱۲۳۴۵۶۷.");
                return View(model);
            }
            if (TenantStore.GetCustomerByMobile(CompanyCode, mobile) != null)
            {
                ModelState.AddModelError("", "این موبایل قبلاً ثبت شده است. از ورود یا «ارسال رمز موقت» استفاده کنید.");
                return View(model);
            }

            var customer = TenantStore.SaveCustomer(CompanyCode, new StoreCustomer
            {
                FullName = model.FullName,
                Mobile = mobile,
                Email = model.Email,
                Address = model.Address,
                PasswordHash = PasswordHelper.Hash(model.Password)
            });
            var claimed = TenantStore.ClaimGuestOrders(CompanyCode, customer);
            CurrentCustomerId = customer.Id;
            Ok("ثبت‌نام انجام شد." + (claimed > 0 ? " سفارش‌های قبلی همین موبایل به حسابتان وصل شد." : ""));
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
        public ActionResult OrderDetails(int id)
        {
            var order = TenantStore.GetOrder(CompanyCode, id);
            if (order == null || order.CustomerId != CurrentCustomer.Id) return ToAction("Orders");
            ViewBag.Customer = CurrentCustomer;
            ViewBag.Returns = TenantStore.ReturnsForOrder(CompanyCode, id);
            ViewBag.ReturnedQty = TenantStore.ReturnedQuantities(CompanyCode, id);
            ViewBag.ShippingRefunded = TenantStore.ShippingAlreadyRefunded(CompanyCode, id);
            return View(order);
        }

        [HttpPost]
        [ShopCustomerOnly]
        [ValidateAntiForgeryToken]
        public ActionResult RequestReturn(int orderId, string reason, int[] productId, int[] quantity)
        {
            var lines = new List<StoreReturnLine>();
            if (productId != null && quantity != null)
            {
                var n = Math.Min(productId.Length, quantity.Length);
                for (var i = 0; i < n; i++)
                {
                    if (productId[i] <= 0 || quantity[i] <= 0) continue;
                    lines.Add(new StoreReturnLine { ProductId = productId[i], Quantity = quantity[i] });
                }
            }
            StoreReturnRequest request;
            var error = TenantStore.RequestReturn(CompanyCode, orderId, CurrentCustomer.Id, reason, lines, out request);
            if (error != null) return Fail(error, "OrderDetails", new { id = orderId });
            return Ok("درخواست مرجوعی ثبت شد و پس از بررسی فروشنده نتیجه اعلام می‌شود.", "OrderDetails", new { id = orderId });
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
            customer.BaleChatId = model.BaleChatId;
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
