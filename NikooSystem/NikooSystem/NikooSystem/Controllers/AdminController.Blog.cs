using System;
using System.Globalization;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>The main site's blog articles.</summary>
    public partial class AdminController
    {
        public ActionResult BlogPosts(GridQuery query, string state = null)
        {
            return View(SiteBlogStore.Grid(query, state));
        }

        [HttpGet]
        public ActionResult EditBlogPost(int? id)
        {
            if (!id.HasValue) return View(new SitePost { Author = SiteContentStore.Settings.SiteName });
            var post = SiteBlogStore.Get(id.Value);
            if (post == null) return HttpNotFound();
            return View(post);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditBlogPost(SitePost model, HttpPostedFileBase coverFile, string publishAt, bool removeCover = false)
        {
            var existing = model.Id > 0 ? SiteBlogStore.Get(model.Id) : null;
            if (model.Id > 0 && existing == null) return HttpNotFound();
            var saved = TrySaveImage(coverFile, "coverFile");
            model.CoverImageUrl = removeCover ? null : saved ?? existing?.CoverImageUrl;
            DateTime publishedAt;
            model.PublishedAt = DateTime.TryParseExact(publishAt, ShopAdminController.PublishAtFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out publishedAt)
                ? publishedAt
                : existing?.PublishedAt ?? DateTime.Now;
            if (!ModelState.IsValid) return View(model);

            model.Title = model.Title.Trim();
            model.Slug = string.IsNullOrWhiteSpace(model.Slug) ? null : model.Slug.Trim();
            model.Summary = Trimmed(model.Summary, 600);
            model.Author = Trimmed(model.Author, 80);
            model.Tags = Trimmed(model.Tags, 400);
            model.SeoTitle = Trimmed(model.SeoTitle, 90);
            model.SeoDescription = Trimmed(model.SeoDescription, 300);
            model = SiteBlogStore.Save(model);
            TempData["Ok"] = "مقاله «" + model.Title + "» ذخیره شد.";
            return RedirectToAction("EditBlogPost", new { id = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteBlogPost(int id)
        {
            SiteBlogStore.Delete(id);
            TempData["Ok"] = "مقاله حذف شد.";
            return RedirectToAction("BlogPosts");
        }

        private static string Trimmed(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }
}
