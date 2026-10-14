using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>تنظیمات، پیام‌های تماس، سوالات متداول و نظرات مشتریان سایت اصلی</summary>
    public static class SiteContentStore
    {
        private static readonly object SettingsLock = new object();
        private static readonly object ListLock = new object();
        private static SiteSettings _settings;

        // ---- Settings

        /// <summary>تنظیمات فعلی برای نمایش؛ تا ذخیره بعدی در حافظه می‌ماند و نباید تغییرش داد</summary>
        public static SiteSettings Settings
        {
            get
            {
                var cached = _settings;
                if (cached != null) return cached;
                lock (SettingsLock)
                {
                    return _settings ?? (_settings = NikooDb.Get(NikooTables.SiteSettings, NikooDb.Key(1)) ?? DefaultSettings());
                }
            }
        }

        /// <summary>یک نسخه تازه از دیتابیس برای فرم ویرایش</summary>
        public static SiteSettings LoadSettings()
        {
            return NikooDb.Get(NikooTables.SiteSettings, NikooDb.Key(1)) ?? DefaultSettings();
        }

        /// <summary>متن‌ها و تنظیمات فرم را ذخیره می‌کند؛ نام کاربری و رمز پنل دست نمی‌خورند</summary>
        public static void SaveSettings(SiteSettings form)
        {
            lock (SettingsLock)
            {
                var stored = LoadSettings();
                form.Id = 1;
                form.AdminUsername = stored.AdminUsername;
                form.AdminPasswordHash = stored.AdminPasswordHash;
                form.ServerIp = stored.ServerIp;
                form.EnamadHtml = stored.EnamadHtml;
                form.EnamadMeta = stored.EnamadMeta;
                form.VerifyHomeTitle = stored.VerifyHomeTitle;
                form.RootFiles = stored.RootFiles;
                form.UpdatedAt = DateTime.Now;
                NikooDb.Upsert(NikooTables.SiteSettings, form);
                _settings = null;
            }
        }

        /// <summary>Changes the Enamad and domain verification fields (the «اینماد» page) and keeps everything else.</summary>
        public static void SaveVerification(Action<SiteSettings> change)
        {
            lock (SettingsLock)
            {
                var stored = LoadSettings();
                change(stored);
                stored.Id = 1;
                NikooDb.Upsert(NikooTables.SiteSettings, stored);
                _settings = null;
            }
        }

        public static void SaveServerIp(string ips)
        {
            lock (SettingsLock)
            {
                var stored = LoadSettings();
                stored.ServerIp = string.IsNullOrWhiteSpace(ips) ? null : ips;
                stored.UpdatedAt = DateTime.Now;
                NikooDb.Upsert(NikooTables.SiteSettings, stored);
                _settings = null;
            }
        }

        // ---- Admin login

        public static string AdminUsername
        {
            get
            {
                var custom = Settings.AdminUsername;
                return !string.IsNullOrWhiteSpace(custom) ? custom : ConfigurationManager.AppSettings["Admin.Username"] ?? "admin";
            }
        }

        public static bool CheckAdminLogin(string username, string password)
        {
            if (!string.Equals(username ?? "", AdminUsername, StringComparison.Ordinal)) return false;
            var hash = Settings.AdminPasswordHash;
            if (!string.IsNullOrEmpty(hash)) return PasswordHelper.Verify(password, hash);
            return string.Equals(password ?? "", ConfigurationManager.AppSettings["Admin.Password"] ?? "Admin@123", StringComparison.Ordinal);
        }

        /// <summary>رمز هنوز همان رمز پیش‌فرض Web.config است (صفحه ورود آن را نشان می‌دهد)</summary>
        public static bool UsesDefaultPassword =>
            string.IsNullOrEmpty(Settings.AdminPasswordHash) && (ConfigurationManager.AppSettings["Admin.Password"] ?? "Admin@123") == "Admin@123";

        public static void ChangeAdminCredentials(string username, string newPassword)
        {
            lock (SettingsLock)
            {
                var stored = LoadSettings();
                stored.AdminUsername = username.Trim();
                stored.AdminPasswordHash = PasswordHelper.Hash(newPassword);
                stored.UpdatedAt = DateTime.Now;
                NikooDb.Upsert(NikooTables.SiteSettings, stored);
                _settings = null;
            }
        }

        // ---- Contact messages

        private static readonly Dictionary<string, string> MessageSorts = new Dictionary<string, string>
        {
            { "created", "t.[CreatedAt]" },
            { "name", "t.[Name]" },
            { "subject", "t.[Subject]" }
        };

        public static SiteMessage AddMessage(SiteMessage message)
        {
            lock (ListLock)
            using (var tx = NikooDb.Begin())
            {
                message.Id = NikooDb.NextId(NikooTables.SiteMessages, null, tx);
                message.CreatedAt = DateTime.Now;
                message.IsRead = false;
                NikooDb.Insert(NikooTables.SiteMessages, message, tx: tx);
                tx.Commit();
                return message;
            }
        }

        /// <param name="status">unread | read | null</param>
        public static PagedList<SiteMessage> MessageGrid(GridQuery query, string status)
        {
            var filter = new SqlFilter().Search(query.Q, "Name", "Email", "Phone", "Subject", "Message");
            if (status == "unread") filter.Eq("IsRead", false);
            else if (status == "read") filter.Eq("IsRead", true);
            return NikooDb.Page(NikooTables.SiteMessages, filter, query.OrderBy(MessageSorts, "created", true), query.Page, query.PageSize);
        }

        public static List<SiteMessage> RecentMessages(int count)
        {
            return NikooDb.Page(NikooTables.SiteMessages, new SqlFilter(), "t.[CreatedAt] DESC", 1, count).ToList();
        }

        public static SiteMessage GetMessage(int id) => NikooDb.Get(NikooTables.SiteMessages, NikooDb.Key(id));

        public static bool UpdateMessage(SiteMessage message) => NikooDb.Update(NikooTables.SiteMessages, message);

        public static bool DeleteMessage(int id) => NikooDb.Delete(NikooTables.SiteMessages, NikooDb.Key(id));

        public static int UnreadMessageCount() => NikooDb.Count(NikooTables.SiteMessages, new SqlFilter().Eq("IsRead", false));

        public static int MarkAllMessagesRead()
        {
            return NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.SiteMessages.Name) + " SET [IsRead] = 1, [ReadAt] = @Now WHERE [IsRead] = 0",
                new Row { ["Now"] = DateTime.Now });
        }

        // ---- FAQ

        public static List<SiteFaq> GetFaqs(bool activeOnly = false)
        {
            var all = NikooDb.List(NikooTables.SiteFaqs, orderBy: "[SortOrder], [Id]");
            return activeOnly ? all.Where(f => f.IsActive).ToList() : all;
        }

        public static SiteFaq GetFaq(int id) => NikooDb.Get(NikooTables.SiteFaqs, NikooDb.Key(id));

        public static void SaveFaq(SiteFaq faq)
        {
            lock (ListLock)
            using (var tx = NikooDb.Begin())
            {
                if (faq.Id > 0 && NikooDb.Exists(NikooTables.SiteFaqs, NikooDb.Key(faq.Id), tx))
                {
                    NikooDb.Update(NikooTables.SiteFaqs, faq, tx: tx);
                }
                else
                {
                    faq.Id = NikooDb.NextId(NikooTables.SiteFaqs, null, tx);
                    NikooDb.Insert(NikooTables.SiteFaqs, faq, tx: tx);
                }
                tx.Commit();
            }
        }

        public static bool DeleteFaq(int id) => NikooDb.Delete(NikooTables.SiteFaqs, NikooDb.Key(id));

        // ---- Testimonials

        public static List<SiteTestimonial> GetTestimonials(bool activeOnly = false)
        {
            var all = NikooDb.List(NikooTables.SiteTestimonials, orderBy: "[SortOrder], [Id]");
            return activeOnly ? all.Where(t => t.IsActive).ToList() : all;
        }

        public static SiteTestimonial GetTestimonial(int id) => NikooDb.Get(NikooTables.SiteTestimonials, NikooDb.Key(id));

        public static void SaveTestimonial(SiteTestimonial item)
        {
            lock (ListLock)
            using (var tx = NikooDb.Begin())
            {
                var existing = item.Id > 0 ? NikooDb.Get(NikooTables.SiteTestimonials, NikooDb.Key(item.Id), tx) : null;
                if (existing != null)
                {
                    item.CreatedAt = existing.CreatedAt;
                    NikooDb.Update(NikooTables.SiteTestimonials, item, tx: tx);
                }
                else
                {
                    item.Id = NikooDb.NextId(NikooTables.SiteTestimonials, null, tx);
                    item.CreatedAt = DateTime.Now;
                    NikooDb.Insert(NikooTables.SiteTestimonials, item, tx: tx);
                }
                tx.Commit();
            }
        }

        public static bool DeleteTestimonial(int id) => NikooDb.Delete(NikooTables.SiteTestimonials, NikooDb.Key(id));

        // ---- Defaults (the texts the site had before they became editable)

        internal static void SeedDefaults()
        {
            if (!NikooDb.Exists(NikooTables.SiteSettings))
                NikooDb.Insert(NikooTables.SiteSettings, DefaultSettings());

            if (!NikooDb.Exists(NikooTables.SiteFaqs))
            {
                var order = 0;
                foreach (var faq in DefaultFaqs())
                {
                    faq.Id = ++order;
                    faq.SortOrder = order * 10;
                    NikooDb.Insert(NikooTables.SiteFaqs, faq);
                }
            }
        }

        private const string OldMetaDescription = "نیکو سیستم آفرین | فروشگاه‌های آنلاین آماده — از دمو تا پرداخت آنلاین";
        private const string DefaultMetaDescription =
            "ساخت فروشگاه اینترنتی، سایت خبری و شرکتی و سامانه نوبت‌دهی آنلاین در چند دقیقه؛ با درگاه پرداخت، دامنه اختصاصی و پشتیبانی. دموها را ببینید و رایگان امتحان کنید.";

        /// <summary>The description search engines show for the home page, unless the owner has written their own.</summary>
        internal static void ApplySeoDefaults()
        {
            lock (SettingsLock)
            {
                var stored = NikooDb.Get(NikooTables.SiteSettings, NikooDb.Key(1));
                if (stored == null || !(string.IsNullOrWhiteSpace(stored.MetaDescription) || stored.MetaDescription.Trim() == OldMetaDescription)) return;
                stored.MetaDescription = DefaultMetaDescription;
                NikooDb.Upsert(NikooTables.SiteSettings, stored);
                _settings = null;
            }
        }

        /// <summary>Adds the FAQs of the platform's modules after the owner's own ones (once; a question already there is skipped).</summary>
        internal static void ApplyModuleFaqs()
        {
            lock (ListLock)
            {
                var faqs = GetFaqs();
                var order = faqs.Select(f => f.SortOrder).DefaultIfEmpty(0).Max();
                var missing = ModuleFaqs().Where(n => faqs.All(f => f.Question?.Trim() != n.Question)).ToList();
                if (missing.Count == 0) return;
                using (var tx = NikooDb.Begin())
                {
                    foreach (var faq in missing)
                    {
                        order += 10;
                        faq.Id = NikooDb.NextId(NikooTables.SiteFaqs, null, tx);
                        faq.SortOrder = order;
                        NikooDb.Insert(NikooTables.SiteFaqs, faq, tx: tx);
                    }
                    tx.Commit();
                }
            }
        }

        private const string OldKindsAnswer = "فروشگاه کالای فیزیکی (پوشاک، سوپرمارکت، دیجیتال و عمومی)، فروش محصولات دیجیتال (دوره آموزشی و کتاب الکترونیک) و سایت محتوایی (خبرگزاری، مجله و سایت شرکتی).";

        /// <summary>The «چه نوع سایت‌هایی…» answer gets online booking, unless the owner has rewritten it.</summary>
        internal static void ApplyKindsFaq()
        {
            var fresh = DefaultFaqs().First(f => f.Question.StartsWith("چه نوع سایت"));
            lock (ListLock)
            {
                foreach (var faq in GetFaqs().Where(f => f.Answer?.Trim() == OldKindsAnswer))
                {
                    faq.Answer = fresh.Answer;
                    NikooDb.Update(NikooTables.SiteFaqs, faq);
                }
            }
        }

        private const string BookingKindsAnswer = "فروشگاه کالای فیزیکی (پوشاک، سوپرمارکت، دیجیتال و عمومی)، فروش محصولات دیجیتال (دوره آموزشی و کتاب الکترونیک)، سایت محتوایی (خبرگزاری، مجله و سایت شرکتی) و سامانه رزرو آنلاین نوبت (مطب، کلینیک، سالن زیبایی و مشاوره).";

        /// <summary>The same answer gets academies and sports clubs, unless the owner has rewritten it.</summary>
        internal static void ApplyCampusKindsFaq()
        {
            var fresh = DefaultFaqs().First(f => f.Question.StartsWith("چه نوع سایت"));
            lock (ListLock)
            {
                foreach (var faq in GetFaqs().Where(f => f.Answer?.Trim() == BookingKindsAnswer))
                {
                    faq.Answer = fresh.Answer;
                    NikooDb.Update(NikooTables.SiteFaqs, faq);
                }
            }
        }

        private const string OldSupportFaqAnswer = "از منوی «پشتیبانی نیکو» در پنل مدیریت سایت خود تیکت بفرستید و گفتگو را همان‌جا دنبال کنید. "
            + "اگر حساب بله را وصل کرده باشید، پاسخ کارشناسان همان لحظه در اپلیکیشن بله هم می‌رسد؛ کافی است روی پیام «پاسخ» بزنید تا جواب شما در همان تیکت ثبت شود. "
            + "از بله هم با دستور /ticket و متن درخواست، تیکت تازه می‌فرستید.";

        private const string SupportFaqAnswer = "از منوی «پشتیبانی نیکو» در پنل مدیریت سایت خود تیکت بفرستید و گفتگو را همان‌جا دنبال کنید؛ "
            + "اسکرین‌شات صفحه یا عکس را هم پیوست کنید تا مشکل سریع‌تر دیده شود. "
            + "اگر حساب بله را وصل کرده باشید، پاسخ کارشناسان همان لحظه در اپلیکیشن بله هم می‌رسد؛ کافی است روی پیام «پاسخ» بزنید و متن یا عکس بفرستید تا در همان تیکت ثبت شود. "
            + "از بله هم با دستور /ticket و متن درخواست، تیکت تازه می‌فرستید.";

        /// <summary>The support FAQ mentions attaching images, unless the owner has rewritten its answer.</summary>
        internal static void ApplySupportFaqImages()
        {
            lock (ListLock)
            {
                foreach (var faq in GetFaqs().Where(f => f.Answer?.Trim() == OldSupportFaqAnswer))
                {
                    faq.Answer = SupportFaqAnswer;
                    NikooDb.Update(NikooTables.SiteFaqs, faq);
                }
            }
        }

        private const string OldTrialFaqAnswer = "بعد از ثبت‌نام همه امکانات فروشگاه را رایگان امتحان می‌کنید. پیش از پایان دوره با پرداخت اشتراک از داخل پنل، فروشگاه دائمی می‌شود؛ اگر پرداخت نشود فروشگاه آزمایشی و داده‌هایش حذف می‌شوند.";

        /// <summary>Adds the renewal FAQ and rewrites the old «…دائمی می‌شود» trial answer, unless the owner has rewritten it.</summary>
        internal static void ApplySubscriptionFaqs()
        {
            var fresh = DefaultFaqs().First(f => f.Question.StartsWith("دوره آزمایشی"));
            lock (ListLock)
            {
                foreach (var faq in GetFaqs().Where(f => f.Answer?.Trim() == OldTrialFaqAnswer))
                {
                    faq.Answer = fresh.Answer;
                    NikooDb.Update(NikooTables.SiteFaqs, faq);
                }
            }
            ApplyModuleFaqs();
        }

        private const string OldDomainFaqAnswer = "بله. دامنه را در تنظیمات فروشگاه وارد کنید و طبق راهنمای همان صفحه رکورد DNS آن را تنظیم کنید.";

        private const string DomainFaqAnswer = "بله. در صفحه «دامنه اختصاصی» پنل، دامنه را وارد می‌کنید و راهنمای قدم‌به‌قدم، IP سرور و رکوردهای DNS آماده (با دکمه کپی) را می‌بینید؛ "
            + "کافی است همان دو رکورد را در پنل خرید دامنه بسازید. سیستم خودکار بررسی می‌کند دامنه درست تنظیم شده یا نه و اگر ایرادی باشد دقیقاً می‌گوید چه چیزی را اصلاح کنید. "
            + "وقتی DNS درست شد، پشتیبانی نیکو خودکار خبردار می‌شود و گواهی امنیتی SSL (https) را صادر می‌کند؛ نتیجه در پنل و پیام بله به شما اعلام می‌شود. "
            + "تا آن زمان سایت با آدرس رایگان خودش در دسترس است.";

        /// <summary>The domain FAQ describes the guide, the automatic check and support's SSL, unless the owner has rewritten it.</summary>
        internal static void ApplyDomainFaq()
        {
            lock (ListLock)
            {
                foreach (var faq in GetFaqs().Where(f => f.Answer?.Trim() == OldDomainFaqAnswer))
                {
                    faq.Answer = DomainFaqAnswer;
                    NikooDb.Update(NikooTables.SiteFaqs, faq);
                }
            }
        }

        private const string OldTermsText = "با ثبت سفارش در نیکو سیستم آفرین، شما تأیید می‌کنید که اطلاعات واردشده صحیح است و محصول نرم‌افزاری مورد نظر را برای استفاده تجاری یا شخصی سفارش می‌دهید.\n"
            + "- پس از پرداخت موفق، سفارش در وضعیت «پرداخت شده» ثبت می‌شود.\n"
            + "- تحویل محصول (سورس، لایسنس یا راه‌اندازی) طبق هماهنگی پشتیبانی انجام می‌شود.\n"
            + "- بازگشت وجه تنها در صورت عدم امکان ارائه خدمت و طبق سیاست فروش بررسی می‌شود.\n"
            + "- کپی‌برداری غیرمجاز از محصولات ممنوع است.";

        private const string OldPrivacyText = "اطلاعات تماس و سفارش شما فقط برای پردازش خرید، پشتیبانی و پیگیری پرداخت استفاده می‌شود و به اشخاص ثالث فروخته نمی‌شود.\n"
            + "اطلاعات پرداخت از طریق درگاه امن بانکی پردازش می‌شود و ما اطلاعات کارت بانکی شما را ذخیره نمی‌کنیم.";

        /// <summary>The full terms and privacy texts (what Enamad looks for), unless the owner has written their own.</summary>
        internal static void ApplyLegalTexts()
        {
            lock (SettingsLock)
            {
                var stored = NikooDb.Get(NikooTables.SiteSettings, NikooDb.Key(1));
                if (stored == null) return;
                var terms = IsUntouched(stored.TermsText, OldTermsText);
                var privacy = IsUntouched(stored.PrivacyText, OldPrivacyText);
                if (!terms && !privacy) return;
                if (terms) stored.TermsText = DefaultTermsText();
                if (privacy) stored.PrivacyText = DefaultPrivacyText();
                NikooDb.Upsert(NikooTables.SiteSettings, stored);
                _settings = null;
            }
        }

        private static bool IsUntouched(string text, string old) =>
            string.IsNullOrWhiteSpace(text) || text.Replace("\r", "").Trim() == old;

        private static string DefaultTermsText() => string.Join("\n",
            "این صفحه شرایط استفاده از وب‌سایت و خدمات «نیکو سیستم آفرین» را بیان می‌کند. ثبت‌نام، ساخت سایت، خرید اشتراک یا سفارش هر یک از محصولات به معنای خواندن و پذیرفتن این قوانین است.",
            "این قوانین تابع قوانین جمهوری اسلامی ایران، به‌ویژه قانون تجارت الکترونیکی و قانون حمایت از حقوق مصرف‌کنندگان است.",
            "## ماده ۱: تعاریف",
            "- نیکو سیستم آفرین (در این متن «ما»): ارائه‌دهنده سامانه ساخت سایت و محصولات نرم‌افزاری که در این وب‌سایت عرضه می‌شوند.",
            "- کاربر: هر شخص حقیقی یا حقوقی که در این وب‌سایت ثبت‌نام می‌کند، سایت می‌سازد یا محصولی سفارش می‌دهد.",
            "- سایت کاربر: فروشگاه اینترنتی، سایت محتوایی یا سامانه نوبت‌دهی که کاربر با سامانه نیکو می‌سازد و خودش آن را مدیریت می‌کند.",
            "- اشتراک: حق استفاده از سامانه برای یک دوره زمانی مشخص در برابر پرداخت هزینه آن.",
            "- پنل مدیریت: بخشی که کاربر پس از ورود، سایت، اشتراک، پرداخت‌ها و تیکت‌های پشتیبانی خود را در آن مدیریت می‌کند.",
            "## ماده ۲: خدمات",
            "- ساخت و میزبانی سایت آماده (فروشگاه کالا، فروش محصولات دیجیتال، سایت خبری و شرکتی و نوبت‌دهی آنلاین) به‌صورت اشتراکی، همراه با امکاناتی که در صفحه هر محصول معرفی شده است.",
            "- دوره آزمایشی رایگان پس از ثبت‌نام؛ مدت آن هنگام ثبت‌نام اعلام می‌شود.",
            "- اشتراک سه‌ماهه، شش‌ماهه (" + Subscriptions.SixMonthDiscount.ToFa().ToFaDigits() + "٪ تخفیف) و یک‌ساله (" + Subscriptions.YearlyDiscount.ToFa().ToFaDigits() + "٪ تخفیف) و بسته‌های فضای ذخیره‌سازی اضافه.",
            "- محصولات نرم‌افزاری آماده که در بخش «محصولات» با قیمت و شرح تحویل معرفی شده‌اند.",
            "- پشتیبانی از راه تیکت پنل مدیریت، تلفن و ایمیل.",
            "مشخصات، امکانات و قیمت هر خدمت همان است که هنگام خرید در صفحه آن نمایش داده می‌شود.",
            "## ماده ۳: ثبت‌نام و حساب کاربری",
            "- کاربر باید اطلاعات درست و کامل (نام، شماره موبایل و ایمیل) وارد کند؛ مسئولیت اطلاعات نادرست با خود اوست.",
            "- نگهداری نام کاربری و رمز عبور بر عهده کاربر است و هر کاری که با حساب او انجام شود به نام او ثبت می‌شود. اگر احتمال سوءاستفاده می‌دهید، فوراً رمز را عوض کنید و به پشتیبانی خبر دهید.",
            "- ثبت‌نام و خرید برای اشخاص دارای اهلیت قانونی (دست‌کم ۱۸ سال) مجاز است.",
            "## ماده ۴: قیمت و پرداخت",
            "- همه قیمت‌ها به تومان است و مبلغ نهایی پیش از پرداخت نمایش داده می‌شود.",
            "- پرداخت به‌صورت آنلاین و از طریق درگاه پرداخت اینترنتی معتبر با همه کارت‌های بانکی عضو شبکه شتاب انجام می‌شود. اطلاعات کارت در صفحه درگاه بانک وارد می‌شود و نزد ما ذخیره نمی‌شود.",
            "- پس از پرداخت موفق، کد پیگیری نمایش داده می‌شود و سفارش یا اشتراک ثبت می‌شود؛ کد پیگیری را نگه دارید.",
            "- اگر مبلغ از حساب کم شد ولی پرداخت ناموفق اعلام شد، بانک معمولاً تا ۷۲ ساعت آن را به حساب شما برمی‌گرداند؛ اگر برنگشت، با کد پیگیری به پشتیبانی خبر دهید.",
            "- تغییر قیمت‌ها روی دوره‌ای که پیش‌تر پرداخت شده اثری ندارد.",
            "## ماده ۵: فعال‌سازی و تحویل",
            "- اشتراک و بسته فضای ذخیره‌سازی بلافاصله پس از پرداخت موفق فعال می‌شوند و در پنل مدیریت دیده می‌شوند.",
            "- محصولات آماده (راه‌اندازی، لایسنس یا سورس) طبق شرح همان محصول تحویل داده می‌شوند؛ کارشناس ما حداکثر ظرف دو روز کاری پس از پرداخت برای هماهنگی با شما تماس می‌گیرد.",
            "- خرید و تمدید دامنه اختصاصی بر عهده کاربر است و دامنه به نام خود او ثبت می‌شود. اتصال آن پس از تنظیم درست DNS از سوی کاربر و صدور گواهی امنیتی (SSL) انجام می‌شود.",
            "## ماده ۶: دوره آزمایشی، تمدید و پایان اشتراک",
            "- در دوره آزمایشی همه امکانات رایگان در دسترس است. اگر تا پایان این دوره اشتراک پرداخت نشود، سایت آزمایشی و همه داده‌هایش حذف می‌شوند.",
            "- دو هفته پیش از پایان اشتراک، یادآوری در پنل مدیریت (و اگر حساب بله وصل باشد، در پیام‌رسان بله) فرستاده می‌شود. تمدید هر زمان انجام شود، دوره تازه به انتهای دوره فعلی اضافه می‌شود.",
            "- اگر اشتراک تمدید نشود، سایت تا " + Models.Store.TenantInfo.RenewalGraceDays.ToFa().ToFaDigits() + " روز پس از پایان باز می‌ماند و پس از آن تا زمان تمدید برای بازدیدکنندگان بسته می‌شود؛ اطلاعات سایت پاک نمی‌شود و با تمدید همان لحظه برمی‌گردد.",
            "- بسته‌های فضای ذخیره‌سازی دائمی‌اند و با پایان اشتراک از بین نمی‌روند.",
            "## ماده ۷: انصراف و بازگشت وجه",
            "- اشتراک: تا ۷ روز پس از پرداخت نخستین اشتراک می‌توانید انصراف دهید؛ مبلغ پرداختی پس از کسر سهم روزهای استفاده‌شده بازگردانده می‌شود. پس از این مدت، هزینه دوره جاری بازگردانده نمی‌شود و اشتراک تا پایان دوره فعال می‌ماند.",
            "- پرداخت تکراری، یا پرداختی که خدمت آن فعال نشده است، به‌طور کامل بازگردانده می‌شود.",
            "- اگر ارائه خدمت به‌علت کوتاهی ما ممکن نباشد (مثلاً قطعی پیوسته بیش از ۷۲ ساعت که به ما مربوط است)، مبلغ روزهای باقی‌مانده بازگردانده می‌شود یا به انتخاب کاربر به اشتراک او اضافه می‌شود.",
            "- محصولات آماده: پیش از شروع تحویل یا راه‌اندازی، مبلغ کامل بازگردانده می‌شود. پس از تحویل سورس یا لایسنس، به‌علت ماهیت دیجیتال محصول امکان بازگشت نیست؛ مگر اینکه محصول با شرح آن مطابقت نداشته باشد و پشتیبانی نتواند ظرف ۷ روز کاری مشکل را برطرف کند.",
            "- بسته فضای ذخیره‌سازی پس از افزوده شدن به سایت بازگردانده نمی‌شود.",
            "- برای بازگشت وجه، از تیکت پنل مدیریت یا فرم «تماس با ما» با ذکر کد پیگیری پرداخت درخواست دهید. مبلغ تأییدشده حداکثر ظرف ۷ روز کاری به کارت یا شماره شبای به نام پرداخت‌کننده واریز می‌شود.",
            "## ماده ۸: تعهدات کاربر و محتوای سایت‌ها",
            "- مسئولیت همه محتوا، کالاها، خدمات، قیمت‌ها و اطلاعاتی که کاربر در سایت خود منتشر می‌کند با خود اوست.",
            "- انتشار محتوای خلاف قوانین جمهوری اسلامی ایران از جمله مصادیق محتوای مجرمانه، فروش کالای قاچاق یا غیرمجاز، نقض حق نشر و علامت تجاری دیگران، فیشینگ، کلاهبرداری و ارسال پیام‌های تبلیغاتی ناخواسته ممنوع است.",
            "- کاربری که در سایت خود کالا یا خدمت می‌فروشد، فروشنده آن است: گرفتن مجوزهای لازم و اینماد سایت خود، رعایت قانون تجارت الکترونیکی و حقوق مصرف‌کننده، ارسال کالا، ضمانت و رسیدگی به شکایت خریداران بر عهده خود اوست. نیکو سیستم آفرین طرف معامله سایت‌های کاربران با خریداران آن‌ها نیست.",
            "- پرداخت خریداران هر سایت از طریق درگاهی که صاحب همان سایت وصل کرده، مستقیم به حساب او واریز می‌شود و ما در این وجوه دخالتی نداریم.",
            "- کاربر نباید در کار سامانه اختلال ایجاد کند، به حساب یا اطلاعات دیگران دسترسی بگیرد یا از سامانه برای حمله به سایت‌های دیگر استفاده کند.",
            "## ماده ۹: مالکیت فکری",
            "- سامانه، کدها، قالب‌ها، طراحی‌ها و نشان نیکو سیستم آفرین متعلق به ماست. اشتراک فقط حق استفاده است و کپی، فروش دوباره یا مهندسی معکوس سامانه بدون اجازه کتبی ممنوع است.",
            "- محتوای هر سایت (متن، عکس، محصولات و اطلاعات مشتریانش) متعلق به صاحب همان سایت است و ما فقط برای نگهداری و نمایش آن در همان سایت از آن استفاده می‌کنیم.",
            "- حق استفاده از محصولات آماده طبق شرح یا لایسنس همان محصول است.",
            "## ماده ۱۰: اطلاعات و حریم خصوصی",
            "- گردآوری و استفاده از اطلاعات کاربران طبق صفحه «حریم خصوصی» انجام می‌شود.",
            "- ما برای حفاظت از اطلاعات تلاش معقول می‌کنیم (ارتباط امن https، نگهداری رمزهای عبور به‌صورت رمزنگاری‌شده و محدود کردن دسترسی‌ها)؛ با این حال توصیه می‌شود کاربر از اطلاعات مهم خود نسخه جداگانه نگه دارد.",
            "## ماده ۱۱: پشتیبانی و دسترس‌پذیری",
            "- درخواست پشتیبانی از راه تیکت پنل مدیریت در همه روزها ثبت می‌شود و در ساعات کاری اعلام‌شده پاسخ داده می‌شود؛ تلفن و ایمیل پشتیبانی در انتهای همین صفحه آمده است.",
            "- ما تلاش می‌کنیم سایت‌ها همیشه در دسترس باشند. توقف‌های کوتاه برای به‌روزرسانی و نگهداری تا جای ممکن در ساعات کم‌بازدید انجام می‌شود.",
            "- مسئولیت قطعی یا اختلالی که از عوامل بیرونی است (قطعی اینترنت کشور یا مراکز داده، اختلال بانک‌ها و درگاه‌های پرداخت، سرویس‌دهنده دامنه کاربر) و حوادث غیرمترقبه (فورس ماژور) با ما نیست.",
            "## ماده ۱۲: محدودیت مسئولیت",
            "- ما مسئول سود یا فروش از دست‌رفته کاربر و خسارت‌های غیرمستقیم نیستیم و در هر حال، مسئولیت ما بیشتر از مبلغی نیست که کاربر برای دوره جاری خدمت پرداخت کرده است.",
            "## ماده ۱۳: تعلیق و پایان همکاری",
            "- اگر کاربر این قوانین را نقض کند، پس از اخطار در پنل مدیریت و مهلت مناسب برای اصلاح، سایت او معلق می‌شود. در مورد محتوای آشکارا مجرمانه یا دستور مراجع قضایی، تعلیق بدون اخطار انجام می‌شود.",
            "- کاربر هر زمان بخواهد می‌تواند استفاده از خدمات را متوقف کند و پیش از آن، نسخه‌ای از اطلاعات سایت خود را از پشتیبانی بخواهد.",
            "## ماده ۱۴: رسیدگی به شکایات",
            "- شکایت یا انتقاد خود را از راه تیکت پنل مدیریت، فرم «تماس با ما»، تلفن یا ایمیل ثبت کنید و کد پیگیری پرداخت یا نام سایت خود را بنویسید.",
            "- دریافت شکایت حداکثر ظرف یک روز کاری اعلام می‌شود و نتیجه بررسی حداکثر ظرف ۳ روز کاری به شما گفته می‌شود.",
            "- اگر از نتیجه راضی نبودید، می‌توانید از راه سامانه رسیدگی به شکایات اینماد (enamad.ir) یا مراجع قانونی صالح پیگیری کنید.",
            "## ماده ۱۵: حل اختلاف",
            "- اختلاف‌ها ابتدا از راه گفتگو حل می‌شوند و در غیر این صورت، مرجع رسیدگی دادگاه‌ها و مراجع قانونی صالح جمهوری اسلامی ایران است.",
            "## ماده ۱۶: تغییر قوانین",
            "- این قوانین ممکن است به‌روز شوند؛ نسخه تازه در همین صفحه منتشر می‌شود و تغییرات مهم پیش از اجرا در پنل مدیریت اعلام می‌شود. ادامه استفاده پس از اعلام، به معنای پذیرش نسخه تازه است.",
            "- تغییرات روی دوره‌هایی که پیش‌تر پرداخت شده‌اند اثری ندارند، مگر به سود کاربر باشند.");

        private static string DefaultPrivacyText() => string.Join("\n",
            "حفظ اطلاعات شما برای ما مهم است. این صفحه می‌گوید چه اطلاعاتی گرفته می‌شود، برای چه کاری استفاده می‌شود و چطور از آن نگهداری می‌کنیم.",
            "## اطلاعاتی که می‌گیریم",
            "- اطلاعاتی که خودتان وارد می‌کنید: نام، شماره موبایل، ایمیل، نام و اطلاعات کسب‌وکار، و پیام‌های فرم تماس و تیکت‌ها.",
            "- اطلاعات خرید: مبلغ، تاریخ، محصول یا اشتراک و کد پیگیری پرداخت. اطلاعات کارت بانکی در صفحه درگاه بانک وارد می‌شود و ما آن را نمی‌بینیم و ذخیره نمی‌کنیم.",
            "- اطلاعات فنی: نشانی IP، نوع مرورگر و صفحه‌های بازدیدشده، برای امنیت، جلوگیری از سوءاستفاده و آمار بازدید.",
            "- کوکی‌ها: برای ورود به حساب، نگه داشتن سبد خرید و تنظیمات؛ بدون آن‌ها ورود به پنل ممکن نیست.",
            "## کاربرد اطلاعات",
            "- ساخت و اداره سایت شما، پردازش سفارش و پرداخت، فعال‌سازی و تمدید اشتراک.",
            "- پاسخ به تیکت‌ها و تماس‌ها و فرستادن اطلاعیه‌های لازم، مانند یادآوری تمدید یا خبر سفارش تازه.",
            "- بهبود خدمات و پیشگیری از تقلب و حمله.",
            "## در اختیار چه کسی قرار می‌گیرد",
            "- اطلاعات شما به هیچ‌کس فروخته یا اجاره داده نمی‌شود.",
            "- فقط به اندازه لازم در اختیار درگاه پرداخت (برای انجام تراکنش) و پیام‌رسان بله (اگر خودتان حساب را وصل کرده باشید، برای اطلاع‌رسانی) قرار می‌گیرد.",
            "- در صورت درخواست مراجع قضایی یا قانونی صالح، طبق قانون در اختیار آن‌ها قرار می‌گیرد.",
            "## اطلاعات خریداران سایت‌های کاربران",
            "- اطلاعات خریداران و مراجعان هر سایت (سفارش‌ها، نوبت‌ها و پیام‌ها) متعلق به صاحب همان سایت است؛ ما فقط آن را نگهداری می‌کنیم و برای هدف دیگری به کار نمی‌بریم. صاحب هر سایت درباره شیوه استفاده از این اطلاعات پاسخگوست.",
            "## نگهداری و امنیت",
            "- ارتباط با سایت از راه پروتکل امن https است، رمزهای عبور به‌صورت رمزنگاری‌شده نگهداری می‌شوند و فقط افراد مجاز به اطلاعات دسترسی دارند.",
            "- اطلاعات تا زمانی که حساب فعال است نگهداری می‌شود. داده‌های سایت آزمایشی که پرداخت نشده، پس از پایان دوره آزمایشی حذف می‌شوند و سوابق پرداخت تا مدتی که قانون لازم بداند نگهداری می‌شوند.",
            "## حقوق شما",
            "- اطلاعات حساب و سایت خود را هر زمان از پنل مدیریت می‌بینید و ویرایش می‌کنید.",
            "- برای حذف حساب یا گرفتن نسخه‌ای از اطلاعات خود، از تیکت پنل مدیریت یا فرم «تماس با ما» درخواست دهید.",
            "پرسش‌های خود درباره حریم خصوصی را از راه‌های تماس انتهای همین صفحه بپرسید.");

        private static IEnumerable<SiteFaq> ModuleFaqs()
        {
            yield return new SiteFaq
            {
                Question = "اشتراک سایت چطور تمدید می‌شود و اگر تمدید نکنم چه می‌شود؟",
                Answer = "اشتراک را خودتان انتخاب می‌کنید: سه‌ماهه، شش‌ماهه (" + Subscriptions.SixMonthDiscount.ToFa() + "٪ ارزان‌تر) یا یک‌ساله (" + Subscriptions.YearlyDiscount.ToFa() + "٪ ارزان‌تر). "
                    + "از صفحه «اشتراک و تمدید» پنل، دوره را انتخاب و آنلاین پرداخت می‌کنید و هر زمان تمدید کنید، دوره تازه به انتهای دوره فعلی اضافه می‌شود و روزی از دست نمی‌رود. "
                    + "دو هفته مانده به پایان، در پنل و پیام بله یادآوری می‌گیرید. اگر تمدید نکنید، سایت تا " + Models.Store.TenantInfo.RenewalGraceDays.ToFa() + " روز بعد از پایان باز می‌ماند و بعد از آن تا تمدید برای بازدیدکنندگان بسته می‌شود؛ "
                    + "هیچ اطلاعاتی پاک نمی‌شود و با تمدید همه‌چیز همان لحظه برمی‌گردد."
            };
            yield return new SiteFaq
            {
                Question = "سایت من در گوگل دیده می‌شود؟",
                Answer = "بله. هر صفحه عنوان و توضیح اختصاصی، آدرس یکتا (canonical) و داده ساختاریافته محصول، مقاله و کسب‌وکار دارد و نقشه سایت (sitemap.xml) و robots.txt خودکار ساخته می‌شوند. "
                    + "کد تأیید گوگل سرچ کنسول را هم از تنظیمات پنل وارد می‌کنید و برای هر محصول، مطلب و صفحه می‌توانید عنوان و توضیح گوگل را جدا بنویسید."
            };
            yield return new SiteFaq
            {
                Question = "محصولات فروشگاه در ترب نمایش داده می‌شوند؟",
                Answer = "بله. فروشگاه‌های کالا به API رسمی نسخه ۳ ترب وصل می‌شوند؛ کافی است از منوی «اتصال به ترب» در پنل آن را روشن کنید و آدرس API را در ترب ثبت کنید. "
                    + "قیمت، تخفیف، موجودی، تصاویر و خصوصیات محصولات خودکار و به‌روز به ترب می‌رسد."
            };
            yield return new SiteFaq
            {
                Question = "آموزشگاه و باشگاه ورزشی چه امکاناتی دارد؟",
                Answer = "کلاس و دوره گروهی با روز، ساعت، ظرفیت، شهریه، سطح و گروه سنی تعریف می‌کنید و مشتری آنلاین ثبت‌نام می‌کند. "
                    + "کلاس خصوصی با مدرس یا مربی، حضوری یا آنلاین، روی ساعت کاری همان مدرس رزرو می‌شود. "
                    + "سالن فوتبال، میز پینگ‌پونگ و هر فضای دیگری را هم می‌شود ساعتی رزرو کرد؛ فقط ساعت‌های خالی نشان داده می‌شود. "
                    + "شهریه و اجاره با زرین‌پال به حساب خودتان می‌آید و هر ثبت‌نام کد پیگیری دارد."
            };
            yield return new SiteFaq
            {
                Question = "برای سایتم می‌توانم اینماد بگیرم؟",
                Answer = "بله، روی دامنه اختصاصی خودتان. صفحه «اینماد» پنل اول با یک چک‌لیست نشان می‌دهد سایت برای بررسی کارشناس اینماد آماده است یا نه (اطلاعات تماس، درباره ما، قوانین و…) و مراحل ثبت‌نام در enamad.ir را قدم‌به‌قدم توضیح می‌دهد. "
                    + "هر روشی که اینماد برای تأیید مالکیت دامنه بخواهد را خودتان از همان صفحه انجام می‌دهید: گذاشتن فایل در ریشه سایت، متاتگ اینماد یا تغییر موقت عنوان صفحه اول؛ بدون نیاز به پشتیبانی و دسترسی به هاست. "
                    + "بعد از صدور، کد نماد را می‌چسبانید تا نماد اعتماد در فوتر همه صفحه‌ها نمایش داده شود."
            };
            yield return new SiteFaq
            {
                Question = "از سفارش و پیام تازه چطور باخبر می‌شوم؟",
                Answer = "با اطلاع‌رسانی بله. حساب بله خود (و همکارتان) را از پنل وصل کنید تا هر سفارش، پرداخت، نوبت، نظر یا پیام تماس همان لحظه با جزئیات و لینک مستقیم در اپلیکیشن بله برایتان ارسال شود."
            };
            yield return new SiteFaq
            {
                Question = "آمار بازدید سایتم را می‌بینم؟",
                Answer = "بله. در داشبورد پنل و صفحه «آمار بازدید»، تعداد بازدید (صفحه‌های دیده‌شده) و بازدیدکننده (افراد متفاوت) امروز، دیروز، این ماه و ماه قبل را می‌بینید، "
                    + "همراه با نمودار روزانه ۳۰ روز اخیر و نمودار ماهانه ۱۲ ماه اخیر. بازدید ربات‌های موتور جستجو و بازدید خود شما به‌عنوان مدیر حساب نمی‌شود تا آمار واقعی باشد."
            };
            yield return new SiteFaq
            {
                Question = "چقدر فضا برای عکس و فایل دارم و اگر پر شد چه می‌شود؟",
                Answer = "هر سایت " + StorageQuota.FormatMb(StorageQuota.DefaultBaseMb) + " فضای رایگان دارد که برای صدها عکس کافی است؛ عکس‌ها خودکار کوچک و فشرده ذخیره می‌شوند. "
                    + "میزان مصرف را در صفحه «فضای ذخیره‌سازی» پنل می‌بینید و پیش از پر شدن هشدار می‌گیرید. اگر فضا پر شود سایت عادی کار می‌کند و فقط بارگذاری فایل تازه متوقف می‌شود؛ "
                    + "با یک دکمه فایل‌های بی‌استفاده را پاک می‌کنید یا بسته‌های ۱ تا ۵۰ گیگابایتی را آنلاین و دائمی می‌خرید که بلافاصله اضافه می‌شوند."
            };
            yield return new SiteFaq
            {
                Question = "اگر عکس‌هایم اندازه‌های مختلف داشته باشند، سایت به‌هم‌ریخته نمی‌شود؟",
                Answer = "نه. عکس‌ها با هر اندازه و شکلی (عمودی، افقی یا مربعی) در کارت محصولات، گالری، مطالب و بنرها در کادرهای هم‌اندازه نمایش داده می‌شوند و لوگو هم در اندازه ثابت سربرگ جا می‌گیرد. "
                    + "در تنظیمات پنل انتخاب می‌کنید عکس کادر را پر کند، کامل و بدون برش دیده شود، یا حالت هوشمند خودش تصمیم بگیرد تا بخش مهم عکس‌های خیلی کشیده بریده نشود."
            };
            yield return new SiteFaq
            {
                Question = "اگر سؤال یا مشکلی داشتم، چطور با پشتیبانی در ارتباط باشم؟",
                Answer = SupportFaqAnswer
            };
            yield return new SiteFaq
            {
                Question = "صفحه اول سایت من چه بخش‌هایی دارد؟",
                Answer = "در فروشگاه: بنر اصلی، دسته‌بندی‌ها، پیشنهاد شگفت‌انگیز با شمارش معکوس، محصولات ویژه و تازه، نظر خریداران، آمار و افتخارات، پرسش‌های پرتکرار و نوار دعوت به عضویت. "
                    + "در سایت شرکتی: آمار، خدمات، درباره ما، چرا ما، روند همکاری، اخبار، رضایت مشتریان، پرسش‌ها و درخواست مشاوره. "
                    + "از منوی «چیدمان صفحات» در پنل ترتیب بخش‌ها را با کشیدن تغییر می‌دهید، هر کدام را پنهان می‌کنید و عنوان و متن‌هایشان را خودتان می‌نویسید."
            };
        }

        private static SiteSettings DefaultSettings()
        {
            return new SiteSettings
            {
                SiteName = "نیکو سیستم آفرین",
                MetaDescription = DefaultMetaDescription,
                HeroEyebrow = "نیکو سیستم آفرین",
                HeroTitle = "فروشگاه آنلاین خودت را در چند دقیقه بساز",
                HeroLead = "بدون برنامه‌نویس و بدون پشتیبان: نوع فروشگاه را انتخاب کن، لوگو و محصولاتت را اضافه کن، درگاه پرداخت و دامنه خودت را وصل کن. {TrialDays} روز رایگان امتحان کن.",
                StoresTitle = "فروشگاه‌های آنلاین آماده",
                StoresSubtitle = "دموهای واقعی را ببینید و همان تجربه را برای کسب‌وکار خود سفارش دهید.",
                StatSatisfaction = 98,
                StatDemos = 100,
                StatCustomers = 500,
                StatSupportHours = 1500,
                ShowFaq = true,
                ShowTestimonials = true,
                Address = "تهران، نارمک\nخیابان شهید نیلفروشان، پلاک ۶۷۹",
                Phone = "021-77458550",
                Email = "info@nikoooo.ir",
                FooterAbout = "نیکو سیستم آفرین؛ فروشگاه‌های آنلاین آماده. دمو را ببینید، سفارش دهید و آنلاین پرداخت کنید.",
                ShowCharity = true,
                CharityTitle = "فعالیت های اجتماعی",
                CharityText = "نیکو سیستم آفرین در کنار فعالیت‌های تخصصی، از طرح‌های خیریه و قرض‌الحسنه «لبخند فردا» حمایت می‌کند؛ جایی برای کمک به نیازمندان، وام بدون بهره و همراهی اجتماعی.",
                CharityUrl = "http://labkhandfarda.ir/",
                TermsText = DefaultTermsText(),
                PrivacyText = DefaultPrivacyText(),
                TrialDays = 0
            };
        }

        private static IEnumerable<SiteFaq> DefaultFaqs()
        {
            yield return new SiteFaq
            {
                Question = "برای ساخت فروشگاه به برنامه‌نویس نیاز دارم؟",
                Answer = "خیر. با ثبت‌نام، فروشگاه شما با محتوای نمونه ساخته می‌شود؛ نام، لوگو، قالب، محصولات، درگاه پرداخت و دامنه را خودتان از پنل مدیریت تنظیم می‌کنید."
            };
            yield return new SiteFaq
            {
                Question = "دوره آزمایشی رایگان چطور کار می‌کند؟",
                Answer = "بعد از ثبت‌نام همه امکانات سایت را رایگان امتحان می‌کنید. پیش از پایان دوره، از صفحه «اشتراک و تمدید» پنل اشتراک سه‌ماهه، شش‌ماهه یا یک‌ساله را پرداخت می‌کنید تا سایت با همه محتوایش فعال بماند؛ "
                    + "اگر پرداخت نشود، سایت آزمایشی و داده‌هایش حذف می‌شوند."
            };
            yield return new SiteFaq
            {
                Question = "چه نوع سایت‌هایی می‌توانم بسازم؟",
                Answer = "فروشگاه کالای فیزیکی (پوشاک، سوپرمارکت، دیجیتال و عمومی)، فروش محصولات دیجیتال (دوره آموزشی و کتاب الکترونیک)، سایت محتوایی (خبرگزاری، مجله و سایت شرکتی)، سامانه رزرو آنلاین نوبت (مطب، کلینیک، سالن زیبایی و مشاوره) و سایت آموزشگاه و باشگاه ورزشی (کلاس گروهی، کلاس خصوصی آنلاین یا حضوری و رزرو ساعتی سالن)."
            };
            yield return new SiteFaq
            {
                Question = "پول فروش مستقیم به حساب خودم واریز می‌شود؟",
                Answer = "بله. کد مرچنت زرین‌پال خودتان را در تنظیمات فروشگاه وارد می‌کنید و پرداخت مشتریان مستقیم به حساب شما می‌رود."
            };
            yield return new SiteFaq
            {
                Question = "می‌توانم دامنه اختصاصی خودم را وصل کنم؟",
                Answer = DomainFaqAnswer
            };
        }
    }
}
