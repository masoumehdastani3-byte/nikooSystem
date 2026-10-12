using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>Paged, filtered and sorted lists: every page is cut by SQL Server, never in memory.</summary>
    public static partial class TenantStore
    {
        private static PagedList<T> Page<T>(EntityMap<T> map, string companyCode, SqlFilter filter, string orderBy, int page, int pageSize)
            where T : class, new()
        {
            EnsureTenantSeed(companyCode);
            return NikooDb.Page(map, filter, orderBy, page, pageSize);
        }

        private static PagedList<T> Grid<T>(EntityMap<T> map, string companyCode, GridQuery query, SqlFilter filter,
            IDictionary<string, string> sorts, string defaultSort, bool defaultDescending) where T : class, new()
        {
            return Page(map, companyCode, filter, query.OrderBy(sorts, defaultSort, defaultDescending), query.Page, query.PageSize);
        }

        private static string Table(TableMap map) => NikooDb.Table(map.Name);

        // ---- Products

        private static readonly Dictionary<string, string> ProductSorts = new Dictionary<string, string>
        {
            { "title", "t.[Title]" },
            { "price", "t.[Price]" },
            { "stock", "t.[Stock]" },
            { "sold", "t.[SoldCount]" },
            { "created", "t.[CreatedAt]" }
        };

        public static PagedList<StoreProduct> ProductGrid(string companyCode, ProductGridQuery query, int lowStockThreshold)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Title", "Sku", "Slug", "Author");
            if (query.CategoryId.HasValue) filter.In("CategoryId", CategoryTree(companyCode, query.CategoryId.Value));
            if (query.Type.HasValue) filter.Eq("ProductType", query.Type.Value.ToString());
            switch (query.Status)
            {
                case "active": filter.Eq("IsActive", true); break;
                case "inactive": filter.Eq("IsActive", false); break;
                case "featured": filter.Eq("IsFeatured", true); break;
                case "sample": filter.Eq("IsSample", true); break;
                case "lowstock": LowStock(filter, lowStockThreshold); break;
            }
            return Grid(NikooTables.Products, companyCode, query, filter, ProductSorts, "created", true);
        }

        private static SqlFilter LowStock(SqlFilter filter, int threshold) =>
            filter.Eq("ProductType", StoreProductType.Physical.ToString()).AtMost("Stock", Math.Max(0, threshold));

        /// <summary>Physical products that are running out, the emptiest first.</summary>
        public static PagedList<StoreProduct> LowStockProducts(string companyCode, int threshold, int take) =>
            Page(NikooTables.Products, companyCode, LowStock(SqlFilter.ForStore(companyCode).Eq("IsActive", true), threshold), "t.[Stock]", 1, take);

        /// <summary>Every product id → title, for pickers that list the whole catalogue.</summary>
        public static Dictionary<int, string> ProductTitles(string companyCode) =>
            NikooDb.Pairs<int, string>("SELECT [Id], [Title] FROM " + Table(NikooTables.Products) + " WHERE [CompanyCode] = @CompanyCode",
                NikooDb.Key(companyCode));

        public static int CountProducts(string companyCode, bool? sample = null)
        {
            var filter = SqlFilter.ForStore(companyCode);
            if (sample.HasValue) filter.Eq("IsSample", sample.Value);
            return NikooDb.Count(NikooTables.Products, filter);
        }

        private static readonly Dictionary<string, string> CatalogSorts = new Dictionary<string, string>
        {
            { "price_asc", "t.[Price] ASC" },
            { "price_desc", "t.[Price] DESC" },
            { "popular", "t.[ViewCount] DESC, t.[IsFeatured] DESC" },
            { "bestseller", "t.[SoldCount] DESC" },
            { "newest", "t.[CreatedAt] DESC" }
        };

        /// <summary>One page of the storefront catalogue (active products only).</summary>
        public static PagedList<StoreProduct> CatalogPage(string companyCode, CatalogQuery query, StoreCollection collection)
        {
            var filter = SqlFilter.ForStore(companyCode).Eq("IsActive", true).Search(query.Q, "Title", "ShortDescription", "Sku", "Author");
            if (query.CategoryId.HasValue) filter.In("CategoryId", CategoryTree(companyCode, query.CategoryId.Value));
            if (collection != null)
                filter.Where("EXISTS (SELECT 1 FROM " + NikooDb.Table("ProductCollections")
                    + " pc WHERE pc.[CompanyCode] = t.[CompanyCode] AND pc.[ProductId] = t.[Id] AND pc.[CollectionId] = {0})", collection.Id);
            if (query.MinPrice.HasValue) filter.AtLeast("Price", query.MinPrice.Value);
            if (query.MaxPrice.HasValue) filter.AtMost("Price", query.MaxPrice.Value);
            if (query.InStock) filter.Where("t.[Stock] > 0 OR t.[ProductType] <> {0}", StoreProductType.Physical.ToString());
            if (query.OnSale) filter.Where("t.[CompareAtPrice] > t.[Price]");
            if (query.Type.HasValue) filter.Eq("ProductType", query.Type.Value.ToString());

            string orderBy;
            if (query.Sort == "rating")
                orderBy = "(SELECT AVG(CAST(r.[Rating] AS FLOAT)) FROM " + Table(NikooTables.Reviews)
                    + " r WHERE r.[CompanyCode] = t.[CompanyCode] AND r.[ProductId] = t.[Id] AND r.[IsApproved] = 1) DESC, t.[SoldCount] DESC";
            else if (!CatalogSorts.TryGetValue(query.Sort ?? "", out orderBy))
                orderBy = CatalogSorts["newest"];

            return Page(NikooTables.Products, companyCode, filter, orderBy, query.Page, CatalogQuery.PageSize);
        }

        /// <summary>Active products of a home page row, chosen by SQL instead of loading the whole catalogue.</summary>
        public static List<StoreProduct> TopProducts(string companyCode, string source, int? collectionId, int? categoryId, int take)
        {
            var filter = SqlFilter.ForStore(companyCode).Eq("IsActive", true);
            var orderBy = "t.[CreatedAt] DESC";
            switch (source)
            {
                case "collection":
                    if (!collectionId.HasValue) return new List<StoreProduct>();
                    filter.Where("EXISTS (SELECT 1 FROM " + NikooDb.Table("ProductCollections")
                        + " pc WHERE pc.[CompanyCode] = t.[CompanyCode] AND pc.[ProductId] = t.[Id] AND pc.[CollectionId] = {0})", collectionId.Value);
                    break;
                case "category":
                    if (!categoryId.HasValue) return new List<StoreProduct>();
                    filter.In("CategoryId", CategoryTree(companyCode, categoryId.Value));
                    break;
                case "bestseller":
                    orderBy = "t.[SoldCount] DESC";
                    break;
                case "onsale":
                    filter.Where("t.[CompareAtPrice] > t.[Price]");
                    orderBy = "CAST(t.[CompareAtPrice] - t.[Price] AS FLOAT) / t.[CompareAtPrice] DESC";
                    break;
                case "featured":
                    filter.Eq("IsFeatured", true);
                    break;
            }
            return Page(NikooTables.Products, companyCode, filter, orderBy, 1, take).ToList();
        }

        /// <summary>Products of the same category or tag as <paramref name="product"/>.</summary>
        public static List<StoreProduct> RelatedProducts(string companyCode, StoreProduct product, int take)
        {
            var filter = SqlFilter.ForStore(companyCode).Eq("IsActive", true).Where("t.[Id] <> {0}", product.Id);
            var tags = product.CollectionIds ?? new List<int>();
            if (tags.Any())
            {
                var row = new List<object> { product.CategoryId };
                row.AddRange(tags.Cast<object>());
                var inTags = string.Join(", ", tags.Select((_, i) => "{" + (i + 1) + "}"));
                filter.Where("t.[CategoryId] = {0} OR EXISTS (SELECT 1 FROM " + NikooDb.Table("ProductCollections")
                    + " pc WHERE pc.[CompanyCode] = t.[CompanyCode] AND pc.[ProductId] = t.[Id] AND pc.[CollectionId] IN (" + inTags + "))", row.ToArray());
            }
            else filter.Eq("CategoryId", product.CategoryId);
            return Page(NikooTables.Products, companyCode, filter, "t.[SoldCount] DESC", 1, take).ToList();
        }

        public static List<StoreProduct> GetProductsByIds(string companyCode, IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            if (!list.Any()) return new List<StoreProduct>();
            return Page(NikooTables.Products, companyCode, SqlFilter.ForStore(companyCode).In("Id", list), null, 1, PagedList<StoreProduct>.MaxPageSize).ToList();
        }

        /// <summary>The latest approved 4- and 5-star reviews with a real comment, each with its (active) product.</summary>
        public static List<KeyValuePair<StoreProductReview, StoreProduct>> HomeReviews(string companyCode, int take)
        {
            var filter = SqlFilter.ForStore(companyCode).Eq("IsApproved", true).AtLeast("Rating", 4).Where("LEN(t.[Comment]) >= {0}", 15);
            var reviews = Page(NikooTables.Reviews, companyCode, filter, "t.[CreatedAt] DESC, t.[Id] DESC", 1, take * 2).ToList();
            var products = GetProductsByIds(companyCode, reviews.Select(r => r.ProductId)).Where(p => p.IsActive).ToDictionary(p => p.Id);
            return reviews
                .Where(r => products.ContainsKey(r.ProductId))
                .Take(take)
                .Select(r => new KeyValuePair<StoreProductReview, StoreProduct>(r, products[r.ProductId]))
                .ToList();
        }

        /// <summary>Active products per category id (not counting sub-categories).</summary>
        public static Dictionary<int, int> ProductCountsByCategory(string companyCode) =>
            NikooDb.Pairs<int, int>("SELECT [CategoryId], COUNT(*) FROM " + Table(NikooTables.Products)
                + " WHERE [CompanyCode] = @CompanyCode AND [IsActive] = 1 GROUP BY [CategoryId]", NikooDb.Key(companyCode));

        /// <summary>All products, or all posts, per category id (not counting sub-categories).</summary>
        public static Dictionary<int, int> ItemCountsByCategory(string companyCode, bool posts) =>
            NikooDb.Pairs<int, int>("SELECT [CategoryId], COUNT(*) FROM " + Table(posts ? (TableMap)NikooTables.Posts : NikooTables.Products)
                + " WHERE [CompanyCode] = @CompanyCode AND [CategoryId] IS NOT NULL GROUP BY [CategoryId]", NikooDb.Key(companyCode));

        /// <summary>Products per collection id.</summary>
        public static Dictionary<int, int> ProductCountsByCollection(string companyCode) =>
            NikooDb.Pairs<int, int>("SELECT [CollectionId], COUNT(*) FROM " + NikooDb.Table("ProductCollections")
                + " WHERE [CompanyCode] = @CompanyCode GROUP BY [CollectionId]", NikooDb.Key(companyCode));

        private static HashSet<int> CategoryTree(string companyCode, int rootId) =>
            TreeHelper.SubtreeIds(GetCategories(companyCode), rootId);

        // ---- Orders and customers

        private static readonly Dictionary<string, string> OrderSorts = new Dictionary<string, string>
        {
            { "number", "t.[Id]" },
            { "customer", "t.[CustomerName]" },
            { "amount", "t.[Amount]" },
            { "status", "t.[Status]" },
            { "date", "t.[CreatedAt]" }
        };

        public static PagedList<StoreOrder> OrderGrid(string companyCode, OrderGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "OrderNumber", "CustomerName", "CustomerMobile", "TrackingCode");
            if (query.Status.HasValue) filter.Eq("Status", query.Status.Value.ToString());
            return Grid(NikooTables.Orders, companyCode, query, filter, OrderSorts, "date", true);
        }

        public static List<StoreOrder> RecentOrders(string companyCode, int take) =>
            Page(NikooTables.Orders, companyCode, SqlFilter.ForStore(companyCode), "t.[CreatedAt] DESC", 1, take).ToList();

        public static PagedList<StoreOrder> CustomerOrders(string companyCode, int customerId, int page, int pageSize = 10) =>
            Page(NikooTables.Orders, companyCode, SqlFilter.ForStore(companyCode).Eq("CustomerId", customerId), "t.[CreatedAt] DESC", page, pageSize);

        public static int CountOrders(string companyCode, StoreOrderStatus? status = null)
        {
            var filter = SqlFilter.ForStore(companyCode);
            if (status.HasValue) filter.Eq("Status", status.Value.ToString());
            return NikooDb.Count(NikooTables.Orders, filter);
        }

        /// <summary>Sum of paid and fulfilled orders.</summary>
        public static long Revenue(string companyCode)
        {
            var key = NikooDb.Key(companyCode);
            var statuses = string.Join(", ", StoreOrder.Fulfilled.Select((s, i) =>
            {
                key["s" + i] = s.ToString();
                return "@s" + i;
            }));
            return NikooDb.Column<long>("SELECT ISNULL(SUM([Amount]), 0) FROM " + Table(NikooTables.Orders)
                + " WHERE [CompanyCode] = @CompanyCode AND [Status] IN (" + statuses + ")", key).FirstOrDefault();
        }

        private static readonly Dictionary<string, string> CustomerSorts = new Dictionary<string, string>
        {
            { "name", "t.[FullName]" },
            { "mobile", "t.[Mobile]" },
            { "created", "t.[CreatedAt]" }
        };

        public static PagedList<StoreCustomer> CustomerGrid(string companyCode, GridQuery query) =>
            Grid(NikooTables.Customers, companyCode, query,
                SqlFilter.ForStore(companyCode).Search(query.Q, "FullName", "Mobile", "Email"),
                CustomerSorts, "created", true);

        public static int CountCustomers(string companyCode) => NikooDb.Count(NikooTables.Customers, SqlFilter.ForStore(companyCode));

        /// <summary>Number of orders per customer id, for the customers on one page.</summary>
        public static Dictionary<int, int> OrderCounts(string companyCode, IEnumerable<int> customerIds)
        {
            var ids = customerIds.Distinct().ToList();
            if (!ids.Any()) return new Dictionary<int, int>();
            var key = NikooDb.Key(companyCode);
            var list = string.Join(", ", ids.Select((id, i) =>
            {
                key["c" + i] = id;
                return "@c" + i;
            }));
            return NikooDb.Pairs<int, int>("SELECT [CustomerId], COUNT(*) FROM " + Table(NikooTables.Orders)
                + " WHERE [CompanyCode] = @CompanyCode AND [CustomerId] IN (" + list + ") GROUP BY [CustomerId]", key);
        }

        // ---- Digital products the customer has bought

        /// <summary>Products in the customer's paid (not cancelled) orders.</summary>
        public static HashSet<int> OwnedProductIds(string companyCode, int customerId)
        {
            var key = NikooDb.Key(companyCode);
            key["CustomerId"] = customerId;
            var statuses = string.Join(", ", StoreOrder.Fulfilled.Select((s, i) =>
            {
                key["s" + i] = s.ToString();
                return "@s" + i;
            }));
            return new HashSet<int>(NikooDb.Column<int>(
                "SELECT DISTINCT oi.[ProductId] FROM " + NikooDb.Table("OrderItems") + " oi JOIN " + Table(NikooTables.Orders)
                + " o ON o.[CompanyCode] = oi.[CompanyCode] AND o.[Id] = oi.[OrderId]"
                + " WHERE o.[CompanyCode] = @CompanyCode AND o.[CustomerId] = @CustomerId AND o.[Status] IN (" + statuses + ")", key));
        }

        public static bool OwnsProduct(string companyCode, int? customerId, int productId) =>
            customerId.HasValue && OwnedProductIds(companyCode, customerId.Value).Contains(productId);

        // ---- Reviews

        private static readonly Dictionary<string, string> ReviewSorts = new Dictionary<string, string>
        {
            { "author", "t.[AuthorName]" },
            { "rating", "t.[Rating]" },
            { "date", "t.[CreatedAt]" }
        };

        public static PagedList<StoreProductReview> ReviewGrid(string companyCode, ReviewGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "AuthorName", "Comment");
            if (query.State == "pending") filter.Eq("IsApproved", false);
            else if (query.State == "approved") filter.Eq("IsApproved", true);
            return Grid(NikooTables.Reviews, companyCode, query, filter, ReviewSorts, "date", true);
        }

        // ---- Simple lists: collections, attributes, pages, gallery

        private static readonly Dictionary<string, string> TitleSorts = new Dictionary<string, string>
        {
            { "title", "t.[Title]" },
            { "sort", "t.[SortOrder]" }
        };

        public static PagedList<StoreCollection> CollectionGrid(string companyCode, GridQuery query) =>
            Grid(NikooTables.Collections, companyCode, query,
                SqlFilter.ForStore(companyCode).Search(query.Q, "Title", "Slug", "Description"), TitleSorts, "sort", false);

        private static readonly Dictionary<string, string> AttributeSorts = new Dictionary<string, string>
        {
            { "name", "t.[Name]" },
            { "type", "t.[InputType]" },
            { "sort", "t.[SortOrder]" }
        };

        public static PagedList<StoreAttributeDef> AttributeGrid(string companyCode, GridQuery query) =>
            Grid(NikooTables.AttributeDefs, companyCode, query,
                SqlFilter.ForStore(companyCode).Search(query.Q, "Name", "Options"), AttributeSorts, "sort", false);

        private static readonly Dictionary<string, string> PageSorts = new Dictionary<string, string>
        {
            { "title", "t.[Title]" },
            { "slug", "t.[Slug]" },
            { "updated", "t.[UpdatedAt]" }
        };

        public static PagedList<StorePage> PageGrid(string companyCode, GridQuery query) =>
            Grid(NikooTables.Pages, companyCode, query,
                SqlFilter.ForStore(companyCode).Search(query.Q, "Title", "Slug"), PageSorts, "title", false);

        public static PagedList<StoreGalleryItem> GalleryGrid(string companyCode, GalleryGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Title", "Album");
            if (!string.IsNullOrWhiteSpace(query.Album)) filter.Eq("Album", query.Album);
            return Grid(NikooTables.GalleryItems, companyCode, query, filter, TitleSorts, "sort", false);
        }

        public static List<string> GalleryAlbums(string companyCode) =>
            NikooDb.Column<string>("SELECT DISTINCT [Album] FROM " + Table(NikooTables.GalleryItems)
                + " WHERE [CompanyCode] = @CompanyCode ORDER BY [Album]", NikooDb.Key(companyCode));
    }
}
