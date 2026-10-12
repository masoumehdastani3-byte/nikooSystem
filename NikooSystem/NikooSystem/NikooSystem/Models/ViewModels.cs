using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NikooSystem.Models
{
    public class CheckoutViewModel
    {
        public Product Product { get; set; }

        [Required(ErrorMessage = "نام الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "موبایل الزامی است")]
        [Display(Name = "موبایل")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست")]
        public string CustomerMobile { get; set; }

        [Required(ErrorMessage = "ایمیل الزامی است")]
        [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
        [Display(Name = "ایمیل")]
        public string CustomerEmail { get; set; }

        [Display(Name = "آدرس")]
        public string CustomerAddress { get; set; }

        [Display(Name = "توضیحات")]
        public string Notes { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "پذیرش قوانین الزامی است")]
        [Display(Name = "قوانین را می‌پذیرم")]
        public bool AcceptTerms { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "نام کاربری الزامی است")]
        [Display(Name = "نام کاربری")]
        public string Username { get; set; }

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور")]
        public string Password { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int ProductCount { get; set; }
        public int ActiveProductCount { get; set; }
        public int OrderCount { get; set; }
        public int PaidOrderCount { get; set; }
        public int PendingOrderCount { get; set; }
        public long TotalRevenue { get; set; }
        public long MonthRevenue { get; set; }
        public List<Order> RecentOrders { get; set; }

        public int StoreCount { get; set; }
        public int TrialStoreCount { get; set; }
        public int PaidStoreCount { get; set; }
        public int ExpiredStoreCount { get; set; }
        public int NewStoresThisWeek { get; set; }
        public List<Store.TenantInfo> RecentStores { get; set; }
        public List<Store.TenantInfo> ExpiringTrials { get; set; }
        /// <summary>Paid stores whose subscription ends within two weeks, has ended, or closed the site; soonest first.</summary>
        public List<Store.TenantInfo> RenewalsDue { get; set; }

        public int UnreadMessageCount { get; set; }
        public List<SiteMessage> RecentMessages { get; set; }

        public int PaymentAppCount { get; set; }
        public long HubPaidAmount { get; set; }
        public int HubPaidCount { get; set; }

        /// <summary>Sign-ups per day for the last 14 days, oldest first.</summary>
        public List<KeyValuePair<DateTime, int>> SignupsByDay { get; set; }

        public VisitReport SiteVisits { get; set; }
        public VisitReport StoreVisits { get; set; }
    }

    public class AdminAccountViewModel
    {
        [Required(ErrorMessage = "نام کاربری الزامی است")]
        [Display(Name = "نام کاربری")]
        [RegularExpression(@"^[A-Za-z0-9_.@-]{3,40}$", ErrorMessage = "نام کاربری ۳ تا ۴۰ حرف انگلیسی، عدد یا . _ - @ باشد")]
        public string Username { get; set; }

        [Required(ErrorMessage = "رمز فعلی الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز فعلی")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "رمز جدید الزامی است")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "رمز جدید حداقل ۸ کاراکتر باشد")]
        [Display(Name = "رمز جدید")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "تکرار رمز با رمز جدید یکسان نیست")]
        [Display(Name = "تکرار رمز جدید")]
        public string ConfirmPassword { get; set; }
    }

    public class PaymentClientForm
    {
        public bool IsNew { get; set; }

        [Required(ErrorMessage = "شناسه اپلیکیشن الزامی است")]
        [RegularExpression(@"^[a-z0-9][a-z0-9_-]{2,49}$", ErrorMessage = "شناسه ۳ تا ۵۰ حرف کوچک انگلیسی، عدد، - یا _ باشد")]
        [Display(Name = "شناسه اپلیکیشن (AppId)")]
        public string AppId { get; set; }

        [Required(ErrorMessage = "نام الزامی است")]
        [Display(Name = "نام")]
        public string Name { get; set; }

        [Display(Name = "دامنه‌های مجاز بازگشت (هر خط یک دامنه)")]
        public string AllowedHosts { get; set; }

        public string ApiKey { get; set; }
    }

    public class ContactViewModel
    {
        [Required(ErrorMessage = "نام الزامی است")]
        [StringLength(100, ErrorMessage = "نام طولانی است")]
        [Display(Name = "نام")]
        public string Name { get; set; }

        [Required(ErrorMessage = "ایمیل الزامی است")]
        [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
        [Display(Name = "ایمیل")]
        public string Email { get; set; }

        [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "شماره تماس معتبر نیست (مثال: 09121234567)")]
        [Display(Name = "شماره تماس (اختیاری)")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "موضوع الزامی است")]
        [StringLength(200, ErrorMessage = "موضوع طولانی است")]
        [Display(Name = "موضوع")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "پیام الزامی است")]
        [StringLength(4000, ErrorMessage = "پیام حداکثر ۴۰۰۰ کاراکتر باشد")]
        [Display(Name = "پیام")]
        public string Message { get; set; }

        /// <summary>Honeypot; people never see or fill it.</summary>
        public string Website { get; set; }
    }
}
