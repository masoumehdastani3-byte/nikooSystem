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
    }
}
