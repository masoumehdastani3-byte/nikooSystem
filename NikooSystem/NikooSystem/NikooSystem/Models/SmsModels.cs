using System;

namespace NikooSystem.Models
{
    /// <summary>اعتبار پیامک خریداری‌شده توسط فروشگاه (آنلاین) یا افزوده‌شده توسط مدیر نیکو.</summary>
    public class SmsPurchase
    {
        public int Id { get; set; }
        public string CompanyCode { get; set; }
        /// <summary>مبلغ اعتبار به‌تومان که به کیف پیامک سایت اضافه می‌شود (منفی = کسر توسط مدیر).</summary>
        public long Credit { get; set; }
        /// <summary>مبلغ پرداختی به‌تومان.</summary>
        public long Amount { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
        public string Authority { get; set; }
        public long? RefId { get; set; }
        public string PaymentMessage { get; set; }
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? PaidAt { get; set; }
    }

    /// <summary>بسته شارژ اعتبار پیامک در پنل فروشگاه.</summary>
    public class SmsPackage
    {
        public long Credit { get; set; }
        public int DiscountPercent { get; set; }
        public long Price { get; set; }
        public long FullPrice { get; set; }
    }

    /// <summary>سابقه یک ارسال پیامک از طرف فروشگاه.</summary>
    public class SmsLog
    {
        public int Id { get; set; }
        public string CompanyCode { get; set; }
        public string Mobile { get; set; }
        /// <summary>password | invoice | other</summary>
        public string Kind { get; set; }
        public string Text { get; set; }
        /// <summary>هزینه کسرشده از اعتبار سایت به‌تومان.</summary>
        public long Cost { get; set; }
        public long? JobId { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>نتیجه تلاش برای ارسال پیامک به مشتری.</summary>
    public class SmsSendResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public long Cost { get; set; }
        public long? JobId { get; set; }

        public static SmsSendResult Ok(long cost, long? jobId) =>
            new SmsSendResult { Success = true, Cost = cost, JobId = jobId };

        public static SmsSendResult Fail(string error) =>
            new SmsSendResult { Success = false, Error = error };
    }
}
