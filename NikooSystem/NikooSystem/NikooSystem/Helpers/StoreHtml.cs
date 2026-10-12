using System.Web.Mvc;
using NikooSystem.Models.Store;

namespace NikooSystem.Helpers
{
    /// <summary>Markup shared by the storefront templates.</summary>
    public static class StoreHtml
    {
        /// <summary>The store's logo and name, linking to its home page; <paramref name="withTagline"/> adds the tagline under the name.</summary>
        public static MvcHtmlString StoreBrand(this HtmlHelper html, string cssClass, bool withTagline = false)
        {
            var settings = (StoreSettings)html.ViewData["Settings"];
            var inner = "";
            if (!string.IsNullOrEmpty(settings.LogoUrl))
            {
                var logo = new TagBuilder("img");
                logo.MergeAttribute("src", settings.LogoUrl);
                logo.MergeAttribute("alt", settings.StoreName);
                inner += logo.ToString(TagRenderMode.SelfClosing);
            }

            var name = new TagBuilder("strong");
            name.SetInnerText(settings.StoreName);
            if (withTagline)
            {
                var tagline = new TagBuilder("small");
                tagline.SetInnerText(settings.Tagline);
                inner += new TagBuilder("span") { InnerHtml = name.ToString() + tagline }.ToString();
            }
            else inner += name.ToString();

            var link = new TagBuilder("a") { InnerHtml = inner };
            link.AddCssClass(cssClass);
            link.MergeAttribute("href", new UrlHelper(html.ViewContext.RequestContext)
                .Action("Index", "Shop", new { company = html.ViewData["CompanyCode"] }));
            return MvcHtmlString.Create(link.ToString());
        }
    }
}
