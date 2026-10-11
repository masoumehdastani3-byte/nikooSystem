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
            ViewBag.Checklist = BuildChecklist();
            return View();
        }

        private List<SetupChecklistItem> BuildChecklist()
        {
            var s = CurrentSettings;
            var products = TenantStore.GetProducts(CompanyCode);
            return new List<SetupChecklistItem>
            {
                new SetupChecklistItem
                {
                    Title = "لوگو و نام فروشگاه",
                    Hint = "لوگوی خودتان را بارگذاری کنید تا مشتری فروشگاه را بشناسد.",
                    Done = TenantProvisioningService.HasCustomLogo(s),
                    Url = Url.Action("Setup", new { company = CompanyCode, step = 1 })
                },
                new SetupChecklistItem
                {
                    Title = "اطلاعات تماس",
                    Hint = "تلفن و آدرس در فوتر و صفحه تماس نمایش داده می‌شود.",
                    Done = !string.IsNullOrWhiteSpace(s.Phone) && !string.IsNullOrWhiteSpace(s.Address),
                    Url = Url.Action("Setup", new { company = CompanyCode, step = 1 })
                },
                new SetupChecklistItem
                {
                    Title = "افزودن اولین محصول واقعی",
                    Hint = "محصول خودتان را با عکس و قیمت اضافه کنید.",
                    Done = products.Any(p => !p.IsSample),
                    Url = Url.Action("Setup", new { company = CompanyCode, step = 3 })
                },
                new SetupChecklistItem
                {
                    Title = "حذف محصولات نمونه",
                    Hint = "محصولات نمونه فقط برای نمایش ظاهر فروشگاه هستند.",
                    Done = !products.Any(p => p.IsSample),
                    Url = Url.Action("Setup", new { company = CompanyCode, step = 3 })
                },
                new SetupChecklistItem
                {
                    Title = "اتصال درگاه پرداخت زرین‌پال",
                    Hint = "بدون درگاه، سفارش ثبت می‌شود ولی پرداخت آنلاین انجام نمی‌شود.",
                    Done = s.EnablePayment && TenantProvisioningService.HasValidMerchant(s),
                    Url = Url.Action("Setup", new { company = CompanyCode, step = 4 })
                },
                new SetupChecklistItem
                {
                    Title = "اتصال دامنه اختصاصی",
                    Hint = "اختیاری؛ فروشگاه همین حالا با آدرس رایگان در دسترس است.",
                    Done = Tenant != null && !string.IsNullOrWhiteSpace(Tenant.CustomDomain),
                    Optional = true,
                    Url = Url.Action("Setup", new { company = CompanyCode, step = 5 })
                }
            };
        }

        [HttpGet]
        public ActionResult Setup(int step = 1)
        {
            var deny = Guard(); if (deny != null) return deny;
            step = Math.Max(1, Math.Min(5, step));
            var products = TenantStore.GetProducts(CompanyCode);
            ViewBag.Step = step;
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            ViewBag.SampleCount = products.Count(p => p.IsSample);
            ViewBag.RealProductCount = products.Count(p => !p.IsSample);
            ViewBag.StoreUrl = TenantProvisioningService.DefaultStoreUrl(CompanyCode, Request);
            ViewBag.Welcome = TempData["Welcome"] != null;
            return View(CurrentSettings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupInfo(string storeName, string tagline, string phone, string address, HttpPostedFileBase logoFile)
        {
            var deny = Guard(); if (deny != null) return deny;
            if (string.IsNullOrWhiteSpace(storeName))
                return SetupError(1, "نام فروشگاه را وارد کنید.");

            var s = CurrentSettings;
            try
            {
                var logo = ImageService.SaveBrandImage(CompanyCode, logoFile, "logo", 480, 240);
                if (logo != null) s.LogoUrl = logo;
            }
            catch (InvalidOperationException ex)
            {
                return SetupError(1, ex.Message);
            }

            var oldName = s.StoreName;
            s.StoreName = storeName.Trim();
            if (!string.IsNullOrEmpty(oldName) && oldName != s.StoreName)
            {
                if (s.About != null) s.About = s.About.Replace(oldName, s.StoreName);
                if (s.FooterText != null) s.FooterText = s.FooterText.Replace(oldName, s.StoreName);
            }
            s.Tagline = (tagline ?? "").Trim();
            s.Phone = (phone ?? "").Trim();
            s.Address = (address ?? "").Trim();
            TenantStore.SaveSettings(s);

            if (Tenant != null && Tenant.Name != s.StoreName)
            {
                Tenant.Name = s.StoreName;
                TenantStore.UpdateTenant(Tenant);
            }
            return RedirectToAction("Setup", new { company = CompanyCode, step = 2 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupLook(string layoutStyle, string primaryColor, string secondaryColor, string heroTitle, string heroSubtitle, HttpPostedFileBase heroFile)
        {
            var deny = Guard(); if (deny != null) return deny;
            var s = CurrentSettings;
            try
            {
                var hero = ImageService.SaveBrandImage(CompanyCode, heroFile, "hero", 1600, 1000);
                if (hero != null) s.HeroImageUrl = hero;
            }
            catch (InvalidOperationException ex)
            {
                return SetupError(2, ex.Message);
            }

            s.LayoutStyle = StoreTemplates.Normalize(layoutStyle);
            if (!string.IsNullOrWhiteSpace(primaryColor)) s.PrimaryColor = primaryColor;
            if (!string.IsNullOrWhiteSpace(secondaryColor)) s.SecondaryColor = secondaryColor;
            if (!string.IsNullOrWhiteSpace(heroTitle)) s.HeroTitle = heroTitle.Trim();
            if (heroSubtitle != null) s.HeroSubtitle = heroSubtitle.Trim();
            TenantStore.SaveSettings(s);
            return RedirectToAction("Setup", new { company = CompanyCode, step = 3 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupProduct(string title, long? price, int? categoryId, int? stock, string shortDescription, HttpPostedFileBase imageFile, bool removeSamples = false)
        {
            var deny = Guard(); if (deny != null) return deny;

            if (removeSamples)
                TenantStore.SaveProducts(CompanyCode, TenantStore.GetProducts(CompanyCode).Where(p => !p.IsSample).ToList());

            if (!string.IsNullOrWhiteSpace(title))
            {
                if (!price.HasValue || price.Value < 0)
                    return SetupError(3, "قیمت محصول را به تومان وارد کنید.");

                var categories = TenantStore.GetCategories(CompanyCode);
                var catId = categoryId.HasValue && categories.Any(c => c.Id == categoryId.Value)
                    ? categoryId.Value
                    : categories.Select(c => c.Id).FirstOrDefault();

                var product = new StoreProduct
                {
                    Title = title.Trim(),
                    ShortDescription = (shortDescription ?? "").Trim(),
                    Description = (shortDescription ?? "").Trim(),
                    Price = price.Value,
                    CategoryId = catId,
                    Stock = stock ?? 10,
                    IsActive = true,
                    IsFeatured = true,
                    Slug = "p-" + Guid.NewGuid().ToString("N").Substring(0, 8)
                };

                try
                {
                    var saved = ImageService.SaveProductImage(CompanyCode, imageFile);
                    if (saved != null)
                        product.Images.Add(new StoreProductImage { ThumbUrl = saved.ThumbUrl, LargeUrl = saved.LargeUrl, Alt = product.Title, SortOrder = 1 });
                }
                catch (InvalidOperationException ex)
                {
                    return SetupError(3, ex.Message);
                }
                catch (ArgumentException)
                {
                    return SetupError(3, "فایل انتخاب‌شده تصویر معتبر نیست.");
                }

                product = TenantStore.UpsertProduct(CompanyCode, product);
                TorobWebhookService.NotifyProductChanged(CompanyCode, product);
                TempData["Ok"] = "محصول «" + product.Title + "» اضافه شد.";
            }

            return RedirectToAction("Setup", new { company = CompanyCode, step = 4 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupPayment(string merchantId, bool enablePayment = false)
        {
            var deny = Guard(); if (deny != null) return deny;
            var s = CurrentSettings;
            merchantId = (merchantId ?? "").Trim();

            Guid parsed;
            if (merchantId.Length > 0 && !Guid.TryParse(merchantId, out parsed))
                return SetupError(4, "مرچنت کد زرین‌پال ۳۶ کاراکتر است؛ آن را دقیقاً از پنل زرین‌پال کپی کنید.");
            if (enablePayment && merchantId.Length == 0)
                return SetupError(4, "برای فعال‌سازی پرداخت آنلاین، مرچنت کد را وارد کنید.");

            s.ZarinPalMerchantId = merchantId;
            s.ZarinPalSandbox = false;
            s.EnablePayment = enablePayment && merchantId.Length > 0;
            TenantStore.SaveSettings(s);
            return RedirectToAction("Setup", new { company = CompanyCode, step = 5 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupDomain(string customDomain)
        {
            var deny = Guard(); if (deny != null) return deny;
            var error = SaveCustomDomain(customDomain);
            if (error != null) return SetupError(5, error);

            var s = CurrentSettings;
            s.OnboardingCompleted = true;
            TenantStore.SaveSettings(s);
            TempData["Ok"] = "تبریک! فروشگاه شما آماده فروش است.";
            return RedirectToAction("Index", new { company = CompanyCode });
        }

        private ActionResult SetupError(int step, string message)
        {
            TempData["Error"] = message;
            return RedirectToAction("Setup", new { company = CompanyCode, step });
        }

        /// <summary>ذخیره دامنه اختصاصی در رجیستری؛ پیام خطا یا null</summary>
        private string SaveCustomDomain(string input)
        {
            if (Tenant == null) return null;
            var domain = TenantProvisioningService.NormalizeDomain(input);
            if (string.Equals(domain, Tenant.CustomDomain ?? "", StringComparison.OrdinalIgnoreCase)) return null;

            var error = TenantProvisioningService.ValidateDomain(CompanyCode, domain);
            if (error != null) return error;

            Tenant.CustomDomain = domain;
            TenantStore.UpdateTenant(Tenant);
            return null;
        }

        [HttpGet]
        public ActionResult Settings()
        {
            var deny = Guard(); if (deny != null) return deny;
            ViewBag.StoreUrl = TenantProvisioningService.DefaultStoreUrl(CompanyCode, Request);
            return View(CurrentSettings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Settings(StoreSettings model, string newPassword, string customDomain, HttpPostedFileBase logoFile, HttpPostedFileBase heroFile, bool removeLogo = false)
        {
            var deny = Guard(); if (deny != null) return deny;
            var existing = CurrentSettings ?? TenantStore.GetSettings(CompanyCode);
            model.CompanyCode = CompanyCode;
            model.LogoUrl = removeLogo ? null : existing?.LogoUrl;
            model.FaviconUrl = existing?.FaviconUrl;
            model.OnboardingCompleted = existing?.OnboardingCompleted ?? false;
            if (string.IsNullOrWhiteSpace(model.HeroImageUrl))
                model.HeroImageUrl = existing?.HeroImageUrl;
            try
            {
                var logo = ImageService.SaveBrandImage(CompanyCode, logoFile, "logo", 480, 240);
                if (logo != null) model.LogoUrl = logo;
                var hero = ImageService.SaveBrandImage(CompanyCode, heroFile, "hero", 1600, 1000);
                if (hero != null) model.HeroImageUrl = hero;
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Settings", new { company = CompanyCode });
            }
            model.AdminUsername = string.IsNullOrWhiteSpace(model.AdminUsername)
                ? (existing?.AdminUsername ?? "admin")
                : model.AdminUsername;
            model.AdminPasswordHash = string.IsNullOrWhiteSpace(newPassword)
                ? (existing?.AdminPasswordHash ?? PasswordHelper.Hash("Admin@123"))
                : PasswordHelper.Hash(newPassword);
            model.PageLayout = existing?.PageLayout;
            if (string.IsNullOrWhiteSpace(model.TorobWebhookBearerToken))
                model.TorobWebhookBearerToken = existing?.TorobWebhookBearerToken;
            if (string.IsNullOrWhiteSpace(model.PublicStoreBaseUrl))
                model.PublicStoreBaseUrl = existing?.PublicStoreBaseUrl;
            model.LayoutStyle = StoreTemplates.Normalize(model.LayoutStyle);
            TenantStore.SaveSettings(model);

            var domainError = SaveCustomDomain(customDomain);
            if (domainError != null)
                TempData["Error"] = "سایر تنظیمات ذخیره شد، اما دامنه ذخیره نشد: " + domainError;
            else
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
            ViewBag.Collections = TenantStore.GetCollections(CompanyCode);
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

            var imageErrors = new List<string>();
            var uploaded = ImageService.SaveProductImages(CompanyCode, imageFiles, imageErrors);
            if (imageErrors.Any())
                TempData["Error"] = "این تصاویر ذخیره نشدند — " + string.Join(" | ", imageErrors);
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
            TorobWebhookService.NotifyProductChanged(CompanyCode, model);
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

        public ActionResult Collections()
        {
            var deny = Guard(); if (deny != null) return deny;
            var products = TenantStore.GetProducts(CompanyCode);
            var sections = HomeSections.Resolve(CurrentSettings);
            var list = TenantStore.GetCollections(CompanyCode);
            ViewBag.ProductCounts = list.ToDictionary(c => c.Id, c => products.Count(p => p.CollectionIds != null && p.CollectionIds.Contains(c.Id)));
            ViewBag.OnHome = new HashSet<int>(sections
                .Where(s => s.IsVisible && s.Type == "products" && s.Source == "collection" && s.CollectionId.HasValue)
                .Select(s => s.CollectionId.Value));
            return View(list);
        }

        [HttpGet]
        public ActionResult EditCollection(int? id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var model = id.HasValue ? TenantStore.GetCollections(CompanyCode).FirstOrDefault(c => c.Id == id.Value) : null;
            if (id.HasValue && model == null) return RedirectToAction("Collections", new { company = CompanyCode });
            model = model ?? new StoreCollection { IsActive = true };
            ViewBag.Products = TenantStore.GetProducts(CompanyCode).OrderBy(p => p.Title).ToList();
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            ViewBag.OnHome = model.Id == 0 || HomeSections.Resolve(CurrentSettings).Any(s =>
                s.IsVisible && s.Type == "products" && s.Source == "collection" && s.CollectionId == model.Id);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCollection(StoreCollection model, int[] productIds, bool showOnHome = false)
        {
            var deny = Guard(); if (deny != null) return deny;
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                TempData["Error"] = "عنوان برچسب الزامی است.";
                return model.Id > 0
                    ? RedirectToAction("EditCollection", new { company = CompanyCode, id = model.Id })
                    : RedirectToAction("EditCollection", new { company = CompanyCode });
            }
            model.Title = model.Title.Trim();
            model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

            var list = TenantStore.GetCollections(CompanyCode);
            var idx = list.FindIndex(c => c.Id == model.Id);
            if (idx < 0)
            {
                model.Id = list.Any() ? list.Max(c => c.Id) + 1 : 1;
                model.CreatedAt = DateTime.Now;
                list.Add(model);
            }
            else
            {
                model.CreatedAt = list[idx].CreatedAt;
                list[idx] = model;
            }
            if (string.IsNullOrWhiteSpace(model.Slug)) model.Slug = "tag-" + model.Id;
            TenantStore.SaveCollections(CompanyCode, list);

            var selected = new HashSet<int>(productIds ?? new int[0]);
            var products = TenantStore.GetProducts(CompanyCode);
            var changed = false;
            foreach (var p in products)
            {
                if (p.CollectionIds == null) p.CollectionIds = new List<int>();
                var has = p.CollectionIds.Contains(model.Id);
                if (selected.Contains(p.Id) && !has) { p.CollectionIds.Add(model.Id); changed = true; }
                else if (!selected.Contains(p.Id) && has) { p.CollectionIds.RemoveAll(x => x == model.Id); changed = true; }
            }
            if (changed) TenantStore.SaveProducts(CompanyCode, products);

            SetCollectionOnHome(model.Id, showOnHome && model.IsActive);
            TempData["Ok"] = "برچسب «" + model.Title + "» ذخیره شد" + (showOnHome && model.IsActive ? " و در صفحه اول نمایش داده می‌شود." : ".");
            return RedirectToAction("Collections", new { company = CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCollection(int id)
        {
            var deny = Guard(); if (deny != null) return deny;
            var list = TenantStore.GetCollections(CompanyCode);
            list.RemoveAll(c => c.Id == id);
            TenantStore.SaveCollections(CompanyCode, list);

            var products = TenantStore.GetProducts(CompanyCode);
            var tagged = products.Where(p => p.CollectionIds != null && p.CollectionIds.Contains(id)).ToList();
            if (tagged.Any())
            {
                foreach (var p in tagged) p.CollectionIds.RemoveAll(x => x == id);
                TenantStore.SaveProducts(CompanyCode, products);
            }

            var sections = HomeSections.Resolve(CurrentSettings);
            if (sections.RemoveAll(s => s.Type == "products" && s.Source == "collection" && s.CollectionId == id) > 0)
                SaveHomeSections(sections);
            TempData["Ok"] = "برچسب حذف شد.";
            return RedirectToAction("Collections", new { company = CompanyCode });
        }

        /// <summary>نمایش یا مخفی‌کردن ردیف محصولات یک برچسب در صفحه اول</summary>
        private void SetCollectionOnHome(int collectionId, bool show)
        {
            var sections = HomeSections.Resolve(CurrentSettings);
            var linked = sections.Where(s => s.Type == "products" && s.Source == "collection" && s.CollectionId == collectionId).ToList();
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

        [HttpGet]
        public ActionResult PageLayout()
        {
            var deny = Guard(); if (deny != null) return deny;
            ViewBag.Sections = HomeSections.Resolve(CurrentSettings);
            ViewBag.Collections = TenantStore.GetCollections(CompanyCode);
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            return View(HomeSections.LayoutOf(CurrentSettings));
        }

        private const int MaxCustomSections = 20;

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PageLayout(string sectionsJson, bool showRelatedProducts = false, int relatedProductsCount = 4,
            bool showReviews = false, bool showSpecifications = false, bool showBuyPerks = false)
        {
            var deny = Guard(); if (deny != null) return deny;
            List<StoreHomeSection> posted;
            try { posted = JsonConvert.DeserializeObject<List<StoreHomeSection>>(sectionsJson ?? "") ?? new List<StoreHomeSection>(); }
            catch (JsonException)
            {
                TempData["Error"] = "اطلاعات بخش‌ها نامعتبر بود؛ دوباره تلاش کنید.";
                return RedirectToAction("PageLayout", new { company = CompanyCode });
            }
            if (posted.Count(s => s != null && HomeSections.IsCustom(s.Type)) > MaxCustomSections)
            {
                TempData["Error"] = "حداکثر " + MaxCustomSections + " بخش دلخواه می‌توانید داشته باشید.";
                return RedirectToAction("PageLayout", new { company = CompanyCode });
            }

            var imageErrors = new List<string>();
            foreach (var sec in posted.Where(s => s != null && s.Type == "banner" && !string.IsNullOrWhiteSpace(s.Id)))
            {
                var file = Request.Files["bannerImage_" + sec.Id];
                try
                {
                    var url = ImageService.SaveBrandImage(CompanyCode, file, "banner", 1600, 800);
                    if (url != null) sec.ImageUrl = url;
                }
                catch (InvalidOperationException ex)
                {
                    imageErrors.Add((sec.Title ?? "بنر") + ": " + ex.Message);
                }
            }

            var settings = CurrentSettings;
            var layout = settings.PageLayout ?? new StorePageLayout();
            layout.HomeSections = HomeSections.Sanitize(posted, settings);
            layout.ShowRelatedProducts = showRelatedProducts;
            layout.RelatedProductsCount = Math.Min(12, Math.Max(1, relatedProductsCount));
            layout.ShowReviews = showReviews;
            layout.ShowSpecifications = showSpecifications;
            layout.ShowBuyPerks = showBuyPerks;
            settings.PageLayout = layout;
            TenantStore.SaveSettings(settings);

            if (imageErrors.Any())
                TempData["Error"] = "چیدمان ذخیره شد، اما این تصاویر ذخیره نشدند — " + string.Join(" | ", imageErrors);
            else
                TempData["Ok"] = "چیدمان صفحات ذخیره شد.";
            return RedirectToAction("PageLayout", new { company = CompanyCode });
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
            var imageErrors = new List<string>();
            var uploaded = ImageService.SaveProductImages(CompanyCode, imageFiles, imageErrors);
            if (imageErrors.Any())
                TempData["Error"] = "این تصاویر ذخیره نشدند — " + string.Join(" | ", imageErrors);
            if (!uploaded.Any())
            {
                if (!imageErrors.Any()) TempData["Error"] = "تصویری انتخاب نشده است.";
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
