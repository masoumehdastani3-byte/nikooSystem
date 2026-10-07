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

        [Required(ErrorMessage = "قیمت الزامی است")]
        [Display(Name = "قیمت (تومان)")]
        [Range(1000, long.MaxValue, ErrorMessage = "قیمت معتبر نیست")]
        public long Price { get; set; }

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

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
