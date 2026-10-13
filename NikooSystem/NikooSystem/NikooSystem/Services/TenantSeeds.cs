using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;

namespace NikooSystem.Services
{
    /// <summary>Everything a new store starts with.</summary>
    internal sealed class TenantSeed
    {
        public StoreSettings Settings;
        public List<StoreCategory> Categories = new List<StoreCategory>();
        public List<StoreAttributeDef> Attributes = new List<StoreAttributeDef>();
        public List<StoreProduct> Products = new List<StoreProduct>();
        public List<StoreMenuItem> Menus = new List<StoreMenuItem>();
        public List<StorePage> Pages = new List<StorePage>();
        public List<StoreGalleryItem> Gallery = new List<StoreGalleryItem>();
        public List<StorePost> Posts = new List<StorePost>();
        public List<StoreService> Services = new List<StoreService>();
        public List<StoreStaff> Staff = new List<StoreStaff>();
    }

    /// <summary>Sample data of a new store, chosen by its store type so the site looks complete from the first visit.</summary>
    internal static class TenantSeeds
    {
        public static TenantSeed For(string code, string storeType, Action<StoreSettings> customize = null)
        {
            var seed = new TenantSeed();
            switch (storeType)
            {
                case "general": General(seed, code); break;
                case "fashion": Fashion(seed, code); break;
                case "grocery": Grocery(seed, code); break;
                case "ebook": Ebook(seed, code); break;
                case "academy": Academy(seed, code); break;
                case "news": News(seed, code); break;
                case "corporate": Corporate(seed, code); break;
                case "clinic": Clinic(seed, code); break;
                case "beauty": Beauty(seed, code); break;
                case "consulting": Consulting(seed, code); break;
                default: Gadgets(seed, code); break;
            }
            customize?.Invoke(seed.Settings);

            var kind = StoreTypes.Get(storeType).Kind;
            seed.Menus = Menus(code, kind, storeType, seed.Categories);
            seed.Pages = Pages(seed.Settings, kind, storeType);
            seed.Gallery = Gallery(kind);
            return seed;
        }

        // ---- Physical shops

        private static void General(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "فروشگاه من", "فروشگاه آنلاین", "#222222", "#e5e5e5",
                "به فروشگاه ما خوش آمدید", "محصولات منتخب با قیمت مناسب و ارسال سریع", "minimal");
            seed.Categories = new List<StoreCategory>
            {
                new StoreCategory { Id = 1, Title = "محصولات", Slug = "products", SortOrder = 1 },
                new StoreCategory { Id = 2, Title = "پیشنهاد ویژه", Slug = "special", SortOrder = 2 }
            };
            seed.Attributes = new List<StoreAttributeDef>
            {
                new StoreAttributeDef { Id = 1, Name = "رنگ", InputType = "text", IsFilterable = true, SortOrder = 1 },
                new StoreAttributeDef { Id = 2, Name = "برند", InputType = "text", IsFilterable = true, SortOrder = 2 }
            };
            seed.Products = new List<StoreProduct>
            {
                P(1, 1, "محصول نمونه اول", "sample-1", "این یک محصول نمونه است؛ آن را ویرایش یا حذف کنید", 250000, true, Img(4), A(1, "رنگ", "مشکی")),
                P(2, 1, "محصول نمونه دوم", "sample-2", "این یک محصول نمونه است؛ آن را ویرایش یا حذف کنید", 480000, true, Img(5), A(1, "رنگ", "سفید")),
                P(3, 2, "محصول نمونه پیشنهادی", "sample-3", "این یک محصول نمونه است؛ آن را ویرایش یا حذف کنید", 150000, false, Img(6))
            };
        }

        private static void Fashion(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "Boutique Mode", "فروشگاه آنلاین پوشاک", "#46d2ad", "#002c3e",
                "پوشاک تازه برای فصل جدید", "کالکشن منتخب زنانه و مردانه با ارسال سریع", "fashion");
            seed.Settings.HeroImageUrl = Img(1);
            seed.Categories = new List<StoreCategory>
            {
                new StoreCategory { Id = 1, Title = "زنانه", Slug = "women", SortOrder = 1 },
                new StoreCategory { Id = 2, ParentId = 1, Title = "مانتو", Slug = "manteau", SortOrder = 1 },
                new StoreCategory { Id = 3, ParentId = 1, Title = "کفش", Slug = "shoes-w", SortOrder = 2 },
                new StoreCategory { Id = 4, Title = "مردانه", Slug = "men", SortOrder = 2 },
                new StoreCategory { Id = 5, ParentId = 4, Title = "پیراهن", Slug = "shirts", SortOrder = 1 }
            };
            seed.Attributes = new List<StoreAttributeDef>
            {
                new StoreAttributeDef { Id = 1, Name = "سایز", InputType = "select", Options = "S,M,L,XL", IsFilterable = true, SortOrder = 1, CategoryIds = new List<int> { 1, 2, 3, 4, 5 } },
                new StoreAttributeDef { Id = 2, Name = "رنگ", InputType = "text", IsFilterable = true, SortOrder = 2, CategoryIds = new List<int> { 1, 2, 3, 4, 5 } },
                new StoreAttributeDef { Id = 3, Name = "جنس", InputType = "text", SortOrder = 3, CategoryIds = new List<int> { 2, 5 } }
            };
            seed.Products = new List<StoreProduct>
            {
                P(1, 2, "مانتو کتان بهاره", "manteau-spring", "مانتو سبک مناسب فصل بهار", 890000, true, Img(1), A(1, "سایز", "M"), A(2, "رنگ", "کرم"), A(3, "جنس", "کتان")),
                P(2, 3, "کفش اسپرت زنانه", "shoes-sport", "کفش راحت برای استفاده روزانه", 1250000, true, Img(2), A(1, "سایز", "38"), A(2, "رنگ", "سفید")),
                P(3, 5, "پیراهن مردانه رسمی", "shirt-formal", "پیراهن اداری با دوخت تمیز", 780000, false, Img(3), A(1, "سایز", "L"), A(2, "رنگ", "آبی"))
            };
        }

        private static void Grocery(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "نیکو مارکت", "فروشگاه ارگانیک تازه", "#6aa84f", "#e8f5d8",
                "مواد تازه، زندگی سالم‌تر", "محصولات ارگانیک با تحویل سریع تا درب منزل", "organic");
            seed.Settings.ShippingCost = 35000;
            seed.Settings.FreeShippingThreshold = 500000;
            seed.Categories = new List<StoreCategory>
            {
                new StoreCategory { Id = 1, Title = "میوه و سبزی", Slug = "produce", SortOrder = 1, Description = "تازه و فصلی" },
                new StoreCategory { Id = 2, Title = "لبنیات", Slug = "dairy", SortOrder = 2, Description = "شیر و ماست" },
                new StoreCategory { Id = 3, Title = "نان و غلات", Slug = "bakery", SortOrder = 3, Description = "تازه پخته" },
                new StoreCategory { Id = 4, Title = "نوشیدنی", Slug = "drinks", SortOrder = 4, Description = "طبیعی و سالم" },
                new StoreCategory { Id = 5, Title = "خشکبار", Slug = "nuts", SortOrder = 5, Description = "مغز و حبوبات" }
            };
            seed.Attributes = new List<StoreAttributeDef>
            {
                new StoreAttributeDef { Id = 1, Name = "وزن", InputType = "text", SortOrder = 1, CategoryIds = new List<int> { 1, 2, 3 } },
                new StoreAttributeDef { Id = 2, Name = "برند", InputType = "text", IsFilterable = true, SortOrder = 2, CategoryIds = new List<int> { 1, 2 } }
            };
            seed.Products = new List<StoreProduct>
            {
                P(1, 1, "سبد میوه فصل", "seasonal-fruit", "ترکیب میوه‌های تازه فصل", 185000, true, Img(2), A(1, "وزن", "۲ کیلو")),
                P(2, 2, "شیر کم‌چرب ۱ لیتری", "milk-1l", "شیر تازه پاستوریزه", 45000, true, Img(4), A(1, "وزن", "۱ لیتر"), A(2, "برند", "نیکو")),
                P(3, 3, "نان سنگک تازه", "sangak", "پخت روزانه", 28000, true, Img(3), A(1, "وزن", "۵۰۰ گرم")),
                P(4, 4, "آبمیوه طبیعی پرتقال", "orange-juice", "بدون شکر افزوده", 78000, true, Img(5), A(1, "وزن", "۱ لیتر"), A(2, "برند", "طبیعی")),
                P(5, 5, "مغز بادام درختی", "almond", "بسته ۵۰۰ گرمی", 320000, false, Img(6), A(1, "وزن", "۵۰۰ گرم")),
                P(6, 1, "گوجه فرنگی ارگانیک", "tomato-org", "محصول گلخانه‌ای تازه", 62000, true, Img(7), A(1, "وزن", "۱ کیلو"))
            };
        }

        private static void Gadgets(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "نیکو دیجیتال", "فروشگاه تکنولوژی و گجت", "#4F46E5", "#EEF2FF",
                "گجت‌های هوشمند، تجربه بهتر", "هدفون، گجت و لوازم دیجیتال با ارسال سریع", "marketplace");
            seed.Settings.ShippingCost = 60000;
            seed.Settings.FreeShippingThreshold = 3000000;
            seed.Categories = new List<StoreCategory>
            {
                new StoreCategory { Id = 1, Title = "صوتی و هدفون", Slug = "audio", SortOrder = 1, Description = "اسپیکر و هدفون" },
                new StoreCategory { Id = 2, Title = "پوشیدنی", Slug = "wearables", SortOrder = 2, Description = "ساعت هوشمند" },
                new StoreCategory { Id = 3, Title = "موبایل و لوازم", Slug = "mobile", SortOrder = 3, Description = "جانبی موبایل" },
                new StoreCategory { Id = 4, Title = "دوربین و تصویر", Slug = "camera", SortOrder = 4, Description = "عکاسی" }
            };
            seed.Attributes = new List<StoreAttributeDef>
            {
                new StoreAttributeDef { Id = 1, Name = "گارانتی", InputType = "select", Options = "۶ ماهه,۱۸ ماهه,بدون گارانتی", IsFilterable = true, SortOrder = 1 },
                new StoreAttributeDef { Id = 2, Name = "رنگ", InputType = "text", IsFilterable = true, SortOrder = 2 }
            };
            seed.Products = new List<StoreProduct>
            {
                P(1, 1, "هدفون بی‌سیم Pro", "wireless-headphones", "صدای فضایی و نویزکنسلینگ", 2890000, true, Img(1), A(1, "گارانتی", "۱۸ ماهه"), A(2, "رنگ", "مشکی")),
                P(2, 2, "ساعت هوشمند Pulse", "smartwatch-pulse", "مانیتورینگ سلامت ۲۴ ساعته", 4590000, true, Img(2), A(1, "گارانتی", "۱۸ ماهه")),
                P(3, 3, "قاب مگنتیک آیفون", "magsafe-case", "محافظ سبک با شارژ مگسیف", 690000, true, Img(3), A(1, "گارانتی", "بدون گارانتی")),
                P(4, 4, "گیمبال موبایل Mini", "gimbal-mini", "ثبات تصویر حرفه‌ای", 2190000, false, Img(5), A(1, "گارانتی", "۶ ماهه"))
            };
        }

        // ---- E-books and courses

        private static void Ebook(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "کتابخانه نیکو", "کتاب الکترونیک و کتاب صوتی", "#7c2d12", "#fdf2e9",
                "کتاب‌های خوب، همیشه همراه شما", "خرید و دانلود فوری کتاب الکترونیک؛ بعد از پرداخت در «کتابخانه من» در دسترس است", "boutique");
            seed.Categories = new List<StoreCategory>
            {
                new StoreCategory { Id = 1, Title = "رمان و داستان", Slug = "novel", SortOrder = 1 },
                new StoreCategory { Id = 2, Title = "روانشناسی", Slug = "psychology", SortOrder = 2 },
                new StoreCategory { Id = 3, Title = "کسب‌وکار", Slug = "business", SortOrder = 3 }
            };
            seed.Attributes = new List<StoreAttributeDef>
            {
                new StoreAttributeDef { Id = 1, Name = "فرمت", InputType = "select", Options = "PDF,EPUB,MP3", IsFilterable = true, SortOrder = 1 },
                new StoreAttributeDef { Id = 2, Name = "تعداد صفحات", InputType = "number", SortOrder = 2 },
                new StoreAttributeDef { Id = 3, Name = "ناشر", InputType = "text", IsFilterable = true, SortOrder = 3 }
            };
            seed.Products = new List<StoreProduct>
            {
                Digital(P(1, 1, "کتاب نمونه: سفر به شب", "sample-novel", "رمانی کوتاه درباره امید؛ نمونه برای نمایش فروشگاه", 85000, true, Img(1), A(1, "فرمت", "EPUB"), A(2, "تعداد صفحات", "۲۴۰"), A(3, "ناشر", "نیکو")),
                    StoreProductType.Ebook, "نویسنده نمونه"),
                Digital(P(2, 2, "کتاب نمونه: ذهن آرام", "sample-calm-mind", "راهنمای عملی مدیریت استرس", 120000, true, Img(2), A(1, "فرمت", "PDF"), A(2, "تعداد صفحات", "۱۸۰")),
                    StoreProductType.Ebook, "دکتر نمونه"),
                Digital(P(3, 3, "کتاب صوتی نمونه: شروع کسب‌وکار", "sample-startup-audio", "کتاب صوتی ۶ ساعته", 150000, true, Img(3), A(1, "فرمت", "MP3")),
                    StoreProductType.File, "گوینده نمونه")
            };
        }

        private static void Academy(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "آکادمی نیکو", "آموزش آنلاین پروژه‌محور", "#0e7490", "#e0f2fe",
                "مهارت تازه، فرصت تازه", "دوره‌های ویدیویی با پشتیبانی و دسترسی دائمی؛ جلسه‌های اول رایگان است", "marketplace");
            seed.Categories = new List<StoreCategory>
            {
                new StoreCategory { Id = 1, Title = "برنامه‌نویسی", Slug = "programming", SortOrder = 1, Description = "وب و موبایل" },
                new StoreCategory { Id = 2, Title = "طراحی", Slug = "design", SortOrder = 2, Description = "UI و گرافیک" },
                new StoreCategory { Id = 3, Title = "بازاریابی", Slug = "marketing", SortOrder = 3, Description = "دیجیتال مارکتینگ" }
            };
            seed.Attributes = new List<StoreAttributeDef>
            {
                new StoreAttributeDef { Id = 1, Name = "سطح", InputType = "select", Options = "مقدماتی,متوسط,پیشرفته", IsFilterable = true, SortOrder = 1 },
                new StoreAttributeDef { Id = 2, Name = "مدت دوره", InputType = "text", SortOrder = 2 },
                new StoreAttributeDef { Id = 3, Name = "مدرک", InputType = "select", Options = "دارد,ندارد", SortOrder = 3 }
            };
            seed.Products = new List<StoreProduct>
            {
                Course(Digital(P(1, 1, "دوره نمونه: برنامه‌نویسی وب از صفر", "sample-web-course", "HTML، CSS و جاوااسکریپت با پروژه واقعی", 1900000, true, Img(4), A(1, "سطح", "مقدماتی"), A(2, "مدت دوره", "۱۲ ساعت"), A(3, "مدرک", "دارد")),
                    StoreProductType.Course, "مدرس نمونه"),
                    "شروع کار", "معرفی دوره و نصب ابزارها", "پروژه اول", "ساخت صفحه فرود", "جمع‌بندی", "انتشار سایت"),
                Course(Digital(P(2, 2, "دوره نمونه: طراحی رابط کاربری با Figma", "sample-figma-course", "از وایرفریم تا پروتوتایپ", 1400000, true, Img(5), A(1, "سطح", "متوسط"), A(2, "مدت دوره", "۸ ساعت")),
                    StoreProductType.Course, "طراح نمونه"),
                    "مبانی", "آشنایی با محیط فیگما", "پروژه", "طراحی اپلیکیشن", "تحویل", "خروجی و هندآف"),
                Course(Digital(P(3, 3, "دوره نمونه: سئو و تولید محتوا", "sample-seo-course", "رشد ارگانیک سایت با محتوای درست", 950000, false, Img(6), A(1, "سطح", "مقدماتی")),
                    StoreProductType.Course, "مدرس نمونه"),
                    "سئو چیست", "موتورهای جستجو چطور کار می‌کنند", "محتوا", "نوشتن برای کاربر و گوگل", "ابزارها", "سرچ کنسول")
            };
        }

        // ---- Content sites

        private static void News(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "خبرگزاری نیکو", "اخبار لحظه‌به‌لحظه ایران و جهان", "#b91c1c", "#111827",
                "خبرگزاری نیکو", "اخبار معتبر، تحلیل دقیق", "magazine");
            seed.Settings.About = "خبرگزاری نیکو یک رسانه خبری مستقل روی پلتفرم نیکو سیستم آفرین است. این متن را از تنظیمات تغییر دهید.";
            seed.Settings.EnablePayment = false;
            seed.Categories = new List<StoreCategory>
            {
                new StoreCategory { Id = 1, Title = "سیاسی", Slug = "politics", SortOrder = 1 },
                new StoreCategory { Id = 2, Title = "اقتصادی", Slug = "economy", SortOrder = 2 },
                new StoreCategory { Id = 3, Title = "فناوری", Slug = "tech", SortOrder = 3 },
                new StoreCategory { Id = 4, Title = "ورزشی", Slug = "sports", SortOrder = 4 },
                new StoreCategory { Id = 5, Title = "فرهنگی", Slug = "culture", SortOrder = 5 }
            };
            seed.Posts = new List<StorePost>
            {
                Post(1, 2, "خبر نمونه: رشد صادرات غیرنفتی در فصل گذشته", "sample-exports", "گزارش اقتصادی", "بر اساس آمار منتشرشده، صادرات غیرنفتی نسبت به فصل مشابه سال قبل افزایش یافته است.", Img(1), 0, featured: true, breaking: true),
                Post(2, 3, "خبر نمونه: رونمایی از دستیار هوشمند فارسی", "sample-ai-assistant", "فناوری", "یک شرکت دانش‌بنیان داخلی از نسل تازه دستیار هوشمند فارسی رونمایی کرد.", Img(2), 1, featured: true),
                Post(3, 4, "خبر نمونه: صعود تیم ملی به مرحله بعد", "sample-national-team", "ورزش", "تیم ملی با یک پیروزی ارزشمند به مرحله حذفی مسابقات راه یافت.", Img(3), 2, featured: true, breaking: true),
                Post(4, 1, "خبر نمونه: نشست مشترک وزیران منطقه برگزار شد", "sample-ministers", "سیاست خارجی", "وزیران کشورهای منطقه درباره همکاری‌های اقتصادی و امنیتی گفت‌وگو کردند.", Img(4), 3),
                Post(5, 5, "خبر نمونه: آغاز جشنواره کتاب کودک", "sample-book-festival", "فرهنگ", "جشنواره کتاب کودک با حضور نویسندگان و ناشران از امروز آغاز شد.", Img(5), 4),
                Post(6, 2, "تحلیل نمونه: آینده بازار مسکن", "sample-housing", "تحلیل", "کارشناسان درباره روند قیمت مسکن در ماه‌های آینده نظرات متفاوتی دارند.", Img(6), 5),
                Post(7, 3, "خبر نمونه: سرعت اینترنت همراه افزایش یافت", "sample-internet", "ارتباطات", "اپراتورها از ارتقای شبکه و افزایش سرعت اینترنت همراه خبر دادند.", Img(7), 6),
                Post(8, 4, "خبر نمونه: رکوردشکنی در مسابقات دوومیدانی", "sample-athletics", "ورزش", "ورزشکار جوان کشورمان رکورد ملی دوی ۴۰۰ متر را شکست.", Img(2), 7)
            };
        }

        private static void Corporate(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "شرکت نیکو", "راهکارهای هوشمند برای کسب‌وکار شما", "#0f4c81", "#e0ecf7",
                "همراه رشد کسب‌وکار شما", "مشاوره، طراحی و پشتیبانی نرم‌افزار با بیش از ۱۰ سال تجربه", "corporate");
            seed.Settings.About = "شرکت نیکو با تیمی متخصص، راهکارهای نرم‌افزاری و مشاوره‌ای برای سازمان‌ها ارائه می‌کند. این متن را از تنظیمات تغییر دهید.";
            seed.Settings.EnablePayment = false;
            seed.Settings.HeroImageUrl = Img(3);
            seed.Categories = new List<StoreCategory>
            {
                new StoreCategory { Id = 1, Title = "مشاوره", Slug = "consulting", SortOrder = 1, Description = "تحلیل نیاز و برنامه‌ریزی تحول دیجیتال" },
                new StoreCategory { Id = 2, Title = "طراحی و توسعه", Slug = "development", SortOrder = 2, Description = "وب‌سایت، اپلیکیشن و سامانه‌های سازمانی" },
                new StoreCategory { Id = 3, Title = "پشتیبانی", Slug = "support", SortOrder = 3, Description = "نگهداری، امنیت و پشتیبانی ۲۴ ساعته" }
            };
            seed.Posts = new List<StorePost>
            {
                Post(1, 2, "خبر نمونه: افتتاح دفتر جدید شرکت", "sample-new-office", "اخبار شرکت", "دفتر جدید شرکت برای خدمت‌رسانی بهتر به مشتریان افتتاح شد.", Img(1), 0, featured: true),
                Post(2, 1, "مقاله نمونه: پنج گام تحول دیجیتال", "sample-digital-transformation", "مقاله", "تحول دیجیتال از شناخت دقیق فرایندها شروع می‌شود؛ در این مقاله پنج گام اصلی را مرور می‌کنیم.", Img(2), 2),
                Post(3, 3, "خبر نمونه: دریافت گواهی امنیت اطلاعات", "sample-iso", "اخبار شرکت", "شرکت موفق به دریافت گواهی‌نامه مدیریت امنیت اطلاعات شد.", Img(4), 4),
                Post(4, 2, "پروژه نمونه: سامانه فروش یک فروشگاه زنجیره‌ای", "sample-project", "نمونه‌کار", "طراحی و پیاده‌سازی سامانه فروش یکپارچه برای ۴۰ شعبه.", Img(5), 6)
            };
        }

        // ---- Booking sites

        private static void Clinic(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "کلینیک نیکو", "نوبت‌دهی آنلاین پزشکان", "#0f766e", "#ccfbf1",
                "نوبت پزشک، بدون انتظار پشت خط", "خدمت و پزشک را انتخاب کنید، روز و ساعت خالی را ببینید و در کمتر از یک دقیقه نوبت بگیرید", "clinic");
            seed.Settings.About = "کلینیک نیکو با پزشکان متخصص و محیطی آرام، خدمات عمومی، پوست و دندانپزشکی ارائه می‌کند. این متن را از «اطلاعات سایت» تغییر دهید.";
            seed.Settings.HeroImageUrl = BookingHero("clinic");
            seed.Settings.BookingNotice = "لطفاً ۱۰ دقیقه پیش از نوبت حضور داشته باشید و کارت ملی همراه بیاورید.";
            seed.Staff = new List<StoreStaff>
            {
                Staff(1, "دکتر سارا احمدی", "متخصص پوست، مو و زیبایی", "team-1", Week(new[] { 0, 1, 2, 3 }, 9 * 60, 13 * 60, 16 * 60, 20 * 60)),
                Staff(2, "دکتر علی رضایی", "پزشک عمومی", "team-2", Week(new[] { 0, 1, 2, 3, 4, 5 }, 8 * 60, 14 * 60)),
                Staff(3, "دکتر مریم کریمی", "دندانپزشک", "team-3", Week(new[] { 1, 3, 5 }, 10 * 60, 18 * 60))
            };
            seed.Services = new List<StoreService>
            {
                Service(1, "ویزیت پزشک عمومی", "معاینه، تشخیص و نسخه؛ مناسب سرماخوردگی، چکاپ و تمدید دارو", 20, 350000, 2),
                Service(2, "ویزیت متخصص پوست", "بررسی مشکلات پوست، مو و ناخن و برنامه درمان", 30, 650000, 1),
                Service(3, "پاکسازی و فیشیال پوست", "پاکسازی عمیق، لایه‌برداری ملایم و ماسک آبرسان", 60, 1200000, 1),
                Service(4, "معاینه و جرم‌گیری دندان", "معاینه کامل، جرم‌گیری و بروساژ", 45, 900000, 3),
                Service(5, "مشاوره درمان ریشه و ترمیم", "بررسی عکس، برآورد هزینه و برنامه درمان", 30, 300000, 3)
            };
        }

        private static void Beauty(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "سالن زیبایی رز", "زیبایی با نوبت دقیق", "#be185d", "#fce7f3",
                "وقتتان را در صف تلف نکنید", "آرایشگر و خدمت دلخواهتان را انتخاب کنید و آنلاین نوبت بگیرید", "salon");
            seed.Settings.About = "سالن زیبایی رز با آرایشگران باتجربه، خدمات مو، ناخن و پوست را با محصولات معتبر ارائه می‌کند. این متن را از «اطلاعات سایت» تغییر دهید.";
            seed.Settings.HeroImageUrl = BookingHero("salon");
            seed.Settings.BookingSlotMinutes = 15;
            seed.Staff = new List<StoreStaff>
            {
                Staff(1, "نگار محمدی", "مدیر سالن، رنگ و لایت", "team-3", Week(new[] { 0, 1, 2, 3, 4, 5 }, 10 * 60, 19 * 60)),
                Staff(2, "الهام نوری", "کوتاهی و براشینگ", "team-1", Week(new[] { 0, 2, 4, 5 }, 11 * 60, 20 * 60)),
                Staff(3, "پریسا جعفری", "کاشت و طراحی ناخن", "team-4", Week(new[] { 1, 3, 5 }, 12 * 60, 20 * 60))
            };
            seed.Services = new List<StoreService>
            {
                Service(1, "کوتاهی و براشینگ", "مشاوره مدل، کوتاهی و حالت‌دهی", 45, 450000, 1, 2),
                Service(2, "رنگ و لایت مو", "رنگ ریشه یا کامل با برند معتبر؛ زمان دقیق بسته به طول مو", 150, 2800000, 1),
                Service(3, "کاشت ناخن", "کاشت پودری یا ژل با طراحی ساده", 90, 950000, 3),
                Service(4, "مانیکور و لاک ژل", "مانیکور کامل و لاک ژل ماندگار", 60, 550000, 3),
                Service(5, "پاکسازی پوست", "پاکسازی، ماساژ صورت و ماسک", 60, 800000, 1)
            };
        }

        private static void Consulting(TenantSeed seed, string code)
        {
            seed.Settings = BaseSettings(code, "مرکز مشاوره نیکو", "جلسه مشاوره حضوری و آنلاین", "#0f766e", "#ccfbf1",
                "مشاوره تخصصی در زمانی که برایتان مناسب است", "مشاور و موضوع را انتخاب کنید و جلسه حضوری یا تصویری رزرو کنید", "clinic");
            seed.Settings.About = "مرکز مشاوره نیکو با مشاوران دارای پروانه، جلسات حضوری و آنلاین در حوزه خانواده، تحصیل و کسب‌وکار برگزار می‌کند. این متن را از «اطلاعات سایت» تغییر دهید.";
            seed.Settings.HeroImageUrl = BookingHero("clinic");
            seed.Settings.BookingSlotMinutes = 60;
            seed.Settings.BookingMinNoticeMinutes = 180;
            seed.Staff = new List<StoreStaff>
            {
                Staff(1, "دکتر نرگس صادقی", "روانشناس بالینی، مشاوره خانواده", "team-1", Week(new[] { 0, 1, 2, 3 }, 10 * 60, 18 * 60)),
                Staff(2, "مهندس حامد یزدانی", "مشاور کسب‌وکار و استارتاپ", "team-2", Week(new[] { 0, 2, 4 }, 9 * 60, 13 * 60, 17 * 60, 21 * 60)),
                Staff(3, "دکتر زهرا امینی", "مشاور تحصیلی و کنکور", "team-4", Week(new[] { 1, 3, 5 }, 14 * 60, 20 * 60))
            };
            seed.Services = new List<StoreService>
            {
                Service(1, "جلسه مشاوره فردی", "۵۰ دقیقه گفت‌وگوی محرمانه با روانشناس", 50, 1200000, 1),
                Service(2, "مشاوره زوج و خانواده", "جلسه مشترک برای حل تعارض و بهبود رابطه", 75, 1800000, 1),
                Service(3, "مشاوره کسب‌وکار", "بررسی مدل کسب‌وکار، بازاریابی و جذب سرمایه", 60, 2500000, 2),
                Service(4, "برنامه‌ریزی تحصیلی", "تعیین هدف، برنامه مطالعه و انتخاب رشته", 45, 900000, 3)
            };
        }

        /// <summary>Generated hero images of the booking templates; <c>clinic</c> or <c>salon</c>.</summary>
        private static string BookingHero(string template) => "/Assets/img/booking/hero-" + template + ".jpg";

        private static StoreStaff Staff(int id, string name, string title, string photo, List<StoreWorkHours> hours)
        {
            return new StoreStaff
            {
                Id = id,
                Name = name,
                Title = title,
                Bio = name + " — " + title + ". این معرفی نمونه است؛ آن را از پنل مدیریت ویرایش کنید.",
                PhotoUrl = "/Assets/img/team/" + photo + ".jpg",
                SortOrder = id,
                IsSample = true,
                Hours = hours
            };
        }

        /// <summary>The same hours on each of <paramref name="days"/>: pairs of (start, end) minutes, e.g. a morning and an evening shift.</summary>
        private static List<StoreWorkHours> Week(int[] days, params int[] startEndPairs)
        {
            var hours = new List<StoreWorkHours>();
            foreach (var day in days)
                for (var i = 0; i + 1 < startEndPairs.Length; i += 2)
                    hours.Add(new StoreWorkHours { Day = day, StartMinute = startEndPairs[i], EndMinute = startEndPairs[i + 1] });
            return hours;
        }

        private static StoreService Service(int id, string title, string description, int minutes, long price, params int[] staffIds)
        {
            return new StoreService
            {
                Id = id,
                Title = title,
                Description = description,
                DurationMinutes = minutes,
                Price = price,
                SortOrder = id,
                IsSample = true,
                StaffIds = staffIds.ToList()
            };
        }

        // ---- Menus, pages and gallery for every kind

        private static List<StoreMenuItem> Menus(string code, string kind, string storeType, List<StoreCategory> categories)
        {
            var shop = "/Shop/" + code;
            var items = new List<StoreMenuItem> { new StoreMenuItem { Id = 1, Title = "خانه", Url = shop, SortOrder = 1 } };
            Action<string, string> add = (title, url) =>
                items.Add(new StoreMenuItem { Id = items.Count + 1, Title = title, Url = url, SortOrder = items.Count + 1 });

            if (kind == SiteKinds.Content && storeType == "news")
            {
                foreach (var c in categories.Take(5)) add(c.Title, shop + "/Blog?categoryId=" + c.Id);
                add("آرشیو", shop + "/Blog");
                add("تماس با ما", shop + "/Contact");
            }
            else if (kind == SiteKinds.Content)
            {
                add("درباره ما", shop + "/Page/about");
                add("خدمات", shop + "/Page/services");
                add("اخبار و مقالات", shop + "/Blog");
                add("نمونه‌کارها", shop + "/Gallery");
                add("تماس با ما", shop + "/Contact");
            }
            else if (kind == SiteKinds.Booking)
            {
                add("خدمات و تعرفه‌ها", shop + "/Services");
                add("رزرو نوبت", shop + "/Book");
                add("پیگیری نوبت", shop + "/Track");
                add("درباره ما", shop + "/Page/about");
                add("تماس با ما", shop + "/Contact");
            }
            else
            {
                add(kind == SiteKinds.Digital ? (storeType == "academy" ? "دوره‌ها" : "کتاب‌ها") : "محصولات", shop + "/Catalog");
                if (kind == SiteKinds.Digital) add(storeType == "academy" ? "دوره‌های من" : "کتابخانه من", "/ShopAccount/" + code + "/Library");
                else add("گالری", shop + "/Gallery");
                add("درباره ما", shop + "/Page/about");
                add("تماس با ما", shop + "/Contact");
            }

            items.Add(new StoreMenuItem { Id = items.Count + 1, Title = "درباره ما", Url = shop + "/Page/about", SortOrder = 1, Location = "footer" });
            items.Add(new StoreMenuItem
            {
                Id = items.Count + 1,
                Title = kind == SiteKinds.Content ? "حریم خصوصی" : kind == SiteKinds.Booking ? "قوانین نوبت‌دهی" : "شرایط فروش",
                Url = shop + "/Page/terms",
                SortOrder = 2,
                Location = "footer"
            });
            return items;
        }

        private static List<StorePage> Pages(StoreSettings settings, string kind, string storeType)
        {
            var pages = new List<StorePage>
            {
                new StorePage { Id = 1, Title = "درباره ما", Slug = "about", BodyHtml = "<p>" + HttpUtility.HtmlEncode(settings.About) + "</p><p>این سایت روی پلتفرم نیکو سیستم آفرین اجرا می‌شود و قابل سفارشی‌سازی کامل است.</p>" },
                new StorePage { Id = 2, Title = "تماس با ما", Slug = "contact", BodyHtml = ContactPageHtml(settings) }
            };
            if (kind == SiteKinds.Content)
            {
                pages.Add(new StorePage { Id = 3, Title = "حریم خصوصی", Slug = "terms", BodyHtml = "<p>اطلاعاتی که در فرم تماس یا نظرات وارد می‌کنید فقط برای پاسخ‌گویی استفاده می‌شود.</p>" });
                if (storeType == "corporate")
                    pages.Add(new StorePage { Id = 4, Title = "خدمات", Slug = "services", BodyHtml = "<ul><li>مشاوره و تحلیل نیاز</li><li>طراحی و توسعه نرم‌افزار</li><li>پشتیبانی و نگهداری</li></ul>" });
            }
            else if (kind == SiteKinds.Booking)
            {
                pages.Add(new StorePage
                {
                    Id = 3,
                    Title = "قوانین نوبت‌دهی",
                    Slug = "terms",
                    BodyHtml = "<ul><li>کد پیگیری نوبت را نگه دارید؛ با آن و شماره موبایل، نوبت را در صفحه «پیگیری نوبت» می‌بینید.</li>"
                        + "<li>لغو نوبت تا " + settings.BookingCancelHours.ToString().ToFaDigits() + " ساعت پیش از زمان آن از همان صفحه امکان‌پذیر است.</li>"
                        + "<li>اگر بیعانه پرداخت کرده‌اید، برای بازگشت آن پس از لغو با ما تماس بگیرید.</li>"
                        + "<li>در صورت تأخیر بیش از ۱۵ دقیقه، ممکن است نوبت به فرد بعدی داده شود.</li></ul>"
                });
            }
            else
            {
                var terms = kind == SiteKinds.Digital
                    ? "<p>پس از پرداخت موفق، فایل‌ها و جلسه‌ها در «کتابخانه من» در دسترس است. محصولات دیجیتال پس از دانلود مرجوع نمی‌شوند.</p>"
                    : "<p>پس از ثبت سفارش و پرداخت موفق، پیگیری از پنل کاربری امکان‌پذیر است.</p>";
                pages.Add(new StorePage { Id = 3, Title = "شرایط فروش", Slug = "terms", BodyHtml = terms });
            }
            return pages;
        }

        private static List<StoreGalleryItem> Gallery(string kind)
        {
            var titles = kind == SiteKinds.Content ? new[] { "پروژه اول", "پروژه دوم", "تیم ما" }
                : kind == SiteKinds.Booking ? new[] { "فضای پذیرش", "اتاق خدمات", "تیم ما" }
                : new[] { "نمای فروشگاه", "محصولات منتخب", "تیم ما" };
            return titles.Select((t, i) => new StoreGalleryItem
            {
                Id = i + 1,
                Title = t,
                Album = i == 1 ? "محصولات" : "فضا",
                ThumbUrl = Img(i + 1),
                LargeUrl = Img(i + 1),
                SortOrder = i + 1
            }).ToList();
        }

        // ---- Builders

        private static StoreSettings BaseSettings(string code, string name, string tagline, string primary, string secondary, string heroTitle, string heroSub, string layoutStyle)
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
                HeroImageUrl = "/Assets/img/hero-img.jpg",
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

        private static string Img(int n) => "/Assets/img/portfolio/portfolio-" + n + ".jpg";

        private static StoreProduct P(int id, int catId, string title, string slug, string desc, long price, bool featured, string img, params StoreProductAttribute[] attrs)
        {
            return new StoreProduct
            {
                Id = id,
                CategoryId = catId,
                Title = title,
                Slug = slug,
                ShortDescription = desc,
                Description = desc + " — قابل ویرایش از پنل مدیریت.",
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
                    new StoreProductImage { ThumbUrl = img, LargeUrl = img, Alt = title, SortOrder = 1 },
                    new StoreProductImage { ThumbUrl = img, LargeUrl = img, Alt = title + " ۲", SortOrder = 2 }
                },
                Attributes = attrs?.ToList() ?? new List<StoreProductAttribute>()
            };
        }

        private static StoreProduct Digital(StoreProduct product, StoreProductType type, string author)
        {
            product.ProductType = type;
            product.Author = author;
            product.Stock = 0;
            product.Sku = null;
            return product;
        }

        /// <summary>Two lessons per (section, first lesson title) pair; the first lesson of the course is a free preview.</summary>
        private static StoreProduct Course(StoreProduct product, params string[] sectionsAndTitles)
        {
            for (var i = 0; i + 1 < sectionsAndTitles.Length; i += 2)
            {
                var section = sectionsAndTitles[i];
                product.Lessons.Add(new StoreLesson { Section = section, Title = sectionsAndTitles[i + 1], Duration = "۱۲:۳۰", IsFree = i == 0, Summary = "خلاصه این جلسه را از پنل مدیریت بنویسید." });
                product.Lessons.Add(new StoreLesson { Section = section, Title = "تمرین: " + sectionsAndTitles[i + 1], Duration = "۰۸:۱۵", Summary = "ویدیوی جلسه را از پنل مدیریت بارگذاری کنید." });
            }
            return product;
        }

        private static StorePost Post(int id, int categoryId, string title, string slug, string kicker, string summary, string cover, int hoursAgo,
            bool featured = false, bool breaking = false)
        {
            var at = DateTime.Now.AddHours(-hoursAgo * 5 - 1);
            return new StorePost
            {
                Id = id,
                CategoryId = categoryId,
                Title = title,
                Slug = slug,
                Kicker = kicker,
                Summary = summary,
                BodyHtml = "<p>" + HttpUtility.HtmlEncode(summary) + "</p><p>این یک مطلب نمونه است که هنگام ساخت سایت اضافه شده؛ آن را از پنل مدیریت ویرایش یا حذف کنید. متن کامل خبر، تصاویر و نقل‌قول‌ها را اینجا می‌نویسید.</p>",
                CoverImageUrl = cover,
                Author = "تحریریه",
                Tags = "نمونه, " + kicker,
                IsFeatured = featured,
                IsBreaking = breaking,
                IsSample = true,
                PublishedAt = at,
                CreatedAt = at,
                ViewCount = (10 - hoursAgo) * 37
            };
        }

        private static StoreProductAttribute A(int id, string name, string value)
        {
            return new StoreProductAttribute { AttributeId = id, Name = name, Value = value };
        }
    }
}
