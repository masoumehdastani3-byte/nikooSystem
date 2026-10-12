using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// «اینماد» of the main site: what Enamad checks on Nikoo's own site (domain, contact details, terms, buy guide…),
    /// the steps at enamad.ir, proving ownership of the domain and the trust seal of the footer.
    /// </summary>
    public partial class AdminController
    {
        [HttpGet]
        public ActionResult Enamad()
        {
            var settings = SiteContentStore.LoadSettings();
            var siteUrl = Seo.SiteUrl(Request);
            var domain = MainDomain();
            ViewBag.Domain = domain;
            ViewBag.SiteRoot = siteUrl;
            ViewBag.Files = SiteVerification.Files(settings);
            var usual = new SiteSettings { SiteName = settings.SiteName, SeoTitle = settings.SeoTitle };
            ViewBag.UsualTitle = SeoPages.SiteHomeTitle(usual);
            ViewBag.Checks = MainEnamadChecks(settings, domain);
            return View(settings);
        }

        /// <summary>The main site's domain from Seo.SiteUrl when it is a real https address, or null.</summary>
        private static string MainDomain()
        {
            Uri uri;
            var setting = (ConfigurationManager.AppSettings["Seo.SiteUrl"] ?? "").Trim();
            if (!Uri.TryCreate(setting, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback) return null;
            return uri.Host;
        }

        private List<EnamadCheck> MainEnamadChecks(SiteSettings s, string domain)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(s.Address)) missing.Add("نشانی");
            if (string.IsNullOrWhiteSpace(s.PostalCode)) missing.Add("کد پستی");
            if (string.IsNullOrWhiteSpace(s.Phone)) missing.Add("تلفن");
            if (string.IsNullOrWhiteSpace(s.Email)) missing.Add("ایمیل");
            var digits = new string((s.Phone ?? "").Where(char.IsDigit).Select(c => (char)('0' + (int)char.GetNumericValue(c))).ToArray());
            var landline = digits.Length >= 10 && digits[0] == '0' && digits[1] != '9';
            var terms = s.TermsText ?? "";
            var termsParts = new[] { "بازگشت وجه", "شکایت" }.Where(w => !terms.Contains(w)).ToList();
            var priced = SiteStore.GetProducts().Count(p => p.IsActive && p.Price > 0);
            var merchant = ConfigurationManager.AppSettings["ZarinPal.MerchantId"] ?? "";
            var gateway = merchant.Length > 0 && !merchant.StartsWith("xxxx")
                && !string.Equals(ConfigurationManager.AppSettings["ZarinPal.Sandbox"], "true", StringComparison.OrdinalIgnoreCase);

            return new List<EnamadCheck>
            {
                new EnamadCheck
                {
                    Ok = domain != null,
                    Title = "دامنه با https",
                    Text = domain != null
                        ? "آدرس اصلی سایت " + domain + " است؛ همین دامنه را در اینماد ثبت کنید. دامنه باید به نام خودتان ثبت شده باشد."
                        : "آدرس اصلی سایت (Seo.SiteUrl) در Web.config سرور با https تنظیم نشده است؛ اینماد سایت را روی همان دامنه با گواهی امنیتی بررسی می‌کند.",
                    Action = "SeoGuide",
                    LinkText = "راهنمای سئو"
                },
                new EnamadCheck
                {
                    Ok = missing.Count == 0,
                    Title = "نشانی، کد پستی، تلفن و ایمیل",
                    Text = missing.Count == 0
                        ? "در فوتر، صفحه تماس و پایین قوانین دیده می‌شوند؛ باید با اطلاعات پرونده اینماد یکی باشند."
                        : string.Join("، ", missing) + " ثبت نشده است؛ اینماد اطلاعات تماس سایت را با اطلاعات پرونده شما مقایسه می‌کند.",
                    Action = "SiteSettings",
                    LinkText = "تنظیمات و متن‌ها"
                },
                new EnamadCheck
                {
                    Ok = landline,
                    Optional = true,
                    Title = "تلفن ثابت",
                    Text = landline ? "شماره تلفن ثابت با پیش‌شماره ثبت شده است." : "اینماد معمولاً یک تلفن ثابت با پیش‌شماره شهر می‌خواهد (مثل 021-12345678)؛ شماره موبایل به‌تنهایی کافی نیست.",
                    Action = "SiteSettings",
                    LinkText = "تنظیمات و متن‌ها"
                },
                new EnamadCheck
                {
                    Ok = !string.IsNullOrWhiteSpace(s.WorkingHours),
                    Optional = true,
                    Title = "ساعات پاسخگویی",
                    Text = !string.IsNullOrWhiteSpace(s.WorkingHours) ? "در صفحه تماس و قوانین نمایش داده می‌شود." : "ساعات پاسخگویی را بنویسید تا در صفحه تماس و قوانین دیده شود.",
                    Action = "SiteSettings",
                    LinkText = "تنظیمات و متن‌ها"
                },
                new EnamadCheck
                {
                    Ok = termsParts.Count == 0,
                    Title = "قوانین و مقررات",
                    Text = termsParts.Count == 0
                        ? "صفحه قوانین شرایط خدمات، پرداخت، تحویل، انصراف و بازگشت وجه و رسیدگی به شکایات را دارد."
                        : "متن قوانین بخش «" + string.Join("» و «", termsParts) + "» را ندارد؛ کارشناس اینماد این بخش‌ها را بررسی می‌کند.",
                    Url = Url.Action("Terms", "Home"),
                    LinkText = "مشاهده صفحه"
                },
                new EnamadCheck
                {
                    Ok = !string.IsNullOrWhiteSpace(s.PrivacyText),
                    Title = "حریم خصوصی",
                    Text = !string.IsNullOrWhiteSpace(s.PrivacyText) ? "صفحه حریم خصوصی منتشر شده است." : "متن حریم خصوصی خالی است.",
                    Url = Url.Action("Privacy", "Home"),
                    LinkText = "مشاهده صفحه"
                },
                new EnamadCheck
                {
                    Ok = true,
                    Title = "راهنمای خرید، درباره ما و تماس با ما",
                    Text = "صفحه‌ها منتشر شده‌اند و در فوتر همه صفحه‌ها لینک دارند؛ راهنمای خرید قیمت‌ها را از محصولات می‌خواند.",
                    Url = Url.Action("BuyGuide", "Home"),
                    LinkText = "راهنمای خرید"
                },
                new EnamadCheck
                {
                    Ok = priced > 0,
                    Title = "محصولات با قیمت",
                    Text = priced > 0 ? priced.ToFa() + " محصول فعال با قیمت دارید؛ کارشناس اینماد باید خدمت و قیمت آن را ببیند." : "هیچ محصول فعالی با قیمت ندارید.",
                    Action = "Products",
                    LinkText = "محصولات"
                },
                new EnamadCheck
                {
                    Ok = gateway,
                    Optional = true,
                    Title = "درگاه پرداخت",
                    Text = gateway ? "درگاه زرین‌پال سایت اصلی تنظیم شده است." : "کد مرچنت زرین‌پال سایت اصلی در Web.config تنظیم نشده یا روی حالت آزمایشی است؛ زرین‌پال برای فعال کردن درگاه، اینماد می‌خواهد."
                }
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EnamadFile(HttpPostedFileBase file, string fileName, string fileContent)
        {
            string name = fileName, content = fileContent;
            if (file != null && file.ContentLength > 0)
            {
                if (file.ContentLength > SiteVerification.MaxFileBytes)
                    return EnamadResult("فایل بیشتر از ۱۰ کیلوبایت است؛ فایل تأیید اینماد و گوگل معمولاً فقط یک خط است.", false);
                using (var reader = new StreamReader(file.InputStream, Encoding.UTF8, true))
                    content = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(name)) name = file.FileName;
            }

            string error = null;
            SiteContentStore.SaveVerification(s => error = SiteVerification.AddFile(s, name, content));
            return error != null
                ? EnamadResult(error, false)
                : EnamadResult("فایل " + SiteVerification.CleanFileName(name) + " در ریشه سایت قرار گرفت؛ با لینک «باز کردن» آن را امتحان کنید.", true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EnamadFileRemove(string name)
        {
            SiteContentStore.SaveVerification(s => SiteVerification.RemoveFile(s, name));
            return EnamadResult("فایل حذف شد.", true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EnamadTitle(string verifyHomeTitle, bool remove = false)
        {
            string title = null;
            SiteContentStore.SaveVerification(s => title = s.VerifyHomeTitle = remove ? null : SiteVerification.CleanTitle(verifyHomeTitle));
            return EnamadResult(title == null
                ? "عنوان موقت حذف شد و صفحه اول عنوان همیشگی خودش را دارد."
                : "عنوان صفحه اول موقتاً عوض شد. بعد از تأیید اینماد، آن را حذف کنید تا عنوان همیشگی برای گوگل برگردد.", true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EnamadMeta(string enamadMeta, bool remove = false)
        {
            bool rejected = false;
            var code = remove ? null : SiteVerification.MetaCode(enamadMeta, out rejected);
            if (!remove && rejected)
                return EnamadResult("کد متاتگ شناخته نشد؛ کل تگ <meta name=\"enamad\" …> یا فقط مقدار content آن را کپی کنید.", false);
            SiteContentStore.SaveVerification(s => s.EnamadMeta = code);
            return EnamadResult(code == null ? "متاتگ اینماد حذف شد." : "متاتگ اینماد در همه صفحه‌های سایت اصلی قرار گرفت.", true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EnamadSeal(string enamadHtml, bool remove = false)
        {
            var seal = remove ? null : SiteVerification.Seal(enamadHtml);
            if (!remove && seal == null)
                return EnamadResult("کد نماد شناخته نشد؛ کدی را که در پنل اینماد با «trustseal.enamad.ir» شروع می‌شود کامل کپی کنید.", false);
            SiteContentStore.SaveVerification(s => s.EnamadHtml = SiteVerification.StoredSeal(seal));
            return EnamadResult(seal == null ? "نماد اینماد از فوتر برداشته شد." : "نماد اینماد در فوتر همه صفحه‌های سایت اصلی قرار گرفت.", true);
        }

        private ActionResult EnamadResult(string message, bool ok)
        {
            TempData[ok ? "Ok" : "Error"] = message;
            return RedirectToAction("Enamad");
        }
    }
}
