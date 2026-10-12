using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;

namespace NikooSystem.Services
{
    /// <summary>The Bale messages admins get for each event, and the events each kind of site can switch on or off.</summary>
    public static class Notifications
    {
        // ---- Stores

        /// <param name="paid">Paid online; otherwise confirmed without online payment.</param>
        public static void StoreOrder(string code, StoreOrder order, bool paid)
        {
            var text = new Message("🛒", "سفارش جدید", BaleNotifier.SiteName(code))
                .Line("شماره سفارش", order.OrderNumber)
                .Line("مبلغ", order.Amount.ToToman() + (paid ? " — پرداخت شد ✅" : " — بدون پرداخت آنلاین"))
                .Line("مشتری", Person(order.CustomerName, order.CustomerMobile))
                .Line("آدرس", BaleNotifier.Clip(order.CustomerAddress, 200))
                .Line("اقلام", Items(order.Items.Select(i => i.Title + " × " + i.Quantity.ToFa())))
                .Line("کد پیگیری پرداخت", order.RefId?.ToString(CultureInfo.InvariantCulture));
            BaleNotifier.Notify(code, NotifyEvent.Order, text.ToString(), "/ShopAdmin/" + code + "/OrderDetails/" + order.Id);
        }

        public static void StoreAppointment(string code, StoreAppointment a)
        {
            var text = new Message("📅", "نوبت جدید", BaleNotifier.SiteName(code))
                .Line("زمان", When(a.StartAt))
                .Line("خدمت", a.ServiceTitle)
                .Line("متخصص", StaffName(code, a))
                .Line("مراجع", Person(a.CustomerName, a.CustomerMobile))
                .Line("بیعانه", a.PaidAmount > 0 ? a.PaidAmount.ToToman() + " پرداخت شد ✅" : null)
                .Line("توضیح مراجع", BaleNotifier.Clip(a.Notes, 300))
                .Line("کد پیگیری", a.Code);
            BaleNotifier.Notify(code, NotifyEvent.Appointment, text.ToString(), "/ShopAdmin/" + code + "/AppointmentDetails/" + a.Id);
        }

        public static void StoreAppointmentCancelled(string code, StoreAppointment a)
        {
            var text = new Message("❌", "لغو نوبت توسط مراجع", BaleNotifier.SiteName(code))
                .Line("زمان", When(a.StartAt))
                .Line("خدمت", a.ServiceTitle)
                .Line("متخصص", StaffName(code, a))
                .Line("مراجع", Person(a.CustomerName, a.CustomerMobile))
                .Line("بیعانه", a.PaidAmount > 0 ? a.PaidAmount.ToToman() + " پرداخت شده بود؛ برای بازگشت آن هماهنگ کنید." : null);
            BaleNotifier.Notify(code, NotifyEvent.Appointment, text.ToString(), "/ShopAdmin/" + code + "/AppointmentDetails/" + a.Id);
        }

        public static void StoreMessage(string code, StoreContactMessage m)
        {
            var text = new Message("✉️", "پیام تماس جدید", BaleNotifier.SiteName(code))
                .Line("از", Person(m.FullName, m.Mobile))
                .Line("ایمیل", m.Email)
                .Line("موضوع", m.Subject)
                .Body(m.Message);
            BaleNotifier.Notify(code, NotifyEvent.Message, text.ToString(), "/ShopAdmin/" + code + "/Message/" + m.Id);
        }

        public static void StoreReview(string code, string productTitle, StoreProductReview r)
        {
            var text = new Message("⭐", "نظر جدید درباره محصول", BaleNotifier.SiteName(code))
                .Line("محصول", productTitle)
                .Line("امتیاز", new string('★', Math.Max(1, Math.Min(5, r.Rating))))
                .Line("از", r.AuthorName)
                .Body(r.Comment);
            BaleNotifier.Notify(code, NotifyEvent.Review, text.ToString(), "/ShopAdmin/" + code + "/Reviews");
        }

        public static void StoreComment(string code, StorePost post, StorePostComment c)
        {
            var text = new Message("💬", c.IsApproved ? "دیدگاه جدید" : "دیدگاه جدید در انتظار تأیید", BaleNotifier.SiteName(code))
                .Line("مطلب", post.Title)
                .Line("از", c.AuthorName)
                .Body(c.Body);
            BaleNotifier.Notify(code, NotifyEvent.Review, text.ToString(), "/ShopAdmin/" + code + "/Comments");
        }

        public static void RenewalReminder(TenantInfo t)
        {
            var code = t.CompanyCode;
            string title, body;
            if (t.IsSubscriptionLocked())
            {
                title = "سایت شما به‌خاطر تمدید نشدن اشتراک بسته شده است";
                body = "بازدیدکنندگان فعلاً صفحه «موقتاً در دسترس نیست» را می‌بینند؛ اطلاعات سایت محفوظ است و با تمدید اشتراک، سایت همان لحظه دوباره باز می‌شود.";
            }
            else if (t.IsSubscriptionExpired())
            {
                title = "اشتراک سایت شما تمام شده است";
                body = "سایت تا " + t.GraceDaysLeft().ToFa() + " روز دیگر باز می‌ماند؛ اگر تا آن زمان تمدید نکنید، تا تمدید اشتراک از دسترس بازدیدکنندگان خارج می‌شود.";
            }
            else
            {
                title = "یادآوری تمدید اشتراک";
                body = "اشتراک سایت شما " + t.SubscriptionDaysLeft().ToFa() + " روز دیگر تمام می‌شود. با تمدید سه‌ماهه، شش‌ماهه یا یک‌ساله از همین حالا، روزهای باقی‌مانده هم حفظ می‌شوند.";
            }
            var text = new Message("⏰", title, BaleNotifier.SiteName(code))
                .Line("پایان اشتراک", t.SubscriptionEndsAt?.ToShamsiText())
                .Body(body);
            BaleNotifier.Notify(code, NotifyEvent.Subscription, text.ToString(), "/ShopAdmin/" + code + "/Subscription");
        }

        public static void DomainDnsOk(TenantInfo t)
        {
            var text = new Message("✅", "DNS دامنه شما درست تنظیم شد", BaleNotifier.SiteName(t.CompanyCode))
                .Line("دامنه", t.CustomDomain)
                .Body("کار شما تمام است. پشتیبانی نیکو برای اتصال نهایی و صدور گواهی امنیتی (SSL و https) خبردار شد؛ "
                      + "معمولاً تا یک روز کاری انجام می‌شود و نتیجه را در بله و صفحه «دامنه اختصاصی» پنل خبر می‌دهیم.");
            BaleNotifier.Notify(t.CompanyCode, NotifyEvent.Domain, text.ToString(), "/ShopAdmin/" + t.CompanyCode + "/Domain");
        }

        public static void DomainDnsReminder(TenantInfo t, DomainCheckResult check)
        {
            var text = new Message("⏳", "دامنه شما هنوز به سرور نیکو وصل نشده است", BaleNotifier.SiteName(t.CompanyCode))
                .Line("دامنه", t.CustomDomain)
                .Line("IP فعلی دامنه", check.RootIps.Count == 0 ? "رکوردی پیدا نشد" : string.Join("، ", check.RootIps))
                .Line("IP درست", TenantProvisioningService.ServerIp)
                .Body("رکوردهای DNS را طبق راهنمای صفحه «دامنه اختصاصی» پنل تنظیم کنید. اگر کمک لازم دارید، از همان صفحه تیکت بفرستید.");
            BaleNotifier.Notify(t.CompanyCode, NotifyEvent.Domain, text.ToString(), "/ShopAdmin/" + t.CompanyCode + "/Domain");
        }

        public static void DomainActivated(TenantInfo t)
        {
            var text = new Message("🎉", "دامنه اختصاصی شما فعال شد", BaleNotifier.SiteName(t.CompanyCode))
                .Line("آدرس سایت", "https://" + t.CustomDomain)
                .Body("از این پس همه آدرس‌های سایت روی این دامنه به گوگل معرفی می‌شوند و آدرس رایگان قبلی هم همچنان کار می‌کند. "
                      + "دامنه را در سرچ کنسول گوگل ثبت کنید و پیش از تاریخ انقضا تمدیدش کنید.");
            BaleNotifier.Notify(t.CompanyCode, NotifyEvent.Domain, text.ToString(), "/ShopAdmin/" + t.CompanyCode + "/Domain");
        }

        // ---- Main site

        public static void DomainNeedsSupport(TenantInfo t, DomainCheckResult check)
        {
            var text = new Message("🌐", "اتصال دامنه: نوبت پشتیبانی است", BaleNotifier.SiteName(BaleNotifier.SiteScope))
                .Line("فروشگاه", t.Name + " (" + t.CompanyCode + ")")
                .Line("دامنه", t.CustomDomain + (check.WwwOk ? " و www." + t.CustomDomain : " (www هنوز تنظیم نشده)"))
                .Line("DNS", check.ViaProxy ? "از پشت CDN به سایت می‌رسد" : string.Join("، ", check.RootIps))
                .Line("مالک", Person(t.OwnerName, t.OwnerMobile))
                .Body("مشتری DNS را درست تنظیم کرده است. binding دامنه را در IIS اضافه و گواهی SSL را صادر کنید، "
                      + "سپس در صفحه «دامنه‌های مشتریان» دکمه «فعال‌سازی انجام شد» را بزنید.");
            BaleNotifier.Notify(BaleNotifier.SiteScope, NotifyEvent.Domain, text.ToString(), "/Admin/Domains");
        }

        public static void DomainReleased(TenantInfo t, string oldDomain, bool wasActive)
        {
            var text = new Message("🌐", "دامنه از فروشگاه جدا شد", BaleNotifier.SiteName(BaleNotifier.SiteScope))
                .Line("فروشگاه", t.Name + " (" + t.CompanyCode + ")")
                .Line("دامنه قبلی", oldDomain + (wasActive ? " (فعال بود)" : " (منتظر SSL بود)"))
                .Line("دامنه تازه", t.CustomDomain)
                .Body("binding و گواهی SSL دامنه قبلی را از IIS حذف کنید." + (string.IsNullOrEmpty(t.CustomDomain) ? "" : " دامنه تازه پس از تنظیم DNS دوباره خبر داده می‌شود."));
            BaleNotifier.Notify(BaleNotifier.SiteScope, NotifyEvent.Domain, text.ToString(), "/Admin/Domains");
        }

        public static void SiteOrder(Order order)
        {
            var store = string.IsNullOrEmpty(order.CompanyCode) ? null : TenantStore.GetTenantForAdmin(order.CompanyCode);
            var text = new Message("💰", store != null ? "پرداخت اشتراک فروشگاه" : "خرید محصول", BaleNotifier.SiteName(BaleNotifier.SiteScope))
                .Line("فروشگاه", store != null ? store.Name + " (" + store.CompanyCode + ")" : null)
                .Line("محصول", order.ProductTitle)
                .Line("اشتراک تا", order.SubscriptionEndsAt?.ToShamsiText())
                .Line("مبلغ", order.Amount.ToToman() + " — پرداخت شد ✅")
                .Line("خریدار", Person(order.CustomerName, order.CustomerMobile))
                .Line("ایمیل", order.CustomerEmail)
                .Line("شماره سفارش", order.OrderNumber)
                .Line("کد پیگیری پرداخت", order.RefId?.ToString(CultureInfo.InvariantCulture));
            BaleNotifier.Notify(BaleNotifier.SiteScope, NotifyEvent.Order, text.ToString(), "/Admin/OrderDetails/" + order.Id);
        }

        public static void HubPayment(HubPayment p, PaymentClient client)
        {
            var text = new Message("💳", "پرداخت موفق درگاه", BaleNotifier.SiteName(BaleNotifier.SiteScope))
                .Line("اپلیکیشن", client?.Name ?? p.AppId)
                .Line("مبلغ", p.Amount.ToToman())
                .Line("بابت", BaleNotifier.Clip(p.Description, 200))
                .Line("پرداخت‌کننده", Person(p.CustomerName, p.CustomerMobile))
                .Line("کد پیگیری پرداخت", p.RefId?.ToString(CultureInfo.InvariantCulture));
            BaleNotifier.Notify(BaleNotifier.SiteScope, NotifyEvent.Payment, text.ToString(), "/Admin/HubPayment/" + p.Id);
        }

        public static void StoragePurchase(StoragePurchase p)
        {
            var store = TenantStore.GetTenantForAdmin(p.CompanyCode);
            var text = new Message("💾", "خرید فضای ذخیره‌سازی", BaleNotifier.SiteName(BaleNotifier.SiteScope))
                .Line("فروشگاه", (store?.Name ?? p.CompanyCode) + " (" + p.CompanyCode + ")")
                .Line("حجم", StorageQuota.FormatMb(p.Megabytes))
                .Line("مبلغ", p.Amount.ToToman() + " — پرداخت شد ✅")
                .Line("کد پیگیری پرداخت", p.RefId?.ToString(CultureInfo.InvariantCulture));
            BaleNotifier.Notify(BaleNotifier.SiteScope, NotifyEvent.Order, text.ToString(), "/Admin/Store/" + p.CompanyCode);
        }

        public static void Signup(TenantInfo t)
        {
            var text = new Message("🎉", "فروشگاه آزمایشی جدید", BaleNotifier.SiteName(BaleNotifier.SiteScope))
                .Line("نام", t.Name)
                .Line("کد شرکت", t.CompanyCode)
                .Line("نوع", StoreTypes.Get(t.StoreType).Name)
                .Line("مالک", Person(t.OwnerName, t.OwnerMobile))
                .Line("پایان دوره آزمایشی", t.TrialEndsAt?.ToShamsiText());
            BaleNotifier.Notify(BaleNotifier.SiteScope, NotifyEvent.Signup, text.ToString(), "/Admin/Store/" + t.CompanyCode);
        }

        public static void SiteMessage(SiteMessage m)
        {
            var text = new Message("✉️", "پیام تماس جدید", BaleNotifier.SiteName(BaleNotifier.SiteScope))
                .Line("از", Person(m.Name, m.Phone))
                .Line("ایمیل", m.Email)
                .Line("موضوع", m.Subject)
                .Body(m.Message);
            BaleNotifier.Notify(BaleNotifier.SiteScope, NotifyEvent.Message, text.ToString(), "/Admin/Message/" + m.Id);
        }

        // ---- Events offered in the admin

        public static List<NotifyEventOption> StoreEvents(SiteKindInfo kind, NotifySettings settings)
        {
            var list = new List<NotifyEventOption>();
            if (kind.IsShop || kind.IsDigital)
            {
                list.Add(Option(settings, NotifyEvent.Order, "سفارش و پرداخت", "هر سفارشی که پرداخت شد یا بدون پرداخت آنلاین ثبت شد"));
                list.Add(Option(settings, NotifyEvent.Review, "نظر مشتریان", "نظری که برای یک محصول ثبت شد"));
            }
            if (kind.IsBooking)
                list.Add(Option(settings, NotifyEvent.Appointment, "نوبت‌ها", "نوبت تازه (همراه بیعانه پرداخت‌شده) و لغو نوبت توسط مراجع"));
            if (kind.IsContent)
                list.Add(Option(settings, NotifyEvent.Review, "دیدگاه خوانندگان", "دیدگاهی که زیر خبر یا مقاله ثبت شد"));
            list.Add(Option(settings, NotifyEvent.Message, "پیام‌های تماس", "پیامی که از فرم «تماس با ما» رسید"));
            list.Add(Option(settings, NotifyEvent.Ticket, "پاسخ پشتیبانی نیکو", "پاسخ پشتیبانی به تیکت‌های شما؛ با «پاسخ» روی همان پیام در بله جواب می‌دهید"));
            return list;
        }

        public static List<NotifyEventOption> SiteEvents(NotifySettings settings) => new List<NotifyEventOption>
        {
            Option(settings, NotifyEvent.Order, "خرید محصول و اشتراک", "پرداخت موفق خرید محصولات و اشتراک فروشگاه‌ها"),
            Option(settings, NotifyEvent.Payment, "تراکنش‌های درگاه", "پرداخت موفق اپلیکیشن‌های وصل‌شده به درگاه پرداخت"),
            Option(settings, NotifyEvent.Signup, "فروشگاه جدید", "ساخت فروشگاه آزمایشی تازه با ثبت‌نام"),
            Option(settings, NotifyEvent.Message, "پیام‌های تماس", "پیامی که از فرم «تماس با ما» سایت رسید"),
            Option(settings, NotifyEvent.Ticket, "تیکت‌های پشتیبانی", "تیکت تازه و پاسخ مشتریان؛ با «پاسخ» روی همان پیام در بله جواب می‌دهید")
        };

        private static NotifyEventOption Option(NotifySettings s, NotifyEvent e, string title, string hint) =>
            new NotifyEventOption { Event = e, Title = title, Hint = hint, Enabled = !s.IsMuted(e) };

        // ---- Formatting

        private static string Person(string name, string mobile)
        {
            name = BaleNotifier.Plain(name);
            if (string.IsNullOrWhiteSpace(mobile)) return name;
            return name.Length == 0 ? mobile.Trim() : name + " — " + mobile.Trim();
        }

        private static string When(DateTime at) =>
            BookingTime.DayNames[BookingTime.DayOf(at)] + " " + at.ToShamsiText() + " ساعت " + at.ToString("HH:mm", CultureInfo.InvariantCulture).ToFaDigits();

        private static string StaffName(string code, StoreAppointment a) =>
            !string.IsNullOrEmpty(a.StaffName) ? a.StaffName : TenantStore.GetStaffMember(code, a.StaffId)?.Name;

        private static string Items(IEnumerable<string> items)
        {
            var list = items.ToList();
            var shown = string.Join("، ", list.Take(5).Select(BaleNotifier.Plain));
            return list.Count > 5 ? shown + " و " + (list.Count - 5).ToFa() + " قلم دیگر" : shown;
        }

        /// <summary>A message with a bold title line, "label: value" lines (empty values are left out) and an optional body.</summary>
        private sealed class Message
        {
            private readonly StringBuilder _text = new StringBuilder();

            public Message(string icon, string title, string site)
            {
                _text.Append(icon).Append(" *").Append(title).Append("* ");
                if (!string.IsNullOrEmpty(site)) _text.Append("— ").Append(site);
                _text.Append('\n');
            }

            public Message Line(string label, string value)
            {
                if (!string.IsNullOrWhiteSpace(value)) _text.Append(label).Append(": ").Append(BaleNotifier.Plain(value)).Append('\n');
                return this;
            }

            public Message Body(string body)
            {
                var clipped = BaleNotifier.Clip(body, 1200);
                if (clipped != null) _text.Append('\n').Append(BaleNotifier.Plain(clipped)).Append('\n');
                return this;
            }

            public override string ToString() => _text.ToString().TrimEnd();
        }
    }
}
