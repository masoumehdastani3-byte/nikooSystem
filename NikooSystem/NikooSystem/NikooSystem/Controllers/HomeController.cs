using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Filters;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    [CountVisits]
    public class HomeController : Controller
    {
        private const int MaxMessagesPerHour = 5;
        private static readonly Dictionary<string, List<DateTime>> MessagesByIp = new Dictionary<string, List<DateTime>>();

        public ActionResult Index()
        {
            var site = SiteContentStore.Settings;
            var products = SiteStore.GetProducts().Where(p => p.IsActive).OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Id).ToList();
            ViewBag.Faqs = site.ShowFaq ? SiteContentStore.GetFaqs(true) : new List<SiteFaq>();
            ViewBag.Testimonials = site.ShowTestimonials ? SiteContentStore.GetTestimonials(true) : new List<SiteTestimonial>();

            var seo = Seo.Of(HttpContext);
            seo.Title = SeoPages.SiteHomeTitle(site);
            seo.Canonical = seo.Url("/");
            seo.Schemas.Add(SeoPages.SiteOrganization(seo, site));
            seo.Schemas.Add(SeoPages.WebSite(seo, site.SiteName, Seo.Text(site.MetaDescription)));
            seo.Schemas.Add(SeoPages.Faq((IEnumerable<SiteFaq>)ViewBag.Faqs));
            return View(products);
        }

        public ActionResult About()
        {
            var site = SiteContentStore.Settings;
            Seo.Of(HttpContext).Description = Seo.FirstText(site.FooterAbout, site.MetaDescription);
            return View();
        }

        public ActionResult Contact()
        {
            var site = SiteContentStore.Settings;
            Seo.Of(HttpContext).Description = "راه‌های ارتباط با " + site.SiteName
                + (string.IsNullOrWhiteSpace(site.Phone) ? "" : "؛ تلفن " + site.Phone.Trim())
                + (string.IsNullOrWhiteSpace(site.WorkingHours) ? "" : "؛ " + site.WorkingHours.Trim())
                + ". مشاوره رایگان برای راه‌اندازی فروشگاه اینترنتی و سایت.";
            return View(new ContactViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Contact(ContactViewModel model)
        {
            if (!string.IsNullOrEmpty(model.Website))
                return RedirectToAction("Contact");

            if (!ModelState.IsValid)
                return View(model);

            if (!TryRegisterMessage(Request.UserHostAddress))
            {
                ModelState.AddModelError("", "تعداد پیام‌های ارسالی زیاد است؛ کمی بعد دوباره تلاش کنید یا تماس بگیرید.");
                return View(model);
            }

            var message = SiteContentStore.AddMessage(new SiteMessage
            {
                Name = model.Name.Trim(),
                Email = model.Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                Subject = model.Subject.Trim(),
                Message = model.Message.Trim(),
                Ip = Request.UserHostAddress
            });
            Notifications.SiteMessage(message);

            TempData["Ok"] = "پیام شما با موفقیت ثبت شد. به‌زودی با شما تماس می‌گیریم.";
            return RedirectToAction("Contact");
        }

        public ActionResult Terms()
        {
            Seo.Of(HttpContext).Description = Seo.FirstText(SiteContentStore.Settings.TermsText);
            return View();
        }

        public ActionResult Privacy()
        {
            Seo.Of(HttpContext).Description = Seo.FirstText(SiteContentStore.Settings.PrivacyText);
            return View();
        }

        /// <summary>«راهنمای خرید»: how a buyer signs up, pays, gets the service and asks for a refund.</summary>
        public ActionResult BuyGuide()
        {
            Seo.Of(HttpContext).Description = "راهنمای ثبت‌نام، خرید اشتراک و محصولات " + SiteContentStore.Settings.SiteName
                + "، پرداخت آنلاین با کارت‌های عضو شتاب، فعال‌سازی، پیگیری پرداخت و بازگشت وجه.";
            ViewBag.TrialDays = TenantProvisioningService.TrialDays;
            return View(SiteStore.GetProducts().Where(p => p.IsActive).OrderBy(p => p.Id).ToList());
        }

        private static bool TryRegisterMessage(string ip)
        {
            ip = ip ?? "unknown";
            var now = DateTime.Now;
            lock (MessagesByIp)
            {
                List<DateTime> list;
                if (!MessagesByIp.TryGetValue(ip, out list))
                    MessagesByIp[ip] = list = new List<DateTime>();
                list.RemoveAll(t => t < now.AddHours(-1));
                if (list.Count >= MaxMessagesPerHour) return false;
                list.Add(now);
                return true;
            }
        }
    }
}
