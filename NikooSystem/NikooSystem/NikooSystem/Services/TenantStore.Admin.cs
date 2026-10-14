using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>Queries and actions of the platform admin (/Admin) on the registered stores.</summary>
    public static partial class TenantStore
    {
        /// <summary>The sample sites linked from the main site; they cannot be deleted from the panel.</summary>
        public static readonly IReadOnlyCollection<string> DemoCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FASHION", "GROCERY", "DIGITAL", "ACADEMY", "BOOKS", "NEWS", "COMPANY", "CLINIC", "CLUB"
        };

        public static bool IsDemo(string companyCode) => companyCode != null && DemoCodes.Contains(companyCode);

        /// <summary>Store type (a product's DemoSlug) → the demo site that shows it.</summary>
        public static readonly IReadOnlyDictionary<string, string> DemoBySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "fashion", "FASHION" },
            { "grocery", "GROCERY" },
            { "digital", "DIGITAL" },
            { "academy", "ACADEMY" },
            { "ebook", "BOOKS" },
            { "news", "NEWS" },
            { "corporate", "COMPANY" },
            { "clinic", "CLINIC" },
            { "club", "CLUB" }
        };

        /// <summary>The store whatever its state (suspended or expired ones included).</summary>
        public static TenantInfo GetTenantForAdmin(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return null;
            return NikooDb.Get(NikooTables.Tenants, NikooDb.Key(companyCode));
        }

        private static readonly Dictionary<string, string> TenantSorts = new Dictionary<string, string>
        {
            { "created", "t.[CreatedAt]" },
            { "code", "t.[CompanyCode]" },
            { "name", "t.[Name]" },
            { "trial", "COALESCE(t.[TrialEndsAt], t.[SubscriptionEndsAt])" }
        };

        /// <param name="status">trial | expired | paid | renew | closed | inactive | demo | null</param>
        public static PagedList<TenantInfo> TenantGrid(GridQuery query, string status, string storeType)
        {
            var filter = new SqlFilter().Search(query.Q, "CompanyCode", "Name", "OwnerName", "OwnerMobile", "OwnerEmail", "CustomDomain");
            var now = DateTime.Now;
            switch (status)
            {
                case "trial":
                    filter.Eq("IsTrial", true).Eq("IsPaid", false).Where("t.[TrialEndsAt] > {0}", now);
                    break;
                case "expired":
                    filter.Eq("IsTrial", true).Eq("IsPaid", false).Where("t.[TrialEndsAt] <= {0}", now);
                    break;
                case "paid":
                    filter.Eq("IsPaid", true).Where("(t.[SubscriptionEndsAt] IS NULL OR t.[SubscriptionEndsAt] > {0})", now);
                    break;
                case "renew":
                    filter.Eq("IsPaid", true).Where("t.[SubscriptionEndsAt] <= {0}", now.AddDays(TenantInfo.RenewalNoticeDays));
                    break;
                case "closed":
                    filter.Eq("IsPaid", true).Where("t.[SubscriptionEndsAt] <= {0}", now.AddDays(-TenantInfo.RenewalGraceDays));
                    break;
                case "inactive":
                    filter.Eq("IsActive", false);
                    break;
                case "demo":
                    filter.In("CompanyCode", DemoCodes);
                    break;
            }
            if (!string.IsNullOrEmpty(storeType)) filter.Eq("StoreType", storeType);
            return NikooDb.Page(NikooTables.Tenants, filter, query.OrderBy(TenantSorts, "created", true), query.Page, query.PageSize);
        }

        /// <summary>How much each part of the store holds, for the store's page in the admin.</summary>
        public static Dictionary<string, int> TenantCounts(string companyCode)
        {
            var counts = new Dictionary<string, int>();
            foreach (var table in new TableMap[] { NikooTables.Products, NikooTables.Orders, NikooTables.Customers, NikooTables.Posts, NikooTables.ContactMessages, NikooTables.Services, NikooTables.Appointments, NikooTables.Classes, NikooTables.Enrollments, NikooTables.Facilities, NikooTables.FacilityBookings })
                counts[table.Name] = NikooDb.Count(table, SqlFilter.ForStore(companyCode));
            return counts;
        }

        /// <summary>Adds days to the trial; an expired trial restarts from today.</summary>
        public static void ExtendTrial(TenantInfo tenant, int days)
        {
            var from = tenant.TrialEndsAt.HasValue && tenant.TrialEndsAt.Value > DateTime.Now ? tenant.TrialEndsAt.Value : DateTime.Now;
            tenant.IsTrial = true;
            tenant.IsPaid = false;
            tenant.PaidAt = null;
            tenant.TrialEndsAt = from.AddDays(days);
            UpdateTenant(tenant);
        }

        /// <summary>Sets a new password for the store's own admin panel.</summary>
        public static void SetStoreAdminPassword(string companyCode, string password)
        {
            var settings = GetSettings(companyCode);
            settings.AdminPasswordHash = PasswordHelper.Hash(password);
            SaveSettings(settings);
        }

        public static void DeleteStore(string companyCode)
        {
            RemoveTenant(companyCode);
            DeleteTenantData(companyCode);
        }
    }
}
