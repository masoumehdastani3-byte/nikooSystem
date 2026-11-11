using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>اتصال سامانه پیامکی پاپوس برای ارسال رمز و صورتحساب فروشگاه‌ها.</summary>
    public partial class AdminController
    {
        [HttpGet]
        public async Task<ActionResult> SmsPanel()
        {
            ViewBag.Ready = PappusSmsClient.IsConfigured;
            ViewBag.ApiKey = MaskKey(PappusSmsClient.ApiKey);
            ViewBag.HasApiKey = !string.IsNullOrEmpty(PappusSmsClient.ApiKey);
            ViewBag.Sender = PappusSmsClient.Sender;
            ViewBag.FromConfig = string.IsNullOrWhiteSpace(SiteContentStore.Settings?.PappusApiKey)
                && !string.IsNullOrEmpty(PappusSmsClient.ApiKey);

            if (PappusSmsClient.IsConfigured)
            {
                try
                {
                    var balance = await PappusSmsClient.GetBalanceAsync();
                    ViewBag.BalanceOk = balance.Success;
                    ViewBag.BalanceToman = balance.Success ? SmsService.RialToToman(balance.BalanceRial) : (long?)null;
                    ViewBag.BalanceError = balance.Success ? null : (balance.Message ?? "خواندن موجودی ممکن نشد.");
                }
                catch (Exception ex)
                {
                    ViewBag.BalanceOk = false;
                    ViewBag.BalanceError = ex.Message;
                }
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SavePappus(string apiKey, string sender, bool remove = false)
        {
            if (remove)
            {
                SiteContentStore.SaveVerification(s =>
                {
                    s.PappusApiKey = null;
                    s.PappusSender = null;
                });
                TempData["Ok"] = "اتصال پاپوس از تنظیمات سایت برداشته شد. اگر در Web.config مقدار باشد، همان استفاده می‌شود.";
                return RedirectToAction("SmsPanel");
            }

            apiKey = (apiKey ?? "").Trim();
            sender = (sender ?? "").Trim();
            if (apiKey.Length == 0 && string.IsNullOrEmpty(PappusSmsClient.ApiKey))
            {
                TempData["Error"] = "کلید API پاپوس را وارد کنید.";
                return RedirectToAction("SmsPanel");
            }
            if (sender.Length == 0)
            {
                TempData["Error"] = "خط فرستنده را وارد کنید.";
                return RedirectToAction("SmsPanel");
            }

            SiteContentStore.SaveVerification(s =>
            {
                if (apiKey.Length > 0) s.PappusApiKey = apiKey;
                s.PappusSender = sender;
            });
            TempData["Ok"] = "اتصال پاپوس ذخیره شد.";
            return RedirectToAction("SmsPanel");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> TestPappus()
        {
            if (!PappusSmsClient.IsConfigured)
            {
                TempData["Error"] = "ابتدا کلید API و خط فرستنده را ذخیره کنید.";
                return RedirectToAction("SmsPanel");
            }
            try
            {
                var balance = await PappusSmsClient.GetBalanceAsync();
                if (!balance.Success)
                {
                    TempData["Error"] = "اتصال به پاپوس ناموفق بود: " + (balance.Message ?? "پاسخ نامعتبر");
                    return RedirectToAction("SmsPanel");
                }
                TempData["Ok"] = "اتصال پاپوس درست است. موجودی پنل: "
                    + SmsService.RialToToman(balance.BalanceRial).ToToman()
                    + " (خط " + PappusSmsClient.Sender + ").";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "خطا در ارتباط با پاپوس: " + ex.Message;
            }
            return RedirectToAction("SmsPanel");
        }

        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 8) return key;
            return key.Substring(0, 4) + new string('•', Math.Min(12, key.Length - 8)) + key.Substring(key.Length - 4);
        }
    }
}
