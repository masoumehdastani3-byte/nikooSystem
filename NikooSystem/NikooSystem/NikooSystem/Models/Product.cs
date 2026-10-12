using System;
using System.ComponentModel.DataAnnotations;

namespace NikooSystem.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان محصول الزامی است")]
        [Display(Name = "عنوان")]
        public string Title { get; set; }

        [Display(Name = "توضیح کوتاه")]
        public string ShortDescription { get; set; }

        [Display(Name = "توضیحات")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        /// <summary>For a store product: the price of a three-month subscription.</summary>
        [Required(ErrorMessage = "قیمت الزامی است")]
        [Display(Name = "قیمت اشتراک سه‌ماهه (تومان)")]
        [Range(1000, long.MaxValue, ErrorMessage = "قیمت معتبر نیست")]
        public long Price { get; set; }

        /// <summary>0 = three months' price × 2 with the standard discount.</summary>
        [Display(Name = "قیمت اشتراک شش‌ماهه (تومان)")]
        [Range(0, long.MaxValue, ErrorMessage = "قیمت معتبر نیست")]
        public long PriceSixMonths { get; set; }

        /// <summary>0 = three months' price × 4 with the standard discount.</summary>
        [Display(Name = "قیمت اشتراک یک‌ساله (تومان)")]
        [Range(0, long.MaxValue, ErrorMessage = "قیمت معتبر نیست")]
        public long PriceYearly { get; set; }

        [Display(Name = "تصویر")]
        public string ImageUrl { get; set; }

        [Display(Name = "دسته‌بندی")]
        public string Category { get; set; }

        [Display(Name = "اسلاگ دمو")]
        public string DemoSlug { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "ویژه")]
        public bool IsFeatured { get; set; }

        [Display(Name = "عنوان در نتایج جستجو")]
        [StringLength(90, ErrorMessage = "حداکثر ۹۰ حرف")]
        public string SeoTitle { get; set; }

        [Display(Name = "توضیح در نتایج جستجو")]
        [StringLength(300, ErrorMessage = "حداکثر ۳۰۰ حرف")]
        public string SeoDescription { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
