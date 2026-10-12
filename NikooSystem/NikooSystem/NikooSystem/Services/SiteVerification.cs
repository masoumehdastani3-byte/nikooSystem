using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Newtonsoft.Json;
using NikooSystem.Helpers;
using NikooSystem.Models;

namespace NikooSystem.Services
{
    /// <summary>A text file served at the root of a site's domain (https://domain/NAME) for Enamad, Google and the like.</summary>
    public sealed class RootFile
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public DateTime AddedAt { get; set; }
    }

    /// <summary>The id and code of a site's Enamad trust seal, read from the HTML Enamad gives.</summary>
    public sealed class EnamadSeal
    {
        public string Id { get; set; }
        public string Code { get; set; }

        public string LinkUrl => "https://trustseal.enamad.ir/?id=" + Id + "&Code=" + Code;
        public string LogoUrl => "https://trustseal.enamad.ir/logo.aspx?id=" + Id + "&Code=" + Code;
    }

    /// <summary>
    /// Proving ownership of a site's domain and showing its trust seal: the files at the root of the domain, Enamad's meta tag,
    /// a temporary home title and the Enamad seal of the footer. The owner pastes what Enamad gives; only the parts that are
    /// needed are kept, so nothing the owner pastes runs as HTML or script on the site.
    /// </summary>
    public static class SiteVerification
    {
        public const int MaxFiles = 5;
        public const int MaxFileBytes = 10 * 1024;
        public const int MaxTitleLength = 200;

        private static readonly Regex FileName = new Regex(@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}\.(txt|html?)$", RegexOptions.IgnoreCase);
        private static readonly Regex ShopFilePath = new Regex(@"^/Shop/([A-Za-z0-9_-]{1,50})/([^/]+)$", RegexOptions.IgnoreCase);
        private static readonly string[] Reserved = { "robots.txt" };
        private static readonly Regex SealParts = new Regex(@"trustseal\.enamad\.ir/?\?id=(\d{1,12})&(?:amp;)?code=([A-Za-z0-9]{1,64})", RegexOptions.IgnoreCase);
        private static readonly Regex MetaContent = new Regex(@"content\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
        private static readonly Regex MetaCodeChars = new Regex(@"^[A-Za-z0-9_.:-]{2,100}$");

        // ---- Files at the root of the domain

        public static List<RootFile> Files(IVerifiedSite s)
        {
            if (string.IsNullOrWhiteSpace(s?.RootFiles)) return new List<RootFile>();
            try { return JsonConvert.DeserializeObject<List<RootFile>>(s.RootFiles) ?? new List<RootFile>(); }
            catch (JsonException) { return new List<RootFile>(); }
        }

        private static void SetFiles(IVerifiedSite s, List<RootFile> files) =>
            s.RootFiles = files.Count == 0 ? null : JsonConvert.SerializeObject(files);

        /// <summary>The file name without folders, or null when it cannot be served at the root (wrong characters or type).</summary>
        public static string CleanFileName(string name)
        {
            name = Path.GetFileName((name ?? "").Trim().Replace('\\', '/').Split('/').Last());
            return FileName.IsMatch(name) && !Reserved.Contains(name, StringComparer.OrdinalIgnoreCase) ? name : null;
        }

        /// <summary>Adds the file, or replaces the one with the same name; returns the error message, or null.</summary>
        public static string AddFile(IVerifiedSite s, string name, string content)
        {
            var clean = CleanFileName(name);
            if (clean == null)
                return "نام فایل باید فقط حروف انگلیسی، عدد، خط تیره یا نقطه باشد و با .txt یا .html تمام شود (مثل 12345678.txt).";
            content = content ?? "";
            if (Encoding.UTF8.GetByteCount(content) > MaxFileBytes)
                return "محتوای فایل بیشتر از ۱۰ کیلوبایت است؛ فایل تأیید اینماد و گوگل معمولاً فقط یک خط است.";
            if (content.Trim().Length == 0)
                return "محتوای فایل خالی است؛ همان متنی را که اینماد داده (یا خود فایل) وارد کنید.";

            var files = Files(s);
            files.RemoveAll(f => string.Equals(f.Name, clean, StringComparison.OrdinalIgnoreCase));
            if (files.Count >= MaxFiles)
                return "حداکثر " + MaxFiles.ToFa() + " فایل تأیید می‌توانید داشته باشید؛ فایلی را که دیگر لازم نیست حذف کنید.";
            files.Add(new RootFile { Name = clean, Content = content, AddedAt = DateTime.Now });
            SetFiles(s, files);
            return null;
        }

        public static bool RemoveFile(IVerifiedSite s, string name)
        {
            var files = Files(s);
            var removed = files.RemoveAll(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) SetFiles(s, files);
            return removed;
        }

        /// <summary>
        /// Serves the request when it is a root file: /NAME on a site's own domain (or on Nikoo's domain for the main site),
        /// or /Shop/CODE/NAME on any host.
        /// </summary>
        public static bool TryServeRequest(HttpContext context)
        {
            var path = context.Request.Path ?? "";
            if (!FileName.IsMatch(path.Substring(path.LastIndexOf('/') + 1))) return false;
            if (path.StartsWith("/Shop/", StringComparison.OrdinalIgnoreCase)) return TryServe(context, null, path);
            if (path.LastIndexOf('/') != 0) return false;
            var tenant = TenantStore.ResolveByHost(context.Request.Url.Host);
            return tenant != null
                ? TryServe(context, tenant.CompanyCode, path)
                : Write(context, Find(SiteContentStore.Settings, path.TrimStart('/')));
        }

        /// <summary>
        /// Answers a request for a root file of the site: <paramref name="path"/> is the request path on the site's own domain
        /// (/NAME), or /Shop/CODE/NAME on Nikoo's domain so the owner can check the file before the domain is connected.
        /// </summary>
        public static bool TryServe(HttpContext context, string company, string path)
        {
            if (path == null) return false;
            if (company == null)
            {
                var match = ShopFilePath.Match(path);
                if (!match.Success) return false;
                company = match.Groups[1].Value;
                path = match.Groups[2].Value;
            }
            var name = path.TrimStart('/');
            if (!FileName.IsMatch(name)) return false;

            var tenant = TenantStore.GetTenant(company);
            return tenant != null && Write(context, Find(TenantStore.GetSettings(tenant.CompanyCode), name));
        }

        private static RootFile Find(IVerifiedSite site, string name) =>
            Files(site).FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

        private static bool Write(HttpContext context, RootFile file)
        {
            if (file == null) return false;
            var response = context.Response;
            response.ContentType = "text/plain";
            response.Charset = "utf-8";
            response.AppendHeader("X-Content-Type-Options", "nosniff");
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Write(file.Content);
            context.ApplicationInstance.CompleteRequest();
            return true;
        }

        // ---- Enamad

        /// <summary>The seal in the HTML Enamad gives (or in a link to it), or null.</summary>
        public static EnamadSeal Seal(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return null;
            var match = SealParts.Match(HttpUtility.HtmlDecode(html));
            return match.Success ? new EnamadSeal { Id = match.Groups[1].Value, Code = match.Groups[2].Value } : null;
        }

        /// <summary>The code to keep for the seal: Enamad's own code rebuilt from the id and code, or null.</summary>
        public static string StoredSeal(EnamadSeal seal) => seal == null ? null
            : "<a referrerpolicy='origin' target='_blank' href='" + seal.LinkUrl + "'><img referrerpolicy='origin' src='" + seal.LogoUrl + "' alt='' style='cursor:pointer' code='" + seal.Code + "'></a>";

        /// <summary>
        /// The seal of the footer, built from its id and code. Enamad checks the address of the page through the referrer,
        /// so the seal shows correctly only on the domain registered at Enamad.
        /// </summary>
        public static IHtmlString SealHtml(IVerifiedSite s)
        {
            var seal = Seal(s?.EnamadHtml);
            if (seal == null) return new HtmlString(string.Empty);
            var link = HttpUtility.HtmlAttributeEncode(seal.LinkUrl);
            var logo = HttpUtility.HtmlAttributeEncode(seal.LogoUrl);
            return new HtmlString("<a referrerpolicy=\"origin\" target=\"_blank\" rel=\"noopener\" href=\"" + link + "\">"
                + "<img referrerpolicy=\"origin\" src=\"" + logo + "\" alt=\"نماد اعتماد الکترونیکی\" style=\"cursor:pointer\" code=\"" + seal.Code + "\" loading=\"lazy\" /></a>");
        }

        /// <summary>
        /// Enamad's meta code from the whole tag (&lt;meta name="enamad" content="…"&gt;) or from the code alone;
        /// null when empty, and <paramref name="rejected"/> when something was given that is not a code.
        /// </summary>
        public static string MetaCode(string input, out bool rejected)
        {
            rejected = false;
            if (string.IsNullOrWhiteSpace(input)) return null;
            var value = input.Trim();
            var content = MetaContent.Match(value);
            if (content.Success) value = content.Groups[1].Value.Trim();
            if (MetaCodeChars.IsMatch(value)) return value;
            rejected = true;
            return null;
        }

        public static string CleanTitle(string title)
        {
            title = Regex.Replace(title ?? "", @"\s+", " ").Trim();
            if (title.Length == 0) return null;
            return title.Length > MaxTitleLength ? title.Substring(0, MaxTitleLength) : title;
        }
    }

    /// <summary>
    /// Serves the root files of the sites. A .txt or .html request goes to IIS's static file handler and never reaches
    /// Global.asax, so this module is registered in Web.config without a precondition to run for those requests too.
    /// </summary>
    public sealed class SiteRootFileModule : IHttpModule
    {
        public void Init(HttpApplication app) =>
            app.BeginRequest += (sender, e) => SiteVerification.TryServeRequest(((HttpApplication)sender).Context);

        public void Dispose() { }
    }
}
