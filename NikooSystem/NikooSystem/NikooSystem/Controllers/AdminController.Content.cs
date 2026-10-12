using System;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>The main site's content: contact messages, texts and settings, FAQ, testimonials and the admin account.</summary>
    public partial class AdminController
    {
        // ---- Contact messages

        public ActionResult Messages(GridQuery query, string status = null)
        {
            return View(SiteContentStore.MessageGrid(query, status));
        }

        public ActionResult Message(int id)
        {
            var message = SiteContentStore.GetMessage(id);
            if (message == null) return HttpNotFound();
            if (!message.IsRead)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.Now;
                SiteContentStore.UpdateMessage(message);
            }
            return View(message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveMessageNote(int id, string adminNote)
        {
            var message = SiteContentStore.GetMessage(id);
            if (message == null) return HttpNotFound();
            message.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
            SiteContentStore.UpdateMessage(message);
            TempData["Ok"] = "یادداشت ذخیره شد.";
            return RedirectToAction("Message", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkMessageUnread(int id)
        {
            var message = SiteContentStore.GetMessage(id);
            if (message == null) return HttpNotFound();
            message.IsRead = false;
            message.ReadAt = null;
            SiteContentStore.UpdateMessage(message);
            TempData["Ok"] = "پیام خوانده‌نشده علامت خورد.";
            return RedirectToAction("Messages");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAllMessagesRead()
        {
            var count = SiteContentStore.MarkAllMessagesRead();
            TempData["Ok"] = count == 0 ? "پیام خوانده‌نشده‌ای نبود." : count.ToFa() + " پیام خوانده شد.";
            return RedirectToAction("Messages");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMessage(int id)
        {
            SiteContentStore.DeleteMessage(id);
            TempData["Ok"] = "پیام حذف شد.";
            return RedirectToAction("Messages");
        }

        // ---- Site texts and settings

        [HttpGet]
        public ActionResult SiteSettings()
        {
            return View(SiteContentStore.LoadSettings());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SiteSettings(SiteSettings model)
        {
            foreach (var field in new[] { "InstagramUrl", "TelegramUrl", "WhatsAppUrl", "LinkedInUrl", "CharityUrl" })
            {
                var property = typeof(SiteSettings).GetProperty(field);
                var value = ((string)property.GetValue(model))?.Trim();
                property.SetValue(model, string.IsNullOrEmpty(value) ? null : value);
                if (!string.IsNullOrEmpty(value) && !IsWebUrl(value))
                    ModelState.AddModelError(field, "آدرس باید با http:// یا https:// شروع شود.");
            }
            model.PostalCode = string.IsNullOrWhiteSpace(model.PostalCode) ? null : model.PostalCode.Trim();
            model.SeoTitle = string.IsNullOrWhiteSpace(model.SeoTitle) ? null : model.SeoTitle.Trim();
            model.MetaDescription = model.MetaDescription?.Trim();
            var verification = Seo.VerificationCode(model.GoogleVerification);
            if (!string.IsNullOrWhiteSpace(model.GoogleVerification) && verification == null)
                ModelState.AddModelError("GoogleVerification", "کد تأیید شناخته نشد؛ تگ meta یا مقدار content آن را از سرچ کنسول کپی کنید.");
            model.GoogleVerification = verification;
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "بعضی فیلدها اشکال دارند؛ موارد قرمز را اصلاح کنید.";
                return View(model);
            }

            SiteContentStore.SaveSettings(model);
            TempData["Ok"] = "تنظیمات سایت ذخیره شد و روی سایت اعمال شد.";
            return RedirectToAction("SiteSettings");
        }

        private static bool IsWebUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        // ---- FAQ

        public ActionResult Faqs()
        {
            return View(SiteContentStore.GetFaqs());
        }

        [HttpGet]
        public ActionResult EditFaq(int? id)
        {
            if (!id.HasValue)
                return View(new SiteFaq { SortOrder = (SiteContentStore.GetFaqs().Count + 1) * 10 });
            var faq = SiteContentStore.GetFaq(id.Value);
            if (faq == null) return HttpNotFound();
            return View(faq);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditFaq(SiteFaq model)
        {
            if (!ModelState.IsValid) return View(model);
            model.Question = model.Question.Trim();
            model.Answer = model.Answer.Trim();
            SiteContentStore.SaveFaq(model);
            TempData["Ok"] = "سؤال ذخیره شد.";
            return RedirectToAction("Faqs");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleFaq(int id)
        {
            var faq = SiteContentStore.GetFaq(id);
            if (faq == null) return HttpNotFound();
            faq.IsActive = !faq.IsActive;
            SiteContentStore.SaveFaq(faq);
            TempData["Ok"] = faq.IsActive ? "سؤال در سایت نمایش داده می‌شود." : "سؤال از سایت پنهان شد.";
            return RedirectToAction("Faqs");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteFaq(int id)
        {
            SiteContentStore.DeleteFaq(id);
            TempData["Ok"] = "سؤال حذف شد.";
            return RedirectToAction("Faqs");
        }

        // ---- Testimonials

        public ActionResult Testimonials()
        {
            return View(SiteContentStore.GetTestimonials());
        }

        [HttpGet]
        public ActionResult EditTestimonial(int? id)
        {
            if (!id.HasValue)
                return View(new SiteTestimonial { SortOrder = (SiteContentStore.GetTestimonials().Count + 1) * 10 });
            var item = SiteContentStore.GetTestimonial(id.Value);
            if (item == null) return HttpNotFound();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTestimonial(SiteTestimonial model, HttpPostedFileBase imageFile, bool removeImage = false)
        {
            var saved = TrySaveImage(imageFile);
            if (!ModelState.IsValid) return View(model);

            var existing = model.Id > 0 ? SiteContentStore.GetTestimonial(model.Id) : null;
            model.ImageUrl = removeImage ? null : saved ?? existing?.ImageUrl;
            model.Name = model.Name.Trim();
            model.Text = model.Text.Trim();
            SiteContentStore.SaveTestimonial(model);
            TempData["Ok"] = "نظر مشتری ذخیره شد.";
            return RedirectToAction("Testimonials");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleTestimonial(int id)
        {
            var item = SiteContentStore.GetTestimonial(id);
            if (item == null) return HttpNotFound();
            item.IsActive = !item.IsActive;
            SiteContentStore.SaveTestimonial(item);
            TempData["Ok"] = item.IsActive ? "نظر در سایت نمایش داده می‌شود." : "نظر از سایت پنهان شد.";
            return RedirectToAction("Testimonials");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteTestimonial(int id)
        {
            SiteContentStore.DeleteTestimonial(id);
            TempData["Ok"] = "نظر حذف شد.";
            return RedirectToAction("Testimonials");
        }

        // ---- Admin account

        [HttpGet]
        public ActionResult Account()
        {
            return View(new AdminAccountViewModel { Username = SiteContentStore.AdminUsername });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Account(AdminAccountViewModel model)
        {
            if (ModelState.IsValidField("CurrentPassword") && !SiteContentStore.CheckAdminLogin(SiteContentStore.AdminUsername, model.CurrentPassword))
                ModelState.AddModelError("CurrentPassword", "رمز فعلی درست نیست.");
            if (!ModelState.IsValid) return View(model);

            SiteContentStore.ChangeAdminCredentials(model.Username, model.NewPassword);
            Session["AdminUser"] = model.Username.Trim();
            TempData["Ok"] = "نام کاربری و رمز پنل عوض شد؛ از این به بعد با اطلاعات جدید وارد شوید.";
            return RedirectToAction("Account");
        }
    }
}
