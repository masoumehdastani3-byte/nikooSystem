using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using Newtonsoft.Json;
using NikooSystem.Models;

namespace NikooSystem.Services
{
    public static class JsonStore
    {
        private static readonly object ProductLock = new object();
        private static readonly object OrderLock = new object();
        private static readonly object PaymentLock = new object();
        private static readonly object ClientLock = new object();

        private static string DataPath
        {
            get
            {
                var path = HostingEnvironment.MapPath("~/App_Data");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        private static string ProductsFile => Path.Combine(DataPath, "products.json");
        private static string OrdersFile => Path.Combine(DataPath, "orders.json");
        private static string PaymentsFile => Path.Combine(DataPath, "payments.json");
        private static string PaymentClientsFile => Path.Combine(DataPath, "payment-clients.json");

        public static List<Product> GetProducts()
        {
            lock (ProductLock)
            {
                EnsureSeed();
                EnsureDemoSlugs();
                return ReadList<Product>(ProductsFile);
            }
        }

        public static Product GetProduct(int id)
        {
            return GetProducts().FirstOrDefault(p => p.Id == id);
        }

        public static void SaveProducts(List<Product> products)
        {
            lock (ProductLock)
            {
                WriteList(ProductsFile, products);
            }
        }

        public static Product AddProduct(Product product)
        {
            lock (ProductLock)
            {
                var list = ReadList<Product>(ProductsFile);
                product.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                product.CreatedAt = DateTime.Now;
                list.Add(product);
                WriteList(ProductsFile, list);
                return product;
            }
        }

        public static bool UpdateProduct(Product product)
        {
            lock (ProductLock)
            {
                var list = ReadList<Product>(ProductsFile);
                var idx = list.FindIndex(x => x.Id == product.Id);
                if (idx < 0) return false;
                product.CreatedAt = list[idx].CreatedAt;
                list[idx] = product;
                WriteList(ProductsFile, list);
                return true;
            }
        }

        public static bool DeleteProduct(int id)
        {
            lock (ProductLock)
            {
                var list = ReadList<Product>(ProductsFile);
                var removed = list.RemoveAll(x => x.Id == id) > 0;
                if (removed) WriteList(ProductsFile, list);
                return removed;
            }
        }

        public static List<Order> GetOrders()
        {
            lock (OrderLock)
            {
                if (!File.Exists(OrdersFile))
                    WriteList(OrdersFile, new List<Order>());
                return ReadList<Order>(OrdersFile).OrderByDescending(o => o.CreatedAt).ToList();
            }
        }

        public static Order GetOrder(int id)
        {
            return GetOrders().FirstOrDefault(o => o.Id == id);
        }

        public static Order GetOrderByAuthority(string authority)
        {
            return GetOrders().FirstOrDefault(o => o.Authority == authority);
        }

        public static Order AddOrder(Order order)
        {
            lock (OrderLock)
            {
                var list = ReadList<Order>(OrdersFile);
                order.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                order.OrderNumber = "LF-" + DateTime.Now.ToString("yyyyMMdd") + "-" + order.Id.ToString("D4");
                order.CreatedAt = DateTime.Now;
                list.Add(order);
                WriteList(OrdersFile, list);
                return order;
            }
        }

        public static bool UpdateOrder(Order order)
        {
            lock (OrderLock)
            {
                var list = ReadList<Order>(OrdersFile);
                var idx = list.FindIndex(x => x.Id == order.Id);
                if (idx < 0) return false;
                list[idx] = order;
                WriteList(OrdersFile, list);
                return true;
            }
        }

        public static List<PaymentClient> GetPaymentClients()
        {
            lock (ClientLock)
            {
                EnsurePaymentClientsSeed();
                return ReadList<PaymentClient>(PaymentClientsFile);
            }
        }

        public static PaymentClient GetPaymentClient(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return null;
            return GetPaymentClients().FirstOrDefault(c =>
                string.Equals(c.AppId, appId, StringComparison.OrdinalIgnoreCase));
        }

        public static List<HubPayment> GetHubPayments()
        {
            lock (PaymentLock)
            {
                if (!File.Exists(PaymentsFile))
                    WriteList(PaymentsFile, new List<HubPayment>());
                return ReadList<HubPayment>(PaymentsFile).OrderByDescending(p => p.CreatedAt).ToList();
            }
        }

        public static HubPayment GetHubPaymentByToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            return GetHubPayments().FirstOrDefault(p =>
                string.Equals(p.Token, token, StringComparison.OrdinalIgnoreCase));
        }

        public static HubPayment AddHubPayment(HubPayment payment)
        {
            lock (PaymentLock)
            {
                var list = ReadList<HubPayment>(PaymentsFile);
                payment.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                if (string.IsNullOrWhiteSpace(payment.Token))
                    payment.Token = Guid.NewGuid().ToString("N");
                payment.CreatedAt = DateTime.Now;
                list.Add(payment);
                WriteList(PaymentsFile, list);
                return payment;
            }
        }

        public static bool UpdateHubPayment(HubPayment payment)
        {
            lock (PaymentLock)
            {
                var list = ReadList<HubPayment>(PaymentsFile);
                var idx = list.FindIndex(x => x.Id == payment.Id);
                if (idx < 0) return false;
                list[idx] = payment;
                WriteList(PaymentsFile, list);
                return true;
            }
        }

        private static void EnsurePaymentClientsSeed()
        {
            if (File.Exists(PaymentClientsFile)) return;
            WriteList(PaymentClientsFile, new List<PaymentClient>
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
            });
        }

        private static void EnsureDemoSlugs()
        {
            if (!File.Exists(ProductsFile)) return;

            var products = ReadList<Product>(ProductsFile);
            if (products == null || products.Count == 0) return;
            if (products.Any(p => !string.IsNullOrEmpty(p.DemoSlug))) return;

            var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "پوشاک", "fashion" },
                { "مواد غذایی", "grocery" },
                { "دیجیتال", "digital" }
            };

            var changed = false;
            foreach (var p in products)
            {
                if (!string.IsNullOrEmpty(p.DemoSlug)) continue;
                foreach (var kv in defaults)
                {
                    if (p.Title != null && p.Title.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        p.DemoSlug = kv.Value;
                        p.IsFeatured = true;
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
                WriteList(ProductsFile, products);
        }

        private static void EnsureSeed()
        {
            if (File.Exists(ProductsFile)) return;

            var products = new List<Product>
            {
                new Product
                {
                    Id = 1,
                    Title = "فروشگاه آنلاین پوشاک (اختصاصی)",
                    ShortDescription = "فروشگاه واقعی چندمستأجری: سبد خرید، پنل ادمین، منوی درختی، گالری و درگاه پرداخت",
                    Description = "فروشگاه آنلاین واقعی پوشاک با پنل مدیریت کامل، سبد خرید، گروه‌بندی درختی، خصوصیات، صفحات، گالری و دامنه/کد شرکت اختصاصی.",
                    Price = 15000,
                    ImageUrl = "/Assets/img/portfolio/portfolio-1.jpg",
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
                    ImageUrl = "/Assets/img/portfolio/portfolio-2.jpg",
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
                    ImageUrl = "/Assets/img/portfolio/portfolio-3.jpg",
                    Category = "فروشگاه آنلاین",
                    DemoSlug = "digital",
                    IsActive = true,
                    IsFeatured = true
                },
                new Product
                {
                    Id = 4,
                    Title = "سیستم مدیریت محتوا (CMS)",
                    ShortDescription = "مدیریت صفحات، بلاگ و محتوای سازمانی",
                    Description = "نرم‌افزار مدیریت محتوا برای ساخت و ویرایش صفحات سایت، بلاگ، گالری و منوها بدون دانش برنامه‌نویسی.",
                    Price = 20000,
                    ImageUrl = "/Assets/img/portfolio/portfolio-4.jpg",
                    Category = "نرم‌افزار سازمانی",
                    DemoSlug = null,
                    IsActive = true,
                    IsFeatured = false
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
                },
                new Product
                {
                    Id = 6,
                    Title = "سیستم حسابداری سبک",
                    ShortDescription = "ثبت فاکتور، موجودی و گزارش مالی پایه",
                    Description = "نرم‌افزار حسابداری سبک برای کسب‌وکارهای کوچک شامل فاکتور فروش، انبار ساده و گزارش‌های درآمد و هزینه.",
                    Price = 25000,
                    ImageUrl = "/Assets/img/portfolio/portfolio-6.jpg",
                    Category = "نرم‌افزار سازمانی",
                    DemoSlug = null,
                    IsActive = true,
                    IsFeatured = false
                }
            };

            WriteList(ProductsFile, products);
            if (!File.Exists(OrdersFile))
                WriteList(OrdersFile, new List<Order>());
        }

        private static List<T> ReadList<T>(string path)
        {
            if (!File.Exists(path))
                return new List<T>();
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }

        private static void WriteList<T>(string path, List<T> data)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}
