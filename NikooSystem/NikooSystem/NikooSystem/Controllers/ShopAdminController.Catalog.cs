using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Products (with the files and lessons of digital ones), collections and attributes.</summary>
    public partial class ShopAdminController
    {
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Products(ProductGridQuery query)
        {
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            ViewBag.Query = query;
            return View(TenantStore.ProductGrid(CompanyCode, query, CurrentSettings.LowStockThreshold));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult EditProduct(int? id)
        {
            var product = id.HasValue
                ? TenantStore.GetProduct(CompanyCode, id.Value)
                : new StoreProduct { IsActive = true, Stock = 10, ProductType = Kind.IsDigital ? StoreProductType.Course : StoreProductType.Physical };
            if (product == null) return ToAction("Products");
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            ViewBag.Collections = TenantStore.GetCollections(CompanyCode);
            ViewBag.AttributeDefs = product.CategoryId > 0
                ? TenantStore.GetAttributeDefsForCategory(CompanyCode, product.CategoryId)
                : TenantStore.GetAttributeDefs(CompanyCode);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult EditProduct(StoreProduct model, IEnumerable<HttpPostedFileBase> imageFiles, string attrJson, string removeImageIds, string lessonsJson)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Fail("عنوان الزامی است.", "EditProduct", new { id = model.Id });

            var existing = model.Id > 0 ? TenantStore.GetProduct(CompanyCode, model.Id) : null;
            model.ProductType = AllowedProductType(model.ProductType);
            model.Images = existing?.Images ?? new List<StoreProductImage>();
            model.Files = existing?.Files ?? new List<StoreProductFile>();
            model.Author = string.IsNullOrWhiteSpace(model.Author) ? null : model.Author.Trim();
            model.PreviewUrl = HomeSections.IsSafeUrl(model.PreviewUrl) ? model.PreviewUrl.Trim() : null;
            if (model.CompareAtPrice.HasValue && model.CompareAtPrice.Value <= 0) model.CompareAtPrice = null;
            CleanSeo(model);

            if (!string.IsNullOrWhiteSpace(removeImageIds))
            {
                var remove = removeImageIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).ToList();
                model.Images = model.Images.Where(i => i.Id == null || !remove.Contains(i.Id)).ToList();
            }

            var imageErrors = new List<string>();
            AddImages(model, ImageService.SaveProductImages(CompanyCode, imageFiles, imageErrors));
            if (imageErrors.Any()) Fail(ImageErrors(imageErrors));

            if (!string.IsNullOrWhiteSpace(attrJson))
            {
                try { model.Attributes = JsonConvert.DeserializeObject<List<StoreProductAttribute>>(attrJson) ?? new List<StoreProductAttribute>(); }
                catch (JsonException) { model.Attributes = model.Attributes ?? new List<StoreProductAttribute>(); }
            }
            model.Lessons = model.ProductType == StoreProductType.Course
                ? CleanLessons(lessonsJson, model.Files, existing?.Lessons)
                : new List<StoreLesson>();

            model = TenantStore.SaveProduct(CompanyCode, model);
            TorobWebhookService.NotifyProductChanged(CompanyCode, model);
            return Ok((model.IsDigital() ? "«" + model.Title + "»" : "محصول") + " ذخیره شد.", "EditProduct", new { id = model.Id });
        }

        private const int MaxLessons = 500;

        /// <summary>Lessons posted by the lesson editor: each needs a title; files must belong to the product and links must be http(s).</summary>
        private static List<StoreLesson> CleanLessons(string json, List<StoreProductFile> files, List<StoreLesson> stored)
        {
            if (string.IsNullOrWhiteSpace(json)) return stored ?? new List<StoreLesson>();
            List<StoreLesson> posted;
            try { posted = JsonConvert.DeserializeObject<List<StoreLesson>>(json) ?? new List<StoreLesson>(); }
            catch (JsonException) { return stored ?? new List<StoreLesson>(); }

            var fileIds = new HashSet<string>(files.Select(f => f.Id));
            return posted
                .Where(l => l != null && !string.IsNullOrWhiteSpace(l.Title))
                .Take(MaxLessons)
                .Select(l => new StoreLesson
                {
                    Id = string.IsNullOrWhiteSpace(l.Id) || l.Id.Length > 32 ? Guid.NewGuid().ToString("N").Substring(0, 10) : l.Id,
                    Section = Clip(l.Section, 120),
                    Title = Clip(l.Title, 200),
                    Summary = Clip(l.Summary, 1000),
                    Duration = Clip(l.Duration, 20),
                    FileId = l.FileId != null && fileIds.Contains(l.FileId) ? l.FileId : null,
                    VideoUrl = HomeSections.IsSafeUrl(l.VideoUrl) ? l.VideoUrl.Trim() : null,
                    IsFree = l.IsFree
                })
                .ToList();
        }

        private static string Clip(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }

        private static void CleanSeo(ISeoFields item)
        {
            item.SeoTitle = Clip(item.SeoTitle, 90);
            item.SeoDescription = Clip(item.SeoDescription, 300);
        }

        private static void CleanSeo(StoreSettings settings)
        {
            settings.SeoTitle = Clip(settings.SeoTitle, 90);
            settings.SeoDescription = Clip(settings.SeoDescription, 300);
        }

        /// <summary>Appends uploaded images after the product's current ones.</summary>
        private static void AddImages(StoreProduct product, IEnumerable<ImageSaveResult> uploaded)
        {
            var sort = product.Images.Select(i => i.SortOrder).DefaultIfEmpty(0).Max();
            foreach (var saved in uploaded.Where(u => u != null))
            {
                product.Images.Add(new StoreProductImage
                {
                    ThumbUrl = saved.ThumbUrl,
                    LargeUrl = saved.LargeUrl,
                    Alt = product.Title,
                    SortOrder = ++sort
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult DeleteProduct(int id)
        {
            TenantStore.DeleteProduct(CompanyCode, id);
            return OkBack("محصول حذف شد.", "Products");
        }

        // ---- Protected files of digital products

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Digital)]
        public ActionResult UploadProductFile(int id, string title, HttpPostedFileBase file)
        {
            var product = TenantStore.GetProduct(CompanyCode, id);
            if (product == null) return ToAction("Products");
            try
            {
                var saved = DigitalFileService.Save(CompanyCode, file, title);
                if (saved == null) return Fail("فایلی انتخاب نشده است.", "EditProduct", new { id });
                product.Files.Add(saved);
                TenantStore.SaveProduct(CompanyCode, product);
                return Ok("فایل «" + saved.Title + "» (" + DigitalFileService.FormatSize(saved.Size) + ") بارگذاری شد.", "EditProduct", new { id });
            }
            catch (InvalidOperationException ex)
            {
                return Fail(ex.Message, "EditProduct", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Digital)]
        public ActionResult DeleteProductFile(int id, string fileId)
        {
            var product = TenantStore.GetProduct(CompanyCode, id);
            var file = product?.Files.FirstOrDefault(f => f.Id == fileId);
            if (file == null) return ToAction("EditProduct", new { id });
            product.Files.Remove(file);
            foreach (var lesson in product.Lessons.Where(l => l.FileId == fileId)) lesson.FileId = null;
            TenantStore.SaveProduct(CompanyCode, product);
            DigitalFileService.Delete(CompanyCode, file);
            return Ok("فایل حذف شد.", "EditProduct", new { id });
        }

        /// <summary>The admin previews a protected file without buying it.</summary>
        [HttpGet]
        [ForKinds(SiteKinds.Digital)]
        public ActionResult ProductFile(int id, string fileId)
        {
            var file = TenantStore.GetProduct(CompanyCode, id)?.Files.FirstOrDefault(f => f.Id == fileId);
            var path = DigitalFileService.PathOf(CompanyCode, file);
            if (path == null) return HttpNotFound();
            DigitalFileService.Send(HttpContext, path, file, false);
            return new EmptyResult();
        }

        // ---- Collections (product tags) and the home page rows that show them

        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Collections(GridQuery query)
        {
            ViewBag.ProductCounts = TenantStore.ProductCountsByCollection(CompanyCode);
            ViewBag.OnHome = new HashSet<int>(HomeSections.Resolve(CurrentSettings)
                .Where(s => s.IsVisible && IsCollectionRow(s))
                .Select(s => s.CollectionId.Value));
            return View(TenantStore.CollectionGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult EditCollection(int? id)
        {
            var model = id.HasValue ? TenantStore.GetCollection(CompanyCode, id.Value) : new StoreCollection { IsActive = true };
            if (model == null) return ToAction("Collections");
            ViewBag.Products = TenantStore.GetProducts(CompanyCode).OrderBy(p => p.Title).ToList();
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            ViewBag.OnHome = model.Id == 0 || HomeSections.Resolve(CurrentSettings).Any(s => s.IsVisible && IsCollectionRow(s, model.Id));
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult SaveCollection(StoreCollection model, int[] productIds, bool showOnHome = false)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Fail("عنوان برچسب الزامی است.", "EditCollection", model.Id > 0 ? new { id = model.Id } : null);

            model.Title = model.Title.Trim();
            model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            TenantStore.SaveCollection(CompanyCode, model, productIds);

            var onHome = showOnHome && model.IsActive;
            SetCollectionOnHome(model.Id, onHome);
            return Ok("برچسب «" + model.Title + "» ذخیره شد" + (onHome ? " و در صفحه اول نمایش داده می‌شود." : "."), "Collections");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult DeleteCollection(int id)
        {
            RemoveCollection(id);
            return OkBack("برچسب حذف شد.", "Collections");
        }

        /// <summary>Deletes the collection, untags its products and drops the home page rows that listed it.</summary>
        private void RemoveCollection(int id)
        {
            TenantStore.DeleteCollection(CompanyCode, id);
            var sections = HomeSections.Resolve(CurrentSettings);
            if (sections.RemoveAll(s => IsCollectionRow(s, id)) > 0)
                SaveHomeSections(sections);
        }

        /// <summary>A home page row listing the products of a collection (of the given one, when <paramref name="collectionId"/> is set).</summary>
        private static bool IsCollectionRow(StoreHomeSection s, int? collectionId = null)
        {
            return s.Type == "products" && s.Source == "collection" && s.CollectionId.HasValue
                && (collectionId == null || s.CollectionId == collectionId);
        }

        /// <summary>نمایش یا مخفی‌کردن ردیف محصولات یک برچسب در صفحه اول</summary>
        private void SetCollectionOnHome(int collectionId, bool show)
        {
            var sections = HomeSections.Resolve(CurrentSettings);
            var linked = sections.Where(s => IsCollectionRow(s, collectionId)).ToList();
            if (show && !linked.Any())
            {
                var hero = sections.FindIndex(s => s.Type == "hero");
                sections.Insert(hero + 1, new StoreHomeSection
                {
                    Id = HomeSections.NewId(),
                    Type = "products",
                    Source = "collection",
                    CollectionId = collectionId,
                    Count = 8
                });
            }
            else if (linked.Any(s => s.IsVisible != show))
            {
                foreach (var s in linked) s.IsVisible = show;
            }
            else return;
            SaveHomeSections(sections);
        }

        private void SaveHomeSections(List<StoreHomeSection> sections)
        {
            var settings = CurrentSettings;
            if (settings.PageLayout == null) settings.PageLayout = new StorePageLayout();
            settings.PageLayout.HomeSections = HomeSections.Sanitize(sections, settings);
            TenantStore.SaveSettings(settings);
        }

        // ---- Attributes

        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Attributes(GridQuery query)
        {
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            return View(TenantStore.AttributeGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult EditAttribute(int? id)
        {
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            return View((id.HasValue ? TenantStore.GetAttributeDef(CompanyCode, id.Value) : null)
                ?? new StoreAttributeDef { InputType = "text", SortOrder = 1, CategoryIds = new List<int>() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult SaveAttribute(StoreAttributeDef model, int[] categoryIds)
        {
            model.CategoryIds = categoryIds?.ToList() ?? new List<int>();
            TenantStore.SaveAttributeDef(CompanyCode, model);
            return Ok("خصوصیت ذخیره شد.", "Attributes");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult DeleteAttribute(int id)
        {
            TenantStore.DeleteAttributeDef(CompanyCode, id);
            return OkBack("خصوصیت حذف شد.", "Attributes");
        }
    }
}
