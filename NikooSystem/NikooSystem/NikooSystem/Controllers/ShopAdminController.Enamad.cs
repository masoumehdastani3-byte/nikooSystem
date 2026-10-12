using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>One item of the «آماده بودن برای اینماد» checklist.</summary>
    public sealed class EnamadCheck
    {
        public bool Ok { get; set; }
        /// <summary>Recommended, not required by Enamad.</summary>
        public bool Optional { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public string Action { get; set; }
        /// <summary>A link outside the panel; used instead of <see cref="Action"/>.</summary>
        public string Url { get; set; }
        public string LinkText { get; set; }
    }

    /// <summary>The «تأیید مالکیت دامنه» and «نماد اعتماد» cards of an Enamad page (a store's or the main site's).</summary>
    public sealed class SiteVerificationVm
    {
        public IVerifiedSite Site { get; set; }
        /// <summary>The controller with the EnamadFile, EnamadFileRemove, EnamadTitle, EnamadMeta and EnamadSeal actions.</summary>
        public string Controller { get; set; }
        public object Route { get; set; }
        public List<RootFile> Files { get; set; }
        /// <summary>Where the root files open now (the domain, or the free address before it is connected).</summary>
        public string SiteRoot { get; set; }
        /// <summary>The site's own domain, or null.</summary>
        public string Domain { get; set; }
        public string UsualTitle { get; set; }
        public bool Disabled { get; set; }
        public string CardClass { get; set; }
        public string ButtonClass { get; set; }
        public bool FreeAddressNote { get; set; }
    }

    /// <summary>
    /// «اینماد»: what the site needs before applying, the steps at enamad.ir, the ways of proving ownership of the domain
    /// (a file at the root of the domain, Enamad's meta tag or a temporary home title) and the trust seal of the footer.
    /// Everything here is done by the owner; only connecting the domain needs Nikoo's support (see the Domain page).
    /// </summary>
    public partial class ShopAdminController
    {
        [HttpGet]
        public ActionResult Enamad()
        {
            var settings = CurrentSettings;
            var domain = Tenant?.ActiveDomain();
            ViewBag.IsDemo = TenantStore.IsDemo(CompanyCode);
            ViewBag.Domain = domain;
            ViewBag.DomainState = Tenant?.DomainStatus() ?? DomainState.None;
            ViewBag.SiteRoot = domain != null ? "https://" + domain : Seo.SiteUrl(Request) + "/Shop/" + CompanyCode;
            ViewBag.Files = SiteVerification.Files(settings);
            ViewBag.Seal = SiteVerification.Seal(settings.EnamadHtml);
            var usual = settings.Clone();
            usual.VerifyHomeTitle = null;
            ViewBag.UsualTitle = SeoPages.StoreHomeTitle(usual);
            ViewBag.Checks = EnamadChecks(settings, domain);
            return View(settings);
        }

        private List<EnamadCheck> EnamadChecks(StoreSettings s, string domain)
        {
            var state = Tenant?.DomainStatus() ?? DomainState.None;
            var pages = TenantStore.GetPages(CompanyCode).Where(p => p.IsPublished).ToList();
            bool HasPage(string slug, params string[] words) => pages.Any(p =>
                string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase) || words.Any(w => (p.Title ?? "").Contains(w)));

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(s.Phone)) missing.Add("تلفن");
            if (string.IsNullOrWhiteSpace(s.Email)) missing.Add("ایمیل");
            if (string.IsNullOrWhiteSpace(s.Address)) missing.Add("نشانی");

            var checks = new List<EnamadCheck>
            {
                new EnamadCheck
                {
                    Ok = state == DomainState.Active,
                    Title = "دامنه اختصاصی با https",
                    Text = state == DomainState.Active
                        ? "سایت روی " + domain + " با گواهی امنیتی باز می‌شود."
                        : state == DomainState.None
                            ? "اینماد فقط برای دامنه‌ای صادر می‌شود که به نام خودتان ثبت شده؛ آدرس رایگان نیکو پذیرفته نمی‌شود."
                            : "اتصال دامنه " + Tenant.CustomDomain + " هنوز کامل نشده؛ پیش از ثبت در اینماد صبر کنید تا فعال شود.",
                    Action = "Domain",
                    LinkText = state == DomainState.Active ? "وضعیت دامنه" : "اتصال دامنه"
                },
                new EnamadCheck
                {
                    Ok = missing.Count == 0,
                    Title = "اطلاعات تماس کامل",
                    Text = missing.Count == 0
                        ? "تلفن، ایمیل و نشانی در فوتر و صفحه تماس دیده می‌شوند؛ باید با اطلاعاتی که در اینماد ثبت می‌کنید یکی باشند."
                        : string.Join("، ", missing) + " ثبت نشده است؛ اینماد اطلاعات تماس سایت را با اطلاعات پرونده شما مقایسه می‌کند.",
                    Action = "Settings",
                    LinkText = "اطلاعات " + Kind.SiteNoun
                },
                new EnamadCheck
                {
                    Ok = HasPage("about", "درباره"),
                    Title = "صفحه «درباره ما»",
                    Text = HasPage("about", "درباره") ? "منتشر شده است؛ معرفی واقعی کسب‌وکار را در آن بنویسید." : "صفحه‌ای با عنوان «درباره ما» بسازید و کسب‌وکارتان را معرفی کنید.",
                    Action = "Pages",
                    LinkText = "صفحات"
                },
                new EnamadCheck
                {
                    Ok = true,
                    Title = "صفحه «تماس با ما»",
                    Text = "خودکار ساخته شده و فرم تماس، تلفن، ایمیل و نشانی را نشان می‌دهد."
                },
                new EnamadCheck
                {
                    Ok = HasPage("terms", "قوانین", "شرایط", "حریم"),
                    Title = Kind.Sells ? "قوانین و شرایط فروش" : "قوانین و حریم خصوصی",
                    Text = HasPage("terms", "قوانین", "شرایط", "حریم")
                        ? "منتشر شده است" + (Kind.IsShop ? "؛ شرایط ارسال، مرجوعی و بازگشت وجه را هم در آن روشن بنویسید." : ".")
                        : "صفحه‌ای با عنوان «قوانین و مقررات» یا «شرایط فروش» بسازید" + (Kind.IsShop ? " و شرایط ارسال، مرجوعی و بازگشت وجه را بنویسید." : "."),
                    Action = "Pages",
                    LinkText = "صفحات"
                }
            };

            if (Kind.Sells)
            {
                var priced = TenantStore.GetProducts(CompanyCode).Count(p => p.IsActive && p.Price > 0);
                checks.Add(new EnamadCheck
                {
                    Ok = priced > 0,
                    Title = Kind.ItemsLabel + " با قیمت",
                    Text = priced > 0 ? priced.ToFa() + " مورد فعال با قیمت دارید؛ کارشناس اینماد باید بتواند کالا یا خدمت و قیمت آن را ببیند." : "دست‌کم چند " + Kind.ItemsLabel + " واقعی با عکس و قیمت منتشر کنید.",
                    Action = "Products",
                    LinkText = Kind.ItemsLabel
                });
                checks.Add(new EnamadCheck
                {
                    Ok = s.EnablePayment && !string.IsNullOrWhiteSpace(s.ZarinPalMerchantId),
                    Optional = true,
                    Title = "درگاه پرداخت",
                    Text = s.EnablePayment && !string.IsNullOrWhiteSpace(s.ZarinPalMerchantId)
                        ? "پرداخت آنلاین فعال است."
                        : "برای اینماد لازم نیست، اما بیشتر درگاه‌ها برای فعال شدن، اینماد می‌خواهند؛ بعد از گرفتن نماد، درگاه را وصل کنید.",
                    Action = "Settings",
                    LinkText = "تنظیمات پرداخت"
                });
            }
            return checks;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EnamadFile(HttpPostedFileBase file, string fileName, string fileContent)
        {
            if (TenantStore.IsDemo(CompanyCode)) return DemoEnamad();
            string name = fileName, content = fileContent;
            if (file != null && file.ContentLength > 0)
            {
                if (file.ContentLength > SiteVerification.MaxFileBytes)
                    return Fail("فایل بیشتر از ۱۰ کیلوبایت است؛ فایل تأیید اینماد و گوگل معمولاً فقط یک خط است.", "Enamad");
                using (var reader = new StreamReader(file.InputStream, Encoding.UTF8, true))
                    content = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(name)) name = file.FileName;
            }

            var settings = TenantStore.GetSettings(CompanyCode);
            var error = SiteVerification.AddFile(settings, name, content);
            if (error != null) return Fail(error, "Enamad");
            TenantStore.SaveSettings(settings);
            return Ok("فایل " + SiteVerification.CleanFileName(name) + " در ریشه سایت قرار گرفت؛ با لینک «باز کردن» آن را امتحان کنید.", "Enamad");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EnamadFileRemove(string name)
        {
            if (TenantStore.IsDemo(CompanyCode)) return DemoEnamad();
            var settings = TenantStore.GetSettings(CompanyCode);
            if (SiteVerification.RemoveFile(settings, name)) TenantStore.SaveSettings(settings);
            return Ok("فایل حذف شد.", "Enamad");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EnamadTitle(string verifyHomeTitle, bool remove = false)
        {
            if (TenantStore.IsDemo(CompanyCode)) return DemoEnamad();
            var settings = TenantStore.GetSettings(CompanyCode);
            settings.VerifyHomeTitle = remove ? null : SiteVerification.CleanTitle(verifyHomeTitle);
            TenantStore.SaveSettings(settings);
            return Ok(settings.VerifyHomeTitle == null
                ? "عنوان موقت حذف شد و صفحه اول عنوان همیشگی خودش را دارد."
                : "عنوان صفحه اول موقتاً عوض شد. بعد از تأیید اینماد، آن را حذف کنید تا عنوان همیشگی برای گوگل برگردد.", "Enamad");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EnamadMeta(string enamadMeta, bool remove = false)
        {
            if (TenantStore.IsDemo(CompanyCode)) return DemoEnamad();
            bool rejected = false;
            var code = remove ? null : SiteVerification.MetaCode(enamadMeta, out rejected);
            if (!remove && rejected)
                return Fail("کد متاتگ شناخته نشد؛ کل تگ <meta name=\"enamad\" …> یا فقط مقدار content آن را کپی کنید.", "Enamad");
            var settings = TenantStore.GetSettings(CompanyCode);
            settings.EnamadMeta = code;
            TenantStore.SaveSettings(settings);
            return Ok(code == null ? "متاتگ اینماد حذف شد." : "متاتگ اینماد در همه صفحه‌های سایت قرار گرفت.", "Enamad");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult EnamadSeal(string enamadHtml, bool remove = false)
        {
            if (TenantStore.IsDemo(CompanyCode)) return DemoEnamad();
            var seal = remove ? null : SiteVerification.Seal(enamadHtml);
            if (!remove && seal == null)
                return Fail("کد نماد شناخته نشد؛ کدی را که در پنل اینماد با «trustseal.enamad.ir» شروع می‌شود کامل کپی کنید.", "Enamad");
            var settings = TenantStore.GetSettings(CompanyCode);
            settings.EnamadHtml = SiteVerification.StoredSeal(seal);
            TenantStore.SaveSettings(settings);
            return Ok(seal == null ? "نماد اینماد از فوتر برداشته شد." : "نماد اینماد در فوتر همه صفحه‌ها قرار گرفت.", "Enamad");
        }

        private ActionResult DemoEnamad() =>
            Fail("در " + Kind.SiteNoun + " نمایشی اینماد ثبت نمی‌شود؛ این کار را در " + Kind.SiteNoun + " خودتان انجام دهید.", "Enamad");
    }
}
