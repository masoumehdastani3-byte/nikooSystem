using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>The upload was refused because the store has no room left; the panel then sends the owner to buy space.</summary>
    public class StorageFullException : InvalidOperationException
    {
        public StorageFullException(string message) : base(message) { }
    }

    /// <summary>
    /// The upload space of each store: images under ~/Uploads/CODE and the files of digital products under App_Data/Files/CODE
    /// (ticket attachments are Nikoo's support and are not counted). Every store gets the same base space; more is bought
    /// from the panel or given by Nikoo's admin (<see cref="Models.Store.TenantInfo.StorageExtraMb"/>).
    /// </summary>
    public static class StorageQuota
    {
        public const int DefaultBaseMb = 500;
        public const long DefaultPricePerGb = 150000;

        /// <summary>A photo is stored as two compressed JPEGs, so it never takes more than this whatever its upload size.</summary>
        private const long ImageEstimateBytes = 1024 * 1024;
        private const string UploadsRoot = "~/Uploads/";
        private const string BlockedKey = "nikoo-storage-full";
        private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
        /// <summary>A file this new may belong to a form still being saved, so cleaning never removes it.</summary>
        private static readonly TimeSpan KeepNewFilesFor = TimeSpan.FromHours(1);
        private static readonly TimeSpan AutoCleanEvery = TimeSpan.FromMinutes(10);

        private static readonly int[][] PackageSizes = { new[] { 1, 0 }, new[] { 5, 10 }, new[] { 20, 20 }, new[] { 50, 30 } };

        private static readonly ConcurrentDictionary<string, Measured> Cache = new ConcurrentDictionary<string, Measured>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, DateTime> LastCleaned = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly object PurchaseLock = new object();

        private sealed class Measured
        {
            public long Images, Files;
            public DateTime At;
        }

        public static int BaseMb
        {
            get
            {
                var configured = SiteContentStore.Settings.StorageBaseMb;
                return configured > 0 ? configured : DefaultBaseMb;
            }
        }

        public static long PricePerGb
        {
            get
            {
                var configured = SiteContentStore.Settings.StoragePricePerGb;
                return configured > 0 ? configured : DefaultPricePerGb;
            }
        }

        public static StorageUsage Usage(string companyCode)
        {
            var measured = Cache.GetOrAdd(companyCode, Measure);
            if (DateTime.Now - measured.At > CacheFor) measured = Cache[companyCode] = Measure(companyCode);
            var extra = Math.Max(0, TenantStore.GetTenantForAdmin(companyCode)?.StorageExtraMb ?? 0);
            return new StorageUsage
            {
                ImageBytes = measured.Images,
                FileBytes = measured.Files,
                BaseMb = BaseMb,
                ExtraMb = extra,
                QuotaBytes = (BaseMb + (long)extra) * 1024 * 1024
            };
        }

        /// <summary>The space an uploaded photo is counted for before it is saved (see <see cref="ImageEstimateBytes"/>).</summary>
        public static long ImageEstimate(long uploadBytes) => Math.Min(uploadBytes, ImageEstimateBytes);

        /// <summary>
        /// Throws <see cref="StorageFullException"/> when <paramref name="bytes"/> more do not fit; files no longer used by the
        /// site are cleared first, so deleting products or pictures frees their space.
        /// </summary>
        public static void EnsureRoom(string companyCode, long bytes)
        {
            if (string.IsNullOrEmpty(companyCode)) return;
            var usage = Usage(companyCode);
            if (usage.UsedBytes + bytes <= usage.QuotaBytes) return;

            DateTime last;
            if (!LastCleaned.TryGetValue(companyCode, out last) || DateTime.Now - last > AutoCleanEvery)
            {
                FreeUnused(companyCode);
                usage = Usage(companyCode);
                if (usage.UsedBytes + bytes <= usage.QuotaBytes) return;
            }

            if (HttpContext.Current != null) HttpContext.Current.Items[BlockedKey] = true;
            throw new StorageFullException(usage.IsFull
                ? "فضای سایت شما پر شده است (" + Format(usage.UsedBytes) + " از " + Format(usage.QuotaBytes) + ")؛ دیگر نمی‌توانید تصویر یا فایل بارگذاری کنید. برای ادامه، حجم بیشتر بخرید."
                : "حجم این فایل (" + Format(bytes) + ") از فضای خالی سایت شما (" + Format(usage.FreeBytes) + ") بیشتر است؛ برای بارگذاری آن حجم بیشتر بخرید.");
        }

        /// <summary>An upload was refused in this request for lack of space.</summary>
        public static bool WasBlocked(HttpContextBase http) => http?.Items[BlockedKey] as bool? == true;

        /// <summary>Keeps the measured usage right after a file was written or removed, without measuring the folders again.</summary>
        public static void Added(string companyCode, long imageBytes, long fileBytes = 0)
        {
            Measured measured;
            if (string.IsNullOrEmpty(companyCode) || !Cache.TryGetValue(companyCode, out measured)) return;
            lock (measured)
            {
                measured.Images = Math.Max(0, measured.Images + imageBytes);
                measured.Files = Math.Max(0, measured.Files + fileBytes);
            }
        }

        public static void Invalidate(string companyCode)
        {
            Measured removed;
            if (!string.IsNullOrEmpty(companyCode)) Cache.TryRemove(companyCode, out removed);
        }

        /// <summary>
        /// Deletes the store's uploaded files that nothing on any site refers to any more (pictures of deleted products, replaced
        /// logos, removed gallery items…) and returns the bytes freed. Files newer than an hour are kept.
        /// </summary>
        public static long FreeUnused(string companyCode)
        {
            if (string.IsNullOrEmpty(companyCode)) return 0;
            LastCleaned[companyCode] = DateTime.Now;

            HashSet<string> used;
            try { used = ReferencedFileNames(companyCode); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Storage cleanup of " + companyCode + " skipped: " + ex.Message);
                return 0;
            }

            long freed = 0;
            var keepAfter = DateTime.Now - KeepNewFilesFor;
            foreach (var file in UploadedFiles(companyCode).Concat(ProductFiles(companyCode)))
            {
                if (file.LastWriteTime > keepAfter || used.Contains(file.Name)) continue;
                try
                {
                    var length = file.Length;
                    file.Delete();
                    freed += length;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            Invalidate(companyCode);
            return freed;
        }

        public static IReadOnlyList<StoragePackage> Packages()
        {
            var perGb = PricePerGb;
            return PackageSizes.Select(p => new StoragePackage
            {
                Gigabytes = p[0],
                DiscountPercent = p[1],
                FullPrice = p[0] * perGb,
                Price = (long)Math.Round(p[0] * perGb * (100 - p[1]) / 100.0 / 1000) * 1000
            }).ToList();
        }

        public static StoragePurchase StartPurchase(string companyCode, int gigabytes)
        {
            var package = Packages().FirstOrDefault(p => p.Gigabytes == gigabytes);
            if (package == null) return null;
            return AddPurchase(new StoragePurchase
            {
                CompanyCode = companyCode,
                Megabytes = package.Gigabytes * 1024,
                Amount = package.Price,
                Status = OrderStatus.PendingPayment
            });
        }

        /// <summary>Marks a verified payment and adds its space; false when it had already been counted.</summary>
        public static bool CompletePurchase(int id, long? refId, string message)
        {
            lock (PurchaseLock)
            {
                var purchase = GetPurchase(id);
                if (purchase == null || purchase.Status == OrderStatus.Paid) return false;
                purchase.Status = OrderStatus.Paid;
                purchase.RefId = refId;
                purchase.PaymentMessage = message;
                purchase.PaidAt = DateTime.Now;
                NikooDb.Update(NikooTables.StoragePurchases, purchase);
                AddExtra(purchase.CompanyCode, purchase.Megabytes);
                return true;
            }
        }

        public static void FailPurchase(StoragePurchase purchase, string message)
        {
            lock (PurchaseLock)
            {
                var stored = GetPurchase(purchase.Id);
                if (stored == null || stored.Status == OrderStatus.Paid) return;
                stored.Status = OrderStatus.Failed;
                stored.PaymentMessage = message;
                NikooDb.Update(NikooTables.StoragePurchases, stored);
            }
        }

        public static void SetAuthority(StoragePurchase purchase, string authority)
        {
            purchase.Authority = authority;
            NikooDb.Update(NikooTables.StoragePurchases, purchase);
        }

        /// <summary>Space added or taken back by Nikoo's admin; the store never goes below its base space.</summary>
        public static int Grant(string companyCode, int megabytes, string note)
        {
            lock (PurchaseLock)
            {
                var tenant = TenantStore.GetTenantForAdmin(companyCode);
                if (tenant == null || megabytes == 0) return 0;
                megabytes = Math.Max(megabytes, -Math.Max(0, tenant.StorageExtraMb));
                if (megabytes == 0) return 0;
                AddPurchase(new StoragePurchase
                {
                    CompanyCode = tenant.CompanyCode,
                    Megabytes = megabytes,
                    Amount = 0,
                    Status = OrderStatus.Paid,
                    PaidAt = DateTime.Now,
                    Note = string.IsNullOrWhiteSpace(note) ? "افزوده‌شده توسط مدیر نیکو" : note.Trim()
                });
                AddExtra(tenant.CompanyCode, megabytes);
                return megabytes;
            }
        }

        public static StoragePurchase GetPurchase(int id) => NikooDb.Get(NikooTables.StoragePurchases, NikooDb.Key(id));

        public static List<StoragePurchase> Purchases(string companyCode)
        {
            return NikooDb.Query(NikooTables.StoragePurchases, new Row { ["CompanyCode"] = companyCode }, "[CreatedAt] DESC");
        }

        /// <summary>«۳۴۰ مگابایت», «۱٫۵ گیگابایت»</summary>
        public static string Format(long bytes)
        {
            const double mb = 1024 * 1024, gb = mb * 1024;
            if (bytes >= gb) return Number(bytes / gb) + " گیگابایت";
            if (bytes >= mb) return Number(bytes / mb) + " مگابایت";
            return Math.Max(bytes > 0 ? 1 : 0, (long)Math.Ceiling(bytes / 1024.0)).ToString(CultureInfo.InvariantCulture).ToFaDigits() + " کیلوبایت";
        }

        /// <summary>Megabytes as shown to owners: whole gigabytes when they are, otherwise megabytes.</summary>
        public static string FormatMb(int megabytes) => Format(megabytes * 1024L * 1024);

        private static string Number(double value)
        {
            return value.ToString(value >= 10 ? "0" : "0.#", CultureInfo.InvariantCulture).Replace(".", "٫").ToFaDigits();
        }

        private static StoragePurchase AddPurchase(StoragePurchase purchase)
        {
            lock (PurchaseLock)
            using (var tx = NikooDb.Begin())
            {
                purchase.Id = NikooDb.NextId(NikooTables.StoragePurchases, null, tx);
                purchase.CreatedAt = DateTime.Now;
                NikooDb.Insert(NikooTables.StoragePurchases, purchase, tx: tx);
                tx.Commit();
                return purchase;
            }
        }

        private static void AddExtra(string companyCode, int megabytes)
        {
            NikooDb.Increment(NikooTables.Tenants, NikooDb.Key(companyCode), "StorageExtraMb", megabytes);
        }

        private static Measured Measure(string companyCode)
        {
            return new Measured
            {
                Images = UploadedFiles(companyCode).Sum(f => f.Length),
                Files = ProductFiles(companyCode).Sum(f => f.Length),
                At = DateTime.Now
            };
        }

        private static IEnumerable<FileInfo> UploadedFiles(string companyCode)
        {
            var dir = HostingEnvironment.MapPath(UploadsRoot + companyCode);
            return dir != null && Directory.Exists(dir)
                ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).ToList()
                : new List<FileInfo>();
        }

        /// <summary>The files of digital products; the tickets folder beside them is support's, not the store's.</summary>
        private static IEnumerable<FileInfo> ProductFiles(string companyCode)
        {
            var dir = HostingEnvironment.MapPath(DigitalFileService.Root + companyCode.ToUpperInvariant());
            return dir != null && Directory.Exists(dir)
                ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.TopDirectoryOnly).ToList()
                : new List<FileInfo>();
        }

        /// <summary>
        /// The names of the store's files mentioned anywhere in the database: every text column of every table is searched for
        /// links into ~/Uploads/CODE (so a picture used by another site or in a page's HTML is kept), plus the stored names of
        /// the store's digital files.
        /// </summary>
        private static HashSet<string> ReferencedFileNames(string companyCode)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var link = new Regex("/Uploads/" + Regex.Escape(companyCode) + @"/(?:[^""'\s<>()?#]*/)?([^/""'\s<>()?#]+)", RegexOptions.IgnoreCase);
            var parameters = new Row { ["Pattern"] = "%/Uploads/" + LikeEscape(companyCode) + "/%" };

            var columns = NikooDb.Select(
                "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND DATA_TYPE IN ('nvarchar', 'varchar', 'ntext', 'text')",
                null, r => new { Table = r.GetString(0), Column = r.GetString(1) });
            foreach (var table in columns.GroupBy(c => c.Table))
            {
                var cols = table.Select(c => Quote(c.Column)).ToList();
                var sql = "SELECT " + string.Join(", ", cols) + " FROM [dbo]." + Quote(table.Key)
                    + " WHERE " + string.Join(" OR ", cols.Select(c => c + " LIKE @Pattern"));
                foreach (var values in NikooDb.Select(sql, parameters, r => Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i))).ToList()))
                    foreach (var value in values.Where(v => v != null))
                        foreach (Match m in link.Matches(value))
                            names.Add(m.Groups[1].Value);
            }

            foreach (var stored in NikooDb.Column<string>("SELECT [StoredName] FROM [dbo].[ProductFiles] WHERE [CompanyCode] = @Code", new Row { ["Code"] = companyCode }))
                names.Add(stored);
            return names;
        }

        private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";

        private static string LikeEscape(string value) => value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
    }
}
