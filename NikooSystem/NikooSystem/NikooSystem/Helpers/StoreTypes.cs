using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Helpers
{
    public class StoreTypeInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string DefaultTemplate { get; set; }
    }

    public static class StoreTypes
    {
        public static readonly IReadOnlyList<StoreTypeInfo> All = new List<StoreTypeInfo>
        {
            new StoreTypeInfo
            {
                Id = "general",
                Name = "فروشگاه عمومی",
                Description = "برای هر نوع کالا؛ ساده و قابل تغییر",
                Icon = "bi-shop",
                DefaultTemplate = "minimal"
            },
            new StoreTypeInfo
            {
                Id = "fashion",
                Name = "پوشاک و مد",
                Description = "سایز، رنگ و گالری تصاویر محصول",
                Icon = "bi-bag-heart",
                DefaultTemplate = "fashion"
            },
            new StoreTypeInfo
            {
                Id = "grocery",
                Name = "مواد غذایی و سوپرمارکت",
                Description = "دسته‌بندی زیاد، وزن و برند محصول",
                Icon = "bi-basket",
                DefaultTemplate = "organic"
            },
            new StoreTypeInfo
            {
                Id = "digital",
                Name = "دیجیتال و گجت",
                Description = "کالای فیزیکی، دوره، فایل و لایسنس",
                Icon = "bi-cpu",
                DefaultTemplate = "marketplace"
            }
        };

        public static StoreTypeInfo Get(string id)
        {
            return All.FirstOrDefault(t => t.Id == (id ?? "").Trim().ToLowerInvariant()) ?? All[0];
        }
    }
}
