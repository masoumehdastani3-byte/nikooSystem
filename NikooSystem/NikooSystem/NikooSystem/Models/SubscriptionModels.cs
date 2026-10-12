namespace NikooSystem.Models
{
    /// <summary>A length of subscription offered to a store (3, 6 or 12 months) and its price.</summary>
    public class SubscriptionPlan
    {
        public int Months { get; set; }
        public long Price { get; set; }
        /// <summary>The same months bought three at a time.</summary>
        public long FullPrice { get; set; }
        public int DiscountPercent { get; set; }
        public long PerMonth { get; set; }
    }
}
