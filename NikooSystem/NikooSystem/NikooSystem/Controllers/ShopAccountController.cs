using System;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class ShopAccountController : ShopBaseController
    {
        [HttpGet]
        public ActionResult Login()
        {
            return View(new StoreLoginVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(StoreLoginVm model)
        {
            // Username field = mobile
            var customer = TenantStore.GetCustomerByMobile(CompanyCode, model.Username);
            if (customer == null || !PasswordHelper.Verify(model.Password, customer.PasswordHash))
            {
                ModelState.AddModelError("", "موبایل یا رمز عبور اشتباه است.");
                return View(model);
            }
            CurrentCustomerId = customer.Id;
            return RedirectToAction("Orders", new { company = CompanyCode });
        }

        [HttpGet]
        public ActionResult Register()
        {
            return View(new StoreRegisterVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(StoreRegisterVm model)
        {
            if (!ModelState.IsValid) return View(model);
            if (TenantStore.GetCustomerByMobile(CompanyCode, model.Mobile) != null)
            {
                ModelState.AddModelError("", "این موبایل قبلاً ثبت شده است.");
                return View(model);
            }

            var customer = TenantStore.UpsertCustomer(CompanyCode, new StoreCustomer
            {
                FullName = model.FullName,
                Mobile = model.Mobile,
                Email = model.Email,
                Address = model.Address,
                PasswordHash = PasswordHelper.Hash(model.Password)
            });
            CurrentCustomerId = customer.Id;
            TempData["Ok"] = "ثبت‌نام انجام شد.";
            return RedirectToAction("Orders", new { company = CompanyCode });
        }

        public ActionResult Logout()
        {
            CurrentCustomerId = null;
            return RedirectToAction("Index", "Shop", new { company = CompanyCode });
        }

        public ActionResult Orders()
        {
            if (!CurrentCustomerId.HasValue)
                return RedirectToAction("Login", new { company = CompanyCode });

            var orders = TenantStore.GetOrders(CompanyCode)
                .Where(o => o.CustomerId == CurrentCustomerId.Value)
                .ToList();
            ViewBag.Customer = TenantStore.GetCustomers(CompanyCode).FirstOrDefault(c => c.Id == CurrentCustomerId.Value);
            return View(orders);
        }

        public ActionResult Profile()
        {
            if (!CurrentCustomerId.HasValue)
                return RedirectToAction("Login", new { company = CompanyCode });
            var customer = TenantStore.GetCustomers(CompanyCode).FirstOrDefault(c => c.Id == CurrentCustomerId.Value);
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Profile(StoreCustomer model, string newPassword)
        {
            if (!CurrentCustomerId.HasValue)
                return RedirectToAction("Login", new { company = CompanyCode });

            var customer = TenantStore.GetCustomers(CompanyCode).FirstOrDefault(c => c.Id == CurrentCustomerId.Value);
            if (customer == null) return RedirectToAction("Login", new { company = CompanyCode });

            customer.FullName = model.FullName;
            customer.Email = model.Email;
            customer.Address = model.Address;
            if (!string.IsNullOrWhiteSpace(newPassword))
                customer.PasswordHash = PasswordHelper.Hash(newPassword);
            TenantStore.UpsertCustomer(CompanyCode, customer);
            TempData["Ok"] = "پروفایل به‌روز شد.";
            return RedirectToAction("Profile", new { company = CompanyCode });
        }
    }
}
