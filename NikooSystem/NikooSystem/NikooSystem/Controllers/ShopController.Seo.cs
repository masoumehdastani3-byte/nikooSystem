using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// What each public page of a site tells search engines: its own title and description, one canonical address
    /// (search, sort and filter variants point to the plain list) and structured data. Texts are in <see cref="SeoPages"/>.
    /// </summary>
    public partial class ShopController
    {
        private SeoInfo PageSeo => Seo.Of(HttpContext);

        private string HomePath => Url.Action("Index", "Shop", new { company = CompanyCode });

        private void SeoHome()
        {
            var seo = PageSeo;
            var settings = CurrentSettings;
            seo.Title = SeoPages.StoreHomeTitle(settings);
            seo.Description = SeoPages.StoreHomeDescription(settings);
            seo.Canonical = seo.Url(HomePath);
            seo.Schemas.Add(SeoPages.WebSite(seo, settings.StoreName, seo.Description));
            seo.Schemas.Add(SeoPages.StoreOrganization(seo, settings, Tenant));
        }

        private void SeoCatalog(CatalogQuery query, StoreCategory category, StoreCollection collection, int page)
        {
            var seo = PageSeo;
            if (collection == null && category != null) SeoCategory(category);
            seo.Page = page;
            if (!string.IsNullOrWhiteSpace(query.Q)) seo.NoIndex = true;

            var keep = new List<string>();
            if (category != null) keep.Add("categoryId=" + category.Id);
            if (collection != null) keep.Add("collectionId=" + collection.Id);
            if (query.Type.HasValue) keep.Add("type=" + query.Type.Value);
            if (page > 1) keep.Add("page=" + page);
            var listPath = Url.Action("Catalog", "Shop", new { company = CompanyCode });
            seo.Canonical = seo.Url(listPath + QueryString(keep));

            var current = collection?.Title ?? category?.Title;
            if (current != null)
                seo.Schemas.Add(SeoPages.Breadcrumbs(seo, SeoPages.Crumb("خانه", HomePath), SeoPages.Crumb(Kind.ItemsLabel, listPath), SeoPages.Crumb(current, null)));
        }

        private void SeoProduct(StoreProduct product, StoreCategory category, IList<StoreProductReview> reviews)
        {
            var seo = PageSeo;
            var settings = CurrentSettings;
            seo.Title = Clean(product.SeoTitle);
            seo.Description = SeoPages.ProductDescription(product, settings.StoreName);
            seo.Type = "product";
            seo.Price = product.Price;
            var image = (product.Images ?? new List<StoreProductImage>()).OrderBy(i => i.SortOrder).FirstOrDefault();
            seo.Image = image?.LargeUrl ?? image?.ThumbUrl;
            seo.Canonical = seo.Url(Url.RouteUrl("ShopProduct", new { company = CompanyCode, slug = product.Slug }));
            var showReviews = HomeSections.LayoutOf(settings).ShowReviews;
            seo.Schemas.Add(SeoPages.Product(seo, product, settings, category, showReviews ? reviews : null));
            seo.Schemas.Add(Crumbs(product.Title, category, "Catalog"));
            if (Kind.IsShop && settings.EnableTorob) TorobMeta(product);
        }

        /// <summary>The tags Torob's crawler reads on a product page (price in Toman); product_id is the API's page_unique.</summary>
        private void TorobMeta(StoreProduct product)
        {
            var available = product.IsActive && product.Stock > 0;
            Action<string, string> add = (name, value) => PageSeo.Meta.Add(new KeyValuePair<string, string>(name, value));
            add("product_id", TorobCatalogService.PageUnique(CompanyCode, product.Id));
            add("product_name", product.Title);
            add("product_price", (available ? product.Price : 0).ToString(CultureInfo.InvariantCulture));
            if (product.CompareAtPrice > product.Price)
                add("product_old_price", product.CompareAtPrice.Value.ToString(CultureInfo.InvariantCulture));
            add("availability", available ? "instock" : "outofstock");
        }

        private void SeoBlog(PostQuery query, StoreCategory category, int page)
        {
            var seo = PageSeo;
            var store = CurrentSettings.StoreName;
            if (category != null) SeoCategory(category);
            else if (!string.IsNullOrWhiteSpace(query.Tag)) seo.Description = "خبرها و مطالب «" + query.Tag.Trim() + "» در " + store;
            else seo.Description = "تازه‌ترین خبرها، مقاله‌ها و گزارش‌های " + store;
            seo.Page = page;
            if (!string.IsNullOrWhiteSpace(query.Q)) seo.NoIndex = true;

            var keep = new List<string>();
            if (category != null) keep.Add("categoryId=" + category.Id);
            if (!string.IsNullOrWhiteSpace(query.Tag)) keep.Add("tag=" + Uri.EscapeDataString(query.Tag.Trim()));
            if (page > 1) keep.Add("page=" + page);
            var listPath = Url.Action("Blog", "Shop", new { company = CompanyCode });
            seo.Canonical = seo.Url(listPath + QueryString(keep));
            if (category != null)
                seo.Schemas.Add(SeoPages.Breadcrumbs(seo, SeoPages.Crumb("خانه", HomePath), SeoPages.Crumb(Kind.ItemsLabel, listPath), SeoPages.Crumb(category.Title, null)));
        }

        private void SeoPost(StorePost post, StoreCategory category)
        {
            var seo = PageSeo;
            seo.Title = Clean(post.SeoTitle);
            seo.Description = SeoPages.PostDescription(post);
            seo.Type = "article";
            seo.Image = post.CoverImageUrl;
            seo.PublishedAt = post.PublishedAt;
            seo.ModifiedAt = post.UpdatedAt ?? post.PublishedAt;
            seo.Canonical = seo.Url(Url.RouteUrl("ShopPost", new { company = CompanyCode, slug = post.Slug }));
            seo.Schemas.Add(SeoPages.Article(seo, post, CurrentSettings, category, Tenant?.StoreType == "news"));
            seo.Schemas.Add(Crumbs(post.Title, category, "Blog"));
        }

        private void SeoStorePage(StorePage page)
        {
            var seo = PageSeo;
            seo.Title = Clean(page.SeoTitle);
            seo.Description = SeoPages.PageDescription(page, CurrentSettings.StoreName);
            seo.Canonical = seo.Url(Url.RouteUrl("ShopPage", new { company = CompanyCode, slug = page.Slug }));
            seo.Schemas.Add(Crumbs(page.Title, null, null));
        }

        /// <summary>Pages without their own texts (services, booking, gallery, contact): a description and the plain address.</summary>
        private void SeoSimple(string description, string action, params string[] keep)
        {
            var seo = PageSeo;
            seo.Description = description;
            seo.Canonical = seo.Url(Url.Action(action, "Shop", new { company = CompanyCode }) + QueryString(keep));
        }

        private void SeoCategory(StoreCategory category)
        {
            var seo = PageSeo;
            seo.Title = Clean(category.SeoTitle);
            seo.Description = SeoPages.CategoryDescription(category, Kind, CurrentSettings.StoreName);
            if (!string.IsNullOrWhiteSpace(category.ImageUrl)) seo.Image = category.ImageUrl;
        }

        private Dictionary<string, object> Crumbs(string current, StoreCategory category, string listAction)
        {
            var crumbs = new List<Tuple<string, string>> { SeoPages.Crumb("خانه", HomePath) };
            if (category != null)
                crumbs.Add(SeoPages.Crumb(category.Title, Url.Action(listAction, "Shop", new { company = CompanyCode, categoryId = category.Id })));
            crumbs.Add(SeoPages.Crumb(current, null));
            return SeoPages.Breadcrumbs(PageSeo, crumbs.ToArray());
        }

        private static string QueryString(IEnumerable<string> parts)
        {
            var list = parts.Where(p => !string.IsNullOrEmpty(p)).ToList();
            return list.Any() ? "?" + string.Join("&", list) : "";
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
