using System;
using System.Web.Mvc;
using NikooSystem.Filters;
using NikooSystem.Helpers;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>The main site's blog: /blog and /blog/{slug}.</summary>
    [CountVisits]
    public class BlogController : Controller
    {
        public ActionResult Index(int page = 1, string tag = null)
        {
            tag = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
            var posts = SiteBlogStore.LivePage(page, tag);
            var site = SiteContentStore.Settings;
            ViewBag.Tag = tag;

            var seo = Seo.Of(HttpContext);
            seo.Title = (tag == null ? "وبلاگ و مقالات آموزشی" : "مقالات " + tag) + SeoPages.Separator + site.SiteName;
            seo.Page = posts.Page;
            seo.Description = tag == null
                ? "آموزش راه‌اندازی فروشگاه اینترنتی، سایت خبری و نوبت‌دهی آنلاین، سئو، فروش اینترنتی و درگاه پرداخت؛ مقاله‌های کاربردی " + site.SiteName + "."
                : "مقاله‌های " + site.SiteName + " درباره " + tag + ".";
            // tag lists repeat the main list's articles, and an empty blog is not worth a search result
            seo.NoIndex = tag != null || posts.TotalCount == 0;
            var path = Url.RouteUrl("Blog");
            seo.Canonical = seo.Url(path + (posts.Page > 1 ? "?page=" + posts.Page : ""));
            seo.Schemas.Add(SeoPages.Breadcrumbs(seo, SeoPages.Crumb("خانه", "/"), SeoPages.Crumb("وبلاگ", null)));
            return View(posts);
        }

        public ActionResult Post(string slug)
        {
            var post = SiteBlogStore.GetLive(slug);
            if (post == null) return HttpNotFound();
            if (VisitStats.CountsItemView(HttpContext, VisitStats.MainSite, "b" + post.Id))
                SiteBlogStore.CountView(post.Id);

            var site = SiteContentStore.Settings;
            ViewBag.Related = SiteBlogStore.Latest(3, post.Id);
            var seo = Seo.Of(HttpContext);
            seo.Title = SeoPages.SitePostTitle(post, site);
            seo.Description = SeoPages.SitePostDescription(post);
            seo.Image = post.CoverImageUrl;
            seo.Type = "article";
            seo.PublishedAt = post.PublishedAt;
            seo.ModifiedAt = post.UpdatedAt ?? post.PublishedAt;
            seo.Canonical = seo.Url(Url.RouteUrl("BlogPost", new { slug = post.Slug }));
            seo.Schemas.Add(SeoPages.SiteArticle(seo, post, site));
            seo.Schemas.Add(SeoPages.Breadcrumbs(seo,
                SeoPages.Crumb("خانه", "/"),
                SeoPages.Crumb("وبلاگ", Url.RouteUrl("Blog")),
                SeoPages.Crumb(post.Title, null)));
            return View(post);
        }
    }
}
