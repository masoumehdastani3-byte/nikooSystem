using System;
using System.ComponentModel.DataAnnotations;

namespace NikooSystem.Models
{
    public enum OrderStatus
    {
        [Display(Name = "در انتظار پرداخت")]
        PendingPayment = 0,
        [Display(Name = "پرداخت شده")]
        Paid = 1,
        [Display(Name = "ناموفق")]
        Failed = 2,
        [Display(Name = "در حال انجام")]
        Processing = 3,
        [Display(Name = "تکمیل شده")]
        Completed = 4,
        [Display(Name = "لغو شده")]
        Cancelled = 5
    }

    public class Order
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public int ProductId { get; set; }
        public string ProductTitle { get; set; }

        /// <summary>کد فروشگاه آزمایشی که با این پرداخت دائمی می‌شود</summary>
        public string CompanyCode { get; set; }
        public long Amount { get; set; }

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

        [Display(Name = "آدرس")]
        public string CustomerAddress { get; set; }

        [Display(Name = "توضیحات سفارش")]
        public string Notes { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
        public string Authority { get; set; }
        public long? RefId { get; set; }
        public string PaymentMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? PaidAt { get; set; }
    }
}
