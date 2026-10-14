using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>
    /// Access to the stores and their data. This file holds the core (tenants, settings, catalogue, orders);
    /// paged lists are in TenantStore.Queries.cs and the content-site tables in TenantStore.Content.cs.
    /// </summary>
    public static partial class TenantStore
    {
        private static readonly Dictionary<string, object> TenantLocks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SeededTenants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static object LockFor(string companyCode)
        {
            lock (TenantLocks)
            {
                if (!TenantLocks.ContainsKey(companyCode))
                    TenantLocks[companyCode] = new object();
                return TenantLocks[companyCode];
            }
        }

        public static List<TenantInfo> GetTenants()
        {
            return NikooDb.List(NikooTables.Tenants, orderBy: "[CreatedAt], [CompanyCode]");
        }

        /// <summary>فروشگاه فعال است و دوره آزمایشی‌اش تمام نشده</summary>
        public static bool IsLive(TenantInfo tenant)
        {
            return tenant != null && tenant.IsActive && !tenant.IsTrialExpired();
        }

        public static TenantInfo GetTenant(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return null;
            var tenant = NikooDb.Get(NikooTables.Tenants, NikooDb.Key(companyCode));
            return IsLive(tenant) ? tenant : null;
        }

        public static TenantInfo ResolveByHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            host = host.Split(':')[0].Trim().TrimEnd('.').ToLowerInvariant();
            if (host == "localhost" || host == "127.0.0.1") return null;

            var root = TenantProvisioningService.RootDomain;
            if (!string.IsNullOrEmpty(root) && host.EndsWith("." + root, StringComparison.Ordinal))
            {
                var sub = host.Substring(0, host.Length - root.Length - 1);
                if (sub != "www" && sub.IndexOf('.') < 0)
                    return GetTenant(sub);
            }

            var bare = host.StartsWith("www.", StringComparison.Ordinal) ? host.Substring(4) : host;
            return GetTenants().FirstOrDefault(t =>
                IsLive(t) &&
                !string.IsNullOrWhiteSpace(t.CustomDomain) &&
                string.Equals(t.CustomDomain.Trim(), bare, StringComparison.OrdinalIgnoreCase));
        }

        public static bool CompanyCodeExists(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return false;
            return NikooDb.Exists(NikooTables.Tenants, NikooDb.Key(companyCode))
                || NikooDb.Exists(NikooTables.Settings, NikooDb.Key(companyCode));
        }

        public static void AddTenant(TenantInfo tenant)
        {
            try
            {
                using (var tx = NikooDb.Begin())
                {
                    if (NikooDb.Exists(NikooTables.Tenants, NikooDb.Key(tenant.CompanyCode), tx))
                        throw new InvalidOperationException("این آدرس فروشگاه قبلاً گرفته شده است.");
                    NikooDb.Insert(NikooTables.Tenants, tenant, tx: tx);
                    tx.Commit();
                }
            }
            catch (SqlException ex) when (NikooDb.IsDuplicateKey(ex))
            {
                throw new InvalidOperationException("این آدرس فروشگاه قبلاً گرفته شده است.");
            }
        }

        public static void UpdateTenant(TenantInfo tenant)
        {
            NikooDb.Update(NikooTables.Tenants, tenant);
        }

        /// <summary>
        /// Writes only the result of a domain check, and only while the domain is still the same, so a slow check doesn't
        /// overwrite what changed on the store meanwhile (a payment, a new domain).
        /// </summary>
        public static void SaveDomainCheck(TenantInfo tenant)
        {
            NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.Tenants.Name)
                + " SET [DomainCheck] = @Check, [DomainDnsOkAt] = @DnsOkAt, [DomainRemindedAt] = @RemindedAt"
                + " WHERE [CompanyCode] = @Code AND [CustomDomain] = @Domain",
                new Row
                {
                    ["Check"] = tenant.DomainCheck,
                    ["DnsOkAt"] = tenant.DomainDnsOkAt,
                    ["RemindedAt"] = tenant.DomainRemindedAt,
                    ["Code"] = tenant.CompanyCode,
                    ["Domain"] = tenant.CustomDomain ?? ""
                });
        }

        public static void RemoveTenant(string companyCode)
        {
            NikooDb.Delete(NikooTables.Tenants, NikooDb.Key(companyCode));
        }

        /// <summary>حذف کامل داده‌ها و فایل‌های آپلودشده فروشگاه</summary>
        public static void DeleteTenantData(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return;
            lock (LockFor(companyCode))
            {
                using (var tx = NikooDb.Begin())
                {
                    NikooDb.DeleteScope(NikooTables.StoreScoped, companyCode, tx);
                    tx.Commit();
                }

                foreach (var folder in new[] { "~/App_Data/Tenants/", "~/Uploads/", DigitalFileService.Root })
                {
                    var dir = HostingEnvironment.MapPath(folder + companyCode);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, true);
                }
                StorageQuota.Invalidate(companyCode);
            }
            lock (TenantLocks) TenantLocks.Remove(companyCode);
            lock (SeededTenants) SeededTenants.Remove(companyCode);
        }

        /// <summary>ساخت داده اولیه فروشگاه جدید بر اساس نوع کسب‌وکار</summary>
        public static void SeedNewTenant(string companyCode, string storeType, Action<StoreSettings> customize)
        {
            lock (LockFor(companyCode))
            {
                if (NikooDb.Exists(NikooTables.Settings, NikooDb.Key(companyCode)))
                    throw new InvalidOperationException("داده این فروشگاه از قبل وجود دارد.");
                SeedTenantData(companyCode.ToUpperInvariant(), storeType, customize);
            }
            lock (SeededTenants) SeededTenants.Add(companyCode);
        }

        public static string ResolveCompanyCode(HttpRequestBase request, string routeCompany = null)
        {
            if (!string.IsNullOrWhiteSpace(routeCompany))
            {
                var t = GetTenant(routeCompany);
                return t?.CompanyCode;
            }

            var byHost = ResolveByHost(request?.Url?.Host);
            return byHost?.CompanyCode;
        }

        public static StoreSettings GetSettings(string companyCode)
        {
            EnsureTenantSeed(companyCode);
            using (var tx = NikooDb.Begin())
            {
                var settings = NikooDb.Get(NikooTables.Settings, NikooDb.Key(companyCode), tx);
                if (settings != null)
                    settings.PageLayout = NikooDb.Get(NikooTables.PageLayouts, NikooDb.Key(settings.CompanyCode), tx);
                return settings;
            }
        }

        public static void SaveSettings(StoreSettings settings)
        {
            lock (LockFor(settings.CompanyCode))
            using (var tx = NikooDb.Begin())
            {
                WriteSettings(settings, tx);
                tx.Commit();
            }
        }

        private static void WriteSettings(StoreSettings settings, DbTx tx)
        {
            NikooDb.Upsert(NikooTables.Settings, settings, tx: tx);
            if (settings.PageLayout == null)
                NikooDb.Delete(NikooTables.PageLayouts, NikooDb.Key(settings.CompanyCode), tx);
            else
                NikooDb.Upsert(NikooTables.PageLayouts, settings.PageLayout, settings.CompanyCode, tx);
        }

        // ---- Generic access to the per-store tables (CompanyCode, Id)

        private static List<T> All<T>(EntityMap<T> map, string companyCode, string orderBy = null) where T : class, new()
        {
            EnsureTenantSeed(companyCode);
            return NikooDb.List(map, companyCode, orderBy);
        }

        private static T Find<T>(EntityMap<T> map, string companyCode, int id) where T : class, new()
        {
            EnsureTenantSeed(companyCode);
            return NikooDb.Get(map, NikooDb.Key(companyCode, id));
        }

        private static T FindBy<T>(EntityMap<T> map, string companyCode, string column, object value) where T : class, new()
        {
            if (value == null) return null;
            EnsureTenantSeed(companyCode);
            var key = NikooDb.Key(companyCode);
            key[column] = value;
            return NikooDb.Get(map, key);
        }

        /// <summary>
        /// Inserts a new row (with the store's next free Id) or rewrites an existing one. <paramref name="prepare"/> runs once the
        /// Id is known and receives the stored row, or null for a new one, so values the form does not post can be carried over.
        /// </summary>
        private static T Save<T>(EntityMap<T> map, string companyCode, T entity, Action<T, T> prepare = null) where T : class, IStoreEntity, new()
        {
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var stored = entity.Id > 0 ? NikooDb.Get(map, NikooDb.Key(companyCode, entity.Id), tx) : null;
                if (stored == null) entity.Id = NikooDb.NextId(map, companyCode, tx);
                prepare?.Invoke(entity, stored);
                NikooDb.Upsert(map, entity, companyCode, tx);
                tx.Commit();
                return entity;
            }
        }

        private static bool Update<T>(EntityMap<T> map, string companyCode, T entity) where T : class, new()
        {
            lock (LockFor(companyCode))
                return NikooDb.Update(map, entity, companyCode);
        }

        private static bool Remove(TableMap map, string companyCode, int id)
        {
            lock (LockFor(companyCode))
                return NikooDb.Delete(map, NikooDb.Key(companyCode, id));
        }

        private static int RemoveMany(TableMap map, string companyCode, IEnumerable<int> ids)
        {
            var removed = 0;
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                foreach (var id in ids.Distinct())
                    if (NikooDb.Delete(map, NikooDb.Key(companyCode, id), tx)) removed++;
                tx.Commit();
            }
            return removed;
        }

        /// <summary>Deletes a category or menu item together with everything nested under it.</summary>
        private static bool RemoveTree<T>(EntityMap<T> map, string companyCode, int id) where T : class, ITreeNode, new()
        {
            var all = All(map, companyCode);
            if (all.All(n => n.Id != id)) return false;
            return RemoveMany(map, companyCode, TreeHelper.SubtreeIds(all, id)) > 0;
        }

        // ---- Categories and attributes

        /// <summary>In tree order: each category right after its parent, siblings by their sort order.</summary>
        public static List<StoreCategory> GetCategories(string companyCode) =>
            TreeHelper.Flatten(All(NikooTables.Categories, companyCode, "[SortOrder], [Id]")).Select(r => r.Node).ToList();

        public static StoreCategory GetCategory(string companyCode, int id) => Find(NikooTables.Categories, companyCode, id);

        public static StoreCategory SaveCategory(string companyCode, StoreCategory category) =>
            Save(NikooTables.Categories, companyCode, category, (c, stored) =>
            {
                if (string.IsNullOrWhiteSpace(c.Slug)) c.Slug = "cat-" + c.Id;
            });

        public static bool DeleteCategory(string companyCode, int id) => RemoveTree(NikooTables.Categories, companyCode, id);

        public static List<StoreAttributeDef> GetAttributeDefs(string companyCode) =>
            All(NikooTables.AttributeDefs, companyCode, "[SortOrder], [Id]");

        public static StoreAttributeDef GetAttributeDef(string companyCode, int id) => Find(NikooTables.AttributeDefs, companyCode, id);

        public static List<StoreAttributeDef> GetAttributeDefsForCategory(string companyCode, int categoryId)
        {
            return GetAttributeDefs(companyCode)
                .Where(a => a.CategoryIds == null || a.CategoryIds.Count == 0 || a.CategoryIds.Contains(categoryId))
                .ToList();
        }

        public static StoreAttributeDef SaveAttributeDef(string companyCode, StoreAttributeDef def) =>
            Save(NikooTables.AttributeDefs, companyCode, def);

        public static bool DeleteAttributeDef(string companyCode, int id) => Remove(NikooTables.AttributeDefs, companyCode, id);

        /// <summary>Products that had <paramref name="oldValue"/> for the attribute get <paramref name="newValue"/> instead.</summary>
        public static int RenameAttributeValue(string companyCode, int attributeId, string oldValue, string newValue)
        {
            var parameters = NikooDb.Key(companyCode);
            parameters["AttributeId"] = attributeId;
            parameters["Old"] = oldValue;
            parameters["New"] = newValue;
            lock (LockFor(companyCode))
                return NikooDb.Execute("UPDATE " + NikooDb.Table("ProductAttributes") + " SET [Value] = @New"
                    + " WHERE [CompanyCode] = @CompanyCode AND [AttributeId] = @AttributeId AND [Value] = @Old", parameters);
        }

        // ---- Products

        public static List<StoreProduct> GetProducts(string companyCode) =>
            All(NikooTables.Products, companyCode, "[CreatedAt] DESC, [Id]");

        public static StoreProduct GetProduct(string companyCode, int id) => Find(NikooTables.Products, companyCode, id);

        public static StoreProduct GetProductBySlug(string companyCode, string slug) =>
            FindBy(NikooTables.Products, companyCode, "Slug", slug);

        /// <summary>Saves the product's own fields; its view and sales counters are kept as stored.</summary>
        public static StoreProduct SaveProduct(string companyCode, StoreProduct product) =>
            Save(NikooTables.Products, companyCode, product, (p, stored) =>
            {
                if (string.IsNullOrWhiteSpace(p.Slug)) p.Slug = "p-" + p.Id;
                if (p.IsDigital()) p.Stock = 0;
                if (stored == null)
                {
                    p.CreatedAt = DateTime.Now;
                    return;
                }
                p.CreatedAt = stored.CreatedAt;
                p.ViewCount = stored.ViewCount;
                p.SoldCount = stored.SoldCount;
                p.UpdatedAt = DateTime.Now;
            });

        /// <summary>Deletes the product and its protected files.</summary>
        public static bool DeleteProduct(string companyCode, int id)
        {
            var product = GetProduct(companyCode, id);
            if (product == null || !Remove(NikooTables.Products, companyCode, id)) return false;
            foreach (var file in product.Files ?? new List<StoreProductFile>())
                DigitalFileService.Delete(companyCode, file);
            return true;
        }

        public static int DeleteSampleProducts(string companyCode) =>
            RemoveMany(NikooTables.Products, companyCode, GetProducts(companyCode).Where(p => p.IsSample).Select(p => p.Id));

        public static void CountProductView(string companyCode, int productId)
        {
            NikooDb.Increment(NikooTables.Products, NikooDb.Key(companyCode, productId), "ViewCount", 1);
        }

        // ---- Collections (product tags)

        public static List<StoreCollection> GetCollections(string companyCode) =>
            All(NikooTables.Collections, companyCode, "[SortOrder], [Id]");

        public static StoreCollection GetCollection(string companyCode, int id) => Find(NikooTables.Collections, companyCode, id);

        /// <summary>Saves the collection and makes exactly <paramref name="productIds"/> carry its tag.</summary>
        public static StoreCollection SaveCollection(string companyCode, StoreCollection collection, IEnumerable<int> productIds)
        {
            Save(NikooTables.Collections, companyCode, collection, (c, stored) =>
            {
                c.CreatedAt = stored?.CreatedAt ?? DateTime.Now;
                if (string.IsNullOrWhiteSpace(c.Slug)) c.Slug = "tag-" + c.Id;
            });
            TagProducts(companyCode, collection.Id, new HashSet<int>(productIds ?? Enumerable.Empty<int>()));
            return collection;
        }

        public static bool DeleteCollection(string companyCode, int id)
        {
            var removed = Remove(NikooTables.Collections, companyCode, id);
            TagProducts(companyCode, id, new HashSet<int>());
            return removed;
        }

        private static void TagProducts(string companyCode, int collectionId, HashSet<int> productIds)
        {
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                foreach (var p in NikooDb.List(NikooTables.Products, companyCode, tx: tx))
                {
                    var tagged = productIds.Contains(p.Id);
                    if (tagged == p.InCollection(collectionId)) continue;
                    p.CollectionIds = (p.CollectionIds ?? new List<int>()).Where(x => x != collectionId).ToList();
                    if (tagged) p.CollectionIds.Add(collectionId);
                    NikooDb.Update(NikooTables.Products, p, companyCode, tx);
                }
                tx.Commit();
            }
        }

        // ---- Menus, pages and gallery

        public static List<StoreMenuItem> GetMenus(string companyCode) =>
            All(NikooTables.MenuItems, companyCode, "[SortOrder], [Id]");

        public static StoreMenuItem GetMenu(string companyCode, int id) => Find(NikooTables.MenuItems, companyCode, id);

        public static StoreMenuItem SaveMenu(string companyCode, StoreMenuItem item) => Save(NikooTables.MenuItems, companyCode, item);

        public static bool DeleteMenu(string companyCode, int id) => RemoveTree(NikooTables.MenuItems, companyCode, id);

        public static List<StorePage> GetPages(string companyCode) => All(NikooTables.Pages, companyCode);

        public static StorePage GetPage(string companyCode, int id) => Find(NikooTables.Pages, companyCode, id);

        public static StorePage GetPageBySlug(string companyCode, string slug)
        {
            var page = FindBy(NikooTables.Pages, companyCode, "Slug", slug);
            return page != null && page.IsPublished ? page : null;
        }

        public static StorePage SavePage(string companyCode, StorePage page) =>
            Save(NikooTables.Pages, companyCode, page, (p, stored) =>
            {
                p.UpdatedAt = DateTime.Now;
                if (string.IsNullOrWhiteSpace(p.Slug)) p.Slug = "page-" + p.Id;
            });

        public static bool DeletePage(string companyCode, int id) => Remove(NikooTables.Pages, companyCode, id);

        public static List<StoreGalleryItem> GetGallery(string companyCode) =>
            All(NikooTables.GalleryItems, companyCode, "[SortOrder], [Id]");

        /// <summary>Appends the items after the existing ones.</summary>
        public static void AddGalleryItems(string companyCode, IEnumerable<StoreGalleryItem> items)
        {
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var sort = NikooDb.List(NikooTables.GalleryItems, companyCode, tx: tx).Select(g => g.SortOrder).DefaultIfEmpty(0).Max();
                foreach (var item in items)
                {
                    item.Id = NikooDb.NextId(NikooTables.GalleryItems, companyCode, tx);
                    item.SortOrder = ++sort;
                    NikooDb.Insert(NikooTables.GalleryItems, item, companyCode, tx);
                }
                tx.Commit();
            }
        }

        public static bool DeleteGalleryItem(string companyCode, int id) => Remove(NikooTables.GalleryItems, companyCode, id);

        // ---- Customers

        public static StoreCustomer GetCustomer(string companyCode, int? id) =>
            id.HasValue ? Find(NikooTables.Customers, companyCode, id.Value) : null;

        public static StoreCustomer GetCustomerByMobile(string companyCode, string mobile) =>
            FindBy(NikooTables.Customers, companyCode, "Mobile", mobile);

        public static StoreCustomer SaveCustomer(string companyCode, StoreCustomer customer) =>
            Save(NikooTables.Customers, companyCode, customer, (c, stored) =>
            {
                c.CreatedAt = stored?.CreatedAt ?? DateTime.Now;
            });

        // ---- Orders

        public static StoreOrder GetOrder(string companyCode, int id) => Find(NikooTables.Orders, companyCode, id);

        public static StoreOrder AddOrder(string companyCode, StoreOrder order)
        {
            order.Id = 0;
            return Save(NikooTables.Orders, companyCode, order, (o, stored) =>
            {
                o.CreatedAt = DateTime.Now;
                o.OrderNumber = companyCode.ToUpperInvariant() + "-" + o.CreatedAt.ToShamsiCompact() + "-" + o.Id.ToString("D4");
            });
        }

        public static bool UpdateOrder(string companyCode, StoreOrder order) => Update(NikooTables.Orders, companyCode, order);

        /// <summary>
        /// Marks a verified order as paid (digital-only orders are completed at once, as there is nothing to ship) and takes its
        /// items out of stock; false when it was already paid, so a repeated payment callback changes nothing.
        /// </summary>
        public static bool MarkOrderPaid(string companyCode, int orderId, long refId, string message)
        {
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var order = NikooDb.Get(NikooTables.Orders, NikooDb.Key(companyCode, orderId), tx);
                if (order == null || StoreOrder.Fulfilled.Contains(order.Status)) return false;
                order.Status = order.IsDigitalOnly() ? StoreOrderStatus.Completed : StoreOrderStatus.Paid;
                order.RefId = refId;
                order.PaidAt = DateTime.Now;
                order.PaymentMessage = message;
                if (!order.StockDeducted) MoveStock(companyCode, order, +1, tx);
                NikooDb.Update(NikooTables.Orders, order, companyCode, tx);
                tx.Commit();
                PostOnlineSale(companyCode, orderId);
                return true;
            }
        }

        /// <summary>
        /// Online payment is off: physical orders go to processing (and out of stock) at once; orders with digital items wait
        /// until the admin confirms the payment, because confirming them gives access to the files.
        /// </summary>
        public static void ConfirmOrderWithoutPayment(string companyCode, StoreOrder order)
        {
            if (order.Items.Any(i => i.IsDigital))
            {
                order.PaymentMessage = "پرداخت آنلاین غیرفعال است؛ پس از تأیید پرداخت توسط فروشنده، دسترسی فعال می‌شود.";
                UpdateOrder(companyCode, order);
                return;
            }
            SetOrderStatus(companyCode, order.Id, StoreOrderStatus.Processing, order.TrackingCode, order.AdminNote, "پرداخت آنلاین غیرفعال — سفارش ثبت شد.");
        }

        /// <summary>
        /// Changes an order's status from the admin panel. Moving into a fulfilled status takes the items out of stock and counts
        /// the sale; cancelling or failing a fulfilled order puts them back.
        /// </summary>
        public static StoreOrder SetOrderStatus(string companyCode, int orderId, StoreOrderStatus status, string trackingCode, string adminNote, string paymentMessage = null)
        {
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var order = NikooDb.Get(NikooTables.Orders, NikooDb.Key(companyCode, orderId), tx);
                if (order == null) return null;

                var wasFulfilled = StoreOrder.Fulfilled.Contains(order.Status);
                var isFulfilled = StoreOrder.Fulfilled.Contains(status);
                if (!wasFulfilled && isFulfilled && !order.StockDeducted) MoveStock(companyCode, order, +1, tx);
                else if (wasFulfilled && !isFulfilled && order.StockDeducted) MoveStock(companyCode, order, -1, tx);

                order.Status = status;
                order.TrackingCode = string.IsNullOrWhiteSpace(trackingCode) ? null : trackingCode.Trim();
                order.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
                if (paymentMessage != null) order.PaymentMessage = paymentMessage;
                NikooDb.Update(NikooTables.Orders, order, companyCode, tx);
                tx.Commit();
                if (!wasFulfilled && isFulfilled) PostOnlineSale(companyCode, orderId);
                else if (wasFulfilled && !isFulfilled) VoidOnlineSale(companyCode, orderId);
                return order;
            }
        }

        /// <summary><paramref name="direction"/> +1 sells the order's items (stock down, sales up); -1 undoes it.</summary>
        private static void MoveStock(string companyCode, StoreOrder order, int direction, DbTx tx)
        {
            foreach (var item in order.Items ?? new List<StoreOrderItem>())
            {
                var key = NikooDb.Key(companyCode, item.ProductId);
                NikooDb.Increment(NikooTables.Products, key, "SoldCount", direction * item.Quantity, tx);
                if (item.IsDigital) continue;
                NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.Products.Name)
                    + " SET [Stock] = CASE WHEN [Stock] - @Amount < 0 THEN 0 ELSE [Stock] - @Amount END"
                    + " WHERE [CompanyCode] = @CompanyCode AND [Id] = @Id",
                    new Row(key) { ["Amount"] = direction * item.Quantity }, tx);
            }
            order.StockDeducted = direction > 0;
        }

        // ---- Reviews

        public static StoreProductReview GetReview(string companyCode, int id) => Find(NikooTables.Reviews, companyCode, id);

        public static List<StoreProductReview> GetProductReviews(string companyCode, int productId)
        {
            var filter = NikooDb.Key(companyCode);
            filter["ProductId"] = productId;
            filter["IsApproved"] = true;
            return NikooDb.Query(NikooTables.Reviews, filter, "[CreatedAt] DESC, [Id]");
        }

        /// <summary>Average and count of the approved reviews, per product id.</summary>
        public static Dictionary<int, ProductRating> GetRatings(string companyCode)
        {
            return NikooDb.Select(
                "SELECT [ProductId], AVG(CAST([Rating] AS FLOAT)), COUNT(*) FROM " + NikooDb.Table(NikooTables.Reviews.Name)
                + " WHERE [CompanyCode] = @CompanyCode AND [IsApproved] = 1 GROUP BY [ProductId]",
                NikooDb.Key(companyCode),
                r => new KeyValuePair<int, ProductRating>(r.GetInt32(0), new ProductRating(r.GetDouble(1), r.GetInt32(2))))
                .ToDictionary(p => p.Key, p => p.Value);
        }

        public static StoreProductReview AddReview(string companyCode, StoreProductReview review)
        {
            review.Id = 0;
            review.Rating = Math.Max(1, Math.Min(5, review.Rating));
            return Save(NikooTables.Reviews, companyCode, review, (r, stored) => r.CreatedAt = DateTime.Now);
        }

        public static bool UpdateReview(string companyCode, StoreProductReview review) => Update(NikooTables.Reviews, companyCode, review);

        public static bool DeleteReview(string companyCode, int id) => Remove(NikooTables.Reviews, companyCode, id);

        // ---- Seeding

        /// <summary>فروشگاه‌های نمونه، فقط وقتی هیچ فروشگاهی در دیتابیس نیست</summary>
        internal static void SeedDefaults()
        {
            if (NikooDb.Exists(NikooTables.Tenants)) return;
            AddDemo("FASHION", "بوتیک مد نیکو", "fashion");
            AddDemo("GROCERY", "سوپرمارکت آنلاین نیکو", "grocery");
            AddDemo("DIGITAL", "فروشگاه دیجیتال نیکو", "digital");
        }

        /// <summary>One demo site for each new kind of site (courses, e-books, news, company), next to the existing shop demos.</summary>
        internal static void SeedKindDemos()
        {
            AddDemo("ACADEMY", "آکادمی نیکو", "academy");
            AddDemo("BOOKS", "کتابخانه نیکو", "ebook");
            AddDemo("NEWS", "خبرگزاری نیکو", "news");
            AddDemo("COMPANY", "شرکت نیکو", "corporate");
        }

        /// <summary>The demo of the booking product, with a week of sample appointments so its calendar is not empty.</summary>
        internal static void SeedBookingDemo()
        {
            if (CompanyCodeExists("CLINIC")) return;
            AddDemo("CLINIC", "کلینیک نیکو", "clinic");
            RefreshSampleAppointments("CLINIC");
        }

        /// <summary>The demo of the academy and sports-club product, with sample private lessons on the calendar.</summary>
        internal static void SeedClubDemo()
        {
            if (CompanyCodeExists("CLUB")) return;
            AddDemo("CLUB", "باشگاه و آموزشگاه نیکو", "club");
            RefreshSampleAppointments("CLUB");
        }

        /// <summary>
        /// Keeps the sample appointments of a demo around the current week: once none of them is upcoming, they are replaced
        /// by a fresh set from three days ago to four days ahead. Bookings made by visitors are left alone.
        /// </summary>
        public static void RefreshSampleAppointments(string code)
        {
            var key = NikooDb.Key(code);
            key["Now"] = DateTime.Now;
            var upcoming = NikooDb.Column<int>("SELECT COUNT(*) FROM " + Table(NikooTables.Appointments)
                + " WHERE [CompanyCode] = @CompanyCode AND [IsSample] = 1 AND [StartAt] > @Now", key).FirstOrDefault();
            if (upcoming > 0) return;

            var services = GetServices(code);
            var staff = GetStaff(code);
            var names = new[] { "مهسا رحیمی", "رضا کاظمی", "فاطمه حسینی", "امیر مرادی", "زهرا قاسمی", "حسین نجفی", "مینا اکبری", "علی‌اکبر شریفی" };
            var today = DateTime.Today;
            var rows = new List<StoreAppointment>();
            var n = 0;
            for (var offset = -3; offset <= 4; offset++)
            {
                var day = today.AddDays(offset);
                foreach (var person in staff)
                {
                    var hours = person.HoursOn(BookingTime.DayOf(day)).FirstOrDefault();
                    var service = services.FirstOrDefault(s => s.OfferedBy(person.Id) && s.StaffIds.Count > 0);
                    if (hours == null || service == null) continue;
                    for (var k = 0; k < 2; k++)
                    {
                        var start = day.AddMinutes(hours.StartMinute + k * 90);
                        var past = start < DateTime.Now;
                        n++;
                        rows.Add(new StoreAppointment
                        {
                            Code = "DEMO" + n.ToString("0000"),
                            ServiceId = service.Id,
                            ServiceTitle = service.Title,
                            StaffId = person.Id,
                            StaffName = person.Name,
                            StartAt = start,
                            EndAt = start.AddMinutes(service.DurationMinutes),
                            Price = service.Price,
                            CustomerName = names[n % names.Length],
                            CustomerMobile = "0912" + (1000000 + n * 7919 % 9000000).ToString("0000000"),
                            Status = past ? (n % 5 == 0 ? AppointmentStatus.NoShow : AppointmentStatus.Completed) : AppointmentStatus.Confirmed,
                            Source = n % 3 == 0 ? "admin" : "online",
                            IsSample = true,
                            CreatedAt = start.AddDays(-2)
                        });
                    }
                }
            }
            lock (LockFor(code))
            using (var tx = NikooDb.Begin())
            {
                var samples = NikooDb.Key(code);
                samples["IsSample"] = true;
                NikooDb.Delete(NikooTables.Appointments, samples, tx);
                foreach (var row in rows)
                {
                    row.Id = NikooDb.NextId(NikooTables.Appointments, code, tx);
                    NikooDb.Insert(NikooTables.Appointments, row, code, tx);
                }
                tx.Commit();
            }
        }

        private static void AddDemo(string code, string name, string storeType)
        {
            if (CompanyCodeExists(code)) return;
            NikooDb.Insert(NikooTables.Tenants, new TenantInfo { CompanyCode = code, Name = name, StoreType = storeType, CustomDomain = "", IsActive = true });
            SeedNewTenant(code, storeType, null);
        }

        /// <summary>انتقال یک‌باره داده‌های قدیمی App_Data (tenants.json و پوشه Tenants) به دیتابیس</summary>
        internal static void ImportLegacyJson(string appData)
        {
            using (var tx = NikooDb.Begin())
            {
                foreach (var t in LegacyJsonImport.Read<List<TenantInfo>>(Path.Combine(appData, "tenants.json")) ?? new List<TenantInfo>())
                {
                    if (string.IsNullOrWhiteSpace(t.CompanyCode) || NikooDb.Exists(NikooTables.Tenants, NikooDb.Key(t.CompanyCode), tx)) continue;
                    NikooDb.Insert(NikooTables.Tenants, t, tx: tx);
                }

                var root = Path.Combine(appData, "Tenants");
                if (Directory.Exists(root))
                {
                    foreach (var dir in Directory.GetDirectories(root))
                    {
                        var code = Path.GetFileName(dir);
                        var settings = LegacyJsonImport.Read<StoreSettings>(Path.Combine(dir, "settings.json"));
                        if (settings != null && !NikooDb.Exists(NikooTables.Settings, NikooDb.Key(code), tx))
                        {
                            settings.CompanyCode = code;
                            WriteSettings(settings, tx);
                        }

                        NikooDb.InsertMissing(NikooTables.Categories, code, LegacyJsonImport.Read<List<StoreCategory>>(Path.Combine(dir, "categories.json")), tx);
                        NikooDb.InsertMissing(NikooTables.AttributeDefs, code, LegacyJsonImport.Read<List<StoreAttributeDef>>(Path.Combine(dir, "attributes.json")), tx);
                        NikooDb.InsertMissing(NikooTables.Products, code, LegacyJsonImport.Read<List<StoreProduct>>(Path.Combine(dir, "products.json")), tx);
                        NikooDb.InsertMissing(NikooTables.Collections, code, LegacyJsonImport.Read<List<StoreCollection>>(Path.Combine(dir, "collections.json")), tx);
                        NikooDb.InsertMissing(NikooTables.MenuItems, code, LegacyJsonImport.Read<List<StoreMenuItem>>(Path.Combine(dir, "menus.json")), tx);
                        NikooDb.InsertMissing(NikooTables.Pages, code, LegacyJsonImport.Read<List<StorePage>>(Path.Combine(dir, "pages.json")), tx);
                        NikooDb.InsertMissing(NikooTables.GalleryItems, code, LegacyJsonImport.Read<List<StoreGalleryItem>>(Path.Combine(dir, "gallery.json")), tx);
                        NikooDb.InsertMissing(NikooTables.Customers, code, LegacyJsonImport.Read<List<StoreCustomer>>(Path.Combine(dir, "customers.json")), tx);
                        NikooDb.InsertMissing(NikooTables.Orders, code, LegacyJsonImport.Read<List<StoreOrder>>(Path.Combine(dir, "orders.json")), tx);
                        NikooDb.InsertMissing(NikooTables.Reviews, code, LegacyJsonImport.Read<List<StoreProductReview>>(Path.Combine(dir, "reviews.json")), tx);
                    }
                }
                tx.Commit();
            }
        }

        public static void EnsureTenantSeed(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return;
            lock (SeededTenants)
                if (SeededTenants.Contains(companyCode)) return;

            lock (LockFor(companyCode))
            {
                if (!NikooDb.Exists(NikooTables.Settings, NikooDb.Key(companyCode)))
                    SeedTenantData(companyCode.ToUpperInvariant());
            }
            lock (SeededTenants) SeededTenants.Add(companyCode);
        }

        private static void SeedTenantData(string code, string storeType = null, Action<StoreSettings> customize = null)
        {
            var type = storeType ?? NikooDb.Get(NikooTables.Tenants, NikooDb.Key(code))?.StoreType
                ?? (code == "FASHION" ? "fashion" : code == "GROCERY" ? "grocery" : "digital");
            var seed = TenantSeeds.For(code, type, customize);

            using (var tx = NikooDb.Begin())
            {
                WriteSettings(seed.Settings, tx);
                NikooDb.SyncList(NikooTables.Categories, code, seed.Categories, tx);
                NikooDb.SyncList(NikooTables.AttributeDefs, code, seed.Attributes, tx);
                NikooDb.SyncList(NikooTables.Products, code, seed.Products, tx);
                NikooDb.SyncList(NikooTables.MenuItems, code, seed.Menus, tx);
                NikooDb.SyncList(NikooTables.Pages, code, seed.Pages, tx);
                NikooDb.SyncList(NikooTables.GalleryItems, code, seed.Gallery, tx);
                NikooDb.SyncList(NikooTables.Posts, code, seed.Posts, tx);
                NikooDb.SyncList(NikooTables.Staff, code, seed.Staff, tx);
                NikooDb.SyncList(NikooTables.Services, code, seed.Services, tx);
                if (seed.Classes.Count > 0) NikooDb.SyncList(NikooTables.Classes, code, seed.Classes, tx);
                if (seed.Facilities.Count > 0) NikooDb.SyncList(NikooTables.Facilities, code, seed.Facilities, tx);
                tx.Commit();
            }
        }
    }
}
