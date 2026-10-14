using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Helpers
{
    /// <summary>A module that comes with Nikoo's sites; what the landing page, product pages and signup advertise.</summary>
    public sealed class PlatformModule
    {
        public string Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string Summary { get; set; }
        /// <summary>The <see cref="SiteKinds"/> that have it; null = every kind.</summary>
        public string[] Kinds { get; set; }
        /// <summary>Also named as a badge next to each kind on the signup page.</summary>
        public bool Highlight { get; set; }

        /// <summary>Which products have it, when not all do (shown as a badge on the landing page).</summary>
        public string Only { get; set; }

        public bool For(SiteKindInfo kind) => Kinds == null || Kinds.Contains(kind.Id);
    }

    /// <summary>
    /// Every module Nikoo's products offer, in the order the site shows them. A new feature of the platform belongs here,
    /// so the landing page, the product pages, the signup page and the FAQ advertise it.
    /// </summary>
    public static class PlatformModules
    {
        public static readonly IReadOnlyList<PlatformModule> All = new List<PlatformModule>
        {
            new PlatformModule
            {
                Id = "seo",
                Icon = "bi-google",
                Name = "سئو و نمایش در گوگل",
                Summary = "عنوان و توضیح اختصاصی هر صفحه، نقشه سایت و robots خودکار، داده ساختاریافته برای نتایج غنی گوگل و اتصال به سرچ کنسول",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "bale",
                Icon = "bi-bell",
                Name = "اطلاع‌رسانی در اپلیکیشن بله",
                Summary = "هر سفارش، پرداخت، نوبت، نظر یا پیام تازه همان لحظه با جزئیات در بله به شما و همکارتان خبر داده می‌شود",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "visits",
                Icon = "bi-bar-chart-line",
                Name = "آمار بازدید روزانه و ماهانه",
                Summary = "تعداد بازدید و بازدیدکننده سایت را امروز، دیروز، این ماه و ماه قبل با نمودار ۳۰ روز و ۱۲ ماه اخیر در داشبورد پنل می‌بینید؛ ربات‌ها و بازدید خود شما حساب نمی‌شوند",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "storage",
                Icon = "bi-hdd",
                Name = "فضای ذخیره‌سازی قابل افزایش",
                Summary = "هر سایت فضای رایگان برای تصاویر و فایل‌ها دارد؛ میزان مصرف را با نمودار در پنل می‌بینید، پیش از پر شدن هشدار می‌گیرید، فایل‌های بی‌استفاده با یک دکمه پاک می‌شوند و هر وقت لازم شد، فضای بیشتر را آنلاین و دائمی می‌خرید",
                Highlight = false
            },
            new PlatformModule
            {
                Id = "subscription",
                Icon = "bi-calendar-check",
                Name = "اشتراک سه‌ماهه، شش‌ماهه یا یک‌ساله",
                Summary = "دوره اشتراک را خودتان انتخاب می‌کنید و با دوره‌های بلندتر تا ۲۰٪ صرفه‌جویی می‌کنید؛ تمدید آنلاین از داخل پنل است، روزهای باقی‌مانده از دست نمی‌رود و دو هفته پیش از پایان، در پنل و بله یادآوری می‌گیرید",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "image-fit",
                Icon = "bi-aspect-ratio",
                Name = "نمایش یکدست تصاویر",
                Summary = "عکس‌ها را با هر اندازه و شکلی بارگذاری کنید؛ خودکار فشرده می‌شوند و در کارت‌ها، گالری و بنرها در کادرهای هم‌اندازه می‌نشینند تا ظاهر سایت مرتب بماند. انتخاب با شماست: پر کردن کادر، نمایش کامل یا حالت هوشمند",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "support",
                Icon = "bi-headset",
                Name = "پشتیبانی تیکتی، همراه با بله",
                Summary = "از داخل پنل برای پشتیبانی نیکو تیکت بفرستید، اسکرین‌شات و عکس پیوست کنید و گفتگو را دنبال کنید؛ پاسخ کارشناسان با تصویرهایش در اپلیکیشن بله هم می‌رسد و با «پاسخ» روی همان پیام، متن یا عکس می‌فرستید",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "torob",
                Icon = "bi-graph-up",
                Name = "اتصال به ترب",
                Summary = "محصولات با قیمت، تخفیف و موجودی به‌روز از راه API رسمی نسخه ۳ ترب، خودکار در ترب دیده می‌شوند",
                Kinds = new[] { SiteKinds.Shop },
                Only = "فروشگاه‌های کالا",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "payment",
                Icon = "bi-credit-card",
                Name = "درگاه پرداخت زرین‌پال",
                Summary = "پول فروش، شهریه یا بیعانه نوبت مستقیم به حساب زرین‌پال خودتان واریز می‌شود",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Booking, SiteKinds.Campus },
                Only = "فروشگاه، آموزش، نوبت‌دهی و باشگاه",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "campus",
                Icon = "bi-trophy",
                Name = "آموزشگاه و باشگاه ورزشی",
                Summary = "کلاس و دوره گروهی با ظرفیت و شهریه، ثبت‌نام آنلاین، کلاس خصوصی با مدرس به‌صورت حضوری یا آنلاین، و رزرو ساعتی سالن و زمین مثل فوتبال و پینگ‌پونگ",
                Kinds = new[] { SiteKinds.Campus },
                Only = "آموزشگاه و باشگاه ورزشی",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "accounting",
                Icon = "bi-journal-text",
                Name = "حسابداری ساده",
                Summary = "سرفصل و سند حسابداری، نوع سند دلخواه، فهرست تامین‌کنندگان و مشتریان، فاکتور خرید و فروش به نام آن‌ها، تعریف حساب‌های بانکی با شبا و اطلاعات درگاه پرداخت، دریافت و پرداخت صندوق و هر بانک، کنترل موجودی کالا و اموال، و دفتر کل و معین با جستجو، مرتب‌سازی و صفحه‌بندی؛ فروش آنلاین، شهریه کلاس و رزرو سالن خودکار در دفاتر ثبت می‌شود",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus },
                Only = "فروشگاه، آموزش و باشگاه",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "domain",
                Icon = "bi-globe",
                Name = "دامنه اختصاصی",
                Summary = "آدرس رایگان روی نیکو سیستم، یا دامنه خودتان با راهنمای قدم‌به‌قدم و رکوردهای DNS آماده در پنل؛ تنظیم دامنه خودکار بررسی می‌شود و گواهی امنیتی SSL (https) را پشتیبانی نیکو فعال می‌کند"
            },
            new PlatformModule
            {
                Id = "templates",
                Icon = "bi-palette",
                Name = "قالب‌های آماده و چیدمان صفحه",
                Summary = "قالب‌های حرفه‌ای در سبک‌های روز (برای فروشگاه‌ها ۱۵ قالب: لوکس طلایی، اپل‌گونه، ژاپنی، نئوبروتال، شیشه‌ای، مارکت‌پلیس و ...؛ "
                    + "برای سایت شرکتی سه سبک رسمی، آژانس خلاق و صنعتی؛ برای خبرگزاری سه سبک مجله، روزنامه کلاسیک و خبر آنلاین پالس؛ "
                    + "و قالب باشگاه و آموزشگاه برای کلاس، مربی و رزرو سالن) با پیش‌نمایش زنده، رنگ‌های دلخواه و چیدمان بخش‌های صفحه اول بدون کدنویسی"
            },
            new PlatformModule
            {
                Id = "home-sections",
                Icon = "bi-stars",
                Name = "صفحه اول فروش‌محور",
                Summary = "پیشنهاد شگفت‌انگیز با شمارش معکوس، تازه‌ترین کالاها، نظر خریداران، آمار و افتخارات، پرسش‌های پرتکرار با داده ساختاریافته گوگل و نوار دعوت به عضویت؛ "
                    + "در سایت شرکتی: چرا ما، روند همکاری، رضایت مشتریان و درخواست مشاوره",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Content },
                Only = "فروشگاه، آموزش و سایت شرکتی"
            },
            new PlatformModule
            {
                Id = "content",
                Icon = "bi-menu-button-wide",
                Name = "منو، صفحات و گالری",
                Summary = "منوی چندسطحی، صفحه‌های دلخواه مثل درباره ما و قوانین، و گالری تصاویر با آلبوم"
            },
            new PlatformModule
            {
                Id = "customers",
                Icon = "bi-person-circle",
                Name = "حساب کاربری مشتری",
                Summary = "مشتری سفارش‌ها، فایل‌ها و دوره‌های خریده‌شده را در پنل خودش می‌بیند و پیگیری می‌کند",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital },
                Only = "فروشگاه و آموزش"
            },
            new PlatformModule
            {
                Id = "messages",
                Icon = "bi-envelope",
                Name = "فرم تماس و صندوق پیام",
                Summary = "پیام‌های فرم تماس در پنل جمع می‌شوند و پیام‌های خوانده‌نشده جدا نمایش داده می‌شوند"
            },
            new PlatformModule
            {
                Id = "trust",
                Icon = "bi-patch-check",
                Name = "اینماد و شبکه‌های اجتماعی",
                Summary = "راهنمای قدم‌به‌قدم گرفتن اینماد با چک‌لیست آماده بودن سایت؛ گذاشتن فایل تأیید در ریشه سایت، متاتگ اینماد و عنوان موقت صفحه اول از پنل، بدون نیاز به پشتیبانی؛ نماد اعتماد امن در فوتر و لینک اینستاگرام، تلگرام، واتس‌اپ، آپارات و…"
            },
            new PlatformModule
            {
                Id = "quick",
                Icon = "bi-pencil-square",
                Name = "ویرایش سریع از روی سایت",
                Summary = "مدیر وارد‌شده روی هر بخش سایت دکمه ویرایش می‌بیند و بدون گشتن در پنل آن را تغییر می‌دهد"
            },
            new PlatformModule
            {
                Id = "tutorials",
                Icon = "bi-play-circle",
                Name = "آموزش داخل پنل",
                Summary = "راهنمای قدم‌به‌قدم و ویدیوی آموزشی روی هر صفحه پنل مدیریت"
            },
            new PlatformModule
            {
                Id = "responsive",
                Icon = "bi-phone",
                Name = "سازگار با موبایل",
                Summary = "سایت و پنل مدیریت روی موبایل، تبلت و رایانه درست و سریع نمایش داده می‌شوند"
            }
        };

        public static IEnumerable<PlatformModule> For(SiteKindInfo kind) => All.Where(m => m.For(kind));

        public static PlatformModule Get(string id) => All.FirstOrDefault(m => m.Id == id);
    }
}
