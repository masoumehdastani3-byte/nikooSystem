using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>
    /// Sample rows of the shared home sections (stats, why us, testimonials, FAQ) and a few buyer reviews for the demo sites,
    /// so every demo shows the full home page. Real stores never get these: their sections stay hidden until the owner writes them.
    /// </summary>
    internal static class HomeSamples
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Rows = new Dictionary<string, Dictionary<string, string>>
        {
            ["FASHION"] = new Dictionary<string, string>
            {
                ["stats"] = "۱۲٬۰۰۰ | مشتری راضی\n۸۵۰ | مدل لباس و اکسسوری\n۷ روز | ضمانت بازگشت کالا\n۴٫۸ از ۵ | امتیاز خریداران"
            },
            ["GROCERY"] = new Dictionary<string, string>
            {
                ["stats"] = "۲٬۰۰۰ | خانواده مشتری\n۴۰۰ | محصول تازه و ارگانیک\nهمان روز | تحویل در تهران\n۱۰۰٪ | ضمانت تازگی"
            },
            ["DIGITAL"] = new Dictionary<string, string>
            {
                ["stats"] = "۹٬۰۰۰ | سفارش موفق\n۱۵۰ | برند معتبر\n۱۸ ماه | گارانتی رسمی\n۲۴ ساعته | ارسال در تهران"
            },
            ["BOOKS"] = new Dictionary<string, string>
            {
                ["stats"] = "۲٬۵۰۰ | عنوان کتاب\n۱۸٬۰۰۰ | کتاب‌خوان\nفوری | دانلود پس از پرداخت\n۴٫۹ از ۵ | رضایت خوانندگان"
            },
            ["ACADEMY"] = new Dictionary<string, string>
            {
                ["stats"] = "۴۰ | دوره تخصصی\n۶٬۵۰۰ | دانشجو\n۹۰۰ ساعت | آموزش ویدیویی\nدائمی | دسترسی به دوره‌ها"
            },
            ["COMPANY"] = new Dictionary<string, string>
            {
                ["stats"] = "۱۰ سال | تجربه\n۲۵۰ | پروژه موفق\n۱۲۰ | مشتری سازمانی\n۲۴/۷ | پشتیبانی فنی",
                ["whyus"] = "تیم متخصص | کارشناسان باتجربه در طراحی، توسعه و مشاوره در همه مراحل کنار شما هستند.\n"
                    + "تحویل به‌موقع | زمان‌بندی شفاف و گزارش پیشرفت هفتگی؛ همیشه می‌دانید کار کجاست.\n"
                    + "پشتیبانی واقعی | پس از تحویل هم پاسخ‌گوی پرسش‌ها و تغییرات شما هستیم.\n"
                    + "قیمت شفاف | پیش‌فاکتور دقیق پیش از شروع کار، بدون هزینه پنهان.\n"
                    + "امنیت اطلاعات | قرارداد محرمانگی و نگهداری امن داده‌های سازمان شما.\n"
                    + "راهکار اختصاصی | طراحی متناسب با نیاز، فرایندها و بودجه کسب‌وکار شما.",
                ["reviews"] = "مهندس رضایی | مدیر فناوری اطلاعات، گروه صنعتی آلفا | نیاز ما را دقیق فهمیدند و سامانه را زودتر از موعد تحویل دادند. پشتیبانی بعد از تحویل هم واقعاً پاسخ‌گوست.\n"
                    + "دکتر سمیرا احمدی | مدیرعامل کلینیک سلامت | از جلسه اول تا راه‌اندازی همه‌چیز شفاف و برنامه‌ریزی‌شده بود و هزینه‌ها دقیقاً مطابق پیش‌فاکتور بود.\n"
                    + "حمید توکلی | مدیر فروش، شرکت پخش بهار | گزارش‌های هفتگی باعث شد همیشه در جریان پیشرفت کار باشیم. همکاری با این تیم را به همه پیشنهاد می‌کنم.",
                ["faq"] = "هزینه پروژه چطور محاسبه می‌شود؟ | پس از جلسه مشاوره و بررسی نیازها، پیش‌فاکتور دقیق با جزئیات هر مرحله برایتان ارسال می‌شود.\n"
                    + "اجرای پروژه چقدر زمان می‌برد؟ | بسته به اندازه کار معمولاً چهار تا دوازده هفته؛ زمان‌بندی دقیق در قرارداد نوشته می‌شود.\n"
                    + "پس از تحویل هم پشتیبانی دارید؟ | بله؛ همه پروژه‌ها شش ماه پشتیبانی دارند و پس از آن قرارداد پشتیبانی سالانه ارائه می‌شود.\n"
                    + "جلسه حضوری هم برگزار می‌کنید؟ | بله؛ جلسه‌ها حضوری در دفتر ما یا به‌صورت آنلاین برگزار می‌شود."
            }
        };

        private static readonly Dictionary<string, string[][]> Reviews = new Dictionary<string, string[][]>
        {
            ["FASHION"] = new[]
            {
                new[] { "نگار احمدی", "5", "کیفیت پارچه و دوخت عالی بود و سایز دقیقاً مطابق جدول سایز درآمد. بسته‌بندی هم خیلی شیک بود." },
                new[] { "مریم کاظمی", "5", "دو روزه به دستم رسید و رنگش دقیقاً مثل عکس سایت بود. حتماً دوباره خرید می‌کنم." },
                new[] { "علی رستمی", "4", "قیمت نسبت به کیفیت خیلی مناسب بود و پشتیبانی برای انتخاب سایز راهنمایی خوبی کرد." }
            },
            ["GROCERY"] = new[]
            {
                new[] { "فاطمه نوری", "5", "میوه‌ها کاملاً تازه بودند و سفارش را همان روز تحویل گرفتم. بسته‌بندی بهداشتی و مرتب بود." },
                new[] { "حسین مرادی", "5", "طعم محصولات ارگانیک واقعاً فرق دارد. خرید هفتگی‌مان را از همین‌جا انجام می‌دهیم." },
                new[] { "زهرا صالحی", "4", "تنوع محصولات خوب است و پیک سر وقت رسید. امیدوارم محصولات بیشتری هم اضافه شود." }
            },
            ["DIGITAL"] = new[]
            {
                new[] { "امیر حسینی", "5", "کالا اصل و پلمپ بود و با گارانتی رسید. ارسال هم از چیزی که فکر می‌کردم سریع‌تر بود." },
                new[] { "سارا جعفری", "5", "قبل از خرید با پشتیبانی مشورت کردم و دقیقاً همان چیزی را که لازم داشتم پیشنهاد دادند." },
                new[] { "محمد کریمی", "4", "قیمتش از بیشتر فروشگاه‌ها بهتر بود و بسته‌بندی کاملاً سالم به دستم رسید." }
            },
            ["BOOKS"] = new[]
            {
                new[] { "الهام رضوی", "5", "بلافاصله بعد از پرداخت کتاب در کتابخانه‌ام بود. کیفیت فایل و صفحه‌آرایی عالی است." },
                new[] { "رضا قاسمی", "5", "خرید خیلی راحت بود و کتاب روی گوشی و لپ‌تاپ هر دو بدون مشکل باز می‌شود." },
                new[] { "نرگس یزدانی", "4", "تنوع کتاب‌ها خوب است و قیمت نسخه الکترونیک خیلی به‌صرفه‌تر از نسخه چاپی است." }
            },
            ["ACADEMY"] = new[]
            {
                new[] { "پویا شریفی", "5", "دوره کاملاً پروژه‌محور بود و بعد از تمام کردنش اولین پروژه کاری‌ام را گرفتم." },
                new[] { "مهسا طاهری", "5", "توضیحات مدرس خیلی روان بود و پشتیبانی به سؤال‌هایم سریع جواب داد." },
                new[] { "کاوه بهرامی", "4", "دسترسی دائمی به ویدیوها عالی است؛ هر وقت لازم شد دوباره مرور می‌کنم." }
            }
        };

        internal static void ApplyDemos()
        {
            foreach (var demo in Rows)
            {
                if (!NikooDb.Exists(NikooTables.Settings, NikooDb.Key(demo.Key))) continue;
                var settings = TenantStore.GetSettings(demo.Key);
                var layout = settings.PageLayout ?? new StorePageLayout();
                var list = layout.HomeSections ?? new List<StoreHomeSection>();
                var builtIns = HomeSections.BuiltInsFor(settings.LayoutStyle);
                foreach (var row in demo.Value)
                {
                    var sec = list.FirstOrDefault(s => s.Type == row.Key);
                    if (sec != null)
                    {
                        if (string.IsNullOrWhiteSpace(sec.Items)) sec.Items = row.Value;
                        continue;
                    }
                    list.Insert(PositionFor(list, builtIns, row.Key), new StoreHomeSection { Id = row.Key, Type = row.Key, IsVisible = true, Items = row.Value });
                }
                layout.HomeSections = list;
                settings.PageLayout = layout;
                TenantStore.SaveSettings(settings);
            }

            foreach (var demo in Reviews)
            {
                if (!NikooDb.Exists(NikooTables.Settings, NikooDb.Key(demo.Key))) continue;
                if (TenantStore.HomeReviews(demo.Key, 3).Count >= 3) continue;
                var products = TenantStore.GetProducts(demo.Key).Where(p => p.IsActive).Take(demo.Value.Length).ToList();
                for (var i = 0; i < products.Count; i++)
                {
                    var r = demo.Value[i];
                    TenantStore.AddReview(demo.Key, new StoreProductReview
                    {
                        ProductId = products[i].Id,
                        AuthorName = r[0],
                        Rating = int.Parse(r[1]),
                        Comment = r[2],
                        IsApproved = true
                    });
                }
            }
        }

        /// <summary>Right after the nearest earlier section of the template that is already in the list (as <see cref="HomeSections.Resolve"/> does).</summary>
        private static int PositionFor(List<StoreHomeSection> list, string[] builtIns, string type)
        {
            for (var j = Array.IndexOf(builtIns, type) - 1; j >= 0; j--)
            {
                var k = list.FindIndex(s => s.Type == builtIns[j]);
                if (k >= 0) return k + 1;
            }
            return 0;
        }
    }
}
