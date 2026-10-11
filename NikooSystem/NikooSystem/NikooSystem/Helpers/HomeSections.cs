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
    }

    public static class HomeSections
    {
        public static readonly IReadOnlyList<HomeSectionType> Types = new List<HomeSectionType>
        {
            new HomeSectionType { Id = "hero", Name = "بنر اصلی", Icon = "bi-image", Hint = "تصویر و متن بالای صفحه (از اطلاعات فروشگاه)" },
            new HomeSectionType { Id = "features", Name = "نوار مزایا", Icon = "bi-patch-check", Hint = "ارسال سریع، پرداخت امن، پشتیبانی" },
            new HomeSectionType { Id = "categories", Name = "دسته‌بندی‌ها", Icon = "bi-grid", Hint = "میانبر گروه‌های اصلی کالا", HasTitle = true },
            new HomeSectionType { Id = "featured", Name = "محصولات ویژه", Icon = "bi-star", Hint = "کالاهایی که تیک «ویژه» دارند", HasTitle = true },
            new HomeSectionType { Id = "promo", Name = "کارت‌های پیشنهاد", Icon = "bi-megaphone", Hint = "کارت‌های تخفیف و پرفروش‌ها" },
            new HomeSectionType { Id = "gallery", Name = "گالری", Icon = "bi-images", Hint = "تصاویر گالری فروشگاه", HasTitle = true },
            new HomeSectionType { Id = "about", Name = "درباره فروشگاه", Icon = "bi-info-circle", Hint = "متن معرفی فروشگاه" },
            new HomeSectionType { Id = "products", Name = "ردیف محصولات", Icon = "bi-bag-check", Hint = "کالاهای یک برچسب، جدیدترین، پرفروش یا تخفیف‌دار", HasTitle = true, IsCustom = true },
            new HomeSectionType { Id = "banner", Name = "بنر تبلیغاتی", Icon = "bi-badge-ad", Hint = "تصویر و متن دلخواه با دکمه", HasTitle = true, IsCustom = true }
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

        private static readonly Dictionary<string, string[]> TemplateSections = new Dictionary<string, string[]>
        {
            { "fashion", new[] { "hero", "features", "categories", "featured", "promo", "about" } },
            { "minimal", new[] { "hero", "featured", "about", "gallery" } },
            { "marketplace", new[] { "hero", "features", "featured", "promo" } },
            { "boutique", new[] { "hero", "categories", "featured", "gallery" } },
            { "organic", new[] { "hero", "categories", "featured", "promo", "gallery", "about" } }
        };

        public static HomeSectionType Type(string id)
        {
            return Types.FirstOrDefault(t => t.Id == id);
        }

        public static bool IsCustom(string type)
        {
            return Type(type)?.IsCustom == true;
        }

        public static string[] BuiltInsFor(string layoutStyle)
        {
            string[] list;
            return TemplateSections.TryGetValue(StoreTemplates.Normalize(layoutStyle), out list) ? list : TemplateSections["organic"];
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
        /// تا با برگشت به آن قالب تنظیماتشان از بین نرود.
        /// </summary>
        public static List<StoreHomeSection> Sanitize(IEnumerable<StoreHomeSection> posted, StoreSettings existing)
        {
            var builtIns = BuiltInsFor(existing?.LayoutStyle);
            var result = new List<StoreHomeSection>();
            foreach (var sec in posted ?? Enumerable.Empty<StoreHomeSection>())
            {
                var type = sec == null ? null : Type(sec.Type);
                if (type == null) continue;

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
                else if (type.Id == "banner")
                {
                    // rendered inside a CSS url('...'), so quotes and parentheses are not allowed
                    clean.ImageUrl = IsSafeUrl(sec.ImageUrl) && sec.ImageUrl.StartsWith("/", StringComparison.Ordinal)
                        && sec.ImageUrl.IndexOfAny("'\"()\\ ".ToCharArray()) < 0
                        ? sec.ImageUrl.Trim() : null;
                    clean.ButtonText = Trim(sec.ButtonText, 40);
                    clean.LinkUrl = IsSafeUrl(sec.LinkUrl) ? sec.LinkUrl.Trim() : null;
                }
                result.Add(clean);
            }

            var otherTemplates = (LayoutOf(existing).HomeSections ?? new List<StoreHomeSection>())
                .Where(s => s != null && Type(s.Type) != null && !IsCustom(s.Type) && !builtIns.Contains(s.Type));
            foreach (var s in otherTemplates)
                if (result.All(r => r.Type != s.Type)) result.Add(s);
            return result;
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
