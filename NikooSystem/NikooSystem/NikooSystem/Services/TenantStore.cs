using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json;
using NikooSystem.Models.Store;

namespace NikooSystem.Services
{
    public static class TenantStore
    {
        private static readonly object RegistryLock = new object();
        private static readonly Dictionary<string, object> TenantLocks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private static string DataRoot
        {
            get
            {
                var path = HostingEnvironment.MapPath("~/App_Data/Tenants");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                return path;
            }
        }

        private static string RegistryFile => Path.Combine(HostingEnvironment.MapPath("~/App_Data"), "tenants.json");

        private static object LockFor(string companyCode)
        {
            lock (TenantLocks)
            {
                if (!TenantLocks.ContainsKey(companyCode))
                    TenantLocks[companyCode] = new object();
                return TenantLocks[companyCode];
            }
        }

        private static string TenantDir(string companyCode)
        {
            var dir = Path.Combine(DataRoot, companyCode);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private static string PathOf(string companyCode, string file) => Path.Combine(TenantDir(companyCode), file);

        public static List<TenantInfo> GetTenants()
        {
            lock (RegistryLock)
            {
                EnsureSeed();
                return ReadList<TenantInfo>(RegistryFile);
            }
        }

        /// <summary>فروشگاه فعال است و دوره آزمایشی‌اش تمام نشده</summary>
        public static bool IsLive(TenantInfo tenant)
        {
            return tenant != null && tenant.IsActive && !tenant.IsTrialExpired();
        }

        public static TenantInfo GetTenant(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return null;
            return GetTenants().FirstOrDefault(t =>
                string.Equals(t.CompanyCode, companyCode, StringComparison.OrdinalIgnoreCase) && IsLive(t));
        }

        public static TenantInfo ResolveByHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            host = host.Split(':')[0].Trim().TrimEnd('.').ToLowerInvariant();
            if (host == "localhost" || host == "127.0.0.1") return null;

            var root = TenantProvisioningService.RootDomain;
            if (!string.IsNullOrEmpty(root) && host.EndsWith("." + root, StringComparison.Ordinal))
            {
                var sub = host.Substring(0, host.Length - root.Length - 1);
                if (sub != "www" && sub.IndexOf('.') < 0)
                    return GetTenant(sub);
            }

            var bare = host.StartsWith("www.", StringComparison.Ordinal) ? host.Substring(4) : host;
            return GetTenants().FirstOrDefault(t =>
                IsLive(t) &&
                !string.IsNullOrWhiteSpace(t.CustomDomain) &&
                string.Equals(t.CustomDomain.Trim(), bare, StringComparison.OrdinalIgnoreCase));
        }

        public static bool CompanyCodeExists(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return false;
            return GetTenants().Any(t => string.Equals(t.CompanyCode, companyCode, StringComparison.OrdinalIgnoreCase))
                || Directory.Exists(Path.Combine(DataRoot, companyCode));
        }

        public static void AddTenant(TenantInfo tenant)
        {
            lock (RegistryLock)
            {
                EnsureSeed();
                var list = ReadList<TenantInfo>(RegistryFile);
                if (list.Any(t => string.Equals(t.CompanyCode, tenant.CompanyCode, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("این آدرس فروشگاه قبلاً گرفته شده است.");
                list.Add(tenant);
                WriteList(RegistryFile, list);
            }
        }

        public static void UpdateTenant(TenantInfo tenant)
        {
            lock (RegistryLock)
            {
                EnsureSeed();
                var list = ReadList<TenantInfo>(RegistryFile);
                var idx = list.FindIndex(t => string.Equals(t.CompanyCode, tenant.CompanyCode, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) return;
                list[idx] = tenant;
                WriteList(RegistryFile, list);
            }
        }

        public static void RemoveTenant(string companyCode)
        {
            lock (RegistryLock)
            {
                EnsureSeed();
                var list = ReadList<TenantInfo>(RegistryFile);
                if (list.RemoveAll(t => string.Equals(t.CompanyCode, companyCode, StringComparison.OrdinalIgnoreCase)) > 0)
                    WriteList(RegistryFile, list);
            }
        }

        /// <summary>حذف کامل داده‌ها و فایل‌های آپلودشده فروشگاه</summary>
        public static void DeleteTenantData(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return;
            lock (LockFor(companyCode))
            {
                var dir = Path.Combine(DataRoot, companyCode);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);

                var uploads = HostingEnvironment.MapPath("~/Uploads/" + companyCode);
                if (!string.IsNullOrEmpty(uploads) && Directory.Exists(uploads)) Directory.Delete(uploads, true);
            }
            lock (TenantLocks) TenantLocks.Remove(companyCode);
        }

        /// <summary>ساخت داده اولیه فروشگاه جدید بر اساس نوع کسب‌وکار</summary>
        public static void SeedNewTenant(string companyCode, string storeType, Action<StoreSettings> customize)
        {
            lock (LockFor(companyCode))
            {
                if (File.Exists(PathOf(companyCode, "settings.json")))
                    throw new InvalidOperationException("داده این فروشگاه از قبل وجود دارد.");
                SeedTenantData(companyCode.ToUpperInvariant(), storeType, customize);
            }
        }

        public static string ResolveCompanyCode(HttpRequestBase request, string routeCompany = null)
        {
            if (!string.IsNullOrWhiteSpace(routeCompany))
            {
                var t = GetTenant(routeCompany);
                return t?.CompanyCode;
            }

            var byHost = ResolveByHost(request?.Url?.Host);
            return byHost?.CompanyCode;
        }

        public static StoreSettings GetSettings(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadObject<StoreSettings>(PathOf(companyCode, "settings.json"));
        }

        public static void SaveSettings(StoreSettings settings)
        {
            lock (LockFor(settings.CompanyCode))
                WriteObject(PathOf(settings.CompanyCode, "settings.json"), settings);
        }

        public static List<StoreCategory> GetCategories(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadList<StoreCategory>(PathOf(companyCode, "categories.json")).OrderBy(c => c.SortOrder).ToList();
        }

        public static void SaveCategories(string companyCode, List<StoreCategory> list)
        {
            lock (LockFor(companyCode))
                WriteList(PathOf(companyCode, "categories.json"), list);
        }

        public static List<StoreAttributeDef> GetAttributeDefs(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadList<StoreAttributeDef>(PathOf(companyCode, "attributes.json")).OrderBy(a => a.SortOrder).ToList();
        }

        public static void SaveAttributeDefs(string companyCode, List<StoreAttributeDef> list)
        {
            lock (LockFor(companyCode))
                WriteList(PathOf(companyCode, "attributes.json"), list);
        }

        public static List<StoreProduct> GetProducts(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadList<StoreProduct>(PathOf(companyCode, "products.json")).OrderByDescending(p => p.CreatedAt).ToList();
        }

        public static StoreProduct GetProduct(string companyCode, int id)
        {
            return GetProducts(companyCode).FirstOrDefault(p => p.Id == id);
        }

        public static StoreProduct GetProductBySlug(string companyCode, string slug)
        {
            return GetProducts(companyCode).FirstOrDefault(p =>
                string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }

        public static void SaveProducts(string companyCode, List<StoreProduct> list)
        {
            lock (LockFor(companyCode))
                WriteList(PathOf(companyCode, "products.json"), list);
        }

        public static StoreProduct UpsertProduct(string companyCode, StoreProduct product)
        {
            lock (LockFor(companyCode))
            {
                var list = ReadList<StoreProduct>(PathOf(companyCode, "products.json"));
                if (product.Id <= 0)
                {
                    product.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                    product.CreatedAt = DateTime.Now;
                    if (string.IsNullOrWhiteSpace(product.Slug))
                        product.Slug = "p-" + product.Id;
                    list.Add(product);
                }
                else
                {
                    product.UpdatedAt = DateTime.Now;
                    var idx = list.FindIndex(x => x.Id == product.Id);
                    if (idx >= 0) list[idx] = product;
                    else list.Add(product);
                }
                WriteList(PathOf(companyCode, "products.json"), list);
                return product;
            }
        }

        public static bool DeleteProduct(string companyCode, int id)
        {
            lock (LockFor(companyCode))
            {
                var list = ReadList<StoreProduct>(PathOf(companyCode, "products.json"));
                var removed = list.RemoveAll(x => x.Id == id) > 0;
                if (removed) WriteList(PathOf(companyCode, "products.json"), list);
                return removed;
            }
        }

        public static List<StoreCollection> GetCollections(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadList<StoreCollection>(PathOf(companyCode, "collections.json"))
                    .OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
        }

        public static void SaveCollections(string companyCode, List<StoreCollection> list)
        {
            lock (LockFor(companyCode))
                WriteList(PathOf(companyCode, "collections.json"), list);
        }

        public static List<StoreMenuItem> GetMenus(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadList<StoreMenuItem>(PathOf(companyCode, "menus.json")).OrderBy(m => m.SortOrder).ToList();
        }

        public static void SaveMenus(string companyCode, List<StoreMenuItem> list)
        {
            lock (LockFor(companyCode))
                WriteList(PathOf(companyCode, "menus.json"), list);
        }

        public static List<StorePage> GetPages(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadList<StorePage>(PathOf(companyCode, "pages.json")).ToList();
        }

        public static StorePage GetPageBySlug(string companyCode, string slug)
        {
            return GetPages(companyCode).FirstOrDefault(p =>
                p.IsPublished && string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }

        public static void SavePages(string companyCode, List<StorePage> list)
        {
            lock (LockFor(companyCode))
                WriteList(PathOf(companyCode, "pages.json"), list);
        }

        public static StorePage UpsertPage(string companyCode, StorePage page)
        {
            lock (LockFor(companyCode))
            {
                var list = ReadList<StorePage>(PathOf(companyCode, "pages.json"));
                page.UpdatedAt = DateTime.Now;
                if (page.Id <= 0)
                {
                    page.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                    if (string.IsNullOrWhiteSpace(page.Slug))
                        page.Slug = "page-" + page.Id;
                    list.Add(page);
                }
                else
                {
                    var idx = list.FindIndex(x => x.Id == page.Id);
                    if (idx >= 0) list[idx] = page;
                    else list.Add(page);
                }
                WriteList(PathOf(companyCode, "pages.json"), list);
                return page;
            }
        }

        public static List<StoreGalleryItem> GetGallery(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadList<StoreGalleryItem>(PathOf(companyCode, "gallery.json")).OrderBy(g => g.SortOrder).ToList();
        }

        public static void SaveGallery(string companyCode, List<StoreGalleryItem> list)
        {
            lock (LockFor(companyCode))
                WriteList(PathOf(companyCode, "gallery.json"), list);
        }

        public static List<StoreCustomer> GetCustomers(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadList<StoreCustomer>(PathOf(companyCode, "customers.json"));
        }

        public static StoreCustomer GetCustomerByMobile(string companyCode, string mobile)
        {
            return GetCustomers(companyCode).FirstOrDefault(c =>
                string.Equals(c.Mobile, mobile, StringComparison.OrdinalIgnoreCase));
        }

        public static StoreCustomer UpsertCustomer(string companyCode, StoreCustomer customer)
        {
            lock (LockFor(companyCode))
            {
                var list = ReadList<StoreCustomer>(PathOf(companyCode, "customers.json"));
                if (customer.Id <= 0)
                {
                    customer.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                    customer.CreatedAt = DateTime.Now;
                    list.Add(customer);
                }
                else
                {
                    var idx = list.FindIndex(x => x.Id == customer.Id);
                    if (idx >= 0) list[idx] = customer;
                    else list.Add(customer);
                }
                WriteList(PathOf(companyCode, "customers.json"), list);
                return customer;
            }
        }

        public static List<StoreOrder> GetOrders(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
                return ReadList<StoreOrder>(PathOf(companyCode, "orders.json")).OrderByDescending(o => o.CreatedAt).ToList();
        }

        public static StoreOrder GetOrder(string companyCode, int id)
        {
            return GetOrders(companyCode).FirstOrDefault(o => o.Id == id);
        }

        public static StoreOrder AddOrder(string companyCode, StoreOrder order)
        {
            lock (LockFor(companyCode))
            {
                var list = ReadList<StoreOrder>(PathOf(companyCode, "orders.json"));
                order.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                order.OrderNumber = companyCode.ToUpperInvariant() + "-" + DateTime.Now.ToString("yyyyMMdd") + "-" + order.Id.ToString("D4");
                order.CreatedAt = DateTime.Now;
                list.Add(order);
                WriteList(PathOf(companyCode, "orders.json"), list);
                return order;
            }
        }

        public static bool UpdateOrder(string companyCode, StoreOrder order)
        {
            lock (LockFor(companyCode))
            {
                var list = ReadList<StoreOrder>(PathOf(companyCode, "orders.json"));
                var idx = list.FindIndex(x => x.Id == order.Id);
                if (idx < 0) return false;
                list[idx] = order;
                WriteList(PathOf(companyCode, "orders.json"), list);
                return true;
            }
        }

        public static bool DeletePage(string companyCode, int id)
        {
            lock (LockFor(companyCode))
            {
                var list = ReadList<StorePage>(PathOf(companyCode, "pages.json"));
                var removed = list.RemoveAll(x => x.Id == id) > 0;
                if (removed) WriteList(PathOf(companyCode, "pages.json"), list);
                return removed;
            }
        }

        public static List<StoreProductReview> GetReviews(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            lock (LockFor(companyCode))
            {
                var path = PathOf(companyCode, "reviews.json");
                if (!File.Exists(path))
                    WriteList(path, new List<StoreProductReview>());
                return ReadList<StoreProductReview>(path).OrderByDescending(r => r.CreatedAt).ToList();
            }
        }

        public static List<StoreProductReview> GetProductReviews(string companyCode, int productId, bool approvedOnly = true)
        {
            var q = GetReviews(companyCode).Where(r => r.ProductId == productId);
            if (approvedOnly) q = q.Where(r => r.IsApproved);
            return q.ToList();
        }

        public static StoreProductReview AddReview(string companyCode, StoreProductReview review)
        {
            lock (LockFor(companyCode))
            {
                var path = PathOf(companyCode, "reviews.json");
                var list = ReadList<StoreProductReview>(path);
                review.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                review.CreatedAt = DateTime.Now;
                if (review.Rating < 1) review.Rating = 1;
                if (review.Rating > 5) review.Rating = 5;
                list.Add(review);
                WriteList(path, list);
                return review;
            }
        }

        public static bool UpdateReview(string companyCode, StoreProductReview review)
        {
            lock (LockFor(companyCode))
            {
                var path = PathOf(companyCode, "reviews.json");
                var list = ReadList<StoreProductReview>(path);
                var idx = list.FindIndex(x => x.Id == review.Id);
                if (idx < 0) return false;
                list[idx] = review;
                WriteList(path, list);
                return true;
            }
        }

        public static bool DeleteReview(string companyCode, int id)
        {
            lock (LockFor(companyCode))
            {
                var path = PathOf(companyCode, "reviews.json");
                var list = ReadList<StoreProductReview>(path);
                var removed = list.RemoveAll(x => x.Id == id) > 0;
                if (removed) WriteList(path, list);
                return removed;
            }
        }

        public static double GetAverageRating(string companyCode, int productId)
        {
            var reviews = GetProductReviews(companyCode, productId);
            if (!reviews.Any()) return 0;
            return reviews.Average(r => r.Rating);
        }

        public static List<StoreAttributeDef> GetAttributeDefsForCategory(string companyCode, int categoryId)
        {
            return GetAttributeDefs(companyCode)
                .Where(a => a.CategoryIds == null || a.CategoryIds.Count == 0 || a.CategoryIds.Contains(categoryId))
                .ToList();
        }

        public static string ShopPath(string companyCode, string action = "Index", object id = null)
        {
            var path = "/Shop/" + companyCode + "/" + action;
            if (id != null) path += "/" + id;
            return path;
        }

        private static void EnsureSeed()
        {
            if (File.Exists(RegistryFile)) return;

            var tenants = new List<TenantInfo>
            {
                new TenantInfo { CompanyCode = "FASHION", Name = "بوتیک مد نیکو", CustomDomain = "", IsActive = true },
                new TenantInfo { CompanyCode = "GROCERY", Name = "سوپرمارکت آنلاین نیکو", CustomDomain = "", IsActive = true },
                new TenantInfo { CompanyCode = "DIGITAL", Name = "فروشگاه دیجیتال نیکو", CustomDomain = "", IsActive = true }
            };
            WriteList(RegistryFile, tenants);
            foreach (var t in tenants)
                EnsureTenantSeed(t.CompanyCode);
        }

        public static void EnsureTenantSeed(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return;
            var settingsPath = PathOf(companyCode, "settings.json");
            if (File.Exists(settingsPath)) return;

            lock (LockFor(companyCode))
            {
                if (File.Exists(settingsPath)) return;
                SeedTenantData(companyCode.ToUpperInvariant());
            }
        }

        private static void SeedTenantData(string code, string storeType = null, Action<StoreSettings> customize = null)
        {
            StoreSettings settings;
            List<StoreCategory> cats;
            List<StoreAttributeDef> attrs;
            List<StoreProduct> products;
            List<StoreMenuItem> menus;
            List<StorePage> pages;
            List<StoreGalleryItem> gallery;

            var type = storeType ?? (code == "FASHION" ? "fashion" : code == "GROCERY" ? "grocery" : "digital");

            if (type == "general")
            {
                settings = BaseSettings(code, "فروشگاه من", "فروشگاه آنلاین", "#222222", "#e5e5e5",
                    "به فروشگاه ما خوش آمدید", "محصولات منتخب با قیمت مناسب و ارسال سریع", "minimal");
                cats = new List<StoreCategory>
                {
                    new StoreCategory { Id = 1, Title = "محصولات", Slug = "products", SortOrder = 1 },
                    new StoreCategory { Id = 2, Title = "پیشنهاد ویژه", Slug = "special", SortOrder = 2 }
                };
                attrs = new List<StoreAttributeDef>
                {
                    new StoreAttributeDef { Id = 1, Name = "رنگ", InputType = "text", IsFilterable = true, SortOrder = 1 },
                    new StoreAttributeDef { Id = 2, Name = "برند", InputType = "text", IsFilterable = true, SortOrder = 2 }
                };
                products = GeneralProducts();
            }
            else if (type == "fashion")
            {
                settings = BaseSettings(code, "Boutique Mode", "فروشگاه آنلاین پوشاک", "#46d2ad", "#002c3e",
                    "پوشاک تازه برای فصل جدید", "کالکشن منتخب زنانه و مردانه با ارسال سریع", "fashion");
                settings.HeroImageUrl = "/Assets/img/portfolio/portfolio-1.jpg";
                cats = new List<StoreCategory>
                {
                    new StoreCategory { Id = 1, Title = "زنانه",Slug = "women", SortOrder = 1 },
                    new StoreCategory { Id = 2, ParentId = 1, Title = "مانتو", Slug = "manteau", SortOrder = 1 },
                    new StoreCategory { Id = 3, ParentId = 1, Title = "کفش", Slug = "shoes-w", SortOrder = 2 },
                    new StoreCategory { Id = 4, Title = "مردانه", Slug = "men", SortOrder = 2 },
                    new StoreCategory { Id = 5, ParentId = 4, Title = "پیراهن", Slug = "shirts", SortOrder = 1 }
                };
                attrs = new List<StoreAttributeDef>
                {
                    new StoreAttributeDef { Id = 1, Name = "سایز", InputType = "select", Options = "S,M,L,XL", IsFilterable = true, SortOrder = 1, CategoryIds = new List<int> { 1, 2, 3, 4, 5 } },
                    new StoreAttributeDef { Id = 2, Name = "رنگ", InputType = "text", IsFilterable = true, SortOrder = 2, CategoryIds = new List<int> { 1, 2, 3, 4, 5 } },
                    new StoreAttributeDef { Id = 3, Name = "جنس", InputType = "text", SortOrder = 3, CategoryIds = new List<int> { 2, 5 } }
                };
                products = FashionProducts();
            }
            else if (type == "grocery")
            {
                settings = BaseSettings(code, "نیکو مارکت", "فروشگاه ارگانیک تازه", "#6aa84f", "#e8f5d8",
                    "مواد تازه، زندگی سالم‌تر", "محصولات ارگانیک با تحویل سریع تا درب منزل", "organic");
                cats = new List<StoreCategory>
                {
                    new StoreCategory { Id = 1, Title = "میوه و سبزی",Slug = "produce", SortOrder = 1, Description = "تازه و فصلی" },
                    new StoreCategory { Id = 2, Title = "لبنیات", Slug = "dairy", SortOrder = 2, Description = "شیر و ماست" },
                    new StoreCategory { Id = 3, Title = "نان و غلات",Slug = "bakery", SortOrder = 3, Description = "تازه پخته" },
                    new StoreCategory { Id = 4, Title = "نوشیدنی",Slug = "drinks", SortOrder = 4, Description = "طبیعی و سالم" },
                    new StoreCategory { Id = 5, Title = "خشکبار",Slug = "nuts", SortOrder = 5, Description = "مغز و حبوبات" }
                };
                attrs = new List<StoreAttributeDef>
                {
                    new StoreAttributeDef { Id = 1, Name = "وزن", InputType = "text", SortOrder = 1, CategoryIds = new List<int> { 1, 2, 3 } },
                    new StoreAttributeDef { Id = 2, Name = "برند", InputType = "text", IsFilterable = true, SortOrder = 2, CategoryIds = new List<int> { 1, 2 } }
                };
                products = GroceryProducts();
            }
            else
            {
                settings = BaseSettings(code, "نیکو دیجیتال", "فروشگاه تکنولوژی و گجت", "#4F46E5", "#EEF2FF",
                    "گجت‌های هوشمند، تجربه بهتر", "هدفون، گجت، نرم‌افزار و لوازم دیجیتال با ارسال سریع", "marketplace");
                cats = new List<StoreCategory>
                {
                    new StoreCategory { Id = 1, Title = "صوتی و هدفون", Slug = "audio", SortOrder = 1, Description = "اسپیکر و هدفون" },
                    new StoreCategory { Id = 2, Title = "پوشیدنی", Slug = "wearables", SortOrder = 2, Description = "ساعت هوشمند" },
                    new StoreCategory { Id = 3, Title = "موبایل و لوازم",Slug = "mobile", SortOrder = 3, Description = "جانبی موبایل" },
                    new StoreCategory { Id = 4, Title = "دوره و نرم‌افزار",Slug = "software", SortOrder = 4, Description = "دانلودی" },
                    new StoreCategory { Id = 5, Title = "دوربین و تصویر",Slug = "camera", SortOrder = 5, Description = "عکاسی" }
                };
                attrs = new List<StoreAttributeDef>
                {
                    new StoreAttributeDef { Id = 1, Name = "نوع تحویل", InputType = "select", Options = "فیزیکی,دانلود,لایسنس", IsFilterable = true, SortOrder = 1, CategoryIds = new List<int> { 1, 2, 3, 4, 5 } },
                    new StoreAttributeDef { Id = 2, Name = "سطح", InputType = "select", Options = "مقدماتی,متوسط,حرفه‌ای", SortOrder = 2, CategoryIds = new List<int> { 4 } }
                };
                products = DigitalProducts();
            }

            customize?.Invoke(settings);

            menus = new List<StoreMenuItem>
            {
                new StoreMenuItem { Id = 1, Title = "خانه", Url = "/Shop/" + code, SortOrder = 1, Location = "main" },
                new StoreMenuItem { Id = 2, Title = "محصولات", Url = "/Shop/" + code + "/Catalog", SortOrder = 2, Location = "main" },
                new StoreMenuItem { Id = 3, Title = "گالری", Url = "/Shop/" + code + "/Gallery", SortOrder = 3, Location = "main" },
                new StoreMenuItem { Id = 4, Title = "درباره ما", Url = "/Shop/" + code + "/Page/about", SortOrder = 4, Location = "main", ParentId = null },
                new StoreMenuItem { Id = 5, ParentId = 4, Title = "تماس با ما", Url = "/Shop/" + code + "/Page/contact", SortOrder = 1, Location = "main" },
                new StoreMenuItem { Id = 6, Title = "شرایط فروش", Url = "/Shop/" + code + "/Page/terms", SortOrder = 1, Location = "footer" }
            };

            pages = new List<StorePage>
            {
                new StorePage { Id = 1, Title = "درباره ما", Slug = "about", IsPublished = true, BodyHtml = "<p>" + settings.About + "</p><p>این فروشگاه روی پلتفرم نیکو سیستم آفرین اجرا می‌شود و قابل سفارشی‌سازی کامل است.</p>" },
                new StorePage { Id = 2, Title = "تماس با ما", Slug = "contact", IsPublished = true, BodyHtml = ContactPageHtml(settings) },
                new StorePage { Id = 3, Title = "شرایط فروش",Slug = "terms", IsPublished = true, BodyHtml = "<p>پس از ثبت سفارش و پرداخت موفق، پیگیری از پنل کاربری امکان‌پذیر است.</p>" }
            };

            gallery = new List<StoreGalleryItem>
            {
                new StoreGalleryItem { Id = 1, Title = "نمای فروشگاه", Album = "فضا", ThumbUrl = "/Assets/img/portfolio/portfolio-1.jpg", LargeUrl = "/Assets/img/portfolio/portfolio-1.jpg", SortOrder = 1 },
                new StoreGalleryItem { Id = 2, Title = "محصولات منتخب", Album = "محصولات", ThumbUrl = "/Assets/img/portfolio/portfolio-2.jpg", LargeUrl = "/Assets/img/portfolio/portfolio-2.jpg", SortOrder = 2 },
                new StoreGalleryItem { Id = 3, Title = "تیم ما", Album = "فضا", ThumbUrl = "/Assets/img/portfolio/portfolio-3.jpg", LargeUrl = "/Assets/img/portfolio/portfolio-3.jpg", SortOrder = 3 }
            };

            WriteObject(PathOf(code, "settings.json"), settings);
            WriteList(PathOf(code, "categories.json"), cats);
            WriteList(PathOf(code, "attributes.json"), attrs);
            WriteList(PathOf(code, "products.json"), products);
            WriteList(PathOf(code, "menus.json"), menus);
            WriteList(PathOf(code, "pages.json"), pages);
            WriteList(PathOf(code, "gallery.json"), gallery);
            WriteList(PathOf(code, "customers.json"), new List<StoreCustomer>());
            WriteList(PathOf(code, "orders.json"), new List<StoreOrder>());
            WriteList(PathOf(code, "reviews.json"), new List<StoreProductReview>());
        }

        private static StoreSettings BaseSettings(string code, string name, string tagline, string primary, string secondary, string heroTitle, string heroSub, string layoutStyle = "organic")
        {
            return new StoreSettings
            {
                CompanyCode = code,
                StoreName = name,
                Tagline = tagline,
                About = name + " یک فروشگاه آنلاین واقعی روی پلتفرم نیکو سیستم آفرین است. مالک فروشگاه می‌تواند محصولات، منوها، صفحات و درگاه پرداخت را مدیریت کند.",
                Phone = "021-77000000",
                Email = "info@" + code.ToLowerInvariant() + ".nikoooo.ir",
                Address = "تهران، نارمک",
                LogoUrl = "/Assets/img/logo.png",
                PrimaryColor = primary,
                SecondaryColor = secondary,
                HeroTitle = heroTitle,
                HeroSubtitle = heroSub,
                HeroImageUrl = "/Assets/img/hero-img.png",
                FooterText = "© " + name + " — قدرت‌گرفته از نیکو سیستم آفرین",
                LayoutStyle = layoutStyle,
                AdminUsername = "admin",
                AdminPasswordHash = PasswordHelper.Hash("Admin@123"),
                ZarinPalMerchantId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
                ZarinPalSandbox = true,
                EnablePayment = true
            };
        }

        private static string ContactPageHtml(StoreSettings s)
        {
            var html = "";
            if (!string.IsNullOrWhiteSpace(s.Phone)) html += "<p>تلفن: " + HttpUtility.HtmlEncode(s.Phone) + "</p>";
            if (!string.IsNullOrWhiteSpace(s.Email)) html += "<p>ایمیل: " + HttpUtility.HtmlEncode(s.Email) + "</p>";
            if (!string.IsNullOrWhiteSpace(s.Address)) html += "<p>" + HttpUtility.HtmlEncode(s.Address) + "</p>";
            return html == "" ? "<p>اطلاعات تماس به‌زودی تکمیل می‌شود.</p>" : html;
        }

        private static List<StoreProduct> GeneralProducts()
        {
            return new List<StoreProduct>
            {
                P(1, 1, "محصول نمونه اول", "sample-1", "این یک محصول نمونه است؛ آن را ویرایش یا حذف کنید", 250000, true, "/Assets/img/portfolio/portfolio-4.jpg",
                    A(1, "رنگ", "مشکی")),
                P(2, 1, "محصول نمونه دوم", "sample-2", "این یک محصول نمونه است؛ آن را ویرایش یا حذف کنید", 480000, true, "/Assets/img/portfolio/portfolio-5.jpg",
                    A(1, "رنگ", "سفید")),
                P(3, 2, "محصول نمونه پیشنهادی", "sample-3", "این یک محصول نمونه است؛ آن را ویرایش یا حذف کنید", 150000, false, "/Assets/img/portfolio/portfolio-6.jpg")
            };
        }

        private static List<StoreProduct> FashionProducts()
        {
            return new List<StoreProduct>
            {
                P(1, 2, "مانتو کتان بهاره", "manteau-spring", "مانتو سبک مناسب فصل بهار", 890000, true, "/Assets/img/portfolio/portfolio-1.jpg",
                    A(1, "سایز", "M"), A(2, "رنگ", "کرم"), A(3, "جنس", "کتان")),
                P(2, 3, "کفش اسپرت زنانه", "shoes-sport", "کفش راحت برای استفاده روزانه", 1250000, true, "/Assets/img/portfolio/portfolio-2.jpg",
                    A(1, "سایز", "38"), A(2, "رنگ", "سفید")),
                P(3, 5, "پیراهن مردانه رسمی", "shirt-formal", "پیراهن اداری با دوخت تمیز", 780000, false, "/Assets/img/portfolio/portfolio-3.jpg",
                    A(1, "سایز", "L"), A(2, "رنگ", "آبی"))
            };
        }

        private static List<StoreProduct> GroceryProducts()
        {
            return new List<StoreProduct>
            {
                P(1, 1, "سبد میوه فصل", "seasonal-fruit", "ترکیب میوه‌های تازه فصل", 185000, true, "/Assets/img/portfolio/portfolio-2.jpg",
                    A(1, "وزن", "۲ کیلو")),
                P(2, 2, "شیر کم‌چرب ۱ لیتری", "milk-1l", "شیر تازه پاستوریزه", 45000, true, "/Assets/img/portfolio/portfolio-4.jpg",
                    A(1, "وزن", "۱ لیتر"), A(2, "برند", "نیکو")),
                P(3, 3, "نان سنگک تازه", "sangak", "پخت روزانه", 28000, true, "/Assets/img/portfolio/portfolio-3.jpg",
                    A(1, "وزن", "۵۰۰ گرم")),
                P(4, 4, "آبمیوه طبیعی پرتقال", "orange-juice", "بدون شکر افزوده", 78000, true, "/Assets/img/portfolio/portfolio-5.jpg",
                    A(1, "وزن", "۱ لیتر"), A(2, "برند", "طبیعی")),
                P(5, 5, "مغز بادام درختی", "almond", "بسته ۵۰۰ گرمی", 320000, false, "/Assets/img/portfolio/portfolio-6.jpg",
                    A(1, "وزن", "۵۰۰ گرم")),
                P(6, 1, "گوجه فرنگی ارگانیک", "tomato-org", "محصول گلخانه‌ای تازه", 62000, true, "/Assets/img/portfolio/portfolio-7.jpg",
                    A(1, "وزن", "۱ کیلو"))
            };
        }

        private static List<StoreProduct> DigitalProducts()
        {
            return new List<StoreProduct>
            {
                P(1, 1, "هدفون بی‌سیم Pro", "wireless-headphones", "صدای فضایی و نویزکنسلینگ", 2890000, true, "/Assets/img/portfolio/portfolio-1.jpg",
                    A(1, "نوع تحویل", "فیزیکی"), A(2, "سطح", "حرفه‌ای")),
                P(2, 2, "ساعت هوشمند Pulse", "smartwatch-pulse", "مانیتورینگ سلامت ۲۴ ساعته", 4590000, true, "/Assets/img/portfolio/portfolio-2.jpg",
                    A(1, "نوع تحویل", "فیزیکی")),
                P(3, 3, "قاب مگنتیک آیفون", "magsafe-case", "محافظ سبک با شارژ مگسیف", 690000, true, "/Assets/img/portfolio/portfolio-3.jpg",
                    A(1, "نوع تحویل", "فیزیکی")),
                P(4, 4, "دوره طراحی UI با Figma", "figma-course", "آموزش پروژه محور رابط کاربری", 490000, true, "/Assets/img/portfolio/portfolio-4.jpg",
                    A(1, "نوع تحویل", "دانلود"), A(2, "سطح", "مقدماتی")),
                P(5, 5, "گیمبال موبایل Mini", "gimbal-mini", "ثبات تصویر حرفه‌ای", 2190000, false, "/Assets/img/portfolio/portfolio-5.jpg",
                    A(1, "نوع تحویل", "فیزیکی")),
                P(6, 4, "لایسنس نرم‌افزار حسابداری", "acc-license", "لایسنس یک‌ساله", 890000, true, "/Assets/img/portfolio/portfolio-6.jpg",
                    A(1, "نوع تحویل", "لایسنس"))
            };
        }

        private static StoreProduct P(int id, int catId, string title, string slug, string desc, long price, bool featured, string img, params StoreProductAttribute[] attrs)
        {
            return new StoreProduct
            {
                Id = id,
                CategoryId = catId,
                Title = title,
                Slug = slug,
                ShortDescription = desc,
                Description = desc + " — قابل ویرایش از پنل مدیریت فروشگاه.",
                Price = price,
                CompareAtPrice = (long)(price * 1.35),
                Stock = 50,
                Sku = "SKU-" + id,
                IsActive = true,
                IsFeatured = featured,
                IsSample = true,
                SoldCount = id * 7,
                ViewCount = id * 40,
                Images = new List<StoreProductImage>
                {
                    new StoreProductImage { Id = Guid.NewGuid().ToString("N"), ThumbUrl = img, LargeUrl = img, Alt = title, SortOrder = 1 },
                    new StoreProductImage { Id = Guid.NewGuid().ToString("N"), ThumbUrl = img, LargeUrl = img, Alt = title + " ۲", SortOrder = 2 }
                },
                Attributes = attrs?.ToList() ?? new List<StoreProductAttribute>()
            };
        }

        private static StoreProductAttribute A(int id, string name, string value)
        {
            return new StoreProductAttribute { AttributeId = id, Name = name, Value = value };
        }

        private static List<T> ReadList<T>(string path)
        {
            if (!File.Exists(path)) return new List<T>();
            return JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
        }

        private static T ReadObject<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }

        private static void WriteList<T>(string path, List<T> data)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private static void WriteObject<T>(string path, T data)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
        }
    }
}
