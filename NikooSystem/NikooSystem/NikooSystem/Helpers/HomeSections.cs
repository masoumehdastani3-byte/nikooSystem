using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models.Store;

namespace NikooSystem.Helpers
{
    public class HomeSectionType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Hint { get; set; }
        /// <summary>عنوان بخش قابل تغییر است</summary>
        public bool HasTitle { get; set; }
        /// <summary>بخش ساخته‌شده توسط فروشنده که می‌تواند چند بار تکرار و حذف شود</summary>
        public bool IsCustom { get; set; }
        /// <summary>نوع سایت‌هایی که این بخش را دارند (<see cref="SiteKinds"/>)</summary>
        public string[] Kinds { get; set; } = Selling;
        /// <summary>راهنمای قالب ردیف‌ها (<see cref="StoreHomeSection.Items"/>)؛ خالی یعنی این بخش ردیف ندارد</summary>
        public string ItemsHint { get; set; }
        public string ItemsSample { get; set; }
        /// <summary>دکمه و لینک بخش قابل تغییر است</summary>
        public bool HasButton { get; set; }

        internal static readonly string[] Selling = { SiteKinds.Shop, SiteKinds.Digital };
        internal static readonly string[] Marketing = { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Content };
        internal static readonly string[] ContentOnly = { SiteKinds.Content };
        internal static readonly string[] Every = { SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Content, SiteKinds.Booking };
        internal static readonly string[] BookingOnly = { SiteKinds.Booking };
    }

    public static class HomeSections
    {
        public static readonly IReadOnlyList<HomeSectionType> Types = new List<HomeSectionType>
        {
            new HomeSectionType { Id = "hero", Name = "بنر اصلی", Icon = "bi-image", Hint = "تصویر و متن بالای صفحه (از اطلاعات سایت)", Kinds = HomeSectionType.Every },
            new HomeSectionType { Id = "features", Name = "نوار مزایا", Icon = "bi-patch-check", Hint = "ارسال سریع، پرداخت امن، پشتیبانی" },
            new HomeSectionType { Id = "categories", Name = "دسته‌بندی‌ها", Icon = "bi-grid", Hint = "میانبر گروه‌های اصلی (در سایت محتوایی: سرویس‌ها و خدمات)", HasTitle = true, Kinds = HomeSectionType.Every },
            new HomeSectionType { Id = "featured", Name = "محصولات ویژه", Icon = "bi-star", Hint = "کالاهایی که تیک «ویژه» دارند", HasTitle = true },
            new HomeSectionType { Id = "promo", Name = "کارت‌های پیشنهاد", Icon = "bi-megaphone", Hint = "کارت‌های تخفیف و پرفروش‌ها" },
            new HomeSectionType { Id = "headline", Name = "تیتر یک", Icon = "bi-lightning", Hint = "مطالبی که تیک «تیتر یک» دارند، با تصویر بزرگ", HasTitle = true, Kinds = new[] { SiteKinds.Content } },
            new HomeSectionType { Id = "latest", Name = "آخرین مطالب", Icon = "bi-clock-history", Hint = "تازه‌ترین خبرها و مقاله‌ها", HasTitle = true, Kinds = new[] { SiteKinds.Content } },
            new HomeSectionType { Id = "topics", Name = "مطالب هر سرویس", Icon = "bi-columns-gap", Hint = "چند مطلب آخر هر دسته کنار هم", HasTitle = true, Kinds = new[] { SiteKinds.Content } },
            new HomeSectionType { Id = "gallery", Name = "گالری", Icon = "bi-images", Hint = "تصاویر گالری سایت", HasTitle = true, Kinds = HomeSectionType.Every },
            new HomeSectionType { Id = "about", Name = "درباره ما", Icon = "bi-info-circle", Hint = "متن معرفی", Kinds = HomeSectionType.Every },
            new HomeSectionType { Id = "contact", Name = "فرم تماس", Icon = "bi-envelope", Hint = "اطلاعات تماس و فرم ارسال پیام", HasTitle = true, Kinds = new[] { SiteKinds.Content, SiteKinds.Booking } },
            new HomeSectionType { Id = "services", Name = "خدمات و تعرفه‌ها", Icon = "bi-clipboard-check", Hint = "کارت هر خدمت با مدت، هزینه و دکمه رزرو", HasTitle = true, Kinds = HomeSectionType.BookingOnly },
            new HomeSectionType { Id = "staff", Name = "متخصصان", Icon = "bi-person-badge", Hint = "معرفی پزشکان، آرایشگرها یا مشاوران با دکمه رزرو", HasTitle = true, Kinds = HomeSectionType.BookingOnly },
            new HomeSectionType { Id = "hours", Name = "ساعت کاری و مراحل رزرو", Icon = "bi-clock", Hint = "روزها و ساعت‌های پذیرش و سه قدم رزرو آنلاین", HasTitle = true, Kinds = HomeSectionType.BookingOnly },
            new HomeSectionType { Id = "deals", Name = "پیشنهاد شگفت‌انگیز", Icon = "bi-lightning-charge", Hint = "کالاهای تخفیف‌دار با درصد تخفیف و شمارش معکوس تا پایان روز", HasTitle = true },
            new HomeSectionType { Id = "newest", Name = "تازه‌ترین کالاها", Icon = "bi-stars", Hint = "آخرین کالاهایی که به فروشگاه اضافه کرده‌اید", HasTitle = true },
            new HomeSectionType
            {
                Id = "stats", Name = "آمار و افتخارات", Icon = "bi-bar-chart", Hint = "چند عدد کلیدی مثل سال‌های تجربه یا تعداد مشتریان", HasTitle = true,
                Kinds = HomeSectionType.Marketing,
                ItemsHint = "هر خط یک عدد: «عدد | توضیح». تا وقتی خالی است، این بخش در سایت نمایش داده نمی‌شود.",
                ItemsSample = "+۱۰ | سال تجربه\n+۲۵۰۰ | مشتری راضی\n۲۴ ساعته | پاسخ‌گویی"
            },
            new HomeSectionType
            {
                Id = "whyus", Name = "چرا ما؟", Icon = "bi-award", Hint = "مزیت‌های شما در کارت‌های آیکون‌دار", HasTitle = true,
                Kinds = HomeSectionType.ContentOnly,
                ItemsHint = "هر خط یک مزیت: «عنوان | توضیح کوتاه». تا وقتی خالی است، این بخش نمایش داده نمی‌شود.",
                ItemsSample = "تیم متخصص | کارشناسان باتجربه در همه مراحل کنار شما هستند\nتحویل به‌موقع | برنامه زمانی شفاف و گزارش پیشرفت"
            },
            new HomeSectionType
            {
                Id = "steps", Name = "روند همکاری", Icon = "bi-signpost-split", Hint = "مراحل کار با شما در چند قدم شماره‌دار", HasTitle = true,
                Kinds = HomeSectionType.ContentOnly,
                ItemsHint = "هر خط یک مرحله: «عنوان | توضیح». خالی بگذارید تا چهار مرحله عمومی (مشاوره، پیشنهاد، اجرا، پشتیبانی) نمایش داده شود.",
                ItemsSample = "تماس و مشاوره | نیاز شما را می‌شنویم\nپیشنهاد و قرارداد | زمان و هزینه دقیق را اعلام می‌کنیم"
            },
            new HomeSectionType
            {
                Id = "reviews", Name = "نظر مشتریان", Icon = "bi-chat-quote", Hint = "در فروشگاه: نظرهای تأییدشده ۴ و ۵ ستاره خریداران؛ در سایت شرکتی: رضایت‌نامه‌هایی که می‌نویسید", HasTitle = true,
                Kinds = HomeSectionType.Marketing,
                ItemsHint = "هر خط یک نظر: «نام | سمت یا شهر | متن نظر». در فروشگاه فقط وقتی نمایش داده می‌شود که هنوز نظر تأییدشده‌ای از خریداران ندارید.",
                ItemsSample = "مهندس رضایی | مدیر فنی، شرکت آلفا | همکاری دقیق و حرفه‌ای بود و پروژه سر وقت تحویل شد."
            },
            new HomeSectionType
            {
                Id = "faq", Name = "پرسش‌های پرتکرار", Icon = "bi-question-circle", Hint = "پرسش و پاسخ به شکل آکاردئونی", HasTitle = true,
                Kinds = HomeSectionType.Marketing,
                ItemsHint = "هر خط یک پرسش: «پرسش | پاسخ». در فروشگاه اگر خالی بماند، پرسش‌های ارسال، پرداخت و پیگیری از روی تنظیمات فروشگاه ساخته می‌شود.",
                ItemsSample = "هزینه ارسال چقدر است؟ | برای خریدهای بالای یک میلیون تومان رایگان است."
            },
            new HomeSectionType
            {
                Id = "cta", Name = "دعوت به اقدام", Icon = "bi-megaphone", Hint = "نوار پایانی با دکمه ثبت‌نام (فروشگاه) یا درخواست مشاوره (سایت شرکتی)", HasTitle = true,
                Kinds = HomeSectionType.Marketing, HasButton = true
            },
            new HomeSectionType { Id = "products", Name = "ردیف محصولات", Icon = "bi-bag-check", Hint = "کالاهای یک برچسب، جدیدترین، پرفروش یا تخفیف‌دار", HasTitle = true, IsCustom = true },
            new HomeSectionType { Id = "posts", Name = "ردیف مطالب", Icon = "bi-journal-text", Hint = "مطالب یک دسته یا پربازدیدترین‌ها", HasTitle = true, IsCustom = true, Kinds = new[] { SiteKinds.Content } },
            new HomeSectionType { Id = "banner", Name = "بنر تبلیغاتی", Icon = "bi-badge-ad", Hint = "تصویر و متن دلخواه با دکمه", HasTitle = true, IsCustom = true, Kinds = HomeSectionType.Every }
        };

        public static IEnumerable<HomeSectionType> TypesFor(string kind) => Types.Where(t => t.Kinds.Contains(kind));

        public static readonly IReadOnlyDictionary<string, string> PostSources = new Dictionary<string, string>
        {
            { "newest", "جدیدترین مطالب" },
            { "popular", "پربازدیدترین مطالب" },
            { "category", "مطالب یک دسته" }
        };

        public static readonly IReadOnlyDictionary<string, string> ProductSources = new Dictionary<string, string>
        {
            { "collection", "کالاهای یک برچسب ویژه" },
            { "newest", "جدیدترین کالاها" },
            { "bestseller", "پرفروش‌ترین کالاها" },
            { "onsale", "کالاهای تخفیف‌دار" },
            { "featured", "محصولات ویژه" },
            { "category", "کالاهای یک گروه" }
        };

        public const int MaxProductsPerSection = 24;
        public const int MaxRelatedProducts = 12;

        public static int ClampRelatedCount(int count) => Math.Min(MaxRelatedProducts, Math.Max(1, count));

        private static readonly Dictionary<string, string[]> TemplateSections = new Dictionary<string, string[]>
        {
            { "fashion", new[] { "hero", "features", "categories", "deals", "featured", "promo", "newest", "stats", "reviews", "about", "faq", "cta" } },
            { "minimal", new[] { "hero", "featured", "deals", "newest", "stats", "reviews", "about", "gallery", "faq", "cta" } },
            { "marketplace", new[] { "hero", "features", "deals", "featured", "promo", "newest", "stats", "reviews", "faq", "cta" } },
            { "boutique", new[] { "hero", "categories", "featured", "deals", "newest", "stats", "gallery", "reviews", "faq", "cta" } },
            { "organic", new[] { "hero", "categories", "deals", "featured", "promo", "newest", "stats", "reviews", "gallery", "about", "faq", "cta" } },
            { "magazine", new[] { "headline", "latest", "topics", "gallery", "about" } },
            { "corporate", new[] { "hero", "stats", "categories", "about", "whyus", "steps", "latest", "reviews", "gallery", "faq", "cta", "contact" } },
            { "clinic", new[] { "hero", "services", "staff", "hours", "about", "gallery", "contact" } },
            { "salon", new[] { "hero", "services", "staff", "gallery", "hours", "about", "contact" } },
            { "club", new[] { "hero", "services", "staff", "hours", "about", "gallery", "contact" } }
        };

        public static HomeSectionType Type(string id)
        {
            return Types.FirstOrDefault(t => t.Id == id);
        }

        public static bool IsCustom(string type)
        {
            return Type(type)?.IsCustom == true;
        }

        private static readonly HashSet<string> SharedTypes = new HashSet<string> { "deals", "newest", "stats", "whyus", "steps", "reviews", "faq", "cta" };

        /// <summary>بخش‌هایی که فروشگاه و سایت شرکتی هر دو با Views/Shop/_HomeRich نمایش می‌دهند</summary>
        public static bool IsShared(string type) => type != null && SharedTypes.Contains(type);

        public static string[] BuiltInsFor(string layoutStyle)
        {
            string[] list;
            return TemplateSections.TryGetValue(StoreTemplates.Get(layoutStyle).Layout, out list) ? list : TemplateSections["organic"];
        }

        public static StorePageLayout LayoutOf(StoreSettings settings)
        {
            return settings?.PageLayout ?? new StorePageLayout();
        }

        /// <summary>بخش‌های صفحه اول برای قالب فعلی به ترتیب نمایش (شامل بخش‌های مخفی)</summary>
        public static List<StoreHomeSection> Resolve(StoreSettings settings)
        {
            var builtIns = BuiltInsFor(settings?.LayoutStyle);
            var result = new List<StoreHomeSection>();
            foreach (var sec in LayoutOf(settings).HomeSections ?? new List<StoreHomeSection>())
            {
                if (sec == null || Type(sec.Type) == null) continue;
                if (IsCustom(sec.Type))
                {
                    if (!string.IsNullOrWhiteSpace(sec.Id) && result.All(r => r.Id != sec.Id)) result.Add(sec);
                }
                else if (builtIns.Contains(sec.Type) && result.All(r => r.Type != sec.Type))
                {
                    sec.Id = sec.Type;
                    result.Add(sec);
                }
            }

            // a template switch can bring in built-ins that were never saved; place them next to their default neighbours
            for (var i = 0; i < builtIns.Length; i++)
            {
                if (result.Any(r => r.Type == builtIns[i])) continue;
                var at = 0;
                for (var j = i - 1; j >= 0; j--)
                {
                    var k = result.FindIndex(r => r.Type == builtIns[j]);
                    if (k >= 0) { at = k + 1; break; }
                }
                result.Insert(at, new StoreHomeSection { Id = builtIns[i], Type = builtIns[i], IsVisible = true });
            }
            return result;
        }

        /// <summary>
        /// بخش‌های ارسال‌شده از فرم را پاک‌سازی می‌کند؛ بخش‌های مخصوص قالب‌های دیگر حفظ می‌شوند
        /// تا با برگشت به آن قالب تنظیماتشان از بین نرود. با <paramref name="kind"/> بخش‌های دلخواهی که برای آن نوع سایت نیست کنار گذاشته می‌شوند.
        /// </summary>
        public static List<StoreHomeSection> Sanitize(IEnumerable<StoreHomeSection> posted, StoreSettings existing, string kind = null)
        {
            var builtIns = BuiltInsFor(existing?.LayoutStyle);
            var result = new List<StoreHomeSection>();
            foreach (var sec in posted ?? Enumerable.Empty<StoreHomeSection>())
            {
                var type = sec == null ? null : Type(sec.Type);
                if (type == null) continue;
                if (kind != null && type.IsCustom && !type.Kinds.Contains(kind)) continue;

                var clean = new StoreHomeSection
                {
                    Type = type.Id,
                    IsVisible = sec.IsVisible,
                    Title = type.HasTitle ? Trim(sec.Title, 80) : null
                };

                if (!type.IsCustom)
                {
                    if (!builtIns.Contains(type.Id) || result.Any(r => r.Type == type.Id)) continue;
                    clean.Id = type.Id;
                }
                else
                {
                    clean.Id = NormalizeId(sec.Id);
                    while (result.Any(r => r.Id == clean.Id)) clean.Id = NewId();
                    clean.Subtitle = Trim(sec.Subtitle, 160);
                }

                if (type.Id == "products")
                {
                    clean.Source = sec.Source != null && ProductSources.ContainsKey(sec.Source) ? sec.Source : "newest";
                    clean.CollectionId = clean.Source == "collection" ? sec.CollectionId : null;
                    clean.CategoryId = clean.Source == "category" ? sec.CategoryId : null;
                    clean.Count = Math.Min(MaxProductsPerSection, Math.Max(1, sec.Count));
                }
                else if (type.Id == "posts")
                {
                    clean.Source = sec.Source != null && PostSources.ContainsKey(sec.Source) ? sec.Source : "newest";
                    clean.CategoryId = clean.Source == "category" ? sec.CategoryId : null;
                    clean.Count = Math.Min(MaxProductsPerSection, Math.Max(1, sec.Count));
                }
                else if (type.Id == "banner")
                {
                    // rendered inside a CSS url('...'), so quotes and parentheses are not allowed
                    clean.ImageUrl = IsSafeUrl(sec.ImageUrl) && sec.ImageUrl.StartsWith("/", StringComparison.Ordinal)
                        && sec.ImageUrl.IndexOfAny("'\"()\\ ".ToCharArray()) < 0
                        ? sec.ImageUrl.Trim() : null;
                    clean.ButtonText = Trim(sec.ButtonText, 40);
                    clean.LinkUrl = IsSafeUrl(sec.LinkUrl) ? sec.LinkUrl.Trim() : null;
                }
                if (type.HasButton)
                {
                    clean.Subtitle = Trim(sec.Subtitle, 200);
                    clean.ButtonText = Trim(sec.ButtonText, 40);
                    clean.LinkUrl = IsSafeUrl(sec.LinkUrl) ? sec.LinkUrl.Trim() : null;
                }
                if (type.ItemsHint != null)
                    clean.Items = CleanItems(sec.Items);
                result.Add(clean);
            }

            var otherTemplates = (LayoutOf(existing).HomeSections ?? new List<StoreHomeSection>())
                .Where(s => s != null && Type(s.Type) != null && !IsCustom(s.Type) && !builtIns.Contains(s.Type));
            foreach (var s in otherTemplates)
                if (result.All(r => r.Type != s.Type)) result.Add(s);
            return result;
        }

        public const int MaxItems = 12;

        /// <summary>ردیف‌های غیرخالی بخش، هر کدام با ستون‌های جداشده با «|» (ستون‌های کم‌تر از <paramref name="columns"/> خالی برمی‌گردند)</summary>
        public static List<string[]> ItemsOf(string items, int columns)
        {
            return (items ?? "").Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Select(line =>
                {
                    var parts = line.Split(new[] { '|' }, columns).Select(p => p.Trim()).ToList();
                    while (parts.Count < columns) parts.Add("");
                    return parts.ToArray();
                })
                .Where(parts => parts[0].Length > 0)
                .Take(MaxItems)
                .ToList();
        }

        /// <summary>پرسش‌های فروشگاهی که فروشنده هنوز ننوشته؛ فقط از روی تنظیمات واقعی فروشگاه ساخته می‌شود</summary>
        public static List<string[]> DefaultShopFaq(StoreSettings s, SiteKindInfo kind)
        {
            var list = new List<string[]>();
            if (kind.IsDigital)
            {
                list.Add(new[] { "محصول خریداری‌شده چطور به دستم می‌رسد؟",
                    "بلافاصله پس از پرداخت موفق، کتاب، دوره یا فایل در «کتابخانه من» در حساب کاربری شما قرار می‌گیرد و همیشه در دسترس است." });
            }
            else
            {
                string shipping;
                if (s.ShippingCost <= 0) shipping = "ارسال همه سفارش‌ها رایگان است.";
                else
                {
                    shipping = "هزینه ارسال " + s.ShippingCost.ToToman() + " است";
                    shipping += s.FreeShippingThreshold > 0
                        ? " و برای خریدهای بالای " + s.FreeShippingThreshold.ToToman() + " رایگان می‌شود."
                        : "؛ مبلغ دقیق پیش از پرداخت در سبد خرید نمایش داده می‌شود.";
                }
                list.Add(new[] { "هزینه ارسال چقدر است؟", shipping });
            }

            list.Add(new[] { "چطور پرداخت کنم؟", s.EnablePayment
                ? "پرداخت آنلاین و از طریق درگاه امن زرین‌پال با همه کارت‌های بانکی عضو شتاب انجام می‌شود."
                : "پس از ثبت سفارش، برای هماهنگی پرداخت" + (kind.IsDigital ? "" : " و ارسال") + " با شما تماس می‌گیریم." });

            list.Add(new[] { kind.IsDigital ? "خریدهایم را کجا ببینم؟" : "چطور سفارشم را پیگیری کنم؟", kind.IsDigital
                ? "با ورود به حساب کاربری، همه خریدها در «کتابخانه من» و سفارش‌ها در «سفارش‌های من» دیده می‌شود."
                : "اگر با حساب کاربری خرید کنید، وضعیت سفارش و کد رهگیری مرسوله در «سفارش‌های من» نمایش داده می‌شود." });

            var contact = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.Phone)) contact.Add("تلفن " + s.Phone.Trim());
            if (!string.IsNullOrWhiteSpace(s.Email)) contact.Add("ایمیل " + s.Email.Trim());
            list.Add(new[] { "سؤال دیگری دارم؛ چطور با شما در تماس باشم؟", contact.Any()
                ? "از راه " + string.Join(" یا ", contact) + " پاسخ‌گوی شما هستیم."
                : "از صفحه «تماس با ما» پیام بگذارید تا با شما تماس بگیریم." });
            return list;
        }

        /// <summary>مراحل همکاری وقتی صاحب سایت مراحل خودش را ننوشته است</summary>
        public static readonly IReadOnlyList<string[]> DefaultSteps = new List<string[]>
        {
            new[] { "تماس و مشاوره", "نیاز و هدف شما را می‌شنویم و مسیر مناسب را پیشنهاد می‌دهیم." },
            new[] { "پیشنهاد و برنامه", "زمان‌بندی، مراحل و هزینه را شفاف و مکتوب اعلام می‌کنیم." },
            new[] { "اجرا", "کار را مرحله‌به‌مرحله انجام می‌دهیم و از پیشرفت آن باخبرتان می‌کنیم." },
            new[] { "تحویل و پشتیبانی", "پس از تحویل هم برای پرسش‌ها و تغییرات کنار شما هستیم." }
        };

        private static string CleanItems(string items)
        {
            var lines = (items ?? "").Replace("\r", "").Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Take(MaxItems)
                .Select(line => line.Length > 400 ? line.Substring(0, 400) : line);
            var text = string.Join("\n", lines);
            return text.Length == 0 ? null : text;
        }

        public static string NewId()
        {
            return "s" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 20 || !id.All(char.IsLetterOrDigit) || Type(id) != null)
                return NewId();
            return id;
        }

        /// <summary>فقط آدرس‌های نسبی سایت یا http/https مجاز است</summary>
        public static bool IsSafeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            url = url.Trim();
            if (url.StartsWith("/", StringComparison.Ordinal)) return !url.StartsWith("//", StringComparison.Ordinal);
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }
}
