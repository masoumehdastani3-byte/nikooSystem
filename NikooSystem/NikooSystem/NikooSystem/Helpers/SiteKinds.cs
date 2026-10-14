using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models.Store;

namespace NikooSystem.Helpers
{
    /// <summary>
    /// The families of sites Nikoo sells. Each store type belongs to one kind, and the kind decides which
    /// features the storefront and the admin panel offer.
    /// </summary>
    public class SiteKindInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        /// <summary>What a buyer gets; shown on the signup page.</summary>
        public string[] Features { get; set; }
        /// <summary>The name of the store's main items in the admin menu (محصولات، دوره‌ها و کتاب‌ها، مطالب).</summary>
        public string ItemsLabel { get; set; }

        public bool IsShop => Id == SiteKinds.Shop;
        public bool IsDigital => Id == SiteKinds.Digital;
        public bool IsContent => Id == SiteKinds.Content;
        public bool IsBooking => Id == SiteKinds.Booking;
        public bool IsCampus => Id == SiteKinds.Campus;
        /// <summary>Staff hours and appointments: clinics, and the private lessons of an academy or sports club.</summary>
        public bool Schedules => IsBooking || IsCampus;
        /// <summary>Sells something: has a catalogue, cart, checkout, orders and customers.</summary>
        public bool Sells => IsShop || IsDigital;
        /// <summary>Receives money online (orders, or deposits of appointments), so it has a payment gateway.</summary>
        public bool TakesPayments => !IsContent;
        /// <summary>How the owner's site is called in the panel: «فروشگاه» for selling sites, otherwise «سایت».</summary>
        public string SiteNoun => Sells ? "فروشگاه" : "سایت";
    }

    public static class SiteKinds
    {
        public const string Shop = "shop";
        public const string Digital = "digital";
        public const string Content = "content";
        public const string Booking = "booking";
        public const string Campus = "campus";

        public static readonly IReadOnlyList<SiteKindInfo> All = new List<SiteKindInfo>
        {
            new SiteKindInfo
            {
                Id = Shop,
                Name = "فروشگاه کالای فیزیکی",
                Description = "برای فروش کالایی که ارسال می‌شود: پوشاک، مواد غذایی، لوازم دیجیتال و…",
                Icon = "bi-box-seam",
                ItemsLabel = "محصولات",
                Features = new[]
                {
                    "مدیریت موجودی انبار و هشدار اتمام موجودی",
                    "هزینه ارسال و ارسال رایگان بالای مبلغ مشخص",
                    "کد رهگیری مرسوله و پیگیری سفارش",
                    "قیمت قبل از تخفیف و برچسب‌های ویژه",
                    "فاکتور خرید و فروش با فهرست تامین‌کنندگان و مشتریان، دریافت و پرداخت، سند و نوع سند، انبار و اموال",
                    "حسابداری دوطرفه با گروه، کل، معین و تفصیلی دو سطحی؛ تراز چهارستونی و دفتر کل، معین و تفصیلی",
                    "حساب‌های بانکی جدا با شبا و درگاه پرداخت؛ فروش آنلاین در دفتر همان بانک",
                    "صفحه اول با پیشنهاد شگفت‌انگیز، نظر خریداران و پرسش‌های پرتکرار"
                }
            },
            new SiteKindInfo
            {
                Id = Digital,
                Name = "کتاب الکترونیک و دوره آموزشی",
                Description = "برای فروش کتاب الکترونیک، فایل دانلودی و دوره‌های ویدیویی",
                Icon = "bi-easel",
                ItemsLabel = "دوره‌ها و کتاب‌ها",
                Features = new[]
                {
                    "فایل‌های محافظت‌شده که فقط خریدار دانلود می‌کند",
                    "دوره با فصل و جلسه، جلسه‌های رایگان پیش‌نمایش",
                    "کتابخانه من و پخش آنلاین ویدیو برای دانشجو",
                    "دسترسی خودکار بلافاصله بعد از پرداخت",
                    "ثبت فروش آنلاین در دفاتر، دریافت و پرداخت و فاکتور فروش",
                    "حسابداری دوطرفه با گروه، کل، معین و تفصیلی؛ تراز چهارستونی و دفتر کل و معین",
                    "حساب‌های بانکی و درگاه پرداخت؛ فروش آنلاین در دفتر همان بانک",
                    "صفحه اول با تازه‌ترین‌ها، آمار، نظر دانشجویان و پرسش‌های پرتکرار"
                }
            },
            new SiteKindInfo
            {
                Id = Content,
                Name = "سایت محتوایی، شرکتی و خبرگزاری",
                Description = "برای انتشار خبر و مقاله، معرفی شرکت و ارتباط با مخاطب",
                Icon = "bi-newspaper",
                ItemsLabel = "مطالب",
                Features = new[]
                {
                    "خبر و مقاله با روتیتر، لید، منبع و برچسب",
                    "تیتر یک، نوار خبر فوری و سرویس‌های خبری",
                    "نظرات خوانندگان با تأیید و پاسخ تحریریه",
                    "فرم تماس، خوراک RSS و دکمه‌های اشتراک‌گذاری",
                    "صفحه اول شرکتی با آمار، چرا ما، روند همکاری و رضایت مشتریان",
                    "سه قالب شرکتی: رسمی، آژانس خلاق و صنعتی / پیمانکاری",
                    "سه قالب خبری: مجله، روزنامه کلاسیک و خبر آنلاین پالس"
                }
            },
            new SiteKindInfo
            {
                Id = Booking,
                Name = "رزرو آنلاین نوبت",
                Description = "برای مطب، کلینیک، سالن زیبایی و مشاوره؛ مشتری خودش وقت می‌گیرد",
                Icon = "bi-calendar-check",
                ItemsLabel = "خدمات",
                Features = new[]
                {
                    "انتخاب خدمت، متخصص، روز و ساعت خالی توسط مشتری",
                    "ساعت کاری هفتگی هر متخصص، تعطیلی و مرخصی",
                    "تقویم هفتگی نوبت‌ها و ثبت نوبت تلفنی در پنل",
                    "بیعانه آنلاین با زرین‌پال، پیگیری و لغو نوبت"
                }
            },
            new SiteKindInfo
            {
                Id = Campus,
                Name = "آموزشگاه و باشگاه ورزشی",
                Description = "کلاس گروهی، کلاس خصوصی آنلاین یا حضوری، و رزرو ساعتی سالن و زمین",
                Icon = "bi-trophy",
                ItemsLabel = "کلاس‌ها",
                Features = new[]
                {
                    "تعریف کلاس و دوره با ظرفیت، شهریه، سطح و گروه سنی",
                    "ثبت‌نام آنلاین و ثبت‌نام تلفنی، با کد پیگیری",
                    "کلاس خصوصی با مدرس، حضوری یا آنلاین، روی ساعت کاری خودش",
                    "رزرو ساعتی سالن، زمین و میز؛ مثل فوتبال و پینگ‌پونگ",
                    "سند خودکار شهریه و اجاره، دریافت و پرداخت، خرید از تامین‌کنندگان، انبار و اموال",
                    "حسابداری دوطرفه با گروه، کل، معین و تفصیلی دو سطحی؛ مرکز هزینه برای هر سالن یا شعبه",
                    "حساب‌های بانکی و درگاه پرداخت؛ شهریه آنلاین در دفتر همان بانک",
                    "پرداخت شهریه و اجاره با زرین‌پال و خبر فوری در بله"
                }
            }
        };

        public static SiteKindInfo Get(string id) => All.FirstOrDefault(k => k.Id == id) ?? All[0];

        /// <summary>The kind of a store, from its store type; stores without a type are physical shops.</summary>
        public static SiteKindInfo Of(TenantInfo tenant) => Get(StoreTypes.Get(tenant?.StoreType).Kind);

        /// <summary>Product types a store of this kind may sell.</summary>
        public static StoreProductType[] ProductTypes(this SiteKindInfo kind) => kind.IsDigital
            ? new[] { StoreProductType.Course, StoreProductType.Ebook, StoreProductType.File }
            : new[] { StoreProductType.Physical };

        public static string TypeText(this StoreProductType type)
        {
            switch (type)
            {
                case StoreProductType.Ebook: return "کتاب الکترونیک";
                case StoreProductType.Course: return "دوره آموزشی";
                case StoreProductType.File: return "فایل دانلودی";
                default: return "کالای فیزیکی";
            }
        }

        public static string TypeIcon(this StoreProductType type)
        {
            switch (type)
            {
                case StoreProductType.Ebook: return "bi-book";
                case StoreProductType.Course: return "bi-play-btn";
                case StoreProductType.File: return "bi-file-earmark-arrow-down";
                default: return "bi-box-seam";
            }
        }
    }
}
