using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// Bale notifications of the store: linking the owner's Bale accounts to Nikoo's bot, choosing the events and a test
    /// message (see <see cref="BaleNotifier"/>).
    /// </summary>
    public partial class ShopAdminController
    {
        [HttpGet]
        public ActionResult Notifications()
        {
            return View(BaleNotifier.Page(CompanyCode, s => NikooSystem.Services.Notifications.StoreEvents(Kind, s), TenantStore.IsDemo(CompanyCode)));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NotifyLinkCode()
        {
            if (TenantStore.IsDemo(CompanyCode))
                return Fail("فروشگاه‌های نمایشی به بله وصل نمی‌شوند؛ این امکان را در فروشگاه خودتان امتحان کنید.", "Notifications");
            if (BaleNotifier.BotToken == null)
                return Fail("بازوی بله نیکو سیستم هنوز راه‌اندازی نشده است؛ کمی بعد دوباره سر بزنید.", "Notifications");
            BaleNotifier.NewLinkCode(CompanyCode);
            return ToAction("Notifications");
        }

        /// <summary>Reads the bot's new messages (linking accounts that sent a code); the page asks this while a code is waiting.</summary>
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, Location = OutputCacheLocation.None)]
        public async Task<ActionResult> NotifyCheck()
        {
            var error = await BaleNotifier.PollAsync();
            var settings = BaleNotifier.Settings(CompanyCode);
            return Json(new
            {
                recipients = BaleNotifier.Recipients(CompanyCode).Count,
                waiting = settings.HasLiveLinkCode(DateTime.Now),
                error
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveNotifyEvents(string[] enabled)
        {
            var offered = NikooSystem.Services.Notifications.StoreEvents(Kind, BaleNotifier.Settings(CompanyCode));
            BaleNotifier.SaveEnabled(CompanyCode, offered, enabled);
            return Ok("رویدادهای اطلاع‌رسانی ذخیره شد.", "Notifications");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> NotifyTest()
        {
            var error = await BaleNotifier.SendTestAsync(CompanyCode);
            return error == null
                ? Ok("پیام آزمایشی به بله فرستاده شد.", "Notifications")
                : Fail("پیام آزمایشی فرستاده نشد: " + error, "Notifications");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveBaleRecipient(int id)
        {
            BaleNotifier.RemoveRecipient(CompanyCode, id);
            return Ok("حساب بله جدا شد و دیگر پیامی دریافت نمی‌کند.", "Notifications");
        }
    }
}
