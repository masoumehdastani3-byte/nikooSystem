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
        public int OrderCount { get; set; }
        public int PaidOrderCount { get; set; }
        public long TotalRevenue { get; set; }
        public List<Order> RecentOrders { get; set; }
    }

    public class ContactViewModel
    {
        [Required(ErrorMessage = "نام الزامی است")]
        public string Name { get; set; }

        [Required(ErrorMessage = "ایمیل الزامی است")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "موضوع الزامی است")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "پیام الزامی است")]
        public string Message { get; set; }
    }
}
