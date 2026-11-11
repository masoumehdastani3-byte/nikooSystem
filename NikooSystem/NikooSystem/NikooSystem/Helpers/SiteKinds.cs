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
        public bool IsMenu => Id == SiteKinds.Menu;
        /// <summary>Staff hours and appointments: clinics, and the private lessons of an academy or sports club.</summary>
        public bool Schedules => IsBooking || IsCampus;
        /// <summary>Sells something: has a catalogue, cart, checkout, orders and customers.</summary>
        public bool Sells => IsShop || IsDigital;
        /// <summary>Receives money online (orders, or deposits of appointments), so it has a payment gateway.</summary>
        public bool TakesPayments => !IsContent && !IsMenu;
        /// <summary>How the owner's site is called in the panel: «فروشگاه» for selling sites, otherwise «سایت».</summary>
        public string SiteNoun => IsMenu ? "منو" : Sells ? "فروشگاه" : "سایت";
        /// <summary>Service-first businesses (salon/clinic/club) — accounting copy stresses services over retail stock.</summary>
        public bool ServiceBusiness => IsBooking || IsCampus;
    }

    public static class SiteKinds
    {
        public const string Shop = "shop";
        public const string Digital = "digital";
        public const string Content = "content";
        public const string Booking = "booking";
        public const string Campus = "campus";
        public const string Menu = "menu";

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
                    "مدیریت فروش آنلاین و حضوری در یک پنل؛ سفارش اینترنتی، صندوق و فاکتور حضوری با موجودی و حسابداری مشترک",
                    "مدیریت موجودی انبار و هشدار اتمام موجودی؛ فروش آنلاین و حضوری از یک موجودی کم می‌شود",
                    "هزینه ارسال و ارسال رایگان بالای مبلغ مشخص؛ درآمد ارسال و هزینه حمل پست/پیک در حسابداری جدا ثبت می‌شود",
                    "کد رهگیری مرسوله، پیگیری سفارش آنلاین و درخواست مرجوعی کالا از حساب مشتری",
                    "قیمت قبل از تخفیف و برچسب‌های ویژه",
                    "صندوق فروش سریع برای فروش حضوری، به‌همراه فاکتور خرید/فروش با تخفیف و مالیات ارزش افزوده، طرف‌حساب، دریافت نقد/بانک، سند، انبار و اموال",
                    "کاربران پنل با نقش مدیر، صندوقدار و منشی؛ هر نقش فقط بخش‌های مجاز را می‌بیند",
                    "داشبورد شخصی با میانبرهای دلخواه برای دسترسی سریع به منوهای پرکاربرد",
                    "حسابداری دوطرفه با گروه، کل، معین و تفصیلی دو سطحی؛ تراز چهارستونی و دفتر کل، معین و تفصیلی",
                    "سال مالی شمسی با فیلتر پیش‌فرض روی سال جاری و بستن سال به سود و زیان انباشته؛ تنظیم نرخ مالیات ارزش افزوده",
                    "حساب‌های بانکی جدا با شبا و درگاه؛ فروش آنلاین و فاکتور حضوری در دفتر همان بانک یا صندوق",
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
                    "دسترسی خودکار بلافاصله بعد از پرداخت آنلاین",
                    "اعطای دستی دسترسی دوره و فایل از پنل برای فروش حضوری یا کارت‌به‌کارت، با سند صندوق/بانک",
                    "مدیریت فروش آنلاین و حضوری با هم؛ درگاه، صندوق و فاکتور در یک پنل و یک گزارش",
                    "کاربران پنل با نقش مدیر، صندوقدار و منشی برای تیم فروش و پشتیبانی",
                    "داشبورد شخصی با میانبرهای دلخواه برای دسترسی سریع به منوهای پرکاربرد",
                    "فاکتور فروش حضوری و دریافت نقد/کارت‌به‌کارت در دفاتر، کنار فروش آنلاین",
                    "حسابداری دوطرفه با گروه، کل، معین و تفصیلی؛ تراز چهارستونی و دفتر کل و معین؛ سال مالی شمسی با بستن سال",
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
                    "کاربران پنل با نقش مدیر برای همکاری تحریریه بدون دادن رمز مالک",
                    "داشبورد شخصی با میانبرهای دلخواه برای دسترسی سریع به منوهای پرکاربرد",
                    "صفحه اول شرکتی با آمار، چرا ما، روند همکاری و رضایت مشتریان",
                    "سه قالب شرکتی: رسمی، آژانس خلاق و صنعتی / پیمانکاری",
                    "سه قالب خبری: مجله، روزنامه کلاسیک و خبر آنلاین پالس"
                }
            },
            new SiteKindInfo
            {
                Id = Booking,
                Name = "رزرو آنلاین نوبت",
                Description = "سه محصول جدا: مطب و کلینیک، آرایشگاه و سالن زیبایی، و مشاوره؛ مشتری خودش وقت می‌گیرد",
                Icon = "bi-calendar-check",
                ItemsLabel = "خدمات",
                Features = new[]
                {
                    "انتخاب خدمت، متخصص، روز و ساعت خالی توسط مشتری از سایت",
                    "ساعت کاری هفتگی هر متخصص، تعطیلی و مرخصی",
                    "تقویم هفتگی و ثبت نوبت تلفنی یا حضوری در پنل؛ حتی اگر رزرو آنلاین روشن نباشد",
                    "بیعانه آنلاین اختیاری با اتصال به درگاه بانک، پیگیری و لغو نوبت با کد رهگیری",
                    "حسابداری و تسویه نقدی/بانکی مانده نوبت از پنل، بدون وابستگی به فروش آنلاین",
                    "کاربران پنل با نقش منشی برای تقویم و نوبت، و مدیر برای تنظیمات کامل",
                    "داشبورد شخصی با میانبرهای دلخواه برای دسترسی سریع به منوهای پرکاربرد",
                    "پیامک تأیید نوبت و یادآوری قبل از نوبت (با شارژ اعتبار پیامک)"
                }
            },
            new SiteKindInfo
            {
                Id = Campus,
                Name = "آموزشگاه و باشگاه",
                Description = "دو محصول جدا: آموزشگاه برای دوره عمومی و خصوصی، باشگاه ورزشی برای کلاس، مربی و رزرو سالن",
                Icon = "bi-trophy",
                ItemsLabel = "کلاس‌ها",
                Features = new[]
                {
                    "دوره عمومی با ظرفیت، شهریه، سطح و گروه سنی؛ چند نفر در یک کلاس",
                    "ثبت‌نام آنلاین و ثبت‌نام تلفنی/حضوری در پنل، با کد پیگیری",
                    "دوره خصوصی با مدرس یا مربی، حضوری یا آنلاین، روی ساعت کاری خودش",
                    "رزرو ساعتی فضا: اتاق و کلاس در آموزشگاه، سالن و زمین و میز در باشگاه",
                    "اداره حضوری با حسابداری: سند خودکار شهریه و اجاره، دریافت نقدی، فاکتور، انبار و اموال",
                    "کاربران پنل با نقش مدیر، صندوقدار و منشی برای پذیرش، صندوق و مدیریت",
                    "داشبورد شخصی با میانبرهای دلخواه برای دسترسی سریع به منوهای پرکاربرد",
                    "حسابداری دوطرفه با گروه، کل، معین و تفصیلی دو سطحی؛ مرکز هزینه برای هر سالن یا شعبه؛ سال مالی شمسی",
                    "حساب‌های بانکی و درگاه پرداخت؛ شهریه آنلاین یا حضوری در دفتر همان بانک یا صندوق",
                    "پرداخت شهریه و اجاره با اتصال به درگاه بانک و خبر فوری در بله"
                }
            },
            new SiteKindInfo
            {
                Id = Menu,
                Name = "منوی کافه و رستوران",
                Description = "منوی میز با QR؛ مشتری منو را روی موبایل می‌بیند و گارسون را صدا می‌زند",
                Icon = "bi-upc-scan",
                ItemsLabel = "آیتم‌های منو",
                Features = new[]
                {
                    "منوی موبایل با دسته‌بندی، جستجو، قیمت و وضعیت ناموجود",
                    "کارت QR جدا برای هر میز؛ شماره میز در فراخوان گارسون از قبل پر می‌شود",
                    "فراخوان گارسون با یادداشت، و بستن درخواست از پنل",
                    "ساعت کاری، یادداشت روز و رمز وای‌فای روی همان منو",
                    "ویرایش غذا و نوشیدنی، قیمت و نشان‌ها از همان بخش محصولات پنل"
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
