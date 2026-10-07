using System;
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
    public class ShopAdminController : ShopBaseController
    {
        private ActionResult Guard()
        {
            return RequireAdmin();
        }

        [HttpGet]
        public ActionResult Login()
        {
            if (IsShopAdmin) return RedirectToAction("Index", new { company = CompanyCode });
            return View(new StoreLoginVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(StoreLoginVm model)
        {
            if (!ModelState.IsValid) return View(model);
            if (string.Equals(model.Username, CurrentSettings.AdminUsername, StringComparison.OrdinalIgnoreCase) &&
                PasswordHelper.Verify(model.Password, CurrentSettings.AdminPasswordHash))
            {
                IsShopAdmin = true;
                return RedirectToAction("Index", new { company = CompanyCode });
            }
            ModelState.AddModelError("", "نام کاربری یا رمز عبور اشتباه است.");
            return View(model);
        }

        public ActionResult Logout()
        {
            IsShopAdmin = false;
            return RedirectToAction("Login", new { company = CompanyCode });
        }

        public ActionResult Index()
        {
            var deny = Guard(); if (deny != null) return deny;
            ViewBag.ProductCount = TenantStore.GetProducts(CompanyCode).Count;
            ViewBag.OrderCount = TenantStore.GetOrders(CompanyCode).Count;
            ViewBag.CustomerCount = TenantStore.GetCustomers(CompanyCode).Count;
            ViewBag.RecentOrders = TenantStore.GetOrders(CompanyCode).Take(8).ToList();
            return View();
        }

        [HttpGet]
        public ActionResult Settings()
        {
            var deny = Guard(); if (deny != null) return deny;
            return View(CurrentSettings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Settings(StoreSettings model, string newPassword)
        {
            var deny = Guard(); if (deny != null) return deny;
            var existing = CurrentSettings ?? TenantStore.GetSettings(CompanyCode);
            model.CompanyCode = CompanyCode;
            model.AdminUsername = string.IsNullOrWhiteSpace(model.AdminUsername)
                ? (existing?.AdminUsername ?? "admin")
                : model.AdminUsername;
            model.AdminPasswordHash = string.IsNullOrWhiteSpace(newPassword)
                ? (existing?.AdminPasswordHash ?? PasswordHelper.Hash("Admin@123"))
                : PasswordHelper.Hash(newPassword);
            if (string.IsNullOrWhiteSpace(model.HomeSectionsJson))
                model.HomeSectionsJson = existing?.HomeSectionsJson;
            model.LayoutStyle = StoreTemplates.Normalize(model.LayoutStyle);
            TenantStore.SaveSettings(model);
            TempData["Ok"] = "تنظیمات ذخیره شد.";
            return RedirectToAction("Settings", new { company = CompanyCode });
        }

        public ActionResult Products()
        {
            var deny = Guard(); if (deny != null) return deny;
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            return View(TenantStore.GetProducts(CompanyCode));
        }

        [HttpGet]
        public ActionResult EditProduct(int? id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var product = id.HasValue ? TenantStore.GetProduct(CompanyCode, id.Value) : new StoreProduct { IsActive = true, Stock = 10 };
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            var catId = product?.CategoryId ?? 0;
            ViewBag.AttributeDefs = catId > 0
                ? TenantStore.GetAttributeDefsForCategory(CompanyCode, catId)
                : TenantStore.GetAttributeDefs(CompanyCode);
            return View(product ?? new StoreProduct());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProduct(StoreProduct model, IEnumerable<HttpPostedFileBase> imageFiles, string attrJson, string removeImageIds)
        {
            var deny = Guard(); if (deny != null) return deny;
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                TempData["Error"] = "عنوان الزامی است.";
                return RedirectToAction("EditProduct", new { company = CompanyCode, id = model.Id });
            }

            var existing = model.Id > 0 ? TenantStore.GetProduct(CompanyCode, model.Id) : null;
            if (existing != null)
            {
                model.Images = existing.Images ?? new List<StoreProductImage>();
                model.CreatedAt = existing.CreatedAt;
                model.SoldCount = existing.SoldCount;
                model.ViewCount = existing.ViewCount;
            }
            else model.Images = new List<StoreProductImage>();

            if (!string.IsNullOrWhiteSpace(removeImageIds))
            {
                var remove = removeImageIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).ToList();
                model.Images = model.Images.Where(i => i.Id == null || !remove.Contains(i.Id)).ToList();
            }

            var uploaded = ImageService.SaveProductImages(CompanyCode, imageFiles);
            var sort = model.Images.Any() ? model.Images.Max(i => i.SortOrder) + 1 : 1;
            foreach (var saved in uploaded)
            {
                model.Images.Add(new StoreProductImage
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ThumbUrl = saved.ThumbUrl,
                    LargeUrl = saved.LargeUrl,
                    Alt = model.Title,
                    SortOrder = sort++
                });
            }

            if (!string.IsNullOrWhiteSpace(attrJson))
            {
                try { model.Attributes = JsonConvert.DeserializeObject<List<StoreProductAttribute>>(attrJson) ?? new List<StoreProductAttribute>(); }
                catch { model.Attributes = model.Attributes ?? new List<StoreProductAttribute>(); }
            }

            if (string.IsNullOrWhiteSpace(model.Slug))
                model.Slug = "p-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            model = TenantStore.UpsertProduct(CompanyCode, model);
            TempData["Ok"] = "محصول ذخیره شد.";
            return RedirectToAction("EditProduct", new { company = CompanyCode, id = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteProduct(int id)
        {
            var deny = Guard(); if (deny != null) return deny;
            TenantStore.DeleteProduct(CompanyCode, id);
            TempData["Ok"] = "محصول حذف شد.";
            return RedirectToAction("Products", new { company = CompanyCode });
        }

        public ActionResult Categories()
        {
            var deny = Guard(); if (deny != null) return deny;
            return View(TenantStore.GetCategories(CompanyCode));
        }

        [HttpGet]
        public ActionResult EditCategory(int? id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetCategories(CompanyCode);
            var model = id.HasValue ? list.FirstOrDefault(c => c.Id == id.Value) : new StoreCategory { IsActive = true, SortOrder = 1 };
            ViewBag.Categories = list;
            return View(model ?? new StoreCategory { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCategory(StoreCategory model)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetCategories(CompanyCode);
            if (model.Id <= 0)
            {
                model.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                if (string.IsNullOrWhiteSpace(model.Slug)) model.Slug = "cat-" + model.Id;
                list.Add(model);
            }
            else
            {
                var idx = list.FindIndex(x => x.Id == model.Id);
                if (idx >= 0) list[idx] = model;
            }
            TenantStore.SaveCategories(CompanyCode, list);
            TempData["Ok"] = "گروه ذخیره شد.";
            return RedirectToAction("Categories", new { company = CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCategory(int id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetCategories(CompanyCode);
            list.RemoveAll(c => c.Id == id || c.ParentId == id);
            TenantStore.SaveCategories(CompanyCode, list);
            TempData["Ok"] = "گروه حذف شد.";
            return RedirectToAction("Categories", new { company = CompanyCode });
        }

        public ActionResult Attributes()
        {
            var deny = Guard(); if (deny != null) return deny;
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            return View(TenantStore.GetAttributeDefs(CompanyCode));
        }

        [HttpGet]
        public ActionResult EditAttribute(int? id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetAttributeDefs(CompanyCode);
            var model = id.HasValue ? list.FirstOrDefault(a => a.Id == id.Value) : new StoreAttributeDef { InputType = "text", SortOrder = 1, CategoryIds = new List<int>() };
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            return View(model ?? new StoreAttributeDef());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveAttribute(StoreAttributeDef model, int[] categoryIds)
        {
            var deny = Guard(); if (deny != null) return deny;
            model.CategoryIds = categoryIds?.ToList() ?? new List<int>();
            var list = TenantStore.GetAttributeDefs(CompanyCode);
            if (model.Id <= 0)
            {
                model.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                list.Add(model);
            }
            else
            {
                var idx = list.FindIndex(x => x.Id == model.Id);
                if (idx >= 0) list[idx] = model;
            }
            TenantStore.SaveAttributeDefs(CompanyCode, list);
            TempData["Ok"] = "خصوصیت ذخیره شد.";
            return RedirectToAction("Attributes", new { company = CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteAttribute(int id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetAttributeDefs(CompanyCode);
            list.RemoveAll(a => a.Id == id);
            TenantStore.SaveAttributeDefs(CompanyCode, list);
            TempData["Ok"] = "خصوصیت حذف شد.";
            return RedirectToAction("Attributes", new { company = CompanyCode });
        }

        public ActionResult Menus()
        {
            var deny = Guard(); if (deny != null) return deny;
            return View(TenantStore.GetMenus(CompanyCode));
        }

        [HttpGet]
        public ActionResult EditMenu(int? id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetMenus(CompanyCode);
            var model = id.HasValue ? list.FirstOrDefault(m => m.Id == id.Value) : new StoreMenuItem { IsActive = true, Location = "main", SortOrder = 1 };
            ViewBag.Menus = list;
            return View(model ?? new StoreMenuItem { IsActive = true, Location = "main" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveMenu(StoreMenuItem model)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetMenus(CompanyCode);
            if (model.Id <= 0)
            {
                model.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                list.Add(model);
            }
            else
            {
                var idx = list.FindIndex(x => x.Id == model.Id);
                if (idx >= 0) list[idx] = model;
            }
            TenantStore.SaveMenus(CompanyCode, list);
            TempData["Ok"] = "منو ذخیره شد.";
            return RedirectToAction("Menus", new { company = CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMenu(int id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetMenus(CompanyCode);
            list.RemoveAll(m => m.Id == id || m.ParentId == id);
            TenantStore.SaveMenus(CompanyCode, list);
            TempData["Ok"] = "منو حذف شد.";
            return RedirectToAction("Menus", new { company = CompanyCode });
        }

        public ActionResult Pages()
        {
            var deny = Guard(); if (deny != null) return deny;
            return View(TenantStore.GetPages(CompanyCode));
        }

        [HttpGet]
        public ActionResult EditPage(int? id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var page = id.HasValue
                ? TenantStore.GetPages(CompanyCode).FirstOrDefault(p => p.Id == id.Value)
                : new StorePage { IsPublished = true };
            return View(page ?? new StorePage());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EditPage(StorePage model)
        {
            var deny = Guard(); if (deny != null) return deny;
            TenantStore.UpsertPage(CompanyCode, model);
            TempData["Ok"] = "صفحه ذخیره شد.";
            return RedirectToAction("Pages", new { company = CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeletePage(int id)
        {
            var deny = Guard(); if (deny != null) return deny;
            TenantStore.DeletePage(CompanyCode, id);
            TempData["Ok"] = "صفحه حذف شد.";
            return RedirectToAction("Pages", new { company = CompanyCode });
        }

        public ActionResult Gallery()
        {
            var deny = Guard(); if (deny != null) return deny;
            return View(TenantStore.GetGallery(CompanyCode));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadGallery(string title, string album, IEnumerable<HttpPostedFileBase> imageFiles)
        {
            var deny = Guard(); if (deny != null) return deny;
            var uploaded = ImageService.SaveProductImages(CompanyCode, imageFiles);
            if (!uploaded.Any())
            {
                TempData["Error"] = "تصویری انتخاب نشده است.";
                return RedirectToAction("Gallery", new { company = CompanyCode });
            }
            var list = TenantStore.GetGallery(CompanyCode);
            var nextId = list.Any() ? list.Max(x => x.Id) + 1 : 1;
            var sort = list.Count + 1;
            foreach (var saved in uploaded)
            {
                list.Add(new StoreGalleryItem
                {
                    Id = nextId++,
                    Title = string.IsNullOrWhiteSpace(title) ? "تصویر" : title,
                    Album = string.IsNullOrWhiteSpace(album) ? "عمومی" : album,
                    ThumbUrl = saved.ThumbUrl,
                    LargeUrl = saved.LargeUrl,
                    SortOrder = sort++
                });
            }
            TenantStore.SaveGallery(CompanyCode, list);
            TempData["Ok"] = uploaded.Count + " تصویر اضافه شد.";
            return RedirectToAction("Gallery", new { company = CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteGalleryItem(int id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetGallery(CompanyCode);
            list.RemoveAll(g => g.Id == id);
            TenantStore.SaveGallery(CompanyCode, list);
            TempData["Ok"] = "تصویر حذف شد.";
            return RedirectToAction("Gallery", new { company = CompanyCode });
        }

        public ActionResult Reviews()
        {
            var deny = Guard(); if (deny != null) return deny;
            ViewBag.Products = TenantStore.GetProducts(CompanyCode).ToDictionary(p => p.Id, p => p.Title);
            return View(TenantStore.GetReviews(CompanyCode));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleReview(int id, bool approved)
        {
            var deny = Guard(); if (deny != null) return deny;
            var review = TenantStore.GetReviews(CompanyCode).FirstOrDefault(r => r.Id == id);
            if (review != null)
            {
                review.IsApproved = approved;
                TenantStore.UpdateReview(CompanyCode, review);
            }
            return RedirectToAction("Reviews", new { company = CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteReview(int id)
        {
            var deny = Guard(); if (deny != null) return deny;
            TenantStore.DeleteReview(CompanyCode, id);
            TempData["Ok"] = "نظر حذف شد.";
            return RedirectToAction("Reviews", new { company = CompanyCode });
        }

        public ActionResult Orders()
        {
            var deny = Guard(); if (deny != null) return deny;
            return View(TenantStore.GetOrders(CompanyCode));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateOrderStatus(int id, StoreOrderStatus status)
        {
            var deny = Guard(); if (deny != null) return deny;
            var order = TenantStore.GetOrder(CompanyCode, id);
            if (order != null)
            {
                order.Status = status;
                TenantStore.UpdateOrder(CompanyCode, order);
            }
            return RedirectToAction("Orders", new { company = CompanyCode });
        }

        public ActionResult Customers()
        {
            var deny = Guard(); if (deny != null) return deny;
            return View(TenantStore.GetCustomers(CompanyCode));
        }
    }
}
