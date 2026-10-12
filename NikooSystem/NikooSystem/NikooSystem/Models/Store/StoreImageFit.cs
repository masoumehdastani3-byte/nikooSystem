using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Models.Store
{
    /// <summary>
    /// How the storefront puts an uploaded image into a fixed frame (product cards, gallery, post covers…), so pictures of any
    /// size and shape keep the pages even. Read by Content/storefront-images.css and storefront-images.js.
    /// </summary>
    public static class StoreImageFit
    {
        /// <summary>Fills the frame; an image much longer or wider than its frame is shown whole instead of losing most of it.</summary>
        public const string Auto = "auto";
        /// <summary>Always fills the frame; the edges that do not fit are cut.</summary>
        public const string Cover = "cover";
        /// <summary>Always shows the whole image, on a plain background.</summary>
        public const string Contain = "contain";

        public class Option
        {
            public string Id, Title, Hint, Icon;
        }

        public static readonly IReadOnlyList<Option> Options = new[]
        {
            new Option { Id = Auto, Icon = "bi-stars", Title = "هوشمند (پیشنهادی)", Hint = "کادر پر می‌شود؛ فقط تصویرهای خیلی کشیده یا خیلی پهن کامل و با زمینه نمایش داده می‌شوند تا بخش مهمشان بریده نشود." },
            new Option { Id = Cover, Icon = "bi-crop", Title = "پر کردن کادر", Hint = "همه تصویرها کادر را کامل پر می‌کنند و لبه‌های اضافه بریده می‌شود؛ ظاهر یکدست‌تر، اما ممکن است گوشه‌ای از محصول دیده نشود." },
            new Option { Id = Contain, Icon = "bi-aspect-ratio", Title = "نمایش کامل تصویر", Hint = "هیچ بخشی از تصویر بریده نمی‌شود و جای خالی کادر با زمینه روشن پر می‌شود؛ مناسب عکس محصول روی زمینه سفید." }
        };

        public static string Normalize(string value)
        {
            return Options.Any(o => o.Id == value) ? value : Auto;
        }
    }
}
