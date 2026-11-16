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
                Id = "folio",
                Layout = "magazine",
                Name = "فولیو (مجله تصویری)",
                Description = "الهام از مجله فرهنگ و سبک زندگی: سیاه و سفید با یک رنگ زعفرانی، تیتر درشت و عکس‌های گوشه‌تیز؛ مناسب مجله فرهنگی، شهری و هنری",
                PrimaryColor = "#111111",
                SecondaryColor = "#e8a317",
                PreviewGradient = "linear-gradient(135deg,#111 0 42%,#f7f4ee 42% 78%,#e8a317 78%)",
                Kinds = new[] { SiteKinds.Content },
                Kicker = "شماره این هفته"
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
                Id = "ledger",
                Layout = "corporate",
                Name = "دفتر (کاغذ و مرکب)",
                Description = "الهام از دفتر وکالت و معماری: کاغذ کرم، مرکب سرمه‌ای، یک خط طلایی و گوشه‌های تیز؛ مناسب دفتر حقوقی، معماری و مشاوره",
                PrimaryColor = "#1e3a5f",
                SecondaryColor = "#c4a35a",
                PreviewGradient = "linear-gradient(180deg,#f6f1e7 0 68%,#1e3a5f 68% 74%,#c4a35a 74%)",
                Kinds = new[] { SiteKinds.Content },
                Kicker = "دفتر کار شما",
                Topbar = "پاسخ‌گویی در ساعت اداری · مشاوره حضوری و آنلاین"
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
                Id = "pearl",
                Layout = "clinic",
                Name = "سپید (بالینی)",
                Description = "الهام از مطب دندان و کلینیک پوست: سفید بالینی، آبی آرام، گوشه‌های تیز و خدمات در ردیف؛ مناسب دندانپزشکی، پوست و آزمایشگاه",
                PrimaryColor = "#1d4e89",
                SecondaryColor = "#e8f1fb",
                PreviewGradient = "linear-gradient(180deg,#f7f9fc 0 62%,#1d4e89 62%)",
                Kinds = new[] { SiteKinds.Booking },
                Kicker = "مراقبت دقیق",
                KickerIcon = "bi-plus-square",
                Topbar = "نوبت اینترنتی · یادآوری پیامکی · فقط ساعت‌های خالی"
            },
            new StoreTemplateInfo
            {
                Id = "ward",
                Layout = "clinic",
                Name = "بخش (درمانگاه)",
                Description = "الهام از درمانگاه و کلینیک چندتخصصی: سرمه‌ای با نوار قرمز، هدر یک‌دست و کارت‌های تخت؛ مناسب درمانگاه، کلینیک چندپزشکه و مرکز تصویربرداری",
                PrimaryColor = "#0c4a6e",
                SecondaryColor = "#e0f2fe",
                PreviewGradient = "linear-gradient(135deg,#9f1239 0 12%,#0c4a6e 12% 70%,#e0f2fe 70%)",
                Kinds = new[] { SiteKinds.Booking },
                Kicker = "درمانگاه و کلینیک",
                KickerIcon = "bi-building",
                Topbar = "پذیرش نوبت · چند پزشک · بیعانه ویزیت"
            },
            new StoreTemplateInfo
            {
                Id = "practice",
                Layout = "clinic",
                Name = "مطب (خصوصی گرم)",
                Description = "الهام از مطب خصوصی: کاغذ کرم، زرشکی گرم، هیروی وسط‌چین و عکس دایره‌ای پزشک؛ مناسب مطب متخصص و پزشک خانواده",
                PrimaryColor = "#8f3d3d",
                SecondaryColor = "#f3e6dc",
                PreviewGradient = "linear-gradient(180deg,#fbf6ef 0 46%,#8f3d3d 46% 54%,#fbf6ef 54%)",
                Kinds = new[] { SiteKinds.Booking },
                Kicker = "مطب خصوصی",
                KickerIcon = "bi-house-door",
                Topbar = "ویزیت با وقت قبلی · پیگیری با کد رهگیری"
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
            },
            new StoreTemplateInfo
            {
                Id = "marble",
                Layout = "salon",
                Name = "مرمر (اسپا)",
                Description = "سنگ روشن و سبز مریم‌گلی، عکس دایره‌ای و فاصله زیاد؛ مناسب اسپا، ماساژ و مراقبت پوست",
                PrimaryColor = "#3f6f64",
                SecondaryColor = "#e7f0ec",
                PreviewGradient = "linear-gradient(180deg,#f4f7f5 0 62%,#3f6f64 62%)",
                Kinds = new[] { SiteKinds.Booking },
                Kicker = "آرامش و مراقبت",
                KickerIcon = "bi-droplet",
                Topbar = "نوبت اسپا · ماساژ · مراقبت پوست"
            },
            new StoreTemplateInfo
            {
                Id = "barber",
                Layout = "salon",
                Name = "آرایشگر (اصلاح مردانه)",
                Description = "مشکی و برنج، هدر تیره و گوشه‌های تیز؛ مناسب پیرایش مردانه و استایل",
                PrimaryColor = "#d6b25e",
                SecondaryColor = "#161616",
                PreviewGradient = "linear-gradient(180deg,#161616 0 70%,#d6b25e 70% 76%,#161616 76%)",
                Kinds = new[] { SiteKinds.Booking },
                Kicker = "اصلاح و استایل",
                KickerIcon = "bi-scissors",
                Topbar = "نوبت اصلاح · شیفت عصر · بدون انتظار"
            },
            new StoreTemplateInfo
            {
                Id = "gloss",
                Layout = "salon",
                Name = "براق (ناخن و زیبایی)",
                Description = "صورتی روشن، کارت‌های خیلی گرد و دکمه‌های کپسولی؛ مناسب کاشت ناخن، مژه و سالن جوان",
                PrimaryColor = "#ff4d8d",
                SecondaryColor = "#ffe4f1",
                PreviewGradient = "linear-gradient(135deg,#ffe4f1,#ffb3cf 45%,#ff4d8d)",
                Kinds = new[] { SiteKinds.Booking },
                Kicker = "ناخن، مژه، زیبایی",
                KickerIcon = "bi-stars",
                Topbar = "کاشت ناخن · مژه · رنگ مو"
            },
            new StoreTemplateInfo
            {
                Id = "club",
                Name = "کلاس و سالن",
                Description = "هیرو با کلاس گروهی، جلسه خصوصی و رزرو فضا؛ مناسب آموزشگاه و باشگاه ورزشی",
                PrimaryColor = "#15803d",
                SecondaryColor = "#dcfce7",
                PreviewGradient = "linear-gradient(135deg,#14532d,#22c55e 55%,#f0fdf4)",
                Kinds = new[] { SiteKinds.Campus },
                Kicker = "کلاس، مربی، سالن",
                Topbar = "کلاس گروهی · جلسه خصوصی آنلاین یا حضوری · رزرو ساعتی سالن"
            },
            new StoreTemplateInfo
            {
                Id = "academy",
                Layout = "club",
                Name = "آکادمی (سرمه‌ای و طلایی)",
                Description = "سرمه‌ای دانشگاهی، خط طلایی و کارت‌های گوشه‌تیز؛ مناسب آموزشگاه زبان و کنکور، و آکادمی ورزشی",
                PrimaryColor = "#1e3a8a",
                SecondaryColor = "#fef3c7",
                PreviewGradient = "linear-gradient(180deg,#f8f5ee 0 18%,#1e3a8a 18% 78%,#fef3c7 78%)",
                Kinds = new[] { SiteKinds.Campus },
                Kicker = "کلاس، سطح، مدرس",
                KickerIcon = "bi-mortarboard",
                Topbar = "دوره عمومی · جلسه خصوصی · ثبت‌نام با کد پیگیری"
            },
            new StoreTemplateInfo
            {
                Id = "pitch",
                Layout = "club",
                Name = "زمین (پرانرژی)",
                Description = "مشکی و سبز فسفری، تیتر درشت و نشان مورب؛ مناسب باشگاه، مدرسه فوتبال و کلاس‌های پرتحرک",
                PrimaryColor = "#111111",
                SecondaryColor = "#c8f542",
                PreviewGradient = "linear-gradient(135deg,#111 0 58%,#c8f542 58%)",
                Kinds = new[] { SiteKinds.Campus },
                Kicker = "تمرین، زمین، مربی",
                KickerIcon = "bi-lightning-charge",
                Topbar = "کلاس گروهی · مربی خصوصی · رزرو زمین"
            },
            new StoreTemplateInfo
            {
                Id = "atelier",
                Layout = "club",
                Name = "کارگاه (گرم)",
                Description = "خاک رسی و کاغذ گرم، کارت‌های نرم؛ مناسب آموزشگاه هنر و موسیقی، و باشگاه یوگا",
                PrimaryColor = "#9a3412",
                SecondaryColor = "#f5ebe3",
                PreviewGradient = "linear-gradient(135deg,#f5ebe3,#e7d3c4 55%,#9a3412)",
                Kinds = new[] { SiteKinds.Campus },
                Kicker = "هنر، موسیقی، مهارت",
                KickerIcon = "bi-palette",
                Topbar = "کارگاه گروهی · جلسه خصوصی · اجاره اتاق"
            },
            new StoreTemplateInfo
            {
                Id = "cafe",
                Name = "نارون (گرم و کلاسیک)",
                Description = "سربرگ سبز تیره، کارت‌های گرد کرم و نشان طلایی؛ مناسب کافه و کافه‌رستوران",
                PrimaryColor = "#1c3d30",
                SecondaryColor = "#c6a15b",
                PreviewGradient = "linear-gradient(135deg,#1c3d30,#c6a15b 70%,#f6f1e7)",
                Kinds = new[] { SiteKinds.Menu },
                Kicker = "منوی دیجیتال",
                KickerIcon = "bi-upc-scan",
                Topbar = "اسکن کنید و منو را روی موبایل ببینید"
            },
            new StoreTemplateInfo
            {
                Id = "night",
                Layout = "cafe",
                Name = "شب (منوی تیره)",
                Description = "زمینه تیره، خطوط طلایی و ردیف نام و قیمت مثل منوی چاپی شب؛ مناسب رستوران و کافه شبانه",
                PrimaryColor = "#e6c98a",
                SecondaryColor = "#16130f",
                PreviewGradient = "linear-gradient(180deg,#16130f 0 72%,#e6c98a 72% 76%,#16130f 76%)",
                Kinds = new[] { SiteKinds.Menu },
                Kicker = "منوی شب",
                KickerIcon = "bi-moon-stars",
                Topbar = "منو را در نور کم بخوانید"
            },
            new StoreTemplateInfo
            {
                Id = "board",
                Layout = "cafe",
                Name = "تخته (پیشخوان روشن)",
                Description = "نوار نارنجی، دسته‌های مربعی و ردیف‌های تخت؛ مناسب فست‌فود، صبحانه و پیشخوان",
                PrimaryColor = "#ea580c",
                SecondaryColor = "#fff7ed",
                PreviewGradient = "linear-gradient(180deg,#ea580c 0 28%,#fff7ed 28%)",
                Kinds = new[] { SiteKinds.Menu },
                Kicker = "منوی پیشخوان",
                KickerIcon = "bi-clipboard",
                Topbar = "سفارش را از روی تخته بخوانید"
            },
            new StoreTemplateInfo
            {
                Id = "bistro",
                Layout = "cafe",
                Name = "بیسترو (رومیزی روشن)",
                Description = "سفره سفید، خط نازک و زرشکی؛ ردیف نام و قیمت مثل منوی رومیزی. مناسب رستوران، کافه آرام و منوی ناهار",
                PrimaryColor = "#9f1239",
                SecondaryColor = "#faf7f2",
                PreviewGradient = "linear-gradient(180deg,#faf7f2 0 22%,#9f1239 22% 28%,#faf7f2 28%)",
                Kinds = new[] { SiteKinds.Menu },
                Kicker = "منوی میز",
                KickerIcon = "bi-cup-hot",
                Topbar = "روی میز بخوانید و گارسون را صدا بزنید"
            },
            new StoreTemplateInfo
            {
                Id = "bench",
                Layout = "minimal",
                Name = "میز کار",
                Description = "کارت‌های گرد روی زمینه روشن و آبی آرام؛ مناسب تعمیرگاه موبایل، لپ‌تاپ و لوازم",
                PrimaryColor = "#0f4c5c",
                SecondaryColor = "#e0f2f1",
                PreviewGradient = "linear-gradient(135deg,#0f4c5c,#14919b 55%,#e0f2f1)",
                Kinds = new[] { SiteKinds.Repair },
                Kicker = "پذیرش و پیگیری",
                KickerIcon = "bi-wrench",
                Topbar = "دستگاه را بسپارید و با کد پیگیری وضعیت را ببینید"
            },
            new StoreTemplateInfo
            {
                Id = "garage",
                Layout = "minimal",
                Name = "گاراژ (شیفت شب)",
                Description = "زمینه زغالی، خط کهربایی و گوشه‌های تیز؛ مناسب تعمیرگاه خودرو و شیفت شب",
                PrimaryColor = "#e6b325",
                SecondaryColor = "#17191e",
                PreviewGradient = "linear-gradient(180deg,#17191e 0 70%,#e6b325 70% 76%,#17191e 76%)",
                Kinds = new[] { SiteKinds.Repair },
                Kicker = "شیفت شب",
                KickerIcon = "bi-moon",
                Topbar = "پذیرش شبانه · پیگیری با کد · تحویل با پیک"
            },
            new StoreTemplateInfo
            {
                Id = "counter",
                Layout = "minimal",
                Name = "پیشخوان (روشن)",
                Description = "نوار نارنجی ایمنی، کارت‌های تخت و کاغذ روشن؛ مناسب پیشخوان پذیرش موبایل و لوازم",
                PrimaryColor = "#e85d04",
                SecondaryColor = "#fff4eb",
                PreviewGradient = "linear-gradient(180deg,#e85d04 0 22%,#fff4eb 22%)",
                Kinds = new[] { SiteKinds.Repair },
                Kicker = "پیشخوان پذیرش",
                KickerIcon = "bi-shop",
                Topbar = "نوبت پذیرش · وضعیت دستگاه · تحویل همان روز"
            },
            new StoreTemplateInfo
            {
                Id = "yard",
                Layout = "minimal",
                Name = "محوطه (ایمنی)",
                Description = "بتن روشن و نوار زرد ایمنی، گوشه‌های تیز؛ مناسب تعمیرگاه خودرو، بدنه و لوازم در محوطه",
                PrimaryColor = "#111111",
                SecondaryColor = "#f5c518",
                PreviewGradient = "linear-gradient(180deg,#f5c518 0 14%,#e7e5e4 14% 86%,#111 86%)",
                Kinds = new[] { SiteKinds.Repair },
                Kicker = "پذیرش محوطه",
                KickerIcon = "bi-cone-striped",
                Topbar = "پذیرش خودرو و لوازم · پیگیری با کد · تحویل در محوطه"
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
