using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>محصولات و سفارش‌های سایت اصلی و پرداخت‌های هاب؛ در دیتابیس nikoo</summary>
    public static class SiteStore
    {
        private static readonly object ProductLock = new object();
        private static readonly object OrderLock = new object();
        private static readonly object PaymentLock = new object();

        public static List<Product> GetProducts()
        {
            return NikooDb.List(NikooTables.SiteProducts);
        }

        private static readonly Dictionary<string, string> ProductSorts = new Dictionary<string, string>
        {
            { "title", "t.[Title]" },
            { "code", "t.[Code]" },
            { "category", "t.[Category]" },
            { "price", "t.[Price]" },
            { "created", "t.[CreatedAt]" }
        };

        public static PagedList<Product> ProductGrid(GridQuery query)
        {
            var filter = new SqlFilter().Search(query.Q, "Title", "Code", "Category", "DemoSlug");
            return NikooDb.Page(NikooTables.SiteProducts, filter, query.OrderBy(ProductSorts, "created", true), query.Page, query.PageSize);
        }

        private static readonly Dictionary<string, string> OrderSorts = new Dictionary<string, string>
        {
            { "number", "t.[OrderNumber]" },
            { "customer", "t.[CustomerName]" },
            { "amount", "t.[Amount]" },
            { "created", "t.[CreatedAt]" }
        };

        public static PagedList<Order> OrderGrid(GridQuery query, OrderStatus? status)
        {
            var filter = new SqlFilter().Search(query.Q, "OrderNumber", "CustomerName", "CustomerMobile", "ProductTitle");
            if (status.HasValue) filter.Eq("Status", status.Value.ToString());
            return NikooDb.Page(NikooTables.SiteOrders, filter, query.OrderBy(OrderSorts, "created", true), query.Page, query.PageSize);
        }

        public static Product GetProduct(int id)
        {
            return NikooDb.Get(NikooTables.SiteProducts, NikooDb.Key(id));
        }

        public static Product AddProduct(Product product)
        {
            lock (ProductLock)
            using (var tx = NikooDb.Begin())
            {
                product.Id = NikooDb.NextId(NikooTables.SiteProducts, null, tx);
                product.CreatedAt = DateTime.Now;
                NikooDb.Insert(NikooTables.SiteProducts, product, tx: tx);
                tx.Commit();
                return product;
            }
        }

        public static bool UpdateProduct(Product product)
        {
            lock (ProductLock)
            using (var tx = NikooDb.Begin())
            {
                var existing = NikooDb.Get(NikooTables.SiteProducts, NikooDb.Key(product.Id), tx);
                if (existing == null) return false;
                product.CreatedAt = existing.CreatedAt;
                NikooDb.Update(NikooTables.SiteProducts, product, tx: tx);
                tx.Commit();
                return true;
            }
        }

        public static bool DeleteProduct(int id)
        {
            lock (ProductLock)
                return NikooDb.Delete(NikooTables.SiteProducts, NikooDb.Key(id));
        }

        public static List<Order> GetOrders()
        {
            return NikooDb.List(NikooTables.SiteOrders).OrderByDescending(o => o.CreatedAt).ToList();
        }

        public static Order GetOrder(int id)
        {
            return NikooDb.Get(NikooTables.SiteOrders, NikooDb.Key(id));
        }

        public static Order AddOrder(Order order)
        {
            lock (OrderLock)
            using (var tx = NikooDb.Begin())
            {
                order.Id = NikooDb.NextId(NikooTables.SiteOrders, null, tx);
                order.OrderNumber = "LF-" + DateTime.Now.ToShamsiCompact() + "-" + order.Id.ToString("D4");
                order.CreatedAt = DateTime.Now;
                NikooDb.Insert(NikooTables.SiteOrders, order, tx: tx);
                tx.Commit();
                return order;
            }
        }

        public static bool UpdateOrder(Order order)
        {
            lock (OrderLock)
                return NikooDb.Update(NikooTables.SiteOrders, order);
        }

        public static PaymentClient GetPaymentClient(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return null;
            return NikooDb.Get(NikooTables.PaymentClients, new Row { ["AppId"] = appId });
        }

        public static HubPayment GetHubPaymentByToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            return NikooDb.Get(NikooTables.HubPayments, new Row { ["Token"] = token });
        }

        public static HubPayment AddHubPayment(HubPayment payment)
        {
            lock (PaymentLock)
            using (var tx = NikooDb.Begin())
            {
                payment.Id = NikooDb.NextId(NikooTables.HubPayments, null, tx);
                if (string.IsNullOrWhiteSpace(payment.Token))
                    payment.Token = Guid.NewGuid().ToString("N");
                payment.CreatedAt = DateTime.Now;
                NikooDb.Insert(NikooTables.HubPayments, payment, tx: tx);
                tx.Commit();
                return payment;
            }
        }

        public static bool UpdateHubPayment(HubPayment payment)
        {
            lock (PaymentLock)
                return NikooDb.Update(NikooTables.HubPayments, payment);
        }

        public static HubPayment GetHubPayment(int id) => NikooDb.Get(NikooTables.HubPayments, NikooDb.Key(id));

        private static readonly Dictionary<string, string> HubPaymentSorts = new Dictionary<string, string>
        {
            { "created", "t.[CreatedAt]" },
            { "amount", "t.[Amount]" },
            { "app", "t.[AppId]" }
        };

        public static PagedList<HubPayment> HubPaymentGrid(GridQuery query, string appId, HubPaymentStatus? status)
        {
            var filter = new SqlFilter().Search(query.Q, "Token", "ExternalRef", "Description", "CustomerName", "CustomerMobile", "CustomerEmail");
            if (!string.IsNullOrEmpty(appId)) filter.Eq("AppId", appId);
            if (status.HasValue) filter.Eq("Status", status.Value.ToString());
            return NikooDb.Page(NikooTables.HubPayments, filter, query.OrderBy(HubPaymentSorts, "created", true), query.Page, query.PageSize);
        }

        /// <summary>Per app: number of payments, number paid and the paid total.</summary>
        public static Dictionary<string, HubAppStats> HubStatsByApp()
        {
            return NikooDb.Select(
                "SELECT [AppId], COUNT(*), SUM(CASE WHEN [Status] = @Paid THEN 1 ELSE 0 END), SUM(CASE WHEN [Status] = @Paid THEN [Amount] ELSE 0 END)"
                + " FROM " + NikooDb.Table(NikooTables.HubPayments.Name) + " GROUP BY [AppId]",
                new Row { ["Paid"] = HubPaymentStatus.Paid.ToString() },
                r => new HubAppStats
                {
                    AppId = r.IsDBNull(0) ? "" : r.GetString(0),
                    Count = r.GetInt32(1),
                    PaidCount = r.GetInt32(2),
                    PaidAmount = r.IsDBNull(3) ? 0 : Convert.ToInt64(r.GetValue(3))
                })
                .GroupBy(s => s.AppId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        public static List<PaymentClient> GetPaymentClients()
        {
            return NikooDb.List(NikooTables.PaymentClients, orderBy: "[Name], [AppId]");
        }

        public static void AddPaymentClient(PaymentClient client)
        {
            if (GetPaymentClient(client.AppId) != null)
                throw new InvalidOperationException("اپلیکیشنی با این شناسه وجود دارد.");
            NikooDb.Insert(NikooTables.PaymentClients, client);
        }

        public static bool UpdatePaymentClient(PaymentClient client) => NikooDb.Update(NikooTables.PaymentClients, client);

        public static bool DeletePaymentClient(string appId) => NikooDb.Delete(NikooTables.PaymentClients, new Row { ["AppId"] = appId });

        /// <summary>A random secret for signing the payment hub's requests and callbacks.</summary>
        public static string NewApiKey()
        {
            var bytes = new byte[24];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>Subscription orders paid (or started) for one store.</summary>
        public static List<Order> OrdersForStore(string companyCode)
        {
            return NikooDb.Query(NikooTables.SiteOrders, new Row { ["CompanyCode"] = companyCode }, "[CreatedAt] DESC");
        }

        /// <summary>محصولات و کلاینت‌های پرداخت نمونه، فقط وقتی جدولشان خالی است</summary>
        internal static void SeedDefaults()
        {
            if (!NikooDb.Exists(NikooTables.SiteProducts))
                NikooDb.SyncList(NikooTables.SiteProducts, null, DefaultProducts());

            if (!NikooDb.Exists(NikooTables.PaymentClients))
                foreach (var client in DefaultPaymentClients())
                    NikooDb.Insert(NikooTables.PaymentClients, client);
        }

        private static readonly Dictionary<string, string> StoreDemoImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "fashion", "/Assets/img/products/store-fashion.jpg" },
            { "grocery", "/Assets/img/products/store-grocery.jpg" },
            { "digital", "/Assets/img/products/store-digital.jpg" },
            { "clinic", "/Assets/img/products/store-booking.jpg" },
            { "club", "/Assets/img/products/store-booking.jpg" },
            { "corporate", "/Assets/img/products/store-cms.jpg" }
        };

        private const string BookingTitle = "پنل رزرو آنلاین نوبت";
        private const string ContentTitle = "سیستم مدیریت محتوا (CMS)";

        /// <summary>
        /// Turns the catalogue's booking product into a live product with the CLINIC demo, or adds it when the catalogue
        /// has no such product. Its price and any image the admin chose are kept.
        /// </summary>
        internal static void ApplyBookingProduct()
        {
            var product = GetProducts().FirstOrDefault(p => p.DemoSlug == "clinic")
                ?? GetProducts().FirstOrDefault(p => string.IsNullOrEmpty(p.DemoSlug) && p.Title != null && p.Title.Contains("رزرو"));
            var isNew = product == null;
            if (isNew) product = new Product { Price = 10000, Title = BookingTitle, IsActive = true };

            product.DemoSlug = "clinic";
            product.IsFeatured = true;
            product.ShortDescription = "سایت نوبت‌دهی آنلاین برای مطب، کلینیک، سالن زیبایی و مشاوره با پنل مدیریت کامل";
            product.Description = "مشتری خدمت، متخصص، روز و ساعت خالی را خودش انتخاب می‌کند و در کمتر از یک دقیقه نوبت می‌گیرد. "
                + "ساعت کاری هفتگی هر متخصص، تعطیلی‌ها و مرخصی‌ها، تقویم هفتگی نوبت‌ها، ثبت نوبت تلفنی، بیعانه آنلاین با زرین‌پال "
                + "و پیگیری و لغو نوبت با کد رهگیری؛ همه روی دامنه و کد شرکت اختصاصی خودتان.";
            if (string.IsNullOrEmpty(product.ImageUrl) || product.ImageUrl.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase))
                product.ImageUrl = StoreDemoImages["clinic"];
            if (isNew) AddProduct(product);
            else UpdateProduct(product);
        }

        private const string ClubTitle = "سایت آموزشگاه و باشگاه ورزشی";

        /// <summary>Adds the academy and sports-club product, or refreshes its text when the catalogue already has it.</summary>
        internal static void ApplyClubProduct()
        {
            var product = GetProducts().FirstOrDefault(p => p.DemoSlug == "club")
                ?? GetProducts().FirstOrDefault(p => string.IsNullOrEmpty(p.DemoSlug) && p.Title != null && p.Title.Contains("باشگاه"));
            var isNew = product == null;
            if (isNew) product = new Product { Price = 10000, Title = ClubTitle, IsActive = true };

            product.Title = ClubTitle;
            product.DemoSlug = "club";
            product.IsFeatured = true;
            product.ShortDescription = "سایت آموزشگاه و باشگاه با کلاس گروهی، مدرس خصوصی آنلاین یا حضوری و رزرو ساعتی سالن";
            product.Description = "کلاس و دوره را با ظرفیت، شهریه، سطح و گروه سنی تعریف کنید و ثبت‌نام آنلاین بگیرید. "
                + "جلسه با مدرس یا مربی خصوصی، حضوری یا آنلاین، روی ساعت کاری خودش رزرو می‌شود. "
                + "سالن فوتبال، میز پینگ‌پونگ و هر فضای دیگری را ساعتی اجاره دهید؛ فقط ساعت‌های خالی نشان داده می‌شود. "
                + "شهریه و اجاره با زرین‌پال به حساب شما می‌آید و هر ثبت‌نام کد پیگیری دارد؛ همه روی دامنه و کد شرکت اختصاصی خودتان.";
            if (string.IsNullOrEmpty(product.ImageUrl) || product.ImageUrl.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase))
                product.ImageUrl = StoreDemoImages["club"];
            if (isNew) AddProduct(product);
            else UpdateProduct(product);
        }

        private const string ContentTemplatesText = "شش قالب آماده: سایت شرکتی در سه سبک (رسمی، آژانس خلاق و صنعتی / پیمانکاری)";
        private const string NewsTemplatesText = "و خبرگزاری در سه سبک (مجله، روزنامه کلاسیک و خبر آنلاین پالس) با تیتر یک";

        /// <summary>Updates the templates in the CMS product's description, only where an earlier unedited wording is still there.</summary>
        internal static void ApplyContentTemplateCount()
        {
            var replacements = new[]
            {
                new[] { "دو قالب آماده: سایت شرکتی", ContentTemplatesText },
                new[] { "چهار قالب آماده: سایت شرکتی در سه سبک (رسمی، آژانس خلاق و صنعتی / پیمانکاری)", ContentTemplatesText },
                new[] { "و خبرگزاری و مجله با تیتر یک", NewsTemplatesText }
            };
            foreach (var product in GetProducts().Where(p => p.DemoSlug == "corporate" && p.Description != null))
            {
                var text = replacements.Aggregate(product.Description, (current, r) => current.Replace(r[0], r[1]));
                if (text == product.Description) continue;
                product.Description = text;
                UpdateProduct(product);
            }
        }

        /// <summary>
        /// Turns the catalogue's CMS product into the corporate/news site product with the COMPANY demo (or adds it), and
        /// drops the old accounting product, which has no real system behind it. Its price and any admin-chosen image are kept.
        /// </summary>
        internal static void ApplyContentProduct()
        {
            var products = GetProducts();
            var product = products.FirstOrDefault(p => p.DemoSlug == "corporate" || p.DemoSlug == "news")
                ?? products.FirstOrDefault(p => string.IsNullOrEmpty(p.DemoSlug) && p.Title != null
                    && (p.Title.Contains("مدیریت محتوا") || p.Title.Contains("CMS")));
            var isNew = product == null;
            if (isNew) product = new Product { Price = 20000, IsActive = true };

            product.Title = ContentTitle;
            product.DemoSlug = "corporate";
            product.IsFeatured = true;
            product.ShortDescription = "سایت شرکتی و خبرگزاری با مدیریت کامل محتوا: خبر و مقاله، خدمات، نمونه‌کار، صفحات و فرم تماس";
            product.Description = "یک سیستم مدیریت محتوا با " + ContentTemplatesText + " برای معرفی شرکت، خدمات، نمونه‌کارها و اخبار، "
                + NewsTemplatesText + "، خبر فوری، سرویس‌های خبری و دیدگاه کاربران. "
                + "خبر و مقاله، دسته‌بندی‌ها، صفحات، منوها، گالری، چیدمان صفحه اول و پیام‌های تماس را بدون برنامه‌نویسی از پنل مدیریت کنید؛ "
                + "همه روی دامنه و کد شرکت اختصاصی خودتان.";
            if (string.IsNullOrEmpty(product.ImageUrl) || product.ImageUrl.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase))
                product.ImageUrl = StoreDemoImages["corporate"];
            if (isNew) AddProduct(product);
            else UpdateProduct(product);

            var orders = GetOrders();
            foreach (var accounting in products.Where(p => p.Id != product.Id && string.IsNullOrEmpty(p.DemoSlug)
                && p.Title != null && p.Title.Contains("حسابداری")))
            {
                if (orders.Any(o => o.ProductId == accounting.Id))
                {
                    accounting.IsActive = false;
                    UpdateProduct(accounting);
                }
                else DeleteProduct(accounting.Id);
            }
        }

        /// <summary>تصویر اختصاصی فروشگاه‌های آماده؛ فقط جای تصاویر عمومی portfolio را می‌گیرد تا تصویر انتخابی ادمین حفظ شود</summary>
        internal static void ApplyStoreDemoImages()
        {
            foreach (var p in GetProducts())
            {
                string image;
                if (string.IsNullOrEmpty(p.DemoSlug) || !StoreDemoImages.TryGetValue(p.DemoSlug, out image)) continue;
                if (!string.IsNullOrEmpty(p.ImageUrl) && !p.ImageUrl.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase)) continue;
                p.ImageUrl = image;
                UpdateProduct(p);
            }
        }

        /// <summary>انتقال یک‌باره فایل‌های products / orders / payments / payment-clients از App_Data</summary>
        internal static void ImportLegacyJson(string appData)
        {
            var products = LegacyJsonImport.Read<List<Product>>(Path.Combine(appData, "products.json"));
            AssignDemoSlugs(products);

            using (var tx = NikooDb.Begin())
            {
                NikooDb.InsertMissing(NikooTables.SiteProducts, null, products, tx);
                NikooDb.InsertMissing(NikooTables.SiteOrders, null, LegacyJsonImport.Read<List<Order>>(Path.Combine(appData, "orders.json")), tx);
                NikooDb.InsertMissing(NikooTables.HubPayments, null, LegacyJsonImport.Read<List<HubPayment>>(Path.Combine(appData, "payments.json")), tx);
                NikooDb.InsertMissing(NikooTables.PaymentClients, null,
                    (LegacyJsonImport.Read<List<PaymentClient>>(Path.Combine(appData, "payment-clients.json")) ?? new List<PaymentClient>())
                        .Where(c => !string.IsNullOrWhiteSpace(c.AppId)),
                    tx);
                tx.Commit();
            }
        }

        /// <summary>فایل‌های قدیمی بدون DemoSlug را بر اساس عنوان به دموی فروشگاه مربوط وصل می‌کند</summary>
        private static void AssignDemoSlugs(List<Product> products)
        {
            if (products == null || products.Count == 0) return;
            if (products.Any(p => !string.IsNullOrEmpty(p.DemoSlug))) return;

            var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "پوشاک", "fashion" },
                { "مواد غذایی", "grocery" },
                { "دیجیتال", "digital" }
            };

            foreach (var p in products)
            {
                foreach (var kv in defaults)
                {
                    if (p.Title != null && p.Title.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        p.DemoSlug = kv.Value;
                        p.IsFeatured = true;
                        break;
                    }
                }
            }
        }

        private static List<PaymentClient> DefaultPaymentClients()
        {
            return new List<PaymentClient>
            {
                new PaymentClient
                {
                    AppId = "demo-app",
                    ApiKey = "demo-secret-change-me",
                    Name = "اپلیکیشن نمونه",
                    AllowedReturnHosts = new[] { "localhost", "127.0.0.1", "nikoooo.ir" }
                },
                new PaymentClient
                {
                    AppId = "goodloan",
                    ApiKey = "GoodLoan-Nikoo-Pay-Secret-Change-Me",
                    Name = "لبخند فردا / GoodLoan",
                    AllowedReturnHosts = new[] { "localhost", "127.0.0.1", "nikoooo.ir", "labkhandfarda.ir", "www.labkhandfarda.ir" }
                }
            };
        }

        private static List<Product> DefaultProducts()
        {
            return new List<Product>
            {
                new Product
                {
                    Id = 1,
                    Title = "فروشگاه آنلاین پوشاک (اختصاصی)",
                    ShortDescription = "فروشگاه واقعی چندمستأجری: سبد خرید، پنل ادمین، منوی درختی، گالری و درگاه پرداخت",
                    Description = "فروشگاه آنلاین واقعی پوشاک با پنل مدیریت کامل، سبد خرید، گروه‌بندی درختی، خصوصیات، صفحات، گالری و دامنه/کد شرکت اختصاصی.",
                    Price = 15000,
                    ImageUrl = StoreDemoImages["fashion"],
                    Category = "فروشگاه آنلاین",
                    DemoSlug = "fashion",
                    IsActive = true,
                    IsFeatured = true
                },
                new Product
                {
                    Id = 2,
                    Title = "فروشگاه آنلاین مواد غذایی (اختصاصی)",
                    ShortDescription = "سوپرمارکت آنلاین واقعی با پنل مدیریت، سفارش و پرداخت",
                    Description = "فروشگاه سوپرمارکت کامل: محصول، گروه، سبد خرید، پنل مشتری، منوی درختی، گالری و درگاه — هر مشتری با کد شرکت خودش.",
                    Price = 18000,
                    ImageUrl = StoreDemoImages["grocery"],
                    Category = "فروشگاه آنلاین",
                    DemoSlug = "grocery",
                    IsActive = true,
                    IsFeatured = true
                },
                new Product
                {
                    Id = 3,
                    Title = "فروشگاه محصولات دیجیتال (اختصاصی)",
                    ShortDescription = "فروشگاه واقعی دوره، لایسنس و فایل با پنل کامل",
                    Description = "فروشگاه دیجیتال واقعی با کاتالوگ داینامیک، سبد خرید، پنل ادمین/مشتری، صفحات و درگاه پرداخت.",
                    Price = 12000,
                    ImageUrl = StoreDemoImages["digital"],
                    Category = "فروشگاه آنلاین",
                    DemoSlug = "digital",
                    IsActive = true,
                    IsFeatured = true
                },
                new Product
                {
                    Id = 4,
                    Title = ContentTitle,
                    ShortDescription = "سایت شرکتی و خبرگزاری با مدیریت کامل محتوا: خبر و مقاله، خدمات، نمونه‌کار، صفحات و فرم تماس",
                    Description = "سیستم مدیریت محتوا با شش قالب آماده (سه سبک سایت شرکتی و سه سبک خبرگزاری)؛ خبر، صفحات، منوها و گالری را بدون برنامه‌نویسی مدیریت کنید.",
                    Price = 20000,
                    ImageUrl = StoreDemoImages["corporate"],
                    Category = "نرم‌افزار سازمانی",
                    DemoSlug = "corporate",
                    IsActive = true,
                    IsFeatured = true
                },
                new Product
                {
                    Id = 5,
                    Title = "پنل رزرو آنلاین نوبت",
                    ShortDescription = "رزرو نوبت کلینیک، سالن و خدمات",
                    Description = "سامانه رزرو آنلاین نوبت با تقویم، یادآوری پیامکی و مدیریت ظرفیت. مناسب کلینیک‌ها، سالن‌های زیبایی و مراکز خدماتی.",
                    Price = 10000,
                    ImageUrl = "/Assets/img/portfolio/portfolio-5.jpg",
                    Category = "نرم‌افزار خدماتی",
                    DemoSlug = null,
                    IsActive = true,
                    IsFeatured = false
                }
            };
        }
    }
}
