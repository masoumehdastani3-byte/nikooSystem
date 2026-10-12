using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// Bale notifications: the bot that serves the main site and every store, and the main site's own recipients and
    /// events (purchases, payment-hub payments, signups and contact messages).
    /// </summary>
    public partial class AdminController
    {
        private const string Site = BaleNotifier.SiteScope;

        [HttpGet]
        public ActionResult Notifications()
        {
            return View(BaleNotifier.Page(Site, NikooSystem.Services.Notifications.SiteEvents, false));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveBaleBot(string token, bool remove = false)
        {
            var error = await BaleNotifier.SaveBotAsync(remove ? null : token);
            if (error != null)
                TempData["Error"] = error;
            else
                TempData["Ok"] = remove ? "بازوی بله برداشته شد و اطلاع‌رسانی همه سایت‌ها خاموش است." : "بازوی بله وصل شد.";
            return RedirectToAction("Notifications");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NotifyLinkCode()
        {
            if (BaleNotifier.BotToken == null)
                TempData["Error"] = "ابتدا توکن بازوی بله را ثبت کنید.";
            else
                BaleNotifier.NewLinkCode(Site);
            return RedirectToAction("Notifications");
        }

        /// <summary>Reads the bot's new messages (linking accounts that sent a code); the page asks this while a code is waiting.</summary>
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, Location = OutputCacheLocation.None)]
        public async Task<ActionResult> NotifyCheck()
        {
            var error = await BaleNotifier.PollAsync();
            var settings = BaleNotifier.Settings(Site);
            return Json(new
            {
                recipients = BaleNotifier.Recipients(Site).Count,
                waiting = settings.HasLiveLinkCode(DateTime.Now),
                error
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveNotifyEvents(string[] enabled)
        {
            BaleNotifier.SaveEnabled(Site, NikooSystem.Services.Notifications.SiteEvents(BaleNotifier.Settings(Site)), enabled);
            TempData["Ok"] = "رویدادهای اطلاع‌رسانی ذخیره شد.";
            return RedirectToAction("Notifications");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> NotifyTest()
        {
            var error = await BaleNotifier.SendTestAsync(Site);
            if (error == null) TempData["Ok"] = "پیام آزمایشی به بله فرستاده شد.";
            else TempData["Error"] = "پیام آزمایشی فرستاده نشد: " + error;
            return RedirectToAction("Notifications");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveBaleRecipient(int id)
        {
            BaleNotifier.RemoveRecipient(Site, id);
            TempData["Ok"] = "حساب بله جدا شد و دیگر پیامی دریافت نمی‌کند.";
            return RedirectToAction("Notifications");
        }
    }
}
