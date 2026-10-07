using System;
using System.ComponentModel.DataAnnotations;

namespace NikooSystem.Models
{
    public enum HubPaymentStatus
    {
        Pending = 0,
        Paid = 1,
        Failed = 2,
        Expired = 3
    }

    public class PaymentClient
    {
        public string AppId { get; set; }
        public string ApiKey { get; set; }
        public string Name { get; set; }
        public string[] AllowedReturnHosts { get; set; }
    }

    public class HubPayment
    {
        public int Id { get; set; }
        public string Token { get; set; }
        public string AppId { get; set; }
        public string ExternalRef { get; set; }
        public long Amount { get; set; }
        public string Description { get; set; }
        public string ReturnUrl { get; set; }
        public string NotifyUrl { get; set; }

        [Display(Name = "نام و نام خانوادگی")]
        public string CustomerName { get; set; }

        [Display(Name = "موبایل")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست (مثال: 09121234567)")]
        public string CustomerMobile { get; set; }

        [Display(Name = "ایمیل")]
        [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
        public string CustomerEmail { get; set; }

        public HubPaymentStatus Status { get; set; } = HubPaymentStatus.Pending;
        public string Authority { get; set; }
        public long? RefId { get; set; }
        public string PaymentMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public bool NotifySent { get; set; }
    }

    public class CreatePaymentRequest
    {
        public long Amount { get; set; }
        public string Description { get; set; }
        public string ReturnUrl { get; set; }
        public string NotifyUrl { get; set; }
        public string ExternalRef { get; set; }
        public string CustomerName { get; set; }
        public string CustomerMobile { get; set; }
        public string CustomerEmail { get; set; }
    }

    public class PayStartViewModel
    {
        public HubPayment Payment { get; set; }

        [Required(ErrorMessage = "نام الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "موبایل الزامی است")]
        [Display(Name = "موبایل")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست (مثال: 09121234567)")]
        public string CustomerMobile { get; set; }

        [Required(ErrorMessage = "ایمیل الزامی است")]
        [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
        [Display(Name = "ایمیل")]
        public string CustomerEmail { get; set; }
    }
}
