using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace NikooSystem.Models
{
    /// <summary>A site whose domain can be verified (Enamad, Google…) and that shows an Enamad seal: the main site or a store.</summary>
    public interface IVerifiedSite
    {
        string EnamadHtml { get; set; }
        string EnamadMeta { get; set; }
        string VerifyHomeTitle { get; set; }
        string RootFiles { get; set; }
    }

    /// <summary>متن‌ها و اطلاعات قابل ویرایش سایت اصلی؛ یک ردیف با Id = 1</summary>
    public class SiteSettings : IVerifiedSite
    {
        public int Id { get; set; } = 1;

        [Required(ErrorMessage = "نام سایت الزامی است")]
        [Display(Name = "نام سایت")]
        public string SiteName { get; set; }

        [Display(Name = "توضیح متا (برای موتورهای جستجو)")]
        public string MetaDescription { get; set; }

        [Display(Name = "عنوان صفحه اصلی در نتایج جستجو")]
        [StringLength(90, ErrorMessage = "حداکثر ۹۰ حرف")]
        public string SeoTitle { get; set; }

        [Display(Name = "کد تأیید گوگل سرچ کنسول")]
        [System.Web.Mvc.AllowHtml]
        public string GoogleVerification { get; set; }

        // ---- صفحه اصلی
        [Display(Name = "متن بالای عنوان")]
        public string HeroEyebrow { get; set; }

        [Required(ErrorMessage = "عنوان صفحه اصلی الزامی است")]
        [Display(Name = "عنوان اصلی")]
        public string HeroTitle { get; set; }

        [Display(Name = "متن معرفی")]
        public string HeroLead { get; set; }

        [Display(Name = "عنوان بخش فروشگاه‌ها")]
        public string StoresTitle { get; set; }

        [Display(Name = "توضیح بخش فروشگاه‌ها")]
        public string StoresSubtitle { get; set; }

        [Display(Name = "درصد رضایت مشتریان")]
        [Range(0, 100, ErrorMessage = "عددی بین ۰ تا ۱۰۰ وارد کنید")]
        public int StatSatisfaction { get; set; }

        [Display(Name = "تعداد دموی فروشگاه")]
        [Range(0, int.MaxValue, ErrorMessage = "عدد معتبر نیست")]
        public int StatDemos { get; set; }

        [Display(Name = "تعداد مشتری راضی")]
        [Range(0, int.MaxValue, ErrorMessage = "عدد معتبر نیست")]
        public int StatCustomers { get; set; }

        [Display(Name = "ساعت پشتیبانی")]
        [Range(0, int.MaxValue, ErrorMessage = "عدد معتبر نیست")]
        public int StatSupportHours { get; set; }

        [Display(Name = "نمایش سوالات متداول در صفحه اصلی")]
        public bool ShowFaq { get; set; } = true;

        [Display(Name = "نمایش نظرات مشتریان در صفحه اصلی")]
        public bool ShowTestimonials { get; set; } = true;

        // ---- تماس و شبکه‌های اجتماعی
        [Display(Name = "آدرس")]
        public string Address { get; set; }

        [Display(Name = "کد پستی")]
        [RegularExpression(@"^\s*[0-9۰-۹]{5}\s*-?\s*[0-9۰-۹]{5}\s*$", ErrorMessage = "کد پستی باید ۱۰ رقم باشد")]
        public string PostalCode { get; set; }

        [Display(Name = "تلفن")]
        public string Phone { get; set; }

        [Display(Name = "ایمیل")]
        [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
        public string Email { get; set; }

        [Display(Name = "ساعات پاسخگویی")]
        public string WorkingHours { get; set; }

        [Display(Name = "اینستاگرام")]
        public string InstagramUrl { get; set; }

        [Display(Name = "تلگرام")]
        public string TelegramUrl { get; set; }

        [Display(Name = "واتساپ")]
        public string WhatsAppUrl { get; set; }

        [Display(Name = "لینکدین")]
        public string LinkedInUrl { get; set; }

        // ---- فوتر
        [Display(Name = "متن معرفی فوتر")]
        public string FooterAbout { get; set; }

        [Display(Name = "نمایش بخش مسئولیت اجتماعی")]
        public bool ShowCharity { get; set; } = true;

        [Display(Name = "عنوان بخش مسئولیت اجتماعی")]
        public string CharityTitle { get; set; }

        [Display(Name = "متن بخش مسئولیت اجتماعی")]
        public string CharityText { get; set; }

        [Display(Name = "لینک بخش مسئولیت اجتماعی")]
        public string CharityUrl { get; set; }

        // ---- صفحات قانونی (متن ساده؛ خط‌هایی که با - شروع شوند فهرست و با ## عنوان می‌شوند)
        [Display(Name = "قوانین و مقررات")]
        public string TermsText { get; set; }

        [Display(Name = "حریم خصوصی")]
        public string PrivacyText { get; set; }

        // ---- پلتفرم
        [Display(Name = "مدت دوره آزمایشی (روز)")]
        [Range(0, 365, ErrorMessage = "عددی بین ۰ تا ۳۶۵ وارد کنید")]
        public int TrialDays { get; set; }

        [Display(Name = "فضای رایگان هر سایت (مگابایت)")]
        [Range(0, 1024 * 1024, ErrorMessage = "عدد معتبر نیست")]
        public int StorageBaseMb { get; set; }

        [Display(Name = "قیمت هر گیگابایت فضای اضافه (تومان)")]
        [Range(0, long.MaxValue, ErrorMessage = "عدد معتبر نیست")]
        public long StoragePricePerGb { get; set; }

        /// <summary>IPs (comma separated) customers point their domains at; set on the «دامنه‌های مشتریان» page. Empty = Web.config.</summary>
        public string ServerIp { get; set; }

        // ---- اینماد و تأیید دامنه سایت اصلی؛ از صفحه «اینماد» پنل مدیریت ثبت می‌شوند، نه از فرم تنظیمات
        public string EnamadHtml { get; set; }
        public string EnamadMeta { get; set; }
        public string VerifyHomeTitle { get; set; }
        public string RootFiles { get; set; }

        // ---- ورود به پنل؛ خالی یعنی مقدار Web.config
        public string AdminUsername { get; set; }
        public string AdminPasswordHash { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>پیام فرم «تماس با ما» سایت اصلی</summary>
    public class SiteMessage
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string Ip { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }

        [Display(Name = "یادداشت داخلی")]
        public string AdminNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class SiteFaq
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "سؤال الزامی است")]
        [Display(Name = "سؤال")]
        public string Question { get; set; }

        [Required(ErrorMessage = "پاسخ الزامی است")]
        [Display(Name = "پاسخ")]
        public string Answer { get; set; }

        [Display(Name = "ترتیب نمایش")]
        public int SortOrder { get; set; }

        [Display(Name = "نمایش در سایت")]
        public bool IsActive { get; set; } = true;
    }

    public class SiteTestimonial
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "نام الزامی است")]
        [Display(Name = "نام")]
        public string Name { get; set; }

        [Display(Name = "سمت یا نام کسب‌وکار")]
        public string Role { get; set; }

        [Required(ErrorMessage = "متن نظر الزامی است")]
        [Display(Name = "متن نظر")]
        public string Text { get; set; }

        [Display(Name = "امتیاز")]
        [Range(1, 5, ErrorMessage = "امتیاز بین ۱ تا ۵ است")]
        public int Rating { get; set; } = 5;

        [Display(Name = "تصویر")]
        public string ImageUrl { get; set; }

        [Display(Name = "ترتیب نمایش")]
        public int SortOrder { get; set; }

        [Display(Name = "نمایش در سایت")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>مقاله وبلاگ سایت اصلی (/blog)</summary>
    public class SitePost : Store.ISeoFields
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان الزامی است")]
        [StringLength(200, ErrorMessage = "حداکثر ۲۰۰ حرف")]
        [Display(Name = "عنوان")]
        public string Title { get; set; }

        /// <summary>The part of the address after /blog/; made from the title when left empty.</summary>
        [Display(Name = "آدرس (اسلاگ)")]
        public string Slug { get; set; }

        [StringLength(600, ErrorMessage = "حداکثر ۶۰۰ حرف")]
        [Display(Name = "خلاصه")]
        public string Summary { get; set; }

        [System.Web.Mvc.AllowHtml]
        [Display(Name = "متن")]
        public string BodyHtml { get; set; }

        public string CoverImageUrl { get; set; }

        [Display(Name = "نویسنده")]
        public string Author { get; set; }

        /// <summary>برچسب‌ها، جداشده با ویرگول</summary>
        [Display(Name = "برچسب‌ها")]
        public string Tags { get; set; }

        public string SeoTitle { get; set; }
        public string SeoDescription { get; set; }

        [Display(Name = "منتشر شود")]
        public bool IsPublished { get; set; } = true;

        public DateTime PublishedAt { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public int ViewCount { get; set; }

        public IEnumerable<string> TagList() =>
            (Tags ?? "").Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 0);

        public bool IsLive() => IsPublished && PublishedAt <= DateTime.Now;
    }
}
