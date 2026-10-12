using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Helpers
{
    public class StoreTemplateInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }
        public string PreviewGradient { get; set; }
        /// <summary>The site kinds (<see cref="SiteKinds"/>) the template is designed for.</summary>
        public string[] Kinds { get; set; } = { SiteKinds.Shop, SiteKinds.Digital };

        private string _layout;
        /// <summary>The page structure (header, hero, sections) the template is drawn on; a skin template reuses a base one.</summary>
        public string Layout
        {
            get { return _layout ?? Id; }
            set { _layout = value; }
        }

        /// <summary>Small line above the hero title; null keeps the base layout's own text.</summary>
        public string Kicker { get; set; }
        public string KickerIcon { get; set; }
        /// <summary>Top bar text; null keeps the base layout's own text.</summary>
        public string Topbar { get; set; }
        public string[] CategoryIcons { get; set; }

        public bool IsSkin => Layout != Id;
    }

    public static class StoreTemplates
    {
        public static readonly IReadOnlyList<StoreTemplateInfo> All = new List<StoreTemplateInfo>
        {
            new StoreTemplateInfo
            {
                Id = "organic",
                Name = "فرنویک (ارگانیک تازه)",
                Description = "الهام از Fernwick: پس‌زمینه کرم، سبز ملایم، هیرو نرم، دسته‌های دایره‌ای و کارت محصول گرد",
                PrimaryColor = "#6aa84f",
                SecondaryColor = "#e8f5d8",
                PreviewGradient = "linear-gradient(135deg,#6aa84f,#c5e1a5 55%,#f7f9f2)"
            },
            new StoreTemplateInfo
            {
                Id = "fashion",
                Name = "مینیکس (فروشگاه کلاسیک)",
                Description = "الهام از Minics: فیروزه‌ای روشن، هیرو خوش‌آمد، گرید محصولات و بخش چرا ما",
                PrimaryColor = "#46d2ad",
                SecondaryColor = "#002c3e",
                PreviewGradient = "linear-gradient(135deg,#46d2ad,#7eead0 50%,#002c3e)"
            },
            new StoreTemplateInfo
            {
                Id = "minimal",
                Name = "مینیمال روشن",
                Description = "هدر تک‌ردیفه بدون نوار بالا، هیرو متن‌محور، گرید ۳ستونه و فوتر مرکز",
                PrimaryColor = "#222222",
                SecondaryColor = "#e5e5e5",
                PreviewGradient = "linear-gradient(135deg,#f5f5f5,#222222)"
            },
            new StoreTemplateInfo
            {
                Id = "marketplace",
                Name = "اسپریلو (تک و گجت)",
                Description = "الهام از Sprylo: ایندیگو، هیرو بنتو چندرنگ، هدر سفید چسبان و گرید محصولات تکنولوژی",
                PrimaryColor = "#4F46E5",
                SecondaryColor = "#EEF2FF",
                PreviewGradient = "linear-gradient(135deg,#4F46E5,#7C3AED 40%,#F97316 70%,#0F172A)"
            },
            new StoreTemplateInfo
            {
                Id = "boutique",
                Name = "بوتیک نرم",
                Description = "هدر مرکز با جستجو، هیرو تصویر نرم، کارت‌های بزرگ دسته و گرید ۳ستونه",
                PrimaryColor = "#6d2c4b",
                SecondaryColor = "#f0d5c4",
                PreviewGradient = "linear-gradient(135deg,#6d2c4b,#f0d5c4)"
            },
            new StoreTemplateInfo
            {
                Id = "studio",
                Layout = "minimal",
                Name = "استودیو (پریمیوم محصول‌محور)",
                Description = "الهام از فروشگاه اپل: پس‌زمینه خاکستری روشن، هدر شیشه‌ای چسبان، تیتر درشت مرکزی، کارت‌های سفید بدون حاشیه و دکمه‌های کپسولی",
                PrimaryColor = "#0071e3",
                SecondaryColor = "#f5f5f7",
                PreviewGradient = "linear-gradient(180deg,#f5f5f7,#ffffff 55%,#0071e3)"
            },
            new StoreTemplateInfo
            {
                Id = "zen",
                Layout = "minimal",
                Name = "ذن (ژاپنی آرام)",
                Description = "الهام از موجی و سبک ژاپندی: کاغذ کرم، خطوط نازک، گوشه‌های تیز، مهر قرمز کنار تیترها و فضای خالی زیاد",
                PrimaryColor = "#a23b2a",
                SecondaryColor = "#ebe5d8",
                PreviewGradient = "linear-gradient(135deg,#f4f1ea,#ebe5d8 60%,#a23b2a)"
            },
            new StoreTemplateInfo
            {
                Id = "noir",
                Layout = "boutique",
                Name = "نوآر (لوکس طلایی)",
                Description = "الهام از برندهای لوکس: هدر و هیروی مشکی، جزئیات طلایی، تصویر قوسی، کارت‌های گوشه‌تیز روی زمینه عاجی",
                PrimaryColor = "#b8924a",
                SecondaryColor = "#111111",
                PreviewGradient = "linear-gradient(135deg,#0d0d0d,#2a2418 55%,#c8a45d)",
                Kicker = "مجموعه منتخب"
            },
            new StoreTemplateInfo
            {
                Id = "bloom",
                Layout = "boutique",
                Name = "بلوم (پاستلی زیبایی)",
                Description = "الهام از گلاسیه: صورتی ملایم، کارت‌های خیلی گرد با زمینه‌های پاستلی، برچسب‌های کپسولی؛ مناسب آرایشی، گل و هدیه",
                PrimaryColor = "#e0567a",
                SecondaryColor = "#fde8ee",
                PreviewGradient = "linear-gradient(135deg,#fde8ee,#fbcfe8 45%,#e0567a)",
                Kicker = "زیبایی روزمره"
            },
            new StoreTemplateInfo
            {
                Id = "brutal",
                Layout = "marketplace",
                Name = "نئوبروتال (جسور و رنگی)",
                Description = "الهام از گام‌رود: حاشیه‌های مشکی ضخیم، سایه‌های تخت، رنگ‌های تند صورتی و زرد و هیروی بنتو؛ مناسب برندهای جوان و محصولات دیجیتال",
                PrimaryColor = "#ff90e8",
                SecondaryColor = "#ffc900",
                PreviewGradient = "linear-gradient(135deg,#ffc900 0 33%,#ff90e8 33% 66%,#111 66%)",
                Kicker = "تازه و متفاوت",
                KickerIcon = "bi-stars",
                Topbar = "ارسال سریع · پرداخت امن · پشتیبانی واقعی"
            },
            new StoreTemplateInfo
            {
                Id = "bazaar",
                Layout = "marketplace",
                Name = "بازار (مارکت‌پلیس ایرانی)",
                Description = "الهام از دیجی‌کالا: قرمز پرانرژی، جستجوی بزرگ، گرید محصولات بدون فاصله با خطوط جداکننده و فوتر روشن؛ مناسب فروشگاه‌های پرکالا",
                PrimaryColor = "#ef394e",
                SecondaryColor = "#fff0f2",
                PreviewGradient = "linear-gradient(135deg,#ef394e,#ff6b81 45%,#f0f0f1)",
                Kicker = "پیشنهاد شگفت‌انگیز",
                KickerIcon = "bi-lightning-fill",
                Topbar = "ارسال سریع به سراسر کشور · ضمانت اصالت کالا · ۷ روز بازگشت"
            },
            new StoreTemplateInfo
            {
                Id = "aurora",
                Layout = "organic",
                Name = "شفق (گلس‌مورفیسم)",
                Description = "الهام از سبک شیشه‌ای اپل و لینیر: پس‌زمینه شفق رنگی، کارت‌های مات شیشه‌ای، تیتر گرادیانی و دکمه‌های درخشان؛ مناسب تکنولوژی و دوره‌ها",
                PrimaryColor = "#6d5dfc",
                SecondaryColor = "#e0e7ff",
                PreviewGradient = "linear-gradient(135deg,#a78bfa,#67e8f9 50%,#f9a8d4)",
                Kicker = "تجربه‌ای تازه از خرید",
                KickerIcon = "bi-stars",
                Topbar = "پرداخت امن · ارسال سریع · پشتیبانی آنلاین",
                CategoryIcons = new[] { "bi-cpu", "bi-headphones", "bi-laptop", "bi-phone", "bi-controller" }
            },
            new StoreTemplateInfo
            {
                Id = "kraft",
                Layout = "organic",
                Name = "کرافت (دست‌ساز و گرم)",
                Description = "الهام از نانوایی‌ها و کارگاه‌های دست‌ساز: کاغذ کرافت، رنگ آجری، کارت‌های دوخت‌دار، مهرهای تخفیف و خط‌های دست‌کشیده",
                PrimaryColor = "#b5532d",
                SecondaryColor = "#f3e9dc",
                PreviewGradient = "linear-gradient(135deg,#f3e9dc,#d9b99b 50%,#b5532d)",
                Kicker = "تازه، دست‌ساز و با عشق",
                KickerIcon = "bi-heart-fill",
                Topbar = "پخت روزانه · بسته‌بندی دست‌ساز · ارسال در همان روز",
                CategoryIcons = new[] { "bi-egg", "bi-cup", "bi-basket2", "bi-gift", "bi-flower1" }
            },
            new StoreTemplateInfo
            {
                Id = "nordic",
                Layout = "fashion",
                Name = "نوردیک (اسکاندیناوی)",
                Description = "الهام از برندهای اسکاندیناوی: رنگ جو دوسر و سبز اکالیپتوس، هیروی تمام‌صفحه روشن، برچسب‌های سفید روی دسته‌ها و فضای آرام؛ مناسب دکور و پوشاک",
                PrimaryColor = "#5f7464",
                SecondaryColor = "#e8e2d9",
                PreviewGradient = "linear-gradient(135deg,#faf8f5,#e8e2d9 55%,#5f7464)"
            },
            new StoreTemplateInfo
            {
                Id = "sport",
                Layout = "fashion",
                Name = "اسپرت (پرانرژی)",
                Description = "الهام از نایکی: مشکی و زرد فسفری، تیترهای خیلی درشت، برچسب‌های مورب و تصویرهای تمام‌صفحه؛ مناسب پوشاک ورزشی و کفش",
                PrimaryColor = "#111111",
                SecondaryColor = "#d4ff3f",
                PreviewGradient = "linear-gradient(135deg,#111 0 55%,#d4ff3f 55%)"
            },
            new StoreTemplateInfo
            {
                Id = "magazine",
                Name = "مجله خبری",
                Description = "نوار خبر فوری، تیتر یک بزرگ، ستون آخرین اخبار و سرویس‌های خبری",
                PrimaryColor = "#b91c1c",
                SecondaryColor = "#111827",
                PreviewGradient = "linear-gradient(135deg,#b91c1c,#111827 60%,#f3f4f6)",
                Kinds = new[] { SiteKinds.Content }
            },
            new StoreTemplateInfo
            {
                Id = "daily",
                Layout = "magazine",
                Name = "روزنامه (کلاسیک چاپی)",
                Description = "الهام از نیویورک‌تایمز و روزنامه‌های چاپی: کاغذ کرم، نام نشریه درشت در وسط با تاریخ روز، خطوط جداکننده ستون‌ها، صفحه اول سه‌ستونه با تیتر یک، خبرهای متنی و پربازدیدها؛ مناسب روزنامه، هفته‌نامه و نشریه تحلیلی",
                PrimaryColor = "#121212",
                SecondaryColor = "#b3261e",
                PreviewGradient = "linear-gradient(180deg,#f8f5ee 0 30%,#121212 30% 34%,#f8f5ee 34% 80%,#b3261e 80%)",
                Kinds = new[] { SiteKinds.Content }
            },
            new StoreTemplateInfo
            {
                Id = "pulse",
                Layout = "magazine",
                Name = "پالس (خبر آنلاین پرسرعت)",
                Description = "الهام از خبرگزاری‌های آنلاین و سایت‌های خبر فناوری: هدر تیره، نوار «زنده» با نقطه چشمک‌زن، صفحه اول بنتو با یک تیتر بزرگ و چهار خبر تصویری، کارت‌های گرد با بزرگ‌نمایی تصویر؛ مناسب خبرگزاری، سایت خبر فناوری، ورزش و اقتصاد",
                PrimaryColor = "#2f6bff",
                SecondaryColor = "#00d9b2",
                PreviewGradient = "linear-gradient(135deg,#0f1115 0 50%,#2f6bff 50% 80%,#00d9b2 80%)",
                Kinds = new[] { SiteKinds.Content }
            },
            new StoreTemplateInfo
            {
                Id = "corporate",
                Name = "شرکتی رسمی",
                Description = "هیرو معرفی، خدمات، آخرین اخبار شرکت، نمونه‌کارها و فرم تماس",
                PrimaryColor = "#0f4c81",
                SecondaryColor = "#e0ecf7",
                PreviewGradient = "linear-gradient(135deg,#0f4c81,#3b82c4 55%,#e0ecf7)",
                Kinds = new[] { SiteKinds.Content }
            },
            new StoreTemplateInfo
            {
                Id = "agency",
                Layout = "corporate",
                Name = "آژانس خلاق (تیره و مدرن)",
                Description = "الهام از سایت آژانس‌های دیجیتال و استودیوهای طراحی: هدر و هیروی تیره، تیتر درشت گرادیانی، تصویر با کارت‌های شناور، نوار متحرک خدمات و کارت‌های شماره‌دار؛ مناسب آژانس تبلیغاتی، استودیو طراحی و شرکت نرم‌افزاری",
                PrimaryColor = "#7c5cff",
                SecondaryColor = "#c6f432",
                PreviewGradient = "linear-gradient(135deg,#0b0d14 0 45%,#7c5cff 75%,#c6f432)",
                Kinds = new[] { SiteKinds.Content },
                Kicker = "از ایده تا اجرا",
                CategoryIcons = new[] { "bi-palette", "bi-code-slash", "bi-megaphone", "bi-camera-reels", "bi-graph-up", "bi-vector-pen" }
            },
            new StoreTemplateInfo
            {
                Id = "industrial",
                Layout = "corporate",
                Name = "صنعتی (مهندسی و پیمانکاری)",
                Description = "الهام از سایت شرکت‌های ساختمانی و تولیدی: نوار تماس و ساعت کاری بالای صفحه، سرمه‌ای و نارنجی ایمنی، هیروی تمام‌عرض با نوار مورب و کادر مشاوره، گوشه‌های تیز و کارت خدمات شماره‌دار؛ مناسب پیمانکاری، کارخانه، حمل‌ونقل و تأسیسات",
                PrimaryColor = "#f07c1b",
                SecondaryColor = "#12263a",
                PreviewGradient = "linear-gradient(135deg,#12263a 0 55%,#f07c1b 55% 68%,#e9edf2 68%)",
                Kinds = new[] { SiteKinds.Content },
                Kicker = "کیفیت، ایمنی، تحویل به‌موقع",
                Topbar = "شنبه تا چهارشنبه ۸ تا ۱۷ · پنجشنبه ۸ تا ۱۳",
                CategoryIcons = new[] { "bi-building", "bi-gear-wide-connected", "bi-tools", "bi-truck", "bi-cone-striped", "bi-bricks" }
            },
            new StoreTemplateInfo
            {
                Id = "clinic",
                Name = "کلینیک آرام",
                Description = "هیرو با دکمه رزرو، کارت خدمات با مدت و هزینه، معرفی متخصصان و ساعت کاری",
                PrimaryColor = "#0f766e",
                SecondaryColor = "#ccfbf1",
                PreviewGradient = "linear-gradient(135deg,#0f766e,#5eead4 55%,#f0fdfa)",
                Kinds = new[] { SiteKinds.Booking }
            },
            new StoreTemplateInfo
            {
                Id = "salon",
                Name = "سالن رز",
                Description = "رنگ‌های گرم، هیرو تصویری، کارت‌های گرد خدمات و آرایشگرها",
                PrimaryColor = "#be185d",
                SecondaryColor = "#fce7f3",
                PreviewGradient = "linear-gradient(135deg,#be185d,#f9a8d4 55%,#fdf2f8)",
                Kinds = new[] { SiteKinds.Booking }
            }
        };

        public static StoreTemplateInfo Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return All[0];
            // backward compatibility
            if (id == "modern") id = "organic";
            if (id == "classic") id = "marketplace";
            return All.FirstOrDefault(t => t.Id == id) ?? All[0];
        }

        public static string Normalize(string id)
        {
            return Get(id).Id;
        }

        public static IEnumerable<StoreTemplateInfo> ForKind(string kind) => All.Where(t => t.Kinds.Contains(kind));

        /// <summary>The template, or the kind's first template when it is not made for that kind of site.</summary>
        public static string NormalizeFor(string id, string kind)
        {
            var template = Get(id);
            return template.Kinds.Contains(kind) ? template.Id : ForKind(kind).First().Id;
        }
    }
}
