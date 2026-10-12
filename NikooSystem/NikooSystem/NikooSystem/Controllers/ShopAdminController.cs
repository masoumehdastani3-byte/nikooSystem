using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// The store owner's panel. This file holds sign-in, the dashboard, the setup wizard and settings; the catalogue, sales
    /// and content sections are in the other ShopAdminController.*.cs files.
    /// </summary>
    [ShopAdminOnly]
    [NoIndex]
    public partial class ShopAdminController : ShopBaseController
    {
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login()
        {
            if (IsShopAdmin) return ToAction("Index");
            return View(new StoreLoginVm());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(StoreLoginVm model)
        {
            if (!ModelState.IsValid) return View(model);
            if (string.Equals(model.Username, CurrentSettings.AdminUsername, StringComparison.OrdinalIgnoreCase) &&
                PasswordHelper.Verify(model.Password, CurrentSettings.AdminPasswordHash))
            {
                IsShopAdmin = true;
                return ToAction("Index");
            }
            ModelState.AddModelError("", "نام کاربری یا رمز عبور اشتباه است.");
            return View(model);
        }

        [AllowAnonymous]
        public ActionResult Logout()
        {
            IsShopAdmin = false;
            return ToAction("Login");
        }

        /// <summary>Tutorial pages with their current and recorded fingerprints; read by tools/tutorials/record.mjs.</summary>
        [HttpGet]
        public ActionResult TutorialIndex()
        {
            return Json(TutorialService.Statuses(), JsonRequestBehavior.AllowGet);
        }

        public ActionResult Index()
        {
            ViewBag.Checklist = BuildChecklist();
            ViewBag.Visits = VisitStats.Report(CompanyCode);
            if (Kind.IsContent)
            {
                ViewBag.PostCount = TenantStore.CountPosts(CompanyCode);
                ViewBag.PostViews = TenantStore.TotalPostViews(CompanyCode);
                ViewBag.PendingComments = TenantStore.CountPendingComments(CompanyCode);
                ViewBag.UnreadMessages = TenantStore.CountUnreadMessages(CompanyCode);
                ViewBag.LatestPosts = TenantStore.PostGrid(CompanyCode, new PostGridQuery { Size = 10 }).ToList();
                return View();
            }
            if (Kind.IsBooking)
            {
                KeepDemoAppointmentsFresh();
                var today = DateTime.Today;
                var monthStart = TenantStore.PersianMonthStart(today);
                var active = new[] { AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.PendingPayment };
                ViewBag.TodayCount = TenantStore.CountAppointmentsToday(CompanyCode);
                ViewBag.WeekCount = TenantStore.CountAppointments(CompanyCode, DateTime.Now, today.AddDays(8), active);
                ViewBag.MonthRevenue = TenantStore.BookingRevenue(CompanyCode, monthStart, today.AddDays(1));
                ViewBag.MonthNoShows = TenantStore.CountAppointments(CompanyCode, monthStart, today.AddDays(1), AppointmentStatus.NoShow);
                ViewBag.UnreadMessages = TenantStore.CountUnreadMessages(CompanyCode);
                ViewBag.TodayAgenda = TenantStore.AppointmentsBetween(CompanyCode, today, today.AddDays(1))
                    .Where(a => a.Status != AppointmentStatus.Cancelled).ToList();
                return View();
            }

            ViewBag.ProductCount = TenantStore.CountProducts(CompanyCode);
            ViewBag.OrderCount = TenantStore.CountOrders(CompanyCode);
            ViewBag.CustomerCount = TenantStore.CountCustomers(CompanyCode);
            ViewBag.Revenue = TenantStore.Revenue(CompanyCode);
            ViewBag.UnreadMessages = TenantStore.CountUnreadMessages(CompanyCode);
            ViewBag.RecentOrders = TenantStore.RecentOrders(CompanyCode, 8);
            ViewBag.LowStock = Kind.IsShop
                ? TenantStore.LowStockProducts(CompanyCode, CurrentSettings.LowStockThreshold, 8).ToList()
                : new List<StoreProduct>();
            return View();
        }

        private List<SetupChecklistItem> BuildChecklist()
        {
            var s = CurrentSettings;
            var items = new List<SetupChecklistItem>
            {
                new SetupChecklistItem
                {
                    Title = "لوگو و نام سایت",
                    Hint = "لوگوی خودتان را بارگذاری کنید تا مخاطب سایت را بشناسد.",
                    Done = TenantProvisioningService.HasCustomLogo(s),
                    Url = SetupUrl(1)
                },
                new SetupChecklistItem
                {
                    Title = "اطلاعات تماس",
                    Hint = "تلفن و آدرس در فوتر و صفحه تماس نمایش داده می‌شود.",
                    Done = !string.IsNullOrWhiteSpace(s.Phone) && !string.IsNullOrWhiteSpace(s.Address),
                    Url = SetupUrl(1)
                }
            };

            if (Kind.IsContent)
            {
                items.Add(new SetupChecklistItem
                {
                    Title = "انتشار اولین مطلب واقعی",
                    Hint = "خبر یا مقاله خودتان را با تصویر و خلاصه منتشر کنید.",
                    Done = TenantStore.CountPosts(CompanyCode, false) > 0,
                    Url = SetupUrl(3)
                });
                items.Add(new SetupChecklistItem
                {
                    Title = "حذف مطالب نمونه",
                    Hint = "مطالب نمونه فقط برای نمایش ظاهر سایت هستند.",
                    Done = TenantStore.CountPosts(CompanyCode, true) == 0,
                    Url = SetupUrl(3)
                });
            }
            else if (Kind.IsBooking)
            {
                items.Add(new SetupChecklistItem
                {
                    Title = "تعریف خدمات واقعی",
                    Hint = "خدمت خودتان را با مدت و هزینه تعریف کنید تا مراجعان برایش نوبت بگیرند.",
                    Done = TenantStore.CountServices(CompanyCode, false) > 0,
                    Url = SetupUrl(3)
                });
                items.Add(new SetupChecklistItem
                {
                    Title = "معرفی متخصصان و ساعت کاری",
                    Hint = "متخصصان نمونه را با افراد واقعی و ساعت کاری‌شان جایگزین کنید؛ نوبت‌ها از همین ساعت‌ها ساخته می‌شوند.",
                    Done = TenantStore.CountStaff(CompanyCode, false) > 0 && TenantStore.CountStaff(CompanyCode, true) == 0,
                    Url = Url.Action("Staff", new { company = CompanyCode })
                });
                items.Add(new SetupChecklistItem
                {
                    Title = "حذف خدمات نمونه",
                    Hint = "خدمات نمونه فقط برای نمایش ظاهر سایت هستند.",
                    Done = TenantStore.CountServices(CompanyCode, true) == 0,
                    Url = SetupUrl(3)
                });
                items.Add(new SetupChecklistItem
                {
                    Title = "دریافت بیعانه آنلاین",
                    Hint = "با اتصال زرین‌پال و تعیین درصد بیعانه، نوبت‌های بدون مراجعه کم می‌شود.",
                    Done = s.EnablePayment && TenantProvisioningService.HasValidMerchant(s) && s.BookingDepositPercent > 0,
                    Optional = true,
                    Url = SetupUrl(4)
                });
            }
            else
            {
                var itemName = Kind.IsDigital ? "دوره یا کتاب" : "محصول";
                items.Add(new SetupChecklistItem
                {
                    Title = "افزودن اولین " + itemName + " واقعی",
                    Hint = Kind.IsDigital ? "کتاب یا دوره خودتان را اضافه کنید و فایل‌ها و جلسه‌هایش را بارگذاری کنید." : "محصول خودتان را با عکس و قیمت اضافه کنید.",
                    Done = TenantStore.CountProducts(CompanyCode, false) > 0,
                    Url = SetupUrl(3)
                });
                items.Add(new SetupChecklistItem
                {
                    Title = "حذف نمونه‌ها",
                    Hint = "موارد نمونه فقط برای نمایش ظاهر سایت هستند.",
                    Done = TenantStore.CountProducts(CompanyCode, true) == 0,
                    Url = SetupUrl(3)
                });
                items.Add(new SetupChecklistItem
                {
                    Title = "اتصال درگاه پرداخت زرین‌پال",
                    Hint = Kind.IsDigital
                        ? "بدون درگاه، خریدار تا تأیید دستی شما به فایل‌ها دسترسی ندارد."
                        : "بدون درگاه، سفارش ثبت می‌شود ولی پرداخت آنلاین انجام نمی‌شود.",
                    Done = s.EnablePayment && TenantProvisioningService.HasValidMerchant(s),
                    Url = SetupUrl(4)
                });
            }

            var domainState = Tenant?.DomainStatus() ?? DomainState.None;
            items.Add(new SetupChecklistItem
            {
                Title = "اتصال دامنه اختصاصی",
                Hint = domainState == DomainState.WaitingDns ? "دامنه ثبت شده؛ رکوردهای DNS را طبق راهنما تنظیم کنید."
                    : domainState == DomainState.WaitingSupport ? "DNS درست است؛ پشتیبانی نیکو در حال فعال‌سازی SSL است."
                    : "اختیاری؛ سایت همین حالا با آدرس رایگان در دسترس است.",
                Done = domainState == DomainState.Active,
                Optional = true,
                Url = Url.Action("Domain", new { company = CompanyCode })
            });
            return items;
        }

        private string SetupUrl(int step) => Url.Action("Setup", new { company = CompanyCode, step });

        // ---- Setup wizard

        [HttpGet]
        public ActionResult Setup(int step = 1)
        {
            step = Math.Max(1, Math.Min(5, step));
            ViewBag.Step = step;
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode);
            ViewBag.SampleCount = Kind.IsContent ? TenantStore.CountPosts(CompanyCode, true)
                : Kind.IsBooking ? TenantStore.CountServices(CompanyCode, true) + TenantStore.CountStaff(CompanyCode, true)
                : TenantStore.CountProducts(CompanyCode, true);
            ViewBag.RealCount = Kind.IsContent ? TenantStore.CountPosts(CompanyCode, false)
                : Kind.IsBooking ? TenantStore.CountServices(CompanyCode, false)
                : TenantStore.CountProducts(CompanyCode, false);
            ViewBag.StoreUrl = TenantProvisioningService.DefaultStoreUrl(CompanyCode, Request);
            ViewBag.Welcome = TempData["Welcome"] != null;
            return View(CurrentSettings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupInfo(string storeName, string tagline, string phone, string address, HttpPostedFileBase logoFile)
        {
            if (string.IsNullOrWhiteSpace(storeName))
                return SetupError(1, "نام سایت را وارد کنید.");

            var s = CurrentSettings;
            var imageError = SaveBrandImage(logoFile, BrandImageKind.Logo, url => s.LogoUrl = url);
            if (imageError != null) return SetupError(1, imageError);

            var oldName = s.StoreName;
            s.StoreName = storeName.Trim();
            if (!string.IsNullOrEmpty(oldName) && oldName != s.StoreName)
            {
                if (s.About != null) s.About = s.About.Replace(oldName, s.StoreName);
                if (s.FooterText != null) s.FooterText = s.FooterText.Replace(oldName, s.StoreName);
            }
            s.Tagline = (tagline ?? "").Trim();
            s.Phone = (phone ?? "").Trim();
            s.Address = (address ?? "").Trim();
            TenantStore.SaveSettings(s);

            if (Tenant != null && Tenant.Name != s.StoreName)
            {
                Tenant.Name = s.StoreName;
                TenantStore.UpdateTenant(Tenant);
            }
            return ToAction("Setup", new { step = 2 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupLook(string layoutStyle, string primaryColor, string secondaryColor, string heroTitle, string heroSubtitle, HttpPostedFileBase heroFile)
        {
            var s = CurrentSettings;
            var imageError = SaveBrandImage(heroFile, BrandImageKind.Hero, url => s.HeroImageUrl = url);
            if (imageError != null) return SetupError(2, imageError);

            s.LayoutStyle = StoreTemplates.NormalizeFor(layoutStyle, Kind.Id);
            if (!string.IsNullOrWhiteSpace(primaryColor)) s.PrimaryColor = primaryColor;
            if (!string.IsNullOrWhiteSpace(secondaryColor)) s.SecondaryColor = secondaryColor;
            if (!string.IsNullOrWhiteSpace(heroTitle)) s.HeroTitle = heroTitle.Trim();
            if (heroSubtitle != null) s.HeroSubtitle = heroSubtitle.Trim();
            TenantStore.SaveSettings(s);
            return ToAction("Setup", new { step = 3 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult SetupProduct(string title, long? price, int? categoryId, int? stock, string shortDescription, string author,
            HttpPostedFileBase imageFile, StoreProductType productType = StoreProductType.Physical, bool removeSamples = false)
        {
            if (removeSamples)
                TenantStore.DeleteSampleProducts(CompanyCode);

            if (!string.IsNullOrWhiteSpace(title))
            {
                if (!price.HasValue || price.Value < 0)
                    return SetupError(3, "قیمت را به تومان وارد کنید.");

                var product = new StoreProduct
                {
                    Title = title.Trim(),
                    ProductType = AllowedProductType(productType),
                    ShortDescription = (shortDescription ?? "").Trim(),
                    Description = (shortDescription ?? "").Trim(),
                    Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim(),
                    Price = price.Value,
                    CategoryId = ExistingCategory(categoryId),
                    Stock = stock ?? 10,
                    IsActive = true,
                    IsFeatured = true
                };

                try
                {
                    AddImages(product, new[] { ImageService.SaveProductImage(CompanyCode, imageFile) });
                }
                catch (InvalidOperationException ex)
                {
                    return SetupError(3, ex.Message);
                }

                product = TenantStore.SaveProduct(CompanyCode, product);
                TorobWebhookService.NotifyProductChanged(CompanyCode, product);
                Ok("«" + product.Title + "» اضافه شد." + (product.IsDigital() ? " فایل‌ها و جلسه‌ها را از صفحه ویرایش آن بارگذاری کنید." : ""));
            }

            return ToAction("Setup", new { step = 4 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Content)]
        public ActionResult SetupPost(string title, string summary, int? categoryId, HttpPostedFileBase coverFile, bool removeSamples = false)
        {
            if (removeSamples)
                TenantStore.DeleteSamplePosts(CompanyCode);

            if (!string.IsNullOrWhiteSpace(title))
            {
                var post = new StorePost
                {
                    Title = title.Trim(),
                    Summary = (summary ?? "").Trim(),
                    BodyHtml = "<p>" + HttpUtility.HtmlEncode((summary ?? "").Trim()) + "</p>",
                    CategoryId = ExistingCategory(categoryId),
                    Author = CurrentSettings.StoreName,
                    IsFeatured = true,
                    PublishedAt = DateTime.Now
                };
                try
                {
                    post.CoverImageUrl = ImageService.SaveProductImage(CompanyCode, coverFile)?.LargeUrl;
                }
                catch (InvalidOperationException ex)
                {
                    return SetupError(3, ex.Message);
                }
                post = TenantStore.SavePost(CompanyCode, post);
                Ok("مطلب «" + post.Title + "» منتشر شد.");
            }
            return ToAction("Setup", new { step = 4 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking)]
        public ActionResult SetupService(string title, int? durationMinutes, long? price, bool removeSamples = false)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                if (!durationMinutes.HasValue || durationMinutes.Value < 5)
                    return SetupError(3, "مدت خدمت را به دقیقه وارد کنید.");
                var service = TenantStore.SaveService(CompanyCode, new StoreService
                {
                    Title = Clip(title, 150),
                    DurationMinutes = Math.Min(480, durationMinutes.Value),
                    Price = Math.Max(0, price ?? 0),
                    IsActive = true,
                    SortOrder = 0
                });
                Ok("خدمت «" + service.Title + "» اضافه شد؛ همه متخصصان آن را ارائه می‌دهند و از صفحه خدمات می‌توانید تغییرش دهید.");
            }
            if (removeSamples)
            {
                if (TenantStore.CountServices(CompanyCode, false) == 0)
                    return SetupError(3, "پیش از حذف خدمات نمونه دست‌کم یک خدمت واقعی تعریف کنید؛ وگرنه نوبت‌دهی متوقف می‌شود.");
                foreach (var sample in TenantStore.GetServices(CompanyCode).Where(x => x.IsSample))
                    TenantStore.DeleteService(CompanyCode, sample.Id);
                if (TenantStore.CountStaff(CompanyCode, false) > 0)
                {
                    foreach (var sample in TenantStore.GetStaff(CompanyCode).Where(x => x.IsSample))
                        TenantStore.DeleteStaff(CompanyCode, sample.Id);
                }
                else
                {
                    Fail("خدمات نمونه حذف شد. متخصصان نمونه ماندند تا نوبت‌دهی قطع نشود؛ از صفحه «متخصصان و ساعت کاری» آن‌ها را با افراد واقعی جایگزین کنید.");
                }
            }
            return ToAction("Setup", new { step = 4 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Booking)]
        public ActionResult SetupPayment(string merchantId, bool enablePayment = false, int? depositPercent = null)
        {
            var s = CurrentSettings;
            merchantId = (merchantId ?? "").Trim();

            if (merchantId.Length > 0 && !Guid.TryParse(merchantId, out _))
                return SetupError(4, "مرچنت کد زرین‌پال ۳۶ کاراکتر است؛ آن را دقیقاً از پنل زرین‌پال کپی کنید.");
            if (enablePayment && merchantId.Length == 0)
                return SetupError(4, "برای فعال‌سازی پرداخت آنلاین، مرچنت کد را وارد کنید.");

            s.ZarinPalMerchantId = merchantId;
            s.ZarinPalSandbox = false;
            s.EnablePayment = enablePayment && merchantId.Length > 0;
            if (Kind.IsBooking && depositPercent.HasValue) s.BookingDepositPercent = Math.Max(0, Math.Min(100, depositPercent.Value));
            TenantStore.SaveSettings(s);
            return ToAction("Setup", new { step = 5 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Content)]
        public ActionResult SetupEngagement(string email, bool moderateComments = false)
        {
            var s = CurrentSettings;
            s.ModerateComments = moderateComments;
            if (!string.IsNullOrWhiteSpace(email)) s.Email = email.Trim();
            TenantStore.SaveSettings(s);
            return ToAction("Setup", new { step = 5 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupDomain(string customDomain)
        {
            var error = SaveCustomDomain(customDomain);
            if (error != null) return SetupError(5, error);

            var s = CurrentSettings;
            s.OnboardingCompleted = true;
            TenantStore.SaveSettings(s);
            return Ok("تبریک! سایت شما آماده است.", "Index");
        }

        private ActionResult SetupError(int step, string message)
        {
            return Fail(message, "Setup", new { step });
        }

        /// <summary>The category when it belongs to the store, else the store's first category (0 when there is none).</summary>
        private int ExistingCategory(int? categoryId)
        {
            var categories = TenantStore.GetCategories(CompanyCode);
            return categoryId.HasValue && categories.Any(c => c.Id == categoryId.Value)
                ? categoryId.Value
                : categories.Select(c => c.Id).FirstOrDefault();
        }

        /// <summary>Physical shops sell physical goods only; digital stores sell books, courses and files.</summary>
        private StoreProductType AllowedProductType(StoreProductType requested)
        {
            if (!Kind.IsDigital) return StoreProductType.Physical;
            return requested == StoreProductType.Physical ? StoreProductType.Ebook : requested;
        }

        /// <summary>ذخیره دامنه اختصاصی در رجیستری؛ پیام خطا یا null</summary>
        private string SaveCustomDomain(string input)
        {
            if (Tenant == null) return null;
            if (TenantStore.IsDemo(CompanyCode) && !string.IsNullOrWhiteSpace(input))
                return "به سایت‌های نمایشی دامنه وصل نمی‌شود؛ این کار را در سایت خودتان انجام دهید.";
            return CustomDomains.Change(Tenant, input);
        }

        /// <summary>Saves an uploaded store image and hands its URL to <paramref name="apply"/>; returns the error message, or null.</summary>
        private string SaveBrandImage(HttpPostedFileBase file, BrandImageKind kind, Action<string> apply)
        {
            try
            {
                var url = ImageService.SaveBrandImage(CompanyCode, file, kind);
                if (url != null) apply(url);
                return null;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        private static string ImageErrors(IEnumerable<string> errors) => "این تصاویر ذخیره نشدند — " + string.Join(" | ", errors);

        // ---- Settings

        [HttpGet]
        public ActionResult Settings()
        {
            ViewBag.StoreUrl = TenantProvisioningService.DefaultStoreUrl(CompanyCode, Request);
            return View(CurrentSettings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Settings(StoreSettings model, string newPassword, HttpPostedFileBase logoFile, HttpPostedFileBase heroFile, bool removeLogo = false)
        {
            var existing = CurrentSettings ?? TenantStore.GetSettings(CompanyCode);
            model.CompanyCode = CompanyCode;
            model.LogoUrl = removeLogo ? null : existing?.LogoUrl;
            model.FaviconUrl = existing?.FaviconUrl;
            model.OnboardingCompleted = existing?.OnboardingCompleted ?? false;
            if (string.IsNullOrWhiteSpace(model.HeroImageUrl))
                model.HeroImageUrl = existing?.HeroImageUrl;

            var imageError = SaveBrandImage(logoFile, BrandImageKind.Logo, url => model.LogoUrl = url)
                ?? SaveBrandImage(heroFile, BrandImageKind.Hero, url => model.HeroImageUrl = url);
            if (imageError != null) return Fail(imageError, "Settings");

            model.AdminUsername = string.IsNullOrWhiteSpace(model.AdminUsername)
                ? (existing?.AdminUsername ?? "admin")
                : model.AdminUsername;
            model.AdminPasswordHash = string.IsNullOrWhiteSpace(newPassword)
                ? (existing?.AdminPasswordHash ?? PasswordHelper.Hash("Admin@123"))
                : PasswordHelper.Hash(newPassword);
            model.PageLayout = existing?.PageLayout;
            model.EnableTorob = Kind.IsShop && existing?.EnableTorob == true;
            model.TorobWebhookBearerToken = existing?.TorobWebhookBearerToken;
            model.PublicStoreBaseUrl = existing?.PublicStoreBaseUrl;
            model.EnamadHtml = existing?.EnamadHtml;
            model.EnamadMeta = existing?.EnamadMeta;
            model.VerifyHomeTitle = existing?.VerifyHomeTitle;
            model.RootFiles = existing?.RootFiles;
            model.LayoutStyle = StoreTemplates.NormalizeFor(model.LayoutStyle, Kind.Id);
            model.ImageFit = StoreImageFit.Normalize(model.ImageFit);
            KeepSettingsOfOtherKinds(model, existing);
            model.ShippingCost = Math.Max(0, model.ShippingCost);
            model.FreeShippingThreshold = Math.Max(0, model.FreeShippingThreshold);
            model.LowStockThreshold = Math.Max(0, model.LowStockThreshold);
            CleanSeo(model);
            var verification = Seo.VerificationCode(model.GoogleVerification);
            var verificationRejected = !string.IsNullOrWhiteSpace(model.GoogleVerification) && verification == null;
            model.GoogleVerification = verificationRejected ? existing?.GoogleVerification : verification;
            TenantStore.SaveSettings(model);

            return verificationRejected
                ? Fail("سایر تنظیمات ذخیره شد، اما کد تأیید گوگل شناخته نشد؛ تگ meta یا مقدار content آن را از سرچ کنسول کپی کنید.", "Settings")
                : Ok("تنظیمات ذخیره شد.", "Settings");
        }

        /// <summary>The settings form shows only the fields of this kind of site; the others keep their stored values.</summary>
        private void KeepSettingsOfOtherKinds(StoreSettings model, StoreSettings existing)
        {
            if (existing == null) return;
            if (!Kind.IsShop)
            {
                model.ShippingCost = existing.ShippingCost;
                model.FreeShippingThreshold = existing.FreeShippingThreshold;
                model.LowStockThreshold = existing.LowStockThreshold;
            }
            if (!Kind.IsContent) model.ModerateComments = existing.ModerateComments;
            if (Kind.IsContent)
            {
                model.EnablePayment = existing.EnablePayment;
                model.ZarinPalMerchantId = existing.ZarinPalMerchantId;
                model.ZarinPalSandbox = existing.ZarinPalSandbox;
            }
            if (Kind.IsBooking)
            {
                model.BookingSlotMinutes = Math.Max(5, Math.Min(120, model.BookingSlotMinutes));
                model.BookingDaysAhead = Math.Max(1, Math.Min(90, model.BookingDaysAhead));
                model.BookingMinNoticeMinutes = Math.Max(0, Math.Min(7 * 24 * 60, model.BookingMinNoticeMinutes));
                model.BookingDepositPercent = Math.Max(0, Math.Min(100, model.BookingDepositPercent));
                model.BookingCancelHours = Math.Max(0, Math.Min(14 * 24, model.BookingCancelHours));
                model.BookingNotice = model.BookingNotice?.Trim();
                if (model.BookingNotice?.Length > 1000) model.BookingNotice = model.BookingNotice.Substring(0, 1000);
            }
            else
            {
                model.BookingSlotMinutes = existing.BookingSlotMinutes;
                model.BookingDaysAhead = existing.BookingDaysAhead;
                model.BookingMinNoticeMinutes = existing.BookingMinNoticeMinutes;
                model.BookingDepositPercent = existing.BookingDepositPercent;
                model.BookingCancelHours = existing.BookingCancelHours;
                model.BookingNotice = existing.BookingNotice;
            }
        }
    }
}
