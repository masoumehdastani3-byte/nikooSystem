using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Helpers
{
    /// <summary>
    /// What the head of a public page tells search engines and social apps: title, description, canonical address, image,
    /// robots and structured data (JSON-LD). Controllers and views fill it in (<see cref="Seo.Of"/>, <c>Html.Seo()</c>);
    /// the layout adds the site's defaults and renders it with _SeoHead.
    /// </summary>
    public sealed class SeoInfo
    {
        /// <summary>The whole &lt;title&gt;; null = the page's ViewBag.Title followed by the site name.</summary>
        public string Title { get; set; }
        public string Description { get; set; }
        /// <summary>Absolute; null = the requested address without its query string.</summary>
        public string Canonical { get; set; }
        /// <summary>Absolute or site-relative.</summary>
        public string Image { get; set; }
        /// <summary>og:type: website, product or article.</summary>
        public string Type { get; set; } = "website";
        public bool NoIndex { get; set; }
        public string SiteName { get; set; }
        /// <summary>Content of the google-site-verification meta tag.</summary>
        public string GoogleVerification { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        /// <summary>Sale price in Toman, for product pages (og product price).</summary>
        public long? Price { get; set; }
        public List<object> Schemas { get; } = new List<object>();
        /// <summary>More &lt;meta name content&gt; tags, such as the product tags price comparison sites (Torob) read.</summary>
        public List<KeyValuePair<string, string>> Meta { get; } = new List<KeyValuePair<string, string>>();
        /// <summary>Page of a paged list; from 2 on it is added to the title so every page has its own.</summary>
        public int Page { get; set; } = 1;

        /// <summary>The public root of the site, e.g. https://nikoooo.ir, https://myshop.ir or https://nikoooo.ir/Shop/CODE.</summary>
        public string BaseUrl { get; set; }
        /// <summary>"/Shop/CODE" on store pages: the part of in-app links that the store's public address replaces.</summary>
        public string StorePrefix { get; set; }

        /// <summary>The public address of an in-app path ("/Shop/CODE/Product/x" → "https://myshop.ir/Product/x").</summary>
        public string Url(string path)
        {
            if (string.IsNullOrEmpty(path)) path = "/";
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return path;
            if (!path.StartsWith("/")) path = "/" + path;
            if (StorePrefix != null && path.StartsWith(StorePrefix, StringComparison.OrdinalIgnoreCase)
                && (path.Length == StorePrefix.Length || "/?".IndexOf(path[StorePrefix.Length]) >= 0))
                path = path.Substring(StorePrefix.Length);
            var root = (BaseUrl ?? "").TrimEnd('/');
            if (path == "/" || path.Length == 0 || path[0] == '?') path = (Seo.IsOrigin(root) ? "/" : "") + path.TrimStart('/');
            return root + path;
        }

        /// <summary>The absolute address of a file of the site (an image under /Uploads or /Assets).</summary>
        public string Asset(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return path;
            if (path.StartsWith("~")) path = path.Substring(1);
            return Seo.Origin(BaseUrl) + (path.StartsWith("/") ? "" : "/") + path;
        }
    }

    public static class Seo
    {
        private const string ItemKey = "NikooSystem.Seo";
        public const int DescriptionLength = 160;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>The SEO data of the current request (created on first use).</summary>
        public static SeoInfo Of(HttpContextBase context)
        {
            var info = context.Items[ItemKey] as SeoInfo;
            if (info == null)
            {
                info = new SeoInfo { BaseUrl = SiteUrl(context.Request) };
                context.Items[ItemKey] = info;
            }
            return info;
        }

        /// <summary>
        /// The main site's public root: the Seo.SiteUrl setting when set (one address for www and bare domain), otherwise
        /// the address of the request.
        /// </summary>
        public static string SiteUrl(HttpRequestBase request)
        {
            var configured = (ConfigurationManager.AppSettings["Seo.SiteUrl"] ?? "").Trim().TrimEnd('/');
            if (configured.Length > 0) return configured;
            return request?.Url != null ? request.Url.GetLeftPart(UriPartial.Authority) : "";
        }

        /// <summary>The main site's root, also when the request came to a store's own domain or subdomain.</summary>
        public static string PlatformUrl(HttpRequestBase request)
        {
            var configured = (ConfigurationManager.AppSettings["Seo.SiteUrl"] ?? "").Trim().TrimEnd('/');
            if (configured.Length > 0) return configured;
            if (request?.Url != null && TenantStore.ResolveByHost(request.Url.Host) == null)
                return request.Url.GetLeftPart(UriPartial.Authority);
            var root = TenantProvisioningService.RootDomain;
            return !string.IsNullOrEmpty(root) ? "https://" + root : PaymentHubService.BaseUrl;
        }

        /// <summary>
        /// A store's public root. On its own domain or subdomain that host; otherwise the custom domain once support has
        /// activated it, the platform subdomain, or /Shop/CODE on the main site, in that order (one address per page, so search
        /// engines don't split it).
        /// </summary>
        public static string StoreUrl(TenantInfo tenant, HttpRequestBase request)
        {
            var scheme = request?.Url?.Scheme ?? "https";
            var host = request?.Url?.Host;
            var byHost = TenantStore.ResolveByHost(host);
            var domain = tenant.ActiveDomain();
            var onPendingDomain = domain == null && !string.IsNullOrWhiteSpace(tenant.CustomDomain)
                && TenantProvisioningService.NormalizeDomain(host) == tenant.CustomDomain.Trim().ToLowerInvariant();
            if (byHost != null && !onPendingDomain && string.Equals(byHost.CompanyCode, tenant.CompanyCode, StringComparison.OrdinalIgnoreCase))
                return request.Url.GetLeftPart(UriPartial.Authority);
            if (domain != null)
                return "https://" + domain;
            var root = TenantProvisioningService.RootDomain;
            if (!string.IsNullOrEmpty(root))
                return scheme + "://" + tenant.CompanyCode.ToLowerInvariant() + "." + root;
            return SiteUrl(request) + "/Shop/" + tenant.CompanyCode;
        }

        /// <summary>Makes <paramref name="info"/> describe the pages of a store.</summary>
        public static void ForStore(SeoInfo info, TenantInfo tenant, HttpRequestBase request)
        {
            info.BaseUrl = StoreUrl(tenant, request);
            info.StorePrefix = "/Shop/" + tenant.CompanyCode;
            if (TenantStore.IsDemo(tenant.CompanyCode)) info.NoIndex = true;
        }

        /// <summary>The layout's part: what the page left empty gets the site's title, description, image and the page's address.</summary>
        public static void Defaults(SeoInfo info, HttpRequestBase request, string pageTitle, string siteName, string description, string image)
        {
            info.SiteName = info.SiteName ?? siteName;
            info.Title = !string.IsNullOrWhiteSpace(info.Title) ? info.Title.Trim()
                : string.IsNullOrWhiteSpace(pageTitle) ? siteName : pageTitle.Trim() + SeoPages.Separator + siteName;
            if (info.Page > 1) info.Title += " - صفحه " + info.Page.ToFa();
            info.Description = Text(info.Description) ?? Text(description);
            info.Image = info.Image ?? image;
            info.Canonical = info.Canonical ?? info.Url(CanonicalPath(request.Path));
        }

        /// <summary>"/Shop/X/Index/" → "/Shop/X": one address for a page that answers to several.</summary>
        public static string CanonicalPath(string path)
        {
            path = (path ?? "/").TrimEnd('/');
            while (path.EndsWith("/Index", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 6).TrimEnd('/');
            return path.Length == 0 ? "/" : path;
        }

        internal static string Origin(string url)
        {
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri) ? uri.GetLeftPart(UriPartial.Authority) : "";
        }

        internal static bool IsOrigin(string url) => url.Length > 0 && Origin(url).Length == url.Length;

        /// <summary>Plain text for a description: no HTML, single spaces, at most <paramref name="max"/> characters (cut at a word).</summary>
        public static string Text(string html, int max = DescriptionLength)
        {
            if (string.IsNullOrWhiteSpace(html)) return null;
            var text = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<[^>]+>", " ");
            text = Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
            if (text.Length <= max) return text.Length == 0 ? null : text;
            var cut = text.LastIndexOf(' ', max - 1);
            return text.Substring(0, cut > max / 2 ? cut : max - 1).TrimEnd('،', ',', '.', ':', '؛', ' ') + "…";
        }

        /// <summary>The first non-empty value, as description text.</summary>
        public static string FirstText(params string[] values) =>
            values.Select(v => Text(v)).FirstOrDefault(v => v != null);

        /// <summary>The verification code from what the owner pasted: the code itself, the whole meta tag or the DNS TXT value.</summary>
        public static string VerificationCode(string pasted)
        {
            if (string.IsNullOrWhiteSpace(pasted)) return null;
            var match = Regex.Match(pasted, "content\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
            var code = (match.Success ? match.Groups[1].Value : pasted).Trim().Trim('"', '\'');
            code = Regex.Replace(code, @"^google-site-verification\s*[=:]\s*", "", RegexOptions.IgnoreCase);
            return Regex.IsMatch(code, @"^[A-Za-z0-9_\-]{10,100}$") ? code : null;
        }

        public static string Json(object schema) => JsonConvert.SerializeObject(Prune(schema), JsonSettings);

        /// <summary>Drops empty values of schema dictionaries (NullValueHandling skips only object properties).</summary>
        private static object Prune(object value)
        {
            var map = value as IDictionary<string, object>;
            if (map != null)
                return map.Where(p => p.Value != null && !(p.Value is string s && s.Length == 0))
                    .ToDictionary(p => p.Key, p => Prune(p.Value));
            var list = value as System.Collections.IEnumerable;
            if (list != null && !(value is string))
                return list.Cast<object>().Select(Prune).ToList();
            return value;
        }
    }

    /// <summary>The «نمایش در گوگل» box of an edit form (_SeoFields): the two fields and what is used when they are empty.</summary>
    public sealed class SeoFieldsVm
    {
        public string Title { get; set; }
        public string Description { get; set; }
        /// <summary>The title search engines get when the field is empty.</summary>
        public string AutoTitle { get; set; }
        public string AutoDescription { get; set; }
        /// <summary>The address shown in the preview; null for an item that is not saved yet.</summary>
        public string Url { get; set; }
        public string TitleName { get; set; } = "SeoTitle";
        public string DescriptionName { get; set; } = "SeoDescription";
    }

    public static class SeoHtml
    {
        /// <summary>The SEO data of the page being rendered.</summary>
        public static SeoInfo Seo(this HtmlHelper html) => Helpers.Seo.Of(html.ViewContext.HttpContext);
    }

    /// <summary>The page is not for search results (cart, checkout, account, admin…): robots meta and X-Robots-Tag noindex.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class NoIndexAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            Seo.Of(filterContext.HttpContext).NoIndex = true;
            filterContext.HttpContext.Response.AppendHeader("X-Robots-Tag", "noindex");
        }
    }
}
