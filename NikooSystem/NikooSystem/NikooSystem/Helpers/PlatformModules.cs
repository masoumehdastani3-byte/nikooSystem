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
    /// Every module Nikoo's products offer, in the order the site shows them (most attractive first).
    /// A new feature of the platform belongs here, so the landing page, the product pages, the signup page and the FAQ advertise it.
    /// </summary>
    public static class PlatformModules
    {
        public static readonly IReadOnlyList<PlatformModule> All = new List<PlatformModule>
        {
            new PlatformModule
            {
                Id = "payment",
                Icon = "bi-credit-card",
                Name = "اتصال به درگاه بانک",
                Summary = "پرداخت آنلاین مشتریان از طریق درگاه بانکی به حساب خودتان واریز می‌شود؛ فروش، شهریه یا بیعانه نوبت بدون واسطهٔ اضافه",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Booking, SiteKinds.Campus },
                Only = "فروشگاه، آموزش، نوبت‌دهی و باشگاه",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "online-offline-sales",
                Icon = "bi-shop-window",
                Name = "مدیریت فروش آنلاین و حضوری در یک پنل",
                Summary = "سفارش اینترنتی با سبد و درگاه بانک کنار صندوق فروش سریع و فاکتور حضوری؛ موجودی از هر دو کانال کم می‌شود، نقد و بانک و پرداخت آنلاین در همان دفاتر می‌نشیند و گزارش فروش آنلاین و آفلاین را یکجا می‌بینید. اگر فعلاً فقط حضوری می‌فروشید هم انبار، صندوق و حسابداری بدون روشن کردن فروش آنلاین کار می‌کند",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus, SiteKinds.Booking },
                Only = "فروشگاه، آموزش، باشگاه و نوبت‌دهی",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "accounting",
                Icon = "bi-journal-text",
                Name = "حسابداری دوطرفه با گروه، کل، معین و تفصیلی",
                Summary = "سرفصل حساب‌های استاندارد سه سطحی (گروه، کل و معین) با حساب تفصیلی شناور دو سطحی برای اشخاص و مراکز هزینه و پروژه، سند با ستون بدهکار و بستانکار و برگه سند چاپی، سال مالی شمسی با فیلتر پیش‌فرض روی سال جاری و بستن سال (انتقال درآمد و هزینه به سود و زیان انباشته)، تراز چهارستونی در سطح گروه، کل، معین و تفصیلی برای هر بازه تاریخ، دفتر کل، دفتر معین و دفتر تفصیلی با مانده ردیف به ردیف، نوع سند دلخواه، فهرست تامین‌کنندگان و مشتریان، فاکتور خرید و فروش حضوری با تخفیف و مالیات بر ارزش افزوده و برگه چاپی، فیلتر از تاریخ تا تاریخ شمسی روی اسناد و فاکتورها و دریافت و پرداخت، تعریف حساب‌های بانکی با شبا و اطلاعات درگاه پرداخت، دریافت و پرداخت صندوق و هر بانک، کنترل موجودی کالا و اموال، همه با جستجو، مرتب‌سازی و صفحه‌بندی؛ حتی بدون فروش آنلاین می‌توانید فاکتور حضوری بزنید و فروش آنلاین، شهریه کلاس و رزرو سالن هم خودکار در دفاتر ثبت می‌شود",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus, SiteKinds.Booking },
                Only = "فروشگاه، آموزش، باشگاه و نوبت‌دهی",
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
                Id = "shipping-returns",
                Icon = "bi-truck",
                Name = "ارسال، هزینه حمل و مرجوعی کالا",
                Summary = "هزینه ارسال و آستانه ارسال رایگان روی سبد و تسویه، ثبت جداگانه درآمد ارسال و هزینه حمل پست/پیک در حسابداری، کد رهگیری مرسوله، و درخواست مرجوعی مشتری با تأیید شما و برگشت موجودی",
                Kinds = new[] { SiteKinds.Shop },
                Only = "فروشگاه‌های کالا",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "templates",
                Icon = "bi-palette",
                Name = "قالب‌های آماده و چیدمان صفحه",
                Summary = "قالب‌های حرفه‌ای در سبک‌های روز (برای فروشگاه‌ها ۱۵ قالب: لوکس طلایی، اپل‌گونه، ژاپنی، نئوبروتال، شیشه‌ای، مارکت‌پلیس و ...؛ "
                    + "برای سایت شرکتی سه سبک رسمی، آژانس خلاق و صنعتی؛ برای خبرگزاری سه سبک مجله، روزنامه کلاسیک و خبر آنلاین پالس؛ "
                    + "و قالب کلاس و سالن برای آموزشگاه و باشگاه ورزشی) با پیش‌نمایش زنده، رنگ‌های دلخواه و چیدمان بخش‌های صفحه اول بدون کدنویسی",
                Highlight = true
            },
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
                Id = "domain",
                Icon = "bi-globe",
                Name = "دامنه اختصاصی",
                Summary = "آدرس رایگان روی نیکو سیستم، یا دامنه خودتان با راهنمای قدم‌به‌قدم و رکوردهای DNS آماده در پنل؛ تنظیم دامنه خودکار بررسی می‌شود و گواهی امنیتی SSL (https) را پشتیبانی نیکو فعال می‌کند",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "panel-users",
                Icon = "bi-people",
                Name = "کاربران پنل با نقش مدیر، صندوقدار و منشی",
                Summary = "علاوه بر حساب مالک، برای همکارانتان ورود جدا بسازید؛ صندوقدار به صندوق و فروش، منشی به نوبت و ثبت‌نام، و مدیر به تقریباً همه بخش‌ها دسترسی دارد و منو بر اساس نقش فیلتر می‌شود",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "my-dashboard",
                Icon = "bi-grid-1x2",
                Name = "داشبورد شخصی با میانبرهای دلخواه",
                Summary = "هر کاربر پنل میانبرهای پرکاربرد خودش را از منوها می‌چیند تا در داشبورد با یک کلیک به صندوق، سفارش، نوبت یا هر بخش دیگری برسد؛ ترتیب و انتخاب برای هر نفر جدا ذخیره می‌شود",
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
                Id = "sms",
                Icon = "bi-chat-dots",
                Name = "پیامک مشتریان",
                Summary = "اعتبار پیامک را از پنل شارژ کنید تا بازیابی رمز، صورتحساب خرید، تأیید نوبت و یادآوری قبل از نوبت برای مشتریان ارسال شود",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Booking, SiteKinds.Campus },
                Only = "فروشگاه، آموزش، نوبت‌دهی و باشگاه",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "visits",
                Icon = "bi-bar-chart-line",
                Name = "آمار بازدید روزانه و ماهانه",
                Summary = "تعداد بازدید و بازدیدکننده سایت را امروز، دیروز، این ماه و ماه قبل با نمودار ۳۰ روز و ۱۲ ماه اخیر در داشبورد پنل می‌بینید و آی‌پی بازدیدکنندگان را با ساعت بازدید، صفحه‌ها و مرورگرشان جستجو می‌کنید؛ ربات‌ها و بازدید خود شما حساب نمی‌شوند",
                Highlight = true
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
                Id = "support",
                Icon = "bi-headset",
                Name = "پشتیبانی تیکتی، همراه با بله",
                Summary = "از داخل پنل برای پشتیبانی نیکو تیکت بفرستید، اسکرین‌شات و عکس پیوست کنید و گفتگو را دنبال کنید؛ پاسخ کارشناسان با تصویرهایش در اپلیکیشن بله هم می‌رسد و با «پاسخ» روی همان پیام، متن یا عکس می‌فرستید",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "campus",
                Icon = "bi-trophy",
                Name = "کلاس، مدرس و رزرو سالن",
                Summary = "در آموزشگاه: دوره عمومی با ظرفیت و شهریه، و دوره خصوصی با مدرس به‌صورت حضوری یا آنلاین. در باشگاه ورزشی: کلاس ورزشی، مربی خصوصی و رزرو ساعتی سالن، زمین و میز",
                Kinds = new[] { SiteKinds.Campus },
                Only = "آموزشگاه و باشگاه ورزشی",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "customers",
                Icon = "bi-person-circle",
                Name = "حساب کاربری مشتری",
                Summary = "مشتری سفارش‌ها، فایل‌ها و دوره‌های خریده‌شده را در پنل خودش می‌بیند و پیگیری می‌کند",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital },
                Only = "فروشگاه و آموزش",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "home-sections",
                Icon = "bi-stars",
                Name = "صفحه اول فروش‌محور",
                Summary = "پیشنهاد شگفت‌انگیز با شمارش معکوس، تازه‌ترین کالاها، نظر خریداران، آمار و افتخارات، پرسش‌های پرتکرار با داده ساختاریافته گوگل و نوار دعوت به عضویت؛ "
                    + "در سایت شرکتی: چرا ما، روند همکاری، رضایت مشتریان و درخواست مشاوره",
                Kinds = new[] { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Content },
                Only = "فروشگاه، آموزش و سایت شرکتی",
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
                Id = "trust",
                Icon = "bi-patch-check",
                Name = "اینماد و شبکه‌های اجتماعی",
                Summary = "راهنمای قدم‌به‌قدم گرفتن اینماد با چک‌لیست آماده بودن سایت؛ گذاشتن فایل تأیید در ریشه سایت، متاتگ اینماد و عنوان موقت صفحه اول از پنل، بدون نیاز به پشتیبانی؛ نماد اعتماد امن در فوتر و لینک اینستاگرام، تلگرام، واتس‌اپ، آپارات و…",
                Highlight = true
            },
            new PlatformModule
            {
                Id = "cafe-menu",
                Icon = "bi-upc-scan",
                Name = "منوی QR کافه و رستوران",
                Summary = "مشتری با اسکن QR روی میز، منو را روی موبایل می‌بیند، قیمت و ناموجودی را می‌خواند و گارسون را صدا می‌زند. کارت چاپ میزها از پنل ساخته می‌شود.",
                Kinds = new[] { SiteKinds.Menu },
                Only = "منوی کافه و رستوران",
                Highlight = true
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
                Id = "messages",
                Icon = "bi-envelope",
                Name = "فرم تماس و صندوق پیام",
                Summary = "پیام‌های فرم تماس در پنل جمع می‌شوند و پیام‌های خوانده‌نشده جدا نمایش داده می‌شوند"
            },
            new PlatformModule
            {
                Id = "storage",
                Icon = "bi-hdd",
                Name = "فضای ذخیره‌سازی قابل افزایش",
                Summary = "هر سایت فضای رایگان برای تصاویر و فایل‌ها دارد؛ میزان مصرف را با نمودار در پنل می‌بینید، پیش از پر شدن هشدار می‌گیرید، فایل‌های بی‌استفاده با یک دکمه پاک می‌شوند و هر وقت لازم شد، فضای بیشتر را آنلاین و دائمی می‌خرید"
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
