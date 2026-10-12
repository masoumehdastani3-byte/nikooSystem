using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;

namespace NikooSystem.Services
{
    /// <summary>
    /// Stores' subscriptions: after the trial the owner pays for 3, 6 or 12 months at a time and renews before the period ends.
    /// An expired subscription leaves the site open for <see cref="TenantInfo.RenewalGraceDays"/> days, then closes it until
    /// renewal; the store's data is never deleted for it. Payments are orders of the main site (<see cref="Order.Months"/>).
    /// </summary>
    public static class Subscriptions
    {
        public static readonly int[] Periods = { 3, 6, 12 };

        /// <summary>Discount of the six-month and yearly plans when the product has no price of its own for them.</summary>
        public const int SixMonthDiscount = 10;
        public const int YearlyDiscount = 20;

        /// <summary>Lengths Nikoo's admin can record by hand.</summary>
        public static readonly int[] GrantPeriods = { 1, 3, 6, 12 };

        private static readonly object ApplyLock = new object();

        public static string PeriodName(int months)
        {
            switch (months)
            {
                case 1: return "یک‌ماهه";
                case 3: return "سه‌ماهه";
                case 6: return "شش‌ماهه";
                case 12: return "یک‌ساله";
                default: return months.ToFa() + " ماهه";
            }
        }

        public static string PeriodLength(int months)
        {
            return months == 12 ? "یک سال" : months.ToFa() + " ماه";
        }

        /// <summary>The main site's product whose price the store pays (the one of its type).</summary>
        public static Product ProductFor(TenantInfo tenant)
        {
            var id = tenant == null ? null : TenantProvisioningService.SubscriptionProductId(tenant.StoreType);
            return id.HasValue ? SiteStore.GetProduct(id.Value) : null;
        }

        public static List<SubscriptionPlan> Plans(Product product)
        {
            return product == null ? new List<SubscriptionPlan>() : Periods.Select(m => Plan(product, m)).ToList();
        }

        public static SubscriptionPlan Plan(Product product, int months)
        {
            if (product == null || !Periods.Contains(months)) return null;
            var full = product.Price * (months / 3);
            long price;
            if (months == 3) price = product.Price;
            else if (months == 6) price = product.PriceSixMonths > 0 ? product.PriceSixMonths : Round(full * (100 - SixMonthDiscount) / 100);
            else price = product.PriceYearly > 0 ? product.PriceYearly : Round(full * (100 - YearlyDiscount) / 100);

            return new SubscriptionPlan
            {
                Months = months,
                Price = price,
                FullPrice = full,
                DiscountPercent = full > price ? (int)Math.Round((full - price) * 100.0 / full) : 0,
                PerMonth = (long)Math.Round(price / (double)months / 100) * 100
            };
        }

        private static long Round(long toman)
        {
            return (long)Math.Round(toman / 1000.0) * 1000;
        }

        /// <summary>Where a period paid now starts: after the current paid period or the rest of the trial; otherwise today.</summary>
        public static DateTime RenewFrom(TenantInfo tenant)
        {
            var from = DateTime.Now;
            if (tenant.IsPaid && tenant.SubscriptionEndsAt > from) from = tenant.SubscriptionEndsAt.Value;
            if (!tenant.IsPaid && tenant.IsTrial && tenant.TrialEndsAt > from) from = tenant.TrialEndsAt.Value;
            return from;
        }

        /// <summary>A pending order for <paramref name="months"/> of the store's subscription, at its product's price.</summary>
        public static Order StartRenewal(TenantInfo tenant, int months)
        {
            var product = ProductFor(tenant);
            var plan = Plan(product, months);
            if (plan == null) return null;
            return SiteStore.AddOrder(new Order
            {
                ProductId = product.Id,
                ProductTitle = "اشتراک " + PeriodName(months) + " — " + product.Title,
                CompanyCode = tenant.CompanyCode,
                Months = months,
                Amount = plan.Price,
                CustomerName = string.IsNullOrWhiteSpace(tenant.OwnerName) ? tenant.Name : tenant.OwnerName,
                CustomerMobile = tenant.OwnerMobile,
                CustomerEmail = tenant.OwnerEmail,
                Status = OrderStatus.PendingPayment
            });
        }

        /// <summary>
        /// Adds a paid order's months to its store. Safe to call again for the same order (the gateway's return and the
        /// admin's status change may both do it): the months are added once.
        /// </summary>
        public static bool Apply(int orderId)
        {
            lock (ApplyLock)
            {
                var order = SiteStore.GetOrder(orderId);
                if (order == null || string.IsNullOrEmpty(order.CompanyCode) || order.SubscriptionEndsAt.HasValue) return false;
                var tenant = TenantStore.GetTenantForAdmin(order.CompanyCode);
                if (tenant == null) return false;

                var months = order.Months > 0 ? order.Months : 3;
                var end = RenewFrom(tenant).AddMonths(months);
                tenant.IsPaid = true;
                tenant.IsTrial = false;
                tenant.TrialEndsAt = null;
                tenant.PaidAt = DateTime.Now;
                tenant.SubscriptionEndsAt = end;
                tenant.SubscriptionMonths = months;
                tenant.RenewalRemindedAt = null;
                TenantStore.UpdateTenant(tenant);

                order.Months = months;
                order.SubscriptionEndsAt = end;
                SiteStore.UpdateOrder(order);
                return true;
            }
        }

        /// <summary>Months recorded by Nikoo's admin: a payment made outside the gateway, or a gift.</summary>
        public static Order Grant(TenantInfo tenant, int months, string note)
        {
            if (!GrantPeriods.Contains(months)) return null;
            var product = ProductFor(tenant);
            var order = SiteStore.AddOrder(new Order
            {
                ProductId = product?.Id ?? 0,
                ProductTitle = "اشتراک " + PeriodName(months) + " — ثبت دستی",
                CompanyCode = tenant.CompanyCode,
                Months = months,
                Amount = 0,
                CustomerName = string.IsNullOrWhiteSpace(tenant.OwnerName) ? tenant.Name : tenant.OwnerName,
                CustomerMobile = tenant.OwnerMobile,
                CustomerEmail = tenant.OwnerEmail,
                Notes = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Status = OrderStatus.Paid,
                PaidAt = DateTime.Now
            });
            Apply(order.Id);
            return SiteStore.GetOrder(order.Id);
        }

        /// <summary>
        /// Bale reminders to owners whose paid period ends within a week or has ended (until a month after the site
        /// closed), at most once every three days. Run by the hourly background job.
        /// </summary>
        public static int SendReminders()
        {
            var now = DateTime.Now;
            var due = TenantStore.GetTenants().Where(t =>
                t.IsActive && t.IsPaid && t.SubscriptionEndsAt.HasValue && !TenantStore.IsDemo(t.CompanyCode) &&
                t.SubscriptionEndsAt.Value <= now.AddDays(7) &&
                t.SubscriptionEndsAt.Value.AddDays(TenantInfo.RenewalGraceDays + 30) > now &&
                (!t.RenewalRemindedAt.HasValue || t.RenewalRemindedAt.Value <= now.AddDays(-3))).ToList();

            var sent = 0;
            foreach (var t in due)
            {
                lock (ApplyLock)
                {
                    var fresh = TenantStore.GetTenantForAdmin(t.CompanyCode);
                    if (fresh == null || fresh.SubscriptionEndsAt != t.SubscriptionEndsAt) continue;
                    fresh.RenewalRemindedAt = now;
                    TenantStore.UpdateTenant(fresh);
                    Notifications.RenewalReminder(fresh);
                    sent++;
                }
            }
            return sent;
        }

        /// <summary>Stores paid for under the old one-time payment get a year from their payment (at least a month from now).</summary>
        internal static void MigratePermanentStores()
        {
            var now = DateTime.Now;
            foreach (var t in TenantStore.GetTenants().Where(t => t.IsPaid && !t.SubscriptionEndsAt.HasValue))
            {
                var end = (t.PaidAt ?? now).AddYears(1);
                t.SubscriptionEndsAt = end > now.AddMonths(1) ? end : now.AddMonths(1);
                t.SubscriptionMonths = 12;
                TenantStore.UpdateTenant(t);
            }
        }
    }
}
