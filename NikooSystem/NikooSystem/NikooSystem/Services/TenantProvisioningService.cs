using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;

namespace NikooSystem.Services
{
    /// <summary>ساخت خودکار فروشگاه آزمایشی و حذف آزمایشی‌های منقضی (اشتراک پس از آن: <see cref="Subscriptions"/>)</summary>
    public static class TenantProvisioningService
    {
        private const string DefaultLogoUrl = "/Assets/img/logo.png";

        private static readonly HashSet<string> ReservedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "www", "admin", "api", "app", "shop", "shopadmin", "shopaccount", "pay", "demo", "signup", "login",
            "mail", "email", "ftp", "smtp", "torob", "nikoo", "nikoooo", "support", "help", "blog", "static",
            "cdn", "uploads", "assets", "content", "scripts", "test", "dev", "home", "products", "checkout"
        };

        private static readonly Regex CodePattern = new Regex("^[a-z][a-z0-9]{2,19}$", RegexOptions.Compiled);

        private static readonly Regex DomainPattern = new Regex(
            @"^(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$", RegexOptions.Compiled);

        public static int TrialDays
        {
            get
            {
                var configured = SiteContentStore.Settings.TrialDays;
                if (configured > 0) return configured;
                int days;
                return int.TryParse(ConfigurationManager.AppSettings["Trial.Days"], out days) && days > 0 ? days : 7;
            }
        }

        /// <summary>دامنه اصلی پلتفرم برای زیردامنه رایگان هر فروشگاه (مثلاً nikoooo.ir)؛ خالی = غیرفعال</summary>
        public static string RootDomain
        {
            get { return (ConfigurationManager.AppSettings["Platform.RootDomain"] ?? "").Trim().TrimEnd('.').ToLowerInvariant(); }
        }

        /// <summary>IP سرور برای رکورد A دامنه اختصاصی مشتری (اولین IP ثبت‌شده)؛ خالی = ثبت نشده</summary>
        public static string ServerIp
        {
            get { return ServerIps.FirstOrDefault() ?? ""; }
        }

        /// <summary>IPهای سرور: تنظیم صفحه «دامنه‌های مشتریان» پنل نیکو، وگرنه Platform.ServerIp در Web.config</summary>
        public static List<string> ServerIps
        {
            get
            {
                var configured = SiteContentStore.Settings.ServerIp;
                if (string.IsNullOrWhiteSpace(configured)) configured = ConfigurationManager.AppSettings["Platform.ServerIp"];
                return (configured ?? "").Split(new[] { ',', ' ', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(ip => ip.Trim()).Distinct().ToList();
            }
        }

        public static string NormalizeCode(string input)
        {
            return (input ?? "").Trim().ToLowerInvariant();
        }

        /// <summary>خطای اعتبارسنجی آدرس فروشگاه یا null اگر آزاد باشد</summary>
        public static string ValidateCode(string code)
        {
            code = NormalizeCode(code);
            if (!CodePattern.IsMatch(code))
                return "آدرس فروشگاه باید ۳ تا ۲۰ حرف انگلیسی یا عدد باشد و با حرف شروع شود (بدون فاصله و علامت).";
            if (ReservedCodes.Contains(code))
                return "این آدرس رزرو شده است؛ آدرس دیگری انتخاب کنید.";
            if (TenantStore.CompanyCodeExists(code))
                return "این آدرس قبلاً گرفته شده است.";
            return null;
        }

        public static bool HasActiveTrial(string mobile)
        {
            return TenantStore.GetTenants().Any(t =>
                t.IsTrial && TenantStore.IsLive(t) &&
                string.Equals(t.OwnerMobile, mobile, StringComparison.Ordinal));
        }

        public static TenantInfo CreateTrialStore(StoreSignupVm vm)
        {
            var code = NormalizeCode(vm.StoreCode).ToUpperInvariant();
            var type = StoreTypes.Get(vm.StoreType);
            var template = StoreTemplates.All.FirstOrDefault(t => t.Id == vm.Template && t.Kinds.Contains(type.Kind))
                           ?? StoreTemplates.Get(type.DefaultTemplate);
            var storeName = vm.StoreName.Trim();

            var tenant = new TenantInfo
            {
                CompanyCode = code,
                Name = storeName,
                CustomDomain = "",
                IsActive = true,
                CreatedAt = DateTime.Now,
                StoreType = type.Id,
                OwnerName = vm.FullName.Trim(),
                OwnerMobile = vm.Mobile.Trim(),
                OwnerEmail = (vm.Email ?? "").Trim(),
                IsTrial = true,
                TrialEndsAt = DateTime.Now.AddDays(TrialDays)
            };

            TenantStore.AddTenant(tenant);
            try
            {
                TenantStore.SeedNewTenant(code, type.Id, s =>
                {
                    s.StoreName = storeName;
                    s.Tagline = type.Name;
                    s.About = storeName + " فروشگاه آنلاین " + type.Name + " است. این متن را از پنل مدیریت ویرایش کنید.";
                    s.Phone = tenant.OwnerMobile;
                    s.Email = tenant.OwnerEmail;
                    s.Address = "";
                    s.LogoUrl = null;
                    s.FooterText = "© " + storeName;
                    s.LayoutStyle = template.Id;
                    s.PrimaryColor = template.PrimaryColor;
                    s.SecondaryColor = template.SecondaryColor;
                    s.AdminUsername = tenant.OwnerMobile;
                    s.AdminPasswordHash = PasswordHelper.Hash(vm.Password);
                    s.ZarinPalMerchantId = "";
                    s.ZarinPalSandbox = false;
                    s.EnablePayment = false;
                    s.OnboardingCompleted = false;
                });
            }
            catch
            {
                TenantStore.RemoveTenant(code);
                TenantStore.DeleteTenantData(code);
                throw;
            }

            return tenant;
        }

        /// <summary>حذف فروشگاه‌های آزمایشی که مهلتشان تمام شده و پرداخت نکرده‌اند</summary>
        public static int PurgeExpiredTrials()
        {
            var expired = TenantStore.GetTenants().Where(t => t.IsTrialExpired()).ToList();
            foreach (var t in expired)
            {
                try
                {
                    TenantStore.RemoveTenant(t.CompanyCode);
                    TenantStore.DeleteTenantData(t.CompanyCode);
                }
                catch
                {
                    // در اجرای بعدی دوباره تلاش می‌شود
                }
            }
            return expired.Count;
        }

        public static string NormalizeDomain(string input)
        {
            var d = (input ?? "").Trim().ToLowerInvariant();
            if (d.StartsWith("http://")) d = d.Substring(7);
            if (d.StartsWith("https://")) d = d.Substring(8);
            var cut = d.IndexOfAny(new[] { '/', '?', '#', ':' });
            if (cut >= 0) d = d.Substring(0, cut);
            d = d.TrimEnd('.');
            if (d.StartsWith("www.")) d = d.Substring(4);
            return d;
        }

        /// <summary>خطای اعتبارسنجی دامنه اختصاصی یا null</summary>
        public static string ValidateDomain(string companyCode, string domain)
        {
            if (string.IsNullOrEmpty(domain)) return null;
            if (!DomainPattern.IsMatch(domain))
                return "دامنه معتبر نیست. نمونه درست: myshop.ir";
            var root = RootDomain;
            if (!string.IsNullOrEmpty(root) && (domain == root || domain.EndsWith("." + root)))
                return "این دامنه متعلق به پلتفرم است؛ دامنه اختصاصی خودتان را وارد کنید.";
            var taken = TenantStore.GetTenants().Any(t =>
                !string.Equals(t.CompanyCode, companyCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((t.CustomDomain ?? "").Trim(), domain, StringComparison.OrdinalIgnoreCase));
            return taken ? "این دامنه به فروشگاه دیگری متصل است." : null;
        }

        /// <summary>آدرس رایگان فروشگاه (زیردامنه پلتفرم یا مسیر /Shop/CODE)</summary>
        public static string DefaultStoreUrl(string companyCode, HttpRequestBase request)
        {
            if (!string.IsNullOrEmpty(RootDomain))
                return "https://" + companyCode.ToLowerInvariant() + "." + RootDomain;
            var authority = request?.Url != null ? request.Url.GetLeftPart(UriPartial.Authority) : "";
            return authority + "/Shop/" + companyCode;
        }

        public static string StorePreviewUrl(string code)
        {
            code = NormalizeCode(code);
            if (!string.IsNullOrEmpty(RootDomain)) return code + "." + RootDomain;
            return "/Shop/" + code.ToUpperInvariant();
        }

        /// <summary>محصول سایت نیکو که قیمت اشتراک فروشگاه از آن خوانده می‌شود (هم‌نوع فروشگاه)</summary>
        public static int? SubscriptionProductId(string storeType)
        {
            var products = SiteStore.GetProducts().Where(p => p.IsActive && !string.IsNullOrEmpty(p.DemoSlug)).ToList();
            var kind = StoreTypes.Get(storeType).Kind;
            var match = products.FirstOrDefault(p => p.DemoSlug == storeType)
                ?? products.FirstOrDefault(p => StoreTypes.Get(p.DemoSlug).Kind == kind)
                ?? products.FirstOrDefault();
            return match?.Id;
        }

        public static bool HasCustomLogo(StoreSettings s)
        {
            return !string.IsNullOrWhiteSpace(s?.LogoUrl) && s.LogoUrl != DefaultLogoUrl;
        }

        public static bool HasValidMerchant(StoreSettings s)
        {
            Guid g;
            return s != null && Guid.TryParse(s.ZarinPalMerchantId, out g);
        }
    }
}
