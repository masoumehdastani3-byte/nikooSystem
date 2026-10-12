using System;

namespace NikooSystem.Models
{
    /// <summary>Upload space bought by a store (online, through Nikoo's gateway) or given to it by Nikoo's admin.</summary>
    public class StoragePurchase
    {
        public int Id { get; set; }
        public string CompanyCode { get; set; }
        public int Megabytes { get; set; }
        public long Amount { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
        public string Authority { get; set; }
        public long? RefId { get; set; }
        public string PaymentMessage { get; set; }
        /// <summary>Set when Nikoo's admin added the space by hand (a gift, or a payment made outside the gateway).</summary>
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? PaidAt { get; set; }
    }

    /// <summary>A size of extra space offered on the purchase page.</summary>
    public class StoragePackage
    {
        public int Gigabytes { get; set; }
        public int DiscountPercent { get; set; }
        public long Price { get; set; }
        public long FullPrice { get; set; }
    }

    /// <summary>The space a store uses and may use, in bytes.</summary>
    public class StorageUsage
    {
        public long ImageBytes { get; set; }
        public long FileBytes { get; set; }
        public long QuotaBytes { get; set; }
        public int BaseMb { get; set; }
        public int ExtraMb { get; set; }

        public long UsedBytes => ImageBytes + FileBytes;
        public long FreeBytes => Math.Max(0, QuotaBytes - UsedBytes);
        public int Percent => QuotaBytes <= 0 ? 100 : (int)Math.Min(100, Math.Round(UsedBytes * 100.0 / QuotaBytes));
        public bool IsFull => UsedBytes >= QuotaBytes;
        public bool IsNearlyFull => !IsFull && Percent >= 90;
    }
}
