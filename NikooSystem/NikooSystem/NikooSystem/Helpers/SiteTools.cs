using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models;

namespace NikooSystem.Helpers
{
    /// <summary>Free utility pages on the main Nikoo site (/tools/...).</summary>
    public sealed class SiteTool
    {
        public string Id { get; set; }
        public string Slug { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string Summary { get; set; }
        public string SeoTitle { get; set; }
        public string Description { get; set; }
        public SiteFaq[] Faqs { get; set; }
    }

    public static class SiteTools
    {
        public static readonly IReadOnlyList<SiteTool> All = new List<SiteTool>
        {
            new SiteTool
            {
                Id = "converter",
                Slug = "tabdil",
                Icon = "bi-currency-exchange",
                Name = "مبدل واحد، ارز و تاریخ",
                Summary = "تبدیل تاریخ شمسی و میلادی، واحدهای طول و وزن و دما، و محاسبه ارز با نرخ قابل ویرایش",
                SeoTitle = "مبدل تاریخ شمسی میلادی، ارز و واحد آنلاین",
                Description = "تبدیل رایگان تاریخ شمسی به میلادی و برعکس، مبدل واحد (متر، کیلو، سانتی‌گراد و…) و محاسبه ارز دلار، یورو، لیر و درهم به تومان؛ بدون نصب و کاملاً در مرورگر.",
                Faqs = new[]
                {
                    Faq("تبدیل تاریخ شمسی به میلادی دقیق است؟",
                        "بله؛ از همان الگوریتم تقویم جلالی استاندارد استفاده می‌شود که در نرم‌افزارهای ایرانی رایج است. برای قراردادها و اسناد رسمی، تاریخ مهرشده روی سند معیار است."),
                    Faq("نرخ ارز از کجا می‌آید؟",
                        "نرخ‌های پیش‌فرض تقریبی هستند و فقط برای محاسبه سریع. می‌توانید هر ارز را در همان صفحه ویرایش کنید؛ عدد در مرورگر شما ذخیره می‌شود و به سرور نیکو ارسال نمی‌شود."),
                    Faq("آیا برای تبدیل واحد به اینترنت نیاز دارم؟",
                        "خیر؛ بعد از باز شدن صفحه، همه محاسبات روی دستگاه خودتان انجام می‌شود.")
                }
            },
            new SiteTool
            {
                Id = "age",
                Slug = "sen",
                Icon = "bi-calendar3",
                Name = "محاسبه سن و تاریخ تولد",
                Summary = "سن دقیق به سال و ماه و روز، روز هفته تولد و تاریخ معادل شمسی یا میلادی",
                SeoTitle = "محاسبه سن آنلاین با تاریخ تولد شمسی یا میلادی",
                Description = "سن خود یا فرزندتان را با تاریخ تولد شمسی یا میلادی حساب کنید؛ سن به سال، ماه و روز، روز هفته تولد و روزهای مانده تا تولد بعدی را ببینید.",
                Faqs = new[]
                {
                    Faq("تاریخ تولد من ذخیره می‌شود؟",
                        "خیر؛ تاریخ فقط در مرورگر شما برای محاسبه استفاده می‌شود و به سرور نیکو فرستاده نمی‌شود."),
                    Faq("چرا گاهی یک روز با تقویم دیگر فرق می‌کند؟",
                        "تقویم شمسی و میلادی سال‌های کبیسه متفاوت دارند. اگر تاریخ را در یک تقویم وارد کنید، معادل دیگر همان روز تقویمی محاسبه می‌شود.")
                }
            },
            new SiteTool
            {
                Id = "qr",
                Slug = "qr-code",
                Icon = "bi-upc-scan",
                Name = "ساخت QR کد",
                Summary = "لینک سایت، واتساپ، اینستاگرام یا هر متنی را به تصویر QR تبدیل و دانلود کنید",
                SeoTitle = "ساخت QR کد رایگان آنلاین",
                Description = "QR کد برای لینک فروشگاه، شبکه‌های اجتماعی، شماره تماس یا متن دلخواه بسازید و فایل PNG را رایگان دانلود کنید؛ بدون ثبت‌نام و بدون واترمارک.",
                Faqs = new[]
                {
                    Faq("QR کد برای چه چیزهایی مناسب است؟",
                        "لینک صفحه محصول، منوی رستوران، کارت ویزیت دیجیتال، گروه تلگرام، پرداخت یا هر آدرسی که می‌خواهید با دوربین موبایل باز شود."),
                    Faq("آیا QR ساخته‌شده روی سرور ذخیره می‌شود؟",
                        "خیر؛ تصویر QR در مرورگر شما ساخته می‌شود و فقط وقتی دانلود می‌کنید روی دستگاهتان ذخیره می‌شود.")
                }
            },
            new SiteTool
            {
                Id = "image",
                Slug = "tasvir",
                Icon = "bi-image",
                Name = "فشرده‌سازی و تبدیل تصویر",
                Summary = "کاهش حجم عکس، تبدیل PNG به JPG یا WebP و تغییر اندازه برای آپلود در سایت یا اینستاگرام",
                SeoTitle = "فشرده‌سازی و تبدیل تصویر آنلاین (PNG به JPG)",
                Description = "عکس را فشرده کنید یا فرمتش را عوض کنید؛ PNG به JPG یا WebP، تنظیم کیفیت و حداکثر عرض. فایل روی دستگاه شما پردازش می‌شود و به سرور آپلود نمی‌شود.",
                Faqs = new[]
                {
                    Faq("آیا عکس من به اینترنت فرستاده می‌شود؟",
                        "خیر؛ همه کارها با مرورگر روی گوشی یا کامپیوتر شما انجام می‌شود. برای حریم خصوصی مناسب است."),
                    Faq("چرا حجم بعد از فشرده‌سازی کم نشد؟",
                        "اگر عکس از قبل JPG با کیفیت پایین باشد یا PNG با متن و خطوط نازک، کم شدن حجم محدود است. کیفیت را کمتر کنید یا حداکثر عرض را کوچک‌تر بگذارید.")
                }
            }
        };

        public static string SlugPattern => string.Join("|", All.Select(t => t.Slug));

        public static SiteTool Get(string slug) =>
            All.FirstOrDefault(t => t.Slug.Equals(slug, System.StringComparison.OrdinalIgnoreCase));

        private static SiteFaq Faq(string q, string a) => new SiteFaq { Question = q, Answer = a };
    }
}
