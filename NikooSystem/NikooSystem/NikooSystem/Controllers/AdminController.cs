using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Filters;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class AdminController : Controller
    {
        [HttpGet]
        public ActionResult Login()
        {
            if (Session["AdminUser"] != null)
                return RedirectToAction("Index");
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var username = ConfigurationManager.AppSettings["Admin.Username"] ?? "admin";
            var password = ConfigurationManager.AppSettings["Admin.Password"] ?? "Admin@123";

            if (model.Username == username && model.Password == password)
            {
                Session["AdminUser"] = model.Username;
                return RedirectToAction("Index");
            }

            ModelState.AddModelError("", "نام کاربری یا رمز عبور اشتباه است.");
            return View(model);
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        [AdminAuthorize]
        public ActionResult Index()
        {
            var orders = JsonStore.GetOrders();
            var paid = orders.Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Processing || o.Status == OrderStatus.Completed).ToList();
            var model = new AdminDashboardViewModel
            {
                ProductCount = JsonStore.GetProducts().Count,
                OrderCount = orders.Count,
                PaidOrderCount = paid.Count,
                TotalRevenue = paid.Sum(o => o.Amount),
                RecentOrders = orders.Take(8).ToList()
            };
            return View(model);
        }

        [AdminAuthorize]
        public ActionResult Products()
        {
            return View(JsonStore.GetProducts());
        }

        [AdminAuthorize]
        [HttpGet]
        public ActionResult CreateProduct()
        {
            return View(new Product { IsActive = true });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateProduct(Product model, HttpPostedFileBase imageFile)
        {
            if (!ModelState.IsValid)
                return View(model);

            model.ImageUrl = SaveImage(imageFile) ?? model.ImageUrl ?? "/Assets/img/portfolio/portfolio-1.jpg";
            JsonStore.AddProduct(model);
            TempData["Success"] = "محصول با موفقیت اضافه شد.";
            return RedirectToAction("Products");
        }

        [AdminAuthorize]
        [HttpGet]
        public ActionResult EditProduct(int id)
        {
            var product = JsonStore.GetProduct(id);
            if (product == null) return HttpNotFound();
            return View(product);
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProduct(Product model, HttpPostedFileBase imageFile)
        {
            if (!ModelState.IsValid)
                return View(model);

            var existing = JsonStore.GetProduct(model.Id);
            if (existing == null) return HttpNotFound();

            var saved = SaveImage(imageFile);
            if (!string.IsNullOrEmpty(saved))
                model.ImageUrl = saved;
            else if (string.IsNullOrEmpty(model.ImageUrl))
                model.ImageUrl = existing.ImageUrl;

            JsonStore.UpdateProduct(model);
            TempData["Success"] = "محصول به‌روزرسانی شد.";
            return RedirectToAction("Products");
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteProduct(int id)
        {
            JsonStore.DeleteProduct(id);
            TempData["Success"] = "محصول حذف شد.";
            return RedirectToAction("Products");
        }

        [AdminAuthorize]
        public ActionResult Orders(string status = null)
        {
            var orders = JsonStore.GetOrders();
            if (!string.IsNullOrEmpty(status) && Enum.TryParse(status, out OrderStatus st))
                orders = orders.Where(o => o.Status == st).ToList();

            ViewBag.Status = status;
            return View(orders);
        }

        [AdminAuthorize]
        public ActionResult OrderDetails(int id)
        {
            var order = JsonStore.GetOrder(id);
            if (order == null) return HttpNotFound();
            return View(order);
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateOrderStatus(int id, OrderStatus status)
        {
            var order = JsonStore.GetOrder(id);
            if (order == null) return HttpNotFound();
            order.Status = status;
            JsonStore.UpdateOrder(order);
            TempData["Success"] = "وضعیت سفارش به‌روزرسانی شد.";
            return RedirectToAction("OrderDetails", new { id });
        }

        private string SaveImage(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return null;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
                return null;

            var folder = Server.MapPath("~/Content/Uploads");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var name = Guid.NewGuid().ToString("N") + ext;
            file.SaveAs(Path.Combine(folder, name));
            return "/Content/Uploads/" + name;
        }
    }
}
