using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using NikooSystem.Models.Store;
using NikooSystem.Models.Torob;
using Newtonsoft.Json.Linq;

namespace NikooSystem.Services
{
    public static class TorobCatalogService
    {
        public const int PageSize = 100;

        public static string PageUnique(string companyCode, int productId)
        {
            return companyCode + "_" + productId;
        }

        public static int? ParseProductIdFromUnique(string companyCode, string pageUnique)
        {
            if (string.IsNullOrWhiteSpace(pageUnique)) return null;
            var prefix = companyCode + "_";
            if (!pageUnique.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            if (int.TryParse(pageUnique.Substring(prefix.Length), out var id)) return id;
            return null;
        }

        public static string GetPublicBaseUrl(StoreSettings settings, TenantInfo tenant, string companyCode, HttpRequestBase request)
        {
            if (!string.IsNullOrWhiteSpace(settings?.PublicStoreBaseUrl))
                return settings.PublicStoreBaseUrl.Trim().TrimEnd('/');

            if (tenant != null && !string.IsNullOrWhiteSpace(tenant.CustomDomain))
            {
                var scheme = request?.Url?.Scheme ?? "https";
                return scheme + "://" + tenant.CustomDomain.Trim().TrimEnd('/');
            }

            if (request?.Url != null)
                return request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/');

            return "https://localhost/Shop/" + companyCode;
        }

        public static string BuildProductPageUrl(string baseUrl, string companyCode, string slug, TenantInfo tenant)
        {
            baseUrl = (baseUrl ?? "").TrimEnd('/');
            slug = (slug ?? "").Trim('/');
            if (tenant != null && !string.IsNullOrWhiteSpace(tenant.CustomDomain))
                return baseUrl + "/Product/" + slug + "/";
            return baseUrl + "/Shop/" + companyCode + "/Product/" + slug + "/";
        }

        public static string ToAbsoluteUrl(string baseUrl, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;
            baseUrl = (baseUrl ?? "").TrimEnd('/');
            if (!url.StartsWith("/")) url = "/" + url;
            return baseUrl + url;
        }

        public static TorobProductsResponse HandleRequest(string companyCode, JObject body, HttpRequestBase request)
        {
            if (body == null || !body.HasValues)
                return Error("request body is empty");

            var settings = TenantStore.GetSettings(companyCode);
            var tenant = TenantStore.GetTenant(companyCode);
            if (settings == null || !settings.EnableTorob)
                return Error("torob integration is disabled");

            var catalog = TenantStore.GetProducts(companyCode)
                .Where(p => p.IsActive)
                .ToList();
            var categories = TenantStore.GetCategories(companyCode)
                .ToDictionary(c => c.Id, c => c.Title);
            var baseUrl = GetPublicBaseUrl(settings, tenant, companyCode, request);

            if (body["page_urls"] != null)
                return ByUrls(companyCode, body, catalog, categories, baseUrl, tenant);

            if (body["page_uniques"] != null)
                return ByUniques(companyCode, body, catalog, categories, baseUrl, tenant);

            if (body["cursor"] != null || (body["sort"]?.ToString() == "product_id_desc" && body["page"] == null))
                return ByCursor(companyCode, body, catalog, categories, baseUrl, tenant);

            if (body["page"] != null)
                return ByPage(companyCode, body, catalog, categories, baseUrl, tenant);

            return Error("invalid request parameters");
        }

        private static TorobProductsResponse ByUrls(string companyCode, JObject body, List<StoreProduct> catalog,
            Dictionary<int, string> categories, string baseUrl, TenantInfo tenant)
        {
            var urls = body["page_urls"]?.ToObject<List<string>>() ?? new List<string>();
            if (urls.Count < 1) return Error("page_urls must contain at least one item");

            var normalized = urls.Select(NormalizeUrl).ToList();
            var products = catalog
                .Where(p => normalized.Contains(NormalizeUrl(BuildProductPageUrl(baseUrl, companyCode, p.Slug, tenant))))
                .Select(p => MapProduct(companyCode, p, categories, baseUrl, tenant))
                .ToList();

            return OkPage(products, 1, products.Count);
        }

        private static TorobProductsResponse ByUniques(string companyCode, JObject body, List<StoreProduct> catalog,
            Dictionary<int, string> categories, string baseUrl, TenantInfo tenant)
        {
            var uniques = body["page_uniques"]?.ToObject<List<string>>() ?? new List<string>();
            if (uniques.Count < 1) return Error("page_uniques must contain at least one item");

            var idSet = new HashSet<int>();
            foreach (var u in uniques)
            {
                var id = ParseProductIdFromUnique(companyCode, u);
                if (id.HasValue) idSet.Add(id.Value);
            }

            var products = catalog
                .Where(p => idSet.Contains(p.Id))
                .Select(p => MapProduct(companyCode, p, categories, baseUrl, tenant))
                .ToList();

            return OkPage(products, 1, products.Count);
        }

        private static TorobProductsResponse ByPage(string companyCode, JObject body, List<StoreProduct> catalog,
            Dictionary<int, string> categories, string baseUrl, TenantInfo tenant)
        {
            var sort = body["sort"]?.ToString();
            if (string.IsNullOrWhiteSpace(sort)) return Error("sort parameter is not provided");

            if (!int.TryParse(body["page"]?.ToString(), out var page) || page < 1)
                return Error("page parameter is invalid");

            var sorted = SortCatalog(catalog, sort).ToList();
            var total = sorted.Count;
            var maxPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)PageSize);
            var slice = sorted.Skip((page - 1) * PageSize).Take(PageSize).ToList();
            var products = slice.Select(p => MapProduct(companyCode, p, categories, baseUrl, tenant)).ToList();

            return new TorobProductsResponse
            {
                CurrentPage = page,
                Total = total,
                MaxPages = maxPages,
                Products = products
            };
        }

        private static TorobProductsResponse ByCursor(string companyCode, JObject body, List<StoreProduct> catalog,
            Dictionary<int, string> categories, string baseUrl, TenantInfo tenant)
        {
            var sort = body["sort"]?.ToString();
            if (sort != "product_id_desc") return Error("cursor pagination requires sort product_id_desc");

            if (body["page"] != null || body["limit"] != null || body["size"] != null)
                return Error("cursor pagination must not include page, limit, or size");

            var sorted = SortCatalog(catalog, sort).ToList();
            IEnumerable<StoreProduct> query = sorted;

            var cursor = body["cursor"]?.ToString();
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                if (!int.TryParse(cursor, out var cursorId))
                    return Error("cursor is invalid");
                query = sorted.Where(p => p.Id < cursorId);
            }

            var slice = query.Take(PageSize).ToList();
            var products = slice.Select(p => MapProduct(companyCode, p, categories, baseUrl, tenant)).ToList();
            string nextCursor = null;
            if (slice.Count == PageSize)
            {
                var last = slice[slice.Count - 1];
                if (sorted.Any(p => p.Id < last.Id))
                    nextCursor = last.Id.ToString(CultureInfo.InvariantCulture);
            }

            return new TorobProductsResponse
            {
                Total = sorted.Count,
                MaxPages = null,
                CurrentPage = null,
                NextCursor = nextCursor,
                Products = products
            };
        }

        private static IEnumerable<StoreProduct> SortCatalog(IEnumerable<StoreProduct> catalog, string sort)
        {
            switch (sort)
            {
                case "date_updated_desc":
                    return catalog.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt);
                case "product_id_desc":
                    return catalog.OrderByDescending(p => p.Id);
                case "date_added_desc":
                default:
                    return catalog.OrderByDescending(p => p.CreatedAt);
            }
        }

        private static TorobProductDto MapProduct(string companyCode, StoreProduct p, Dictionary<int, string> categories,
            string baseUrl, TenantInfo tenant)
        {
            var available = p.IsActive && p.Stock > 0;
            var images = (p.Images ?? new List<StoreProductImage>())
                .OrderBy(i => i.SortOrder)
                .Select(i => ToAbsoluteUrl(baseUrl, string.IsNullOrWhiteSpace(i.LargeUrl) ? i.ThumbUrl : i.LargeUrl))
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (images.Count == 0)
                images.Add(ToAbsoluteUrl(baseUrl, "/Assets/img/portfolio/portfolio-1.jpg"));

            var spec = new Dictionary<string, string>();
            foreach (var a in p.Attributes ?? new List<StoreProductAttribute>())
            {
                if (string.IsNullOrWhiteSpace(a?.Name)) continue;
                spec[a.Name.Trim()] = (a.Value ?? "").Trim();
            }

            var added = p.CreatedAt;
            var updated = p.UpdatedAt ?? p.CreatedAt;
            categories.TryGetValue(p.CategoryId, out var catName);

            return new TorobProductDto
            {
                PageUnique = PageUnique(companyCode, p.Id),
                PageUrl = BuildProductPageUrl(baseUrl, companyCode, p.Slug, tenant),
                ProductGroupId = p.Id.ToString(CultureInfo.InvariantCulture),
                Title = Truncate(p.Title, 500),
                Subtitle = Truncate(p.ShortDescription, 500),
                CurrentPrice = available ? p.Price : 0,
                OldPrice = p.CompareAtPrice.HasValue && p.CompareAtPrice.Value > p.Price ? p.CompareAtPrice : null,
                Availability = available,
                CategoryName = Truncate(catName, 200),
                ImageLinks = images,
                ShortDesc = Truncate(p.ShortDescription, 500),
                Spec = spec,
                DateAdded = FormatTorobDate(added),
                DateUpdated = FormatTorobDate(updated)
            };
        }

        private static string FormatTorobDate(DateTime dt)
        {
            var offset = new DateTimeOffset(dt, TimeSpan.FromHours(3.5));
            return offset.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            url = url.Trim().TrimEnd('/');
            return url.ToLowerInvariant();
        }

        private static TorobProductsResponse OkPage(List<TorobProductDto> products, int page, int total)
        {
            var maxPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)PageSize);
            return new TorobProductsResponse
            {
                CurrentPage = page,
                Total = total,
                MaxPages = maxPages == 0 && total > 0 ? 1 : maxPages,
                Products = products
            };
        }

        private static TorobProductsResponse Error(string message)
        {
            throw new TorobApiException(message);
        }
    }

    public class TorobApiException : Exception
    {
        public TorobApiException(string message) : base(message) { }
    }
}
