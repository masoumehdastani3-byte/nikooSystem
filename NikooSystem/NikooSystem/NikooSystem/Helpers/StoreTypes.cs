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
        /// <summary>کلمه‌ای که در برچسب «نام …» فرم ثبت‌نام می‌آید؛ مثلاً تعمیرگاه، مطب، فروشگاه.</summary>
        public string NameNoun { get; set; } = "فروشگاه";
        public string Description { get; set; }
        public string NameLabel => "نام " + (string.IsNullOrWhiteSpace(NameNoun) ? "فروشگاه" : NameNoun);
        public string NamePlaceholder => string.IsNullOrWhiteSpace(NameNoun) || NameNoun == "فروشگاه"
            ? "مثلاً پوشاک آریا"
            : "مثلاً " + NameNoun + " آریا";
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
                NameNoun = "آموزشگاه",
                Description = "دوره‌های ویدیویی با فصل، جلسه و پنل دانشجو",
                Icon = "bi-easel",
                DefaultTemplate = "marketplace"
            },
            new StoreTypeInfo
            {
                Id = "news",
                Kind = SiteKinds.Content,
                Name = "خبرگزاری و مجله",
                NameNoun = "خبرگزاری",
                Description = "خبر و مقاله، تیتر یک، خبر فوری و سرویس‌های خبری",
                Icon = "bi-newspaper",
                DefaultTemplate = "magazine"
            },
            new StoreTypeInfo
            {
                Id = "corporate",
                Kind = SiteKinds.Content,
                Name = "سایت شرکتی",
                NameNoun = "شرکت",
                Description = "معرفی شرکت و خدمات، اخبار، نمونه‌کار و فرم تماس",
                Icon = "bi-building",
                DefaultTemplate = "corporate"
            },
            new StoreTypeInfo
            {
                Id = "clinic",
                Kind = SiteKinds.Booking,
                Name = "مطب و کلینیک",
                NameNoun = "مطب",
                Description = "نوبت پزشک، دندانپزشک، فیزیوتراپی و آزمایشگاه",
                Icon = "bi-heart",
                DefaultTemplate = "clinic"
            },
            new StoreTypeInfo
            {
                Id = "beauty",
                Kind = SiteKinds.Booking,
                Name = "آرایشگاه و سالن زیبایی",
                NameNoun = "آرایشگاه",
                Description = "نوبت کوتاهی، رنگ، ناخن و پوست با انتخاب آرایشگر",
                Icon = "bi-scissors",
                DefaultTemplate = "salon"
            },
            new StoreTypeInfo
            {
                Id = "consulting",
                Kind = SiteKinds.Booking,
                Name = "مشاوره و خدمات حرفه‌ای",
                NameNoun = "مرکز مشاوره",
                Description = "مشاوره حقوقی، روانشناسی، تحصیلی و جلسه آنلاین",
                Icon = "bi-chat-dots",
                DefaultTemplate = "clinic"
            },
            new StoreTypeInfo
            {
                Id = "school",
                Kind = SiteKinds.Campus,
                Name = "آموزشگاه",
                NameNoun = "آموزشگاه",
                Description = "دوره عمومی با ظرفیت و شهریه، و دوره خصوصی حضوری یا آنلاین",
                Icon = "bi-journal-bookmark",
                DefaultTemplate = "club"
            },
            new StoreTypeInfo
            {
                Id = "club",
                Kind = SiteKinds.Campus,
                Name = "باشگاه ورزشی",
                NameNoun = "باشگاه",
                Description = "کلاس ورزشی، مربی خصوصی و رزرو ساعتی سالن و زمین",
                Icon = "bi-trophy",
                DefaultTemplate = "club"
            },
            new StoreTypeInfo
            {
                Id = "cafe",
                Kind = SiteKinds.Menu,
                Name = "کافه و رستوران",
                NameNoun = "کافه",
                Description = "منوی میز با QR، قیمت و فراخوان گارسون",
                Icon = "bi-cup-straw",
                DefaultTemplate = "cafe"
            },
            new StoreTypeInfo
            {
                Id = "repair",
                Kind = SiteKinds.Repair,
                Name = "تعمیرگاه",
                NameNoun = "تعمیرگاه",
                Description = "پذیرش موبایل، لپ‌تاپ، لوازم خانگی و خودرو با کد پیگیری",
                Icon = "bi-wrench",
                DefaultTemplate = "bench"
            }
        };

        public static StoreTypeInfo Get(string id)
        {
            return All.FirstOrDefault(t => t.Id == (id ?? "").Trim().ToLowerInvariant()) ?? All[0];
        }

        public static IEnumerable<StoreTypeInfo> OfKind(string kind) => All.Where(t => t.Kind == kind);
    }
}
