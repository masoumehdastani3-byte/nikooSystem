using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Helpers
{
    public class StoreTypeInfo
    {
        public string Id { get; set; }
        /// <summary>One of <see cref="SiteKinds"/>.</summary>
        public string Kind { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string DefaultTemplate { get; set; }
    }

    public static class StoreTypes
    {
        public static readonly IReadOnlyList<StoreTypeInfo> All = new List<StoreTypeInfo>
        {
            new StoreTypeInfo
            {
                Id = "general",
                Kind = SiteKinds.Shop,
                Name = "فروشگاه عمومی",
                Description = "برای هر نوع کالا؛ ساده و قابل تغییر",
                Icon = "bi-shop",
                DefaultTemplate = "minimal"
            },
            new StoreTypeInfo
            {
                Id = "fashion",
                Kind = SiteKinds.Shop,
                Name = "پوشاک و مد",
                Description = "سایز، رنگ و گالری تصاویر محصول",
                Icon = "bi-bag-check",
                DefaultTemplate = "fashion"
            },
            new StoreTypeInfo
            {
                Id = "grocery",
                Kind = SiteKinds.Shop,
                Name = "مواد غذایی و سوپرمارکت",
                Description = "دسته‌بندی زیاد، وزن و برند محصول",
                Icon = "bi-basket",
                DefaultTemplate = "organic"
            },
            new StoreTypeInfo
            {
                Id = "digital",
                Kind = SiteKinds.Shop,
                Name = "دیجیتال و گجت",
                Description = "موبایل، هدفون، ساعت هوشمند و لوازم جانبی",
                Icon = "bi-cpu",
                DefaultTemplate = "marketplace"
            },
            new StoreTypeInfo
            {
                Id = "ebook",
                Kind = SiteKinds.Digital,
                Name = "کتاب الکترونیک و فایل",
                Description = "PDF، EPUB، کتاب صوتی و فایل‌های دانلودی",
                Icon = "bi-book",
                DefaultTemplate = "boutique"
            },
            new StoreTypeInfo
            {
                Id = "academy",
                Kind = SiteKinds.Digital,
                Name = "آموزشگاه آنلاین",
                Description = "دوره‌های ویدیویی با فصل، جلسه و پنل دانشجو",
                Icon = "bi-easel",
                DefaultTemplate = "marketplace"
            },
            new StoreTypeInfo
            {
                Id = "news",
                Kind = SiteKinds.Content,
                Name = "خبرگزاری و مجله",
                Description = "خبر و مقاله، تیتر یک، خبر فوری و سرویس‌های خبری",
                Icon = "bi-newspaper",
                DefaultTemplate = "magazine"
            },
            new StoreTypeInfo
            {
                Id = "corporate",
                Kind = SiteKinds.Content,
                Name = "سایت شرکتی",
                Description = "معرفی شرکت و خدمات، اخبار، نمونه‌کار و فرم تماس",
                Icon = "bi-building",
                DefaultTemplate = "corporate"
            },
            new StoreTypeInfo
            {
                Id = "clinic",
                Kind = SiteKinds.Booking,
                Name = "مطب و کلینیک",
                Description = "نوبت پزشک، دندانپزشک، فیزیوتراپی و آزمایشگاه",
                Icon = "bi-heart",
                DefaultTemplate = "clinic"
            },
            new StoreTypeInfo
            {
                Id = "beauty",
                Kind = SiteKinds.Booking,
                Name = "سالن زیبایی و آرایشگاه",
                Description = "خدمات مو، ناخن و پوست با انتخاب آرایشگر",
                Icon = "bi-scissors",
                DefaultTemplate = "salon"
            },
            new StoreTypeInfo
            {
                Id = "consulting",
                Kind = SiteKinds.Booking,
                Name = "مشاوره و خدمات حرفه‌ای",
                Description = "مشاوره حقوقی، روانشناسی، تحصیلی و جلسه آنلاین",
                Icon = "bi-chat-dots",
                DefaultTemplate = "clinic"
            },
            new StoreTypeInfo
            {
                Id = "club",
                Kind = SiteKinds.Campus,
                Name = "آموزشگاه و باشگاه ورزشی",
                Description = "کلاس گروهی، مدرس خصوصی آنلاین یا حضوری، و رزرو ساعتی سالن",
                Icon = "bi-trophy",
                DefaultTemplate = "club"
            }
        };

        public static StoreTypeInfo Get(string id)
        {
            return All.FirstOrDefault(t => t.Id == (id ?? "").Trim().ToLowerInvariant()) ?? All[0];
        }

        public static IEnumerable<StoreTypeInfo> OfKind(string kind) => All.Where(t => t.Kind == kind);
    }
}
