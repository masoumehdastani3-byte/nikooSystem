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

        public static List<Order> RecentOrders(int take) =>
            NikooDb.Page(NikooTables.SiteOrders, new SqlFilter(), "t.[CreatedAt] DESC", 1, take).ToList();

        public static bool ProductHasOrders(int productId) =>
            NikooDb.Count(NikooTables.SiteOrders, new SqlFilter().Eq("ProductId", productId)) > 0;

        /// <summary>Counts and revenue of the main site's orders, without loading the rows.</summary>
        internal static void ReadOrderStats(out int count, out int paid, out int pending, out long revenue, out long monthRevenue, DateTime monthAgo)
        {
            var row = NikooDb.Select(
                "SELECT COUNT(*), "
                + "ISNULL(SUM(CASE WHEN [Status] IN (@Paid, @Processing, @Completed) THEN 1 ELSE 0 END), 0), "
                + "ISNULL(SUM(CASE WHEN [Status] = @Pending THEN 1 ELSE 0 END), 0), "
                + "ISNULL(SUM(CASE WHEN [Status] IN (@Paid, @Processing, @Completed) THEN [Amount] ELSE 0 END), 0), "
                + "ISNULL(SUM(CASE WHEN [Status] IN (@Paid, @Processing, @Completed) AND COALESCE([PaidAt], [CreatedAt]) >= @Month THEN [Amount] ELSE 0 END), 0) "
                + "FROM " + NikooDb.Table(NikooTables.SiteOrders.Name),
                new Row
                {
                    ["Paid"] = OrderStatus.Paid.ToString(),
                    ["Processing"] = OrderStatus.Processing.ToString(),
                    ["Completed"] = OrderStatus.Completed.ToString(),
                    ["Pending"] = OrderStatus.PendingPayment.ToString(),
                    ["Month"] = monthAgo
                },
                r => new[]
                {
                    Convert.ToInt64(r.GetValue(0)),
                    Convert.ToInt64(r.GetValue(1)),
                    Convert.ToInt64(r.GetValue(2)),
                    Convert.ToInt64(r.GetValue(3)),
                    Convert.ToInt64(r.GetValue(4))
                }).First();
            count = (int)row[0];
            paid = (int)row[1];
            pending = (int)row[2];
            revenue = row[3];
            monthRevenue = row[4];
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
            { "digital", "/Assets/img/products/store-gadget.jpg" },
            { "clinic", "/Assets/img/products/store-clinic.jpg" },
            { "beauty", "/Assets/img/products/store-salon.jpg" },
            { "consulting", "/Assets/img/products/store-consulting.jpg" },
            { "school", "/Assets/img/products/store-school.jpg" },
            { "club", "/Assets/img/products/store-club.jpg" },
            { "corporate", "/Assets/img/products/store-cms.jpg" }
        };

        private const string BookingTitle = "سایت مطب و کلینیک";
        private const string BeautyTitle = "سایت آرایشگاه";
        private const string ConsultingTitle = "سایت مشاوره";
        private const string ContentTitle = "سیستم مدیریت محتوا (CMS)";

        /// <summary>Turns the shared booking catalogue row into the clinic product, or adds it.</summary>
        internal static void ApplyBookingProduct()
        {
            var product = GetProducts().FirstOrDefault(p => p.DemoSlug == "clinic")
                ?? GetProducts().FirstOrDefault(p => string.IsNullOrEmpty(p.DemoSlug) && p.Title != null && (p.Title.Contains("رزرو") || p.Title.Contains("نوبت")));
            var isNew = product == null;
            if (isNew) product = new Product { Price = 10000, Title = BookingTitle, IsActive = true };

            product.Title = BookingTitle;
            product.DemoSlug = "clinic";
            product.IsFeatured = true;
            if (string.IsNullOrWhiteSpace(product.Category)) product.Category = "نرم‌افزار خدماتی";
            product.ShortDescription = "سایت نوبت‌دهی مطب و کلینیک با تقویم پزشک، بیعانه و پیامک یادآوری";
            product.Description = "بیمار پزشک، خدمت، روز و ساعت خالی را خودش انتخاب می‌کند. "
                + "ساعت کاری، تعطیلی و مرخصی هر پزشک جداست و منشی نوبت تلفنی را در همان تقویم ثبت می‌کند. "
                + "بیعانه آنلاین، پیامک تأیید و یادآوری، و پیگیری با کد رهگیری روی دامنه خودتان است. "
                + "حتی اگر رزرو از سایت را باز نکنید، تقویم و نوبت تلفنی در پنل کار می‌کند.";
            if (string.IsNullOrEmpty(product.ImageUrl) || product.ImageUrl.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(product.ImageUrl, "/Assets/img/products/store-booking.jpg", StringComparison.OrdinalIgnoreCase))
                product.ImageUrl = StoreDemoImages["clinic"];
            if (isNew) AddProduct(product);
            else UpdateProduct(product);
        }

        /// <summary>Adds the beauty-salon product, separate from the clinic.</summary>
        internal static void ApplyBeautyProduct()
        {
            var product = GetProducts().FirstOrDefault(p => p.DemoSlug == "beauty");
            var isNew = product == null;
            if (isNew)
            {
                var clinic = GetProducts().FirstOrDefault(p => p.DemoSlug == "clinic");
                product = new Product { Price = clinic != null && clinic.Price > 0 ? clinic.Price : 10000, Title = BeautyTitle, IsActive = true, Category = "نرم‌افزار خدماتی" };
            }

            product.Title = BeautyTitle;
            product.DemoSlug = "beauty";
            product.IsFeatured = true;
            if (string.IsNullOrWhiteSpace(product.Category)) product.Category = "نرم‌افزار خدماتی";
            product.ShortDescription = "سایت نوبت‌دهی آرایشگاه و سالن زیبایی با انتخاب آرایشگر و ساعت خالی";
            product.Description = "مشتری آرایشگر، خدمت، روز و ساعت خالی را خودش انتخاب می‌کند. "
                + "کوتاهی، رنگ، ناخن و پوست هر کدام مدت و هزینه جدا دارند و ساعت کاری هر آرایشگر مستقل است. "
                + "نوبت تلفنی در همان تقویم ثبت می‌شود، بیعانه و پیامک یادآوری اختیاری است و هر نوبت کد پیگیری دارد.";
            if (string.IsNullOrEmpty(product.ImageUrl) || product.ImageUrl.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase))
                product.ImageUrl = StoreDemoImages["beauty"];
            if (isNew) AddProduct(product);
            else UpdateProduct(product);
        }

        /// <summary>Adds the consulting product, separate from clinic and salon.</summary>
        internal static void ApplyConsultingProduct()
        {
            var product = GetProducts().FirstOrDefault(p => p.DemoSlug == "consulting");
            var isNew = product == null;
            if (isNew)
            {
                var clinic = GetProducts().FirstOrDefault(p => p.DemoSlug == "clinic");
                product = new Product { Price = clinic != null && clinic.Price > 0 ? clinic.Price : 10000, Title = ConsultingTitle, IsActive = true, Category = "نرم‌افزار خدماتی" };
            }

            product.Title = ConsultingTitle;
            product.DemoSlug = "consulting";
            product.IsFeatured = true;
            if (string.IsNullOrWhiteSpace(product.Category)) product.Category = "نرم‌افزار خدماتی";
            product.ShortDescription = "سایت نوبت‌دهی مشاوره برای جلسه حضوری یا آنلاین";
            product.Description = "مراجع مشاور، موضوع جلسه و ساعت خالی را خودش انتخاب می‌کند. "
                + "جلسه می‌تواند حضوری یا آنلاین باشد و نوبت تلفنی در همان تقویم ثبت می‌شود. "
                + "بیعانه، کد پیگیری و خبر جلسه در بله از پنل تنظیم می‌شود.";
            if (string.IsNullOrEmpty(product.ImageUrl) || product.ImageUrl.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase))
                product.ImageUrl = StoreDemoImages["consulting"];
            if (isNew) AddProduct(product);
            else UpdateProduct(product);
        }

        /// <summary>Splits the shared booking catalogue row into clinic, salon and consulting products.</summary>
        internal static void ApplySplitBookingProducts()
        {
            ApplyBookingProduct();
            ApplyBeautyProduct();
            ApplyConsultingProduct();
        }

        private const string ClubTitle = "سایت باشگاه ورزشی";
        private const string SchoolTitle = "سایت آموزشگاه";

        /// <summary>Turns the combined catalogue row into the sports-club product, or adds that product.</summary>
        internal static void ApplyClubProduct()
        {
            var product = GetProducts().FirstOrDefault(p => p.DemoSlug == "club")
                ?? GetProducts().FirstOrDefault(p => string.IsNullOrEmpty(p.DemoSlug) && p.Title != null && p.Title.Contains("باشگاه"));
            var isNew = product == null;
            if (isNew) product = new Product { Price = 10000, Title = ClubTitle, IsActive = true };

            product.Title = ClubTitle;
            product.DemoSlug = "club";
            product.IsFeatured = true;
            product.ShortDescription = "سایت باشگاه ورزشی با کلاس، مربی خصوصی و رزرو ساعتی سالن و زمین";
            product.Description = "کلاس ورزشی را با ظرفیت، شهریه و گروه سنی تعریف کنید؛ ثبت‌نام آنلاین یا تلفنی از پنل. "
                + "جلسه با مربی خصوصی، حضوری یا آنلاین، روی ساعت کاری خودش رزرو می‌شود. "
                + "سالن فوتبال، میز پینگ‌پونگ و سالن بدنسازی را ساعتی اجاره دهید؛ فقط ساعت‌های خالی نشان داده می‌شود. "
                + "شهریه و اجاره آنلاین با درگاه بانک یا حضوری با دریافت نقدی و فاکتور در حسابداری ثبت می‌شود.";
            if (string.IsNullOrEmpty(product.ImageUrl) || product.ImageUrl.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase))
                product.ImageUrl = StoreDemoImages["club"];
            if (isNew) AddProduct(product);
            else UpdateProduct(product);
        }

        /// <summary>Adds the academy product, separate from the sports club.</summary>
        internal static void ApplySchoolProduct()
        {
            var product = GetProducts().FirstOrDefault(p => p.DemoSlug == "school");
            var isNew = product == null;
            if (isNew)
            {
                var club = GetProducts().FirstOrDefault(p => p.DemoSlug == "club");
                product = new Product { Price = club != null && club.Price > 0 ? club.Price : 10000, Title = SchoolTitle, IsActive = true };
            }

            product.Title = SchoolTitle;
            product.DemoSlug = "school";
            product.IsFeatured = true;
            if (string.IsNullOrWhiteSpace(product.Category)) product.Category = "نرم‌افزار خدماتی";
            product.ShortDescription = "سایت آموزشگاه با دوره عمومی و دوره خصوصی، حضوری یا آنلاین";
            product.Description = "دوره عمومی زبان، هنر، موسیقی یا کنکور را با ظرفیت، شهریه، سطح و گروه سنی تعریف کنید تا چند دانشجو در یک کلاس ثبت‌نام کنند. "
                + "دوره خصوصی را هم با مدرس، حضوری یا آنلاین، روی ساعت کاری همان مدرس بگذارید. "
                + "ثبت‌نام آنلاین یا تلفنی از پنل انجام می‌شود و هر ثبت‌نام کد پیگیری دارد. "
                + "شهریه هر دو نوع دوره، آنلاین با درگاه بانک یا حضوری با دریافت نقدی و فاکتور، در حسابداری ثبت می‌شود.";
            if (string.IsNullOrEmpty(product.ImageUrl) || product.ImageUrl.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase))
                product.ImageUrl = StoreDemoImages["school"];
            if (isNew) AddProduct(product);
            else UpdateProduct(product);
        }

        /// <summary>Splits the old combined catalogue row into the academy product and the sports-club product.</summary>
        internal static void ApplySplitCampusProducts()
        {
            ApplyClubProduct();
            ApplySchoolProduct();
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

            foreach (var accounting in products.Where(p => p.Id != product.Id && string.IsNullOrEmpty(p.DemoSlug)
                && p.Title != null && p.Title.Contains("حسابداری")))
            {
                if (ProductHasOrders(accounting.Id))
                {
                    accounting.IsActive = false;
                    UpdateProduct(accounting);
                }
                else DeleteProduct(accounting.Id);
            }
        }

        /// <summary>تصویر هر محصول نرم‌افزاری را با خود محصول عوض می‌کند؛ فقط اگر هنوز تصویر عمومی یا تصویر محصول دیگری باشد.</summary>
        internal static void ApplyMatchedProductImages()
        {
            var previous = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "digital", new[] { "/Assets/img/products/store-digital.jpg" } },
                { "clinic", new[] { "/Assets/img/booking/hero-clinic.jpg", "/Assets/img/products/store-booking.jpg" } },
                { "beauty", new[] { "/Assets/img/booking/hero-salon.jpg", "/Assets/img/products/store-booking.jpg" } },
                { "consulting", new[] { "/Assets/img/booking/hero-clinic.jpg", "/Assets/img/products/store-booking.jpg" } },
                { "school", new[] { "/Assets/img/hero-img.jpg", "/Assets/img/products/store-booking.jpg" } },
                { "club", new[] { "/Assets/img/products/store-booking.jpg", "/Assets/img/hero-img.jpg" } }
            };

            foreach (var product in GetProducts())
            {
                string image;
                string[] olds;
                if (string.IsNullOrEmpty(product.DemoSlug)
                    || !StoreDemoImages.TryGetValue(product.DemoSlug, out image)
                    || !previous.TryGetValue(product.DemoSlug, out olds)) continue;
                if (string.Equals(product.ImageUrl, image, StringComparison.OrdinalIgnoreCase)) continue;

                var current = product.ImageUrl ?? "";
                var stock = string.IsNullOrEmpty(current)
                    || current.StartsWith("/Assets/img/portfolio/", StringComparison.OrdinalIgnoreCase)
                    || olds.Any(old => string.Equals(current, old, StringComparison.OrdinalIgnoreCase));
                if (!stock) continue;

                product.ImageUrl = image;
                UpdateProduct(product);
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

        /// <summary>
        /// Refreshes catalogue copy so shop/booking/campus products say in-person operations are possible,
        /// and the gadget store is not mistaken for course/file sales.
        /// </summary>
        internal static void ApplyOfflineProductMessaging()
        {
            ApplyBookingProduct();
            ApplyClubProduct();

            foreach (var product in GetProducts())
            {
                if (product.DemoSlug == "fashion"
                    && product.ShortDescription != null
                    && product.ShortDescription.IndexOf("فروش حضوری", StringComparison.Ordinal) < 0)
                {
                    product.ShortDescription = "فروشگاه واقعی چندمستأجری: سبد خرید، فروش حضوری با فاکتور، انبار، گالری و درگاه پرداخت";
                    if (product.Description != null && product.Description.IndexOf("فاکتور فروش حضوری", StringComparison.Ordinal) < 0)
                        product.Description = "فروشگاه پوشاک با پنل مدیریت کامل، سبد خرید آنلاین، فاکتور فروش حضوری و موجودی یکپارچه، گروه‌بندی درختی، خصوصیات، صفحات، گالری و دامنه/کد شرکت اختصاصی.";
                    UpdateProduct(product);
                }
                else if (product.DemoSlug == "digital"
                    && (product.ShortDescription == null
                        || product.ShortDescription.IndexOf("دوره", StringComparison.Ordinal) >= 0
                        || product.ShortDescription.IndexOf("گجت", StringComparison.Ordinal) < 0))
                {
                    product.ShortDescription = "فروشگاه کالای دیجیتال و گجت با سبد خرید، انبار، فاکتور حضوری و درگاه پرداخت";
                    product.Description = "فروشگاه کالای دیجیتال و گجت (موبایل، لوازم جانبی و…) با کاتالوگ، سبد خرید، موجودی، فاکتور فروش حضوری، پنل ادمین/مشتری، صفحات و درگاه پرداخت. برای دوره و فایل آموزشی از محصول آموزش استفاده کنید.";
                    UpdateProduct(product);
                }
                else if (product.DemoSlug == "grocery"
                    && product.ShortDescription != null
                    && product.ShortDescription.IndexOf("حضوری", StringComparison.Ordinal) < 0)
                {
                    product.ShortDescription = "سوپرمارکت با پنل مدیریت، سفارش آنلاین، فاکتور حضوری و پرداخت";
                    if (product.Description != null && product.Description.IndexOf("فاکتور", StringComparison.Ordinal) < 0)
                        product.Description = "فروشگاه سوپرمارکت کامل: محصول، گروه، سبد خرید، فاکتور فروش حضوری، پنل مشتری، منوی درختی، گالری و درگاه — هر مشتری با کد شرکت خودش.";
                    UpdateProduct(product);
                }
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
                    ShortDescription = "فروشگاه واقعی چندمستأجری: سبد خرید، فروش حضوری با فاکتور، انبار، گالری و درگاه پرداخت",
                    Description = "فروشگاه پوشاک با پنل مدیریت کامل، سبد خرید آنلاین، فاکتور فروش حضوری و موجودی یکپارچه، گروه‌بندی درختی، خصوصیات، صفحات، گالری و دامنه/کد شرکت اختصاصی.",
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
                    ShortDescription = "فروشگاه کالای دیجیتال و گجت با سبد خرید، انبار، فاکتور حضوری و درگاه پرداخت",
                    Description = "فروشگاه کالای دیجیتال و گجت (موبایل، لوازم جانبی و…) با کاتالوگ، سبد خرید، موجودی، فاکتور فروش حضوری، پنل ادمین/مشتری، صفحات و درگاه پرداخت. برای دوره و فایل آموزشی از محصول آموزش استفاده کنید.",
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
