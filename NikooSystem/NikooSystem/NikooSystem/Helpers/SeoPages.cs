using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NikooSystem.Models;
using NikooSystem.Models.Store;

namespace NikooSystem.Helpers
{
    /// <summary>
    /// The titles and descriptions search engines get when the owner leaves the SEO fields empty, and the schema.org
    /// structured data (JSON-LD) of the main site and the stores. The admin forms show the same defaults as placeholders.
    /// </summary>
    public static class SeoPages
    {
        public const string Separator = " | ";

        // ---- Titles and descriptions of stores

        public static string StoreHomeTitle(StoreSettings s) =>
            Clean(s.VerifyHomeTitle) ?? Clean(s.SeoTitle) ?? s.StoreName + (string.IsNullOrWhiteSpace(s.Tagline) ? "" : Separator + s.Tagline.Trim());

        public static string StoreHomeDescription(StoreSettings s) =>
            Seo.FirstText(s.SeoDescription, WithoutSignupHint(s.About), s.HeroSubtitle, s.Tagline) ?? s.StoreName;

        /// <summary>The About text of a new site ends with a note to its owner, which is not for search results.</summary>
        public static string WithoutSignupHint(string about) =>
            about?.Replace("این متن را از پنل مدیریت ویرایش کنید.", "").Replace(" فروشگاه آنلاین سایت ", " سایت ");

        /// <summary>The owner's SEO title, or the item's title followed by the site name.</summary>
        public static string ItemTitle(ISeoFields item, string title, string siteName) =>
            Clean(item?.SeoTitle) ?? title + Separator + siteName;

        public static string ProductDescription(StoreProduct p, string storeName) =>
            Seo.FirstText(p.SeoDescription, p.ShortDescription, p.Description)
            ?? (p.IsDigital() ? "تهیه " : "خرید ") + p.Title + " از " + storeName;

        public static string PostDescription(StorePost p) =>
            Seo.FirstText(p.SeoDescription, p.Summary, p.BodyHtml) ?? p.Title;

        public static string CategoryDescription(StoreCategory c, SiteKindInfo kind, string storeName) =>
            Seo.FirstText(c.SeoDescription, c.Description)
            ?? (kind.IsContent ? "تازه‌ترین مطالب " + c.Title + " در " + storeName : "خرید " + c.Title + " از " + storeName);

        public static string PageDescription(StorePage p, string storeName) =>
            Seo.FirstText(p.SeoDescription, WithoutSignupHint(p.BodyHtml)) ?? p.Title + " - " + storeName;

        // ---- Main site

        public static string SiteHomeTitle(SiteSettings s) =>
            Clean(s.VerifyHomeTitle) ?? Clean(s.SeoTitle) ?? s.SiteName + Separator + "ساخت فروشگاه اینترنتی، سایت خبری و نوبت‌دهی آنلاین";

        public static string SiteProductTitle(Product p, SiteSettings s) =>
            Clean(p.SeoTitle) ?? p.Title + Separator + s.SiteName;

        public static string SiteProductDescription(Product p) =>
            Seo.FirstText(p.SeoDescription, p.ShortDescription, p.Description) ?? p.Title;

        // ---- Structured data of stores

        /// <summary>schema.org type of the business behind a store type.</summary>
        public static string BusinessType(string storeType)
        {
            switch (storeType)
            {
                case "academy": return "EducationalOrganization";
                case "news": return "NewsMediaOrganization";
                case "corporate": return "Organization";
                case "clinic": return "MedicalClinic";
                case "beauty": return "BeautySalon";
                case "consulting": return "ProfessionalService";
                default: return "OnlineStore";
            }
        }

        public static Dictionary<string, object> StoreOrganization(SeoInfo seo, StoreSettings s, TenantInfo tenant)
        {
            var org = Schema(BusinessType(tenant?.StoreType));
            org["@id"] = seo.Url("/") + "#organization";
            org["name"] = s.StoreName;
            org["url"] = seo.Url("/");
            org["logo"] = seo.Asset(s.LogoUrl);
            org["image"] = seo.Asset(s.HeroImageUrl ?? s.LogoUrl);
            org["description"] = Seo.Text(WithoutSignupHint(s.About) ?? s.Tagline, 300);
            org["telephone"] = Clean(s.Phone);
            org["email"] = Clean(s.Email);
            if (!string.IsNullOrWhiteSpace(s.Address))
                org["address"] = new Dictionary<string, object> { { "@type", "PostalAddress" }, { "streetAddress", s.Address.Trim() }, { "addressCountry", "IR" } };
            var sameAs = Links(s.InstagramUrl, s.TelegramUrl, s.LinkedInUrl, s.TwitterUrl, s.FacebookUrl, s.AparatUrl, s.YouTubeUrl);
            if (sameAs.Any()) org["sameAs"] = sameAs;
            return org;
        }

        public static Dictionary<string, object> WebSite(SeoInfo seo, string name, string description)
        {
            var site = Schema("WebSite");
            site["@id"] = seo.Url("/") + "#website";
            site["name"] = name;
            site["url"] = seo.Url("/");
            site["description"] = description;
            site["inLanguage"] = "fa-IR";
            return site;
        }

        /// <summary>Home first; each crumb is a name and an in-app path (null for the page itself).</summary>
        public static Dictionary<string, object> Breadcrumbs(SeoInfo seo, params Tuple<string, string>[] crumbs)
        {
            var list = Schema("BreadcrumbList");
            list["itemListElement"] = crumbs.Select((c, i) =>
            {
                var item = new Dictionary<string, object> { { "@type", "ListItem" }, { "position", i + 1 }, { "name", c.Item1 } };
                item["item"] = c.Item2 == null ? seo.Canonical : seo.Url(c.Item2);
                return item;
            }).ToList();
            return list;
        }

        public static Tuple<string, string> Crumb(string name, string path) => Tuple.Create(name, path);

        public static Dictionary<string, object> Product(SeoInfo seo, StoreProduct p, StoreSettings s, StoreCategory category,
            IList<StoreProductReview> reviews)
        {
            var course = p.ProductType == StoreProductType.Course;
            var book = p.ProductType == StoreProductType.Ebook;
            var product = Schema(course ? "Course" : book ? "Book" : "Product");
            product["@id"] = seo.Canonical + "#product";
            product["name"] = p.Title;
            product["description"] = ProductDescription(p, s.StoreName);
            product["url"] = seo.Canonical;
            var images = (p.Images ?? new List<StoreProductImage>()).OrderBy(i => i.SortOrder)
                .Select(i => seo.Asset(i.LargeUrl ?? i.ThumbUrl)).Where(u => u != null).Distinct().ToList();
            if (images.Any()) product["image"] = images;
            product["sku"] = Clean(p.Sku);
            if (category != null) product[course || book ? "genre" : "category"] = category.Title;
            var store = new Dictionary<string, object> { { "@type", "Organization" }, { "name", s.StoreName }, { "url", seo.Url("/") } };
            if (course) product["provider"] = store;
            else if (!book) product["brand"] = new Dictionary<string, object> { { "@type", "Brand" }, { "name", s.StoreName } };
            if (!string.IsNullOrWhiteSpace(p.Author))
                product[course ? "instructor" : "author"] = new Dictionary<string, object> { { "@type", "Person" }, { "name", p.Author.Trim() } };

            var offer = Schema("Offer", context: false);
            offer["url"] = seo.Canonical;
            offer["priceCurrency"] = "IRR";
            offer["price"] = p.Price * 10;
            offer["priceValidUntil"] = DateTime.Today.AddYears(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            offer["availability"] = "https://schema.org/" + (!p.IsDigital() && p.Stock <= 0 ? "OutOfStock" : "InStock");
            if (!p.IsDigital()) offer["itemCondition"] = "https://schema.org/NewCondition";
            offer["seller"] = store;
            if (course) offer["category"] = p.Price > 0 ? "Paid" : "Free";
            product["offers"] = offer;

            if (reviews != null && reviews.Any())
            {
                product["aggregateRating"] = new Dictionary<string, object>
                {
                    { "@type", "AggregateRating" },
                    { "ratingValue", Math.Round(reviews.Average(r => r.Rating), 1) },
                    { "reviewCount", reviews.Count },
                    { "bestRating", 5 },
                    { "worstRating", 1 }
                };
                product["review"] = reviews.OrderByDescending(r => r.CreatedAt).Take(5).Select(r => new Dictionary<string, object>
                {
                    { "@type", "Review" },
                    { "author", new Dictionary<string, object> { { "@type", "Person" }, { "name", r.AuthorName } } },
                    { "datePublished", r.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                    { "reviewBody", Seo.Text(r.Comment, 500) },
                    { "reviewRating", new Dictionary<string, object> { { "@type", "Rating" }, { "ratingValue", r.Rating }, { "bestRating", 5 } } }
                }).ToList();
            }
            return product;
        }

        public static Dictionary<string, object> Article(SeoInfo seo, StorePost post, StoreSettings s, StoreCategory category, bool news)
        {
            var article = Schema(news ? "NewsArticle" : "BlogPosting");
            article["@id"] = seo.Canonical + "#article";
            article["headline"] = post.Title.Length > 110 ? post.Title.Substring(0, 109) + "…" : post.Title;
            article["description"] = PostDescription(post);
            article["mainEntityOfPage"] = seo.Canonical;
            var image = seo.Asset(post.CoverImageUrl ?? s.HeroImageUrl ?? s.LogoUrl);
            if (image != null) article["image"] = new[] { image };
            article["datePublished"] = Iso(post.PublishedAt);
            article["dateModified"] = Iso(post.UpdatedAt ?? post.PublishedAt);
            article["author"] = string.IsNullOrWhiteSpace(post.Author)
                ? new Dictionary<string, object> { { "@type", "Organization" }, { "name", s.StoreName }, { "url", seo.Url("/") } }
                : new Dictionary<string, object> { { "@type", "Person" }, { "name", post.Author.Trim() } };
            var publisher = new Dictionary<string, object> { { "@type", "Organization" }, { "name", s.StoreName }, { "url", seo.Url("/") } };
            var logo = seo.Asset(s.LogoUrl);
            if (logo != null) publisher["logo"] = new Dictionary<string, object> { { "@type", "ImageObject" }, { "url", logo } };
            article["publisher"] = publisher;
            if (category != null) article["articleSection"] = category.Title;
            var tags = post.TagList().ToList();
            if (tags.Any()) article["keywords"] = string.Join(", ", tags);
            article["inLanguage"] = "fa-IR";
            return article;
        }

        // ---- Structured data of the main site

        public static Dictionary<string, object> SiteOrganization(SeoInfo seo, SiteSettings s)
        {
            var org = Schema("Organization");
            org["@id"] = seo.Url("/") + "#organization";
            org["name"] = s.SiteName;
            org["url"] = seo.Url("/");
            org["logo"] = seo.Asset("/Assets/img/logo_full.png");
            org["description"] = Seo.Text(s.MetaDescription ?? s.FooterAbout, 300);
            org["telephone"] = Clean(s.Phone);
            org["email"] = Clean(s.Email);
            if (!string.IsNullOrWhiteSpace(s.Address))
                org["address"] = new Dictionary<string, object> { { "@type", "PostalAddress" }, { "streetAddress", Seo.Text(s.Address, 300) }, { "addressCountry", "IR" } };
            var sameAs = Links(s.InstagramUrl, s.TelegramUrl, s.LinkedInUrl);
            if (sameAs.Any()) org["sameAs"] = sameAs;
            return org;
        }

        /// <summary>A product of the main site: a web application sold by subscription.</summary>
        public static Dictionary<string, object> SiteProduct(SeoInfo seo, Product p, SiteSettings s)
        {
            var app = Schema("SoftwareApplication");
            app["@id"] = seo.Canonical + "#product";
            app["name"] = p.Title;
            app["description"] = SiteProductDescription(p);
            app["url"] = seo.Canonical;
            app["image"] = seo.Asset(p.ImageUrl);
            app["applicationCategory"] = "BusinessApplication";
            app["operatingSystem"] = "Web";
            app["inLanguage"] = "fa-IR";
            app["publisher"] = new Dictionary<string, object> { { "@type", "Organization" }, { "name", s.SiteName }, { "url", seo.Url("/") } };
            if (!string.IsNullOrEmpty(p.DemoSlug))
            {
                var kind = SiteKinds.Get(StoreTypes.Get(p.DemoSlug).Kind);
                app["featureList"] = kind.Features.Concat(PlatformModules.For(kind).Select(m => m.Name)).ToList();
            }
            var offer = Schema("Offer", context: false);
            offer["url"] = seo.Canonical;
            offer["priceCurrency"] = "IRR";
            offer["price"] = p.Price * 10;
            offer["availability"] = "https://schema.org/InStock";
            app["offers"] = offer;
            return app;
        }

        public static Dictionary<string, object> Faq(IEnumerable<SiteFaq> faqs)
        {
            var list = faqs.Where(f => !string.IsNullOrWhiteSpace(f.Question) && !string.IsNullOrWhiteSpace(f.Answer)).ToList();
            if (!list.Any()) return null;
            var page = Schema("FAQPage");
            page["mainEntity"] = list.Select(f => new Dictionary<string, object>
            {
                { "@type", "Question" },
                { "name", f.Question.Trim() },
                { "acceptedAnswer", new Dictionary<string, object> { { "@type", "Answer" }, { "text", Seo.Text(f.Answer, 1000) } } }
            }).ToList();
            return page;
        }

        // ---- Helpers

        private static Dictionary<string, object> Schema(string type, bool context = true)
        {
            var schema = new Dictionary<string, object>();
            if (context) schema["@context"] = "https://schema.org";
            schema["@type"] = type;
            return schema;
        }

        private static List<string> Links(params string[] urls) =>
            urls.Where(u => !string.IsNullOrWhiteSpace(u) && u.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase))
                .Select(u => u.Trim()).Distinct().ToList();

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static string Iso(DateTime at) =>
            new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Local)).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    }
}
