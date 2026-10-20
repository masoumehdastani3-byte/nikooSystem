using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Posts and comments of content sites; categories, menus, pages, gallery, home layout and messages of every site.</summary>
    public partial class ShopAdminController
    {
        // ---- Posts

        [ForKinds(SiteKinds.Content)]
        public ActionResult Posts(PostGridQuery query)
        {
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            ViewBag.Query = query;
            return View(TenantStore.PostGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Content)]
        public ActionResult EditPost(int? id)
        {
            var post = id.HasValue ? TenantStore.GetPost(CompanyCode, id.Value) : new StorePost { Author = CurrentSettings.StoreName };
            if (post == null) return ToAction("Posts");
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            return View(post);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        [ForKinds(SiteKinds.Content)]
        public ActionResult EditPost(StorePost model, HttpPostedFileBase coverFile, string publishAt, bool removeCover = false)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Fail("تیتر الزامی است.", "EditPost", model.Id > 0 ? new { id = model.Id } : null);

            var existing = model.Id > 0 ? TenantStore.GetPost(CompanyCode, model.Id) : null;
            model.CoverImageUrl = removeCover ? null : existing?.CoverImageUrl;
            try
            {
                var cover = ImageService.SaveProductImage(CompanyCode, coverFile);
                if (cover != null) model.CoverImageUrl = cover.LargeUrl;
            }
            catch (InvalidOperationException ex)
            {
                Fail(ex.Message);
            }

            model.Title = model.Title.Trim();
            model.Slug = string.IsNullOrWhiteSpace(model.Slug) ? null : model.Slug.Trim();
            model.Kicker = Clip(model.Kicker, 120);
            model.Summary = Clip(model.Summary, 600);
            model.Author = Clip(model.Author, 80);
            model.Source = Clip(model.Source, 120);
            model.Tags = Clip(model.Tags, 400);
            CleanSeo(model);
            model.IsSample = false;
            if (model.CategoryId.HasValue && TenantStore.GetCategory(CompanyCode, model.CategoryId.Value) == null) model.CategoryId = null;
            DateTime publishedAt;
            model.PublishedAt = DateTime.TryParseExact(publishAt, PublishAtFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out publishedAt)
                ? publishedAt
                : existing?.PublishedAt ?? DateTime.Now;

            model = TenantStore.SavePost(CompanyCode, model);
            return Ok("مطلب «" + model.Title + "» ذخیره شد.", "EditPost", new { id = model.Id });
        }

        /// <summary>The Gregorian value posted by <c>Html.ShamsiDateTime</c>; parsed without the fa-IR culture of the site.</summary>
        public const string PublishAtFormat = "yyyy-MM-ddTHH:mm";

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Content)]
        public ActionResult DeletePost(int id)
        {
            TenantStore.DeletePost(CompanyCode, id);
            return OkBack("مطلب و نظرهایش حذف شد.", "Posts");
        }

        // ---- Readers' comments

        [ForKinds(SiteKinds.Content)]
        public ActionResult Comments(CommentGridQuery query)
        {
            var page = TenantStore.CommentGrid(CompanyCode, query);
            ViewBag.Posts = TenantStore.PostsByIds(CompanyCode, page.Select(c => c.PostId));
            ViewBag.Query = query;
            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Content)]
        public ActionResult ToggleComment(int id, bool approved)
        {
            var comment = TenantStore.GetComment(CompanyCode, id);
            if (comment != null)
            {
                comment.IsApproved = approved;
                TenantStore.UpdateComment(CompanyCode, comment);
            }
            return BackTo("Comments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Content)]
        public ActionResult ReplyComment(int id, string reply)
        {
            var comment = TenantStore.GetComment(CompanyCode, id);
            if (comment == null) return BackTo("Comments");
            comment.AdminReply = Clip(reply, 2000);
            if (comment.AdminReply != null) comment.IsApproved = true;
            TenantStore.UpdateComment(CompanyCode, comment);
            return OkBack(comment.AdminReply == null ? "پاسخ حذف شد." : "پاسخ ثبت و نظر منتشر شد.", "Comments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Content)]
        public ActionResult DeleteComment(int id)
        {
            TenantStore.DeleteComment(CompanyCode, id);
            return OkBack("نظر حذف شد.", "Comments");
        }

        // ---- Contact-form messages

        public ActionResult Messages(MessageGridQuery query)
        {
            ViewBag.Query = query;
            return View(TenantStore.MessageGrid(CompanyCode, query));
        }

        [HttpGet]
        public ActionResult Message(int id)
        {
            var message = TenantStore.GetMessage(CompanyCode, id);
            if (message == null) return ToAction("Messages");
            if (!message.IsRead)
            {
                message.IsRead = true;
                TenantStore.UpdateMessage(CompanyCode, message);
            }
            return View(message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMessage(int id)
        {
            TenantStore.DeleteMessage(CompanyCode, id);
            return OkBack("پیام حذف شد.", "Messages");
        }

        // ---- Categories and menus (trees, paged after flattening so children stay under their parent)

        public ActionResult Categories(GridQuery query)
        {
            return View(query.Slice(TreeHelper.Flatten(TenantStore.GetCategories(CompanyCode)),
                r => r.Node.Title + " " + r.Node.Slug,
                new Dictionary<string, Func<TreeRow<StoreCategory>, object>>
                {
                    ["title"] = r => r.Node.Title,
                    ["slug"] = r => r.Node.Slug,
                    ["sort"] = r => r.Node.SortOrder,
                    ["status"] = r => r.Node.IsActive
                }));
        }

        [HttpGet]
        public ActionResult EditCategory(int? id)
        {
            var list = TenantStore.GetCategories(CompanyCode);
            ViewBag.Categories = list;
            return View((id.HasValue ? list.FirstOrDefault(c => c.Id == id.Value) : null)
                ?? new StoreCategory { IsActive = true, SortOrder = 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCategory(StoreCategory model)
        {
            CleanSeo(model);
            TenantStore.SaveCategory(CompanyCode, model);
            return Ok("گروه ذخیره شد.", "Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCategory(int id)
        {
            TenantStore.DeleteCategory(CompanyCode, id);
            return OkBack("گروه حذف شد.", "Categories");
        }

        public ActionResult Menus(GridQuery query, string location = null)
        {
            var menus = TenantStore.GetMenus(CompanyCode).AsEnumerable();
            if (!string.IsNullOrEmpty(location)) menus = menus.Where(m => m.Location == location);
            return View(query.Slice(TreeHelper.Flatten(menus.OrderBy(m => m.Location).ThenBy(m => m.SortOrder)),
                r => r.Node.Title + " " + r.Node.Url,
                new Dictionary<string, Func<TreeRow<StoreMenuItem>, object>>
                {
                    ["title"] = r => r.Node.Title,
                    ["location"] = r => r.Node.Location,
                    ["link"] = r => r.Node.Url,
                    ["sort"] = r => r.Node.SortOrder
                }));
        }

        [HttpGet]
        public ActionResult EditMenu(int? id)
        {
            var list = TenantStore.GetMenus(CompanyCode);
            ViewBag.Menus = list;
            return View((id.HasValue ? list.FirstOrDefault(m => m.Id == id.Value) : null)
                ?? new StoreMenuItem { IsActive = true, Location = "main", SortOrder = 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveMenu(StoreMenuItem model)
        {
            TenantStore.SaveMenu(CompanyCode, model);
            return Ok("منو ذخیره شد.", "Menus");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMenu(int id)
        {
            TenantStore.DeleteMenu(CompanyCode, id);
            return OkBack("منو حذف شد.", "Menus");
        }

        // ---- Pages

        public ActionResult Pages(GridQuery query)
        {
            return View(TenantStore.PageGrid(CompanyCode, query));
        }

        [HttpGet]
        public ActionResult EditPage(int? id)
        {
            return View((id.HasValue ? TenantStore.GetPage(CompanyCode, id.Value) : null)
                ?? new StorePage { IsPublished = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EditPage(StorePage model)
        {
            CleanSeo(model);
            TenantStore.SavePage(CompanyCode, model);
            return Ok("صفحه ذخیره شد.", "Pages");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeletePage(int id)
        {
            TenantStore.DeletePage(CompanyCode, id);
            return OkBack("صفحه حذف شد.", "Pages");
        }

        // ---- Gallery

        public ActionResult Gallery(GalleryGridQuery query)
        {
            ViewBag.Albums = TenantStore.GalleryAlbums(CompanyCode);
            ViewBag.Query = query;
            return View(TenantStore.GalleryGrid(CompanyCode, query));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadGallery(string title, string album, IEnumerable<HttpPostedFileBase> imageFiles)
        {
            var imageErrors = new List<string>();
            var uploaded = ImageService.SaveProductImages(CompanyCode, imageFiles, imageErrors);
            if (!uploaded.Any())
                return Fail(imageErrors.Any() ? ImageErrors(imageErrors) : "تصویری انتخاب نشده است.", "Gallery");
            if (imageErrors.Any()) Fail(ImageErrors(imageErrors));

            TenantStore.AddGalleryItems(CompanyCode, uploaded.Select(saved => new StoreGalleryItem
            {
                Title = string.IsNullOrWhiteSpace(title) ? "تصویر" : title,
                Album = string.IsNullOrWhiteSpace(album) ? "عمومی" : album,
                ThumbUrl = saved.ThumbUrl,
                LargeUrl = saved.LargeUrl
            }).ToList());
            return Ok(uploaded.Count + " تصویر اضافه شد.", "Gallery");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteGalleryItem(int id)
        {
            TenantStore.DeleteGalleryItem(CompanyCode, id);
            return OkBack("تصویر حذف شد.", "Gallery");
        }

        // ---- Home page layout

        [HttpGet]
        public ActionResult PageLayout()
        {
            ViewBag.Sections = HomeSections.Resolve(CurrentSettings);
            ViewBag.Collections = Kind.Sells ? TenantStore.GetCollections(CompanyCode) : new List<StoreCollection>();
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            return View(HomeSections.LayoutOf(CurrentSettings));
        }

        private const int MaxCustomSections = 20;

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PageLayout(string sectionsJson, bool showRelatedProducts = false, int relatedProductsCount = 4,
            bool showReviews = false, bool showSpecifications = false, bool showBuyPerks = false)
        {
            List<StoreHomeSection> posted;
            try { posted = JsonConvert.DeserializeObject<List<StoreHomeSection>>(sectionsJson ?? "") ?? new List<StoreHomeSection>(); }
            catch (JsonException)
            {
                return Fail("اطلاعات بخش‌ها نامعتبر بود؛ دوباره تلاش کنید.", "PageLayout");
            }
            if (posted.Count(s => s != null && HomeSections.IsCustom(s.Type)) > MaxCustomSections)
                return Fail("حداکثر " + MaxCustomSections + " بخش دلخواه می‌توانید داشته باشید.", "PageLayout");

            var imageErrors = new List<string>();
            foreach (var sec in posted.Where(s => s != null && s.Type == "banner" && !string.IsNullOrWhiteSpace(s.Id)))
            {
                var error = SaveBrandImage(Request.Files["bannerImage_" + sec.Id], BrandImageKind.Banner, url => sec.ImageUrl = url);
                if (error != null) imageErrors.Add((sec.Title ?? "بنر") + ": " + error);
            }

            var settings = CurrentSettings;
            var layout = settings.PageLayout ?? new StorePageLayout();
            layout.HomeSections = HomeSections.Sanitize(posted, settings, Kind.Id);
            if (Kind.Sells)
            {
                layout.ShowRelatedProducts = showRelatedProducts;
                layout.RelatedProductsCount = HomeSections.ClampRelatedCount(relatedProductsCount);
                layout.ShowReviews = showReviews;
                layout.ShowSpecifications = showSpecifications;
                layout.ShowBuyPerks = showBuyPerks;
            }
            settings.PageLayout = layout;
            TenantStore.SaveSettings(settings);

            return imageErrors.Any()
                ? Fail("چیدمان ذخیره شد، اما " + ImageErrors(imageErrors), "PageLayout")
                : Ok("چیدمان صفحات ذخیره شد.", "PageLayout");
        }
    }
}
