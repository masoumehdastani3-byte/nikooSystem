using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Helpers
{
    public sealed class ShopAdminNavItem
    {
        public string Action { get; set; }
        public string Icon { get; set; }
        public string Text { get; set; }
        public int Badge { get; set; }
        public string[] Also { get; set; }
        public bool Active { get; set; }
    }

    public sealed class ShopAdminNavGroup
    {
        public string Title { get; set; }
        public string Icon { get; set; }
        public List<ShopAdminNavItem> Items { get; set; } = new List<ShopAdminNavItem>();
        public bool Active => Items.Any(i => i.Active);
        public int Badge => Items.Sum(i => i.Badge);
    }

    /// <summary>Sidebar menu of the store panel; also feeds the personal dashboard shortcuts.</summary>
    public static class ShopAdminNav
    {
        public static List<ShopAdminNavGroup> Build(
            SiteKindInfo kind,
            string company,
            string currentAction,
            PanelRole panelRole,
            bool isPanelOwner,
            StoreSettings settings,
            bool renewalDue,
            string storeType = null)
        {
            var nav = new List<ShopAdminNavGroup>();
            ShopAdminNavGroup group = null;
            Action<string, string> section = (title, icon) => nav.Add(group = new ShopAdminNavGroup { Title = title, Icon = icon });
            Action<string, string, string, int, string[]> add = (action, icon, text, badge, also) =>
            {
                if (!PanelAccess.Allows(panelRole, action)) return;
                group.Items.Add(new ShopAdminNavItem
                {
                    Action = action,
                    Icon = icon,
                    Text = text,
                    Badge = badge,
                    Also = also ?? new string[0],
                    Active = IsActive(currentAction, action, also)
                });
            };

            var unreadMessages = TenantStore.CountUnreadMessages(company);
            var pendingComments = kind.IsContent ? TenantStore.CountPendingComments(company) : 0;
            var storage = StorageQuota.Usage(company);

            section(null, null);
            add("Index", "bi-speedometer2", "داشبورد", 0, null);
            add("Visits", "bi-bar-chart-line", "آمار بازدید", 0, new[] { "VisitIps" });
            if (kind.IsContent)
            {
                section("محتوا", "bi-journal-text");
                add("Posts", "bi-newspaper", "خبرها و مقاله‌ها", 0, new[] { "EditPost" });
                add("Categories", "bi-diagram-3", "سرویس‌ها و دسته‌ها", 0, new[] { "EditCategory" });
                add("Comments", "bi-chat-left-text", "نظرات خوانندگان", pendingComments, null);
                add("Messages", "bi-envelope", "پیام‌های تماس", unreadMessages, new[] { "Message" });
            }
            else if (kind.IsCampus)
            {
                var school = string.Equals(storeType, "school", StringComparison.OrdinalIgnoreCase);
                section(school ? "دوره عمومی" : "کلاس و ثبت‌نام", "bi-easel");
                add("Classes", "bi-easel", school ? "دوره‌های عمومی" : "کلاس‌ها و دوره‌ها", 0, new[] { "EditClass" });
                add("Enrollments", "bi-person-check", "ثبت‌نام‌ها", TenantStore.CountEnrollmentsToday(company), null);
                add("CampusGuide", "bi-signpost", "راهنمای راه‌اندازی", 0, null);
                section(school ? "اتاق و کلاس" : "سالن و زمین", "bi-trophy");
                add("Facilities", "bi-trophy", school ? "اتاق‌ها و کلاس‌ها" : "سالن‌ها و زمین‌ها", 0, new[] { "EditFacility" });
                add("FacilityBookings", "bi-calendar-week", "رزرو ساعتی", TenantStore.CountFacilityBookingsToday(company), null);
                section(school ? "دوره خصوصی" : "کلاس خصوصی", "bi-camera-video");
                add("Calendar", "bi-calendar-week", "تقویم جلسات", 0, null);
                add("Appointments", "bi-card-checklist", school ? "جلسه‌های خصوصی" : "جلسات خصوصی", TenantStore.CountAppointmentsToday(company), new[] { "AppointmentDetails", "NewAppointment" });
                add("Services", "bi-clipboard-check", school ? "دوره خصوصی و تعرفه" : "کلاس خصوصی و تعرفه", 0, new[] { "EditService" });
                add("Staff", "bi-person-badge", school ? "مدرس‌ها" : "مدرس‌ها و مربی‌ها", 0, new[] { "EditStaff" });
                add("TimeOff", "bi-calendar-x", "تعطیلی و مرخصی", 0, null);
                section(null, null);
                add("Messages", "bi-envelope", "پیام‌های تماس", unreadMessages, new[] { "Message" });
            }
            else if (kind.IsBooking)
            {
                section("نوبت‌دهی", "bi-calendar-week");
                add("Calendar", "bi-calendar-week", "تقویم نوبت‌ها", 0, null);
                add("Appointments", "bi-card-checklist", "فهرست نوبت‌ها", TenantStore.CountAppointmentsToday(company), new[] { "AppointmentDetails", "NewAppointment" });
                add("Services", "bi-clipboard-check", "خدمات و تعرفه‌ها", 0, new[] { "EditService" });
                add("Staff", "bi-person-badge", "متخصصان و ساعت کاری", 0, new[] { "EditStaff" });
                add("TimeOff", "bi-calendar-x", "تعطیلی و مرخصی", 0, null);
                section(null, null);
                add("Messages", "bi-envelope", "پیام‌های تماس", unreadMessages, new[] { "Message" });
            }
            else if (kind.IsMenu)
            {
                section("منو", "bi-upc-scan");
                add("Products", "bi-cup", "آیتم‌های منو", 0, new[] { "EditProduct" });
                add("Categories", "bi-diagram-3", "دسته‌ها", 0, new[] { "EditCategory" });
                add("MenuQr", "bi-upc-scan", "کارت QR میزها", 0, null);
                add("WaiterCalls", "bi-bell", "فراخوان گارسون", TenantStore.OpenWaiterCallCount(company), new[] { "CloseWaiterCall" });
                section(null, null);
                add("Messages", "bi-envelope", "پیام‌های تماس", unreadMessages, new[] { "Message" });
            }
            else
            {
                section(kind.ItemsLabel, kind.IsDigital ? "bi-easel" : "bi-box-seam");
                add("Products", kind.IsDigital ? "bi-easel" : "bi-box", "فهرست " + kind.ItemsLabel, 0, new[] { "EditProduct" });
                add("Categories", "bi-diagram-3", "گروه‌ها", 0, new[] { "EditCategory" });
                add("Collections", "bi-tags", "برچسب‌های ویژه", 0, new[] { "EditCollection" });
                add("Attributes", "bi-list-check", "خصوصیات", 0, new[] { "EditAttribute" });
                section("فروش و مشتریان", "bi-cart-check");
                add("Orders", "bi-receipt", "سفارش‌ها", 0, new[] { "OrderDetails" });
                if (kind.IsShop)
                {
                    var pendingReturns = TenantStore.CountPendingReturns(company);
                    add("Returns", "bi-arrow-return-left", "مرجوعی‌ها", pendingReturns, new[] { "ReturnDetails" });
                    add("ShippingGuide", "bi-truck", "ارسال و مرجوعی", 0, null);
                }
                if (kind.IsDigital)
                    add("GrantAccess", "bi-key", "اعطای دسترسی", 0, null);
                add("Customers", kind.IsDigital ? "bi-camera-video" : "bi-people", kind.IsDigital ? "دانشجویان و خریداران" : "مشتریان", 0, null);
                add("Reviews", "bi-chat-dots", "نظرات", 0, null);
                add("Messages", "bi-envelope", "پیام‌های تماس", unreadMessages, new[] { "Message" });
            }
            if (kind.IsShop || kind.IsDigital || kind.IsCampus || kind.IsBooking)
            {
                section("حسابداری", "bi-journal-text");
                add("AccountingGuide", "bi-signpost", "راهنمای حسابداری", 0, null);
                add("FiscalYear", "bi-calendar3-range", "سال مالی", 0, null);
                add("Accounts", "bi-list-check", "سرفصل حساب‌ها", 0, new[] { "EditAccount" });
                add("DetailAccounts", "bi-diagram-3", "حساب‌های تفصیلی", 0, new[] { "EditDetail" });
                add("Journal", "bi-journal-text", "اسناد حسابداری", 0, new[] { "EditJournal", "Voucher" });
                add("DocumentTypes", "bi-bookmark", "انواع سند", 0, new[] { "EditDocumentType" });
                add("Parties", "bi-person-lines-fill", AccountingCopy.Parties(kind), 0, new[] { "EditParty" });
                add("Banks", "bi-bank", "بانک‌ها و درگاه", 0, new[] { "EditBank" });
                add("Payments", "bi-cash-stack", "دریافت و پرداخت", 0, new[] { "EditPayment" });
                add("Purchases", "bi-box", "فاکتور خرید", 0, new[] { "EditPurchase" });
                add("Pos", "bi-shop-window", AccountingCopy.Pos(kind), 0, null);
                add("SaleInvoices", "bi-receipt", AccountingCopy.SaleInvoice(kind), 0, new[] { "EditSaleInvoice" });
                add("Inventory", "bi-box-seam", AccountingCopy.Inventory(kind), 0, null);
                add("Assets", "bi-tags", "اموال", 0, new[] { "EditAsset" });
                add("Books", "bi-file-earmark-text", "تراز و دفاتر", 0, null);
            }
            section("ظاهر سایت", "bi-palette");
            add("PageLayout", "bi-layout-text-window-reverse", "چیدمان صفحات", 0, null);
            add("Menus", "bi-menu-button-wide", "منوها", 0, new[] { "EditMenu" });
            add("Pages", "bi-file-earmark-text", "صفحات", 0, new[] { "EditPage" });
            add("Gallery", "bi-images", "گالری", 0, null);
            section("بازاریابی و اتصال‌ها", "bi-megaphone");
            add("SeoGuide", "bi-google", "سئو و گوگل", 0, null);
            if (kind.IsShop)
                add("Torob", "bi-graph-up", "اتصال به ترب", 0, null);
            add("Enamad", "bi-patch-check", "اینماد", 0, null);
            add("Notifications", "bi-bell", "اطلاع‌رسانی بله", 0, null);
            if (kind.IsShop || kind.IsDigital || kind.IsBooking || kind.IsCampus)
                add("Sms", "bi-chat-dots", "پیامک مشتریان", 0, null);
            section("تنظیمات", "bi-sliders");
            add("Settings", "bi-gear", "اطلاعات " + kind.SiteNoun, 0, null);
            if (isPanelOwner)
                add("PanelUsers", "bi-people", "کاربران پنل", 0, new[] { "EditPanelUser" });
            add("Domain", "bi-globe", "دامنه اختصاصی", 0, null);
            add("Setup", "bi-stars", "راه‌اندازی سریع", 0, null);
            add("Storage", "bi-hdd", "فضای ذخیره‌سازی", storage.IsFull ? 1 : 0, null);
            add("Subscription", "bi-credit-card", "اشتراک و تمدید", renewalDue ? 1 : 0, null);
            section(null, null);
            add("Support", "bi-headset", "پشتیبانی نیکو", SupportDesk.CountUnreadForStore(company), new[] { "Ticket", "NewTicket" });
            add("MyDashboard", "bi-grid-1x2", "داشبورد من", 0, null);

            return nav.Where(g => g.Items.Count > 0).ToList();
        }

        public static IEnumerable<ShopAdminNavItem> Flatten(IEnumerable<ShopAdminNavGroup> nav) =>
            nav.SelectMany(g => g.Items);

        public static List<ShopAdminNavItem> ResolvePins(IEnumerable<ShopAdminNavGroup> nav, IEnumerable<string> pins)
        {
            var byAction = Flatten(nav)
                .Where(i => !string.Equals(i.Action, "Index", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(i.Action, "MyDashboard", StringComparison.OrdinalIgnoreCase))
                .GroupBy(i => i.Action, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var result = new List<ShopAdminNavItem>();
            foreach (var pin in pins ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(pin)) continue;
                ShopAdminNavItem item;
                if (byAction.TryGetValue(pin.Trim(), out item))
                    result.Add(item);
            }
            return result;
        }

        public static IReadOnlyList<string> DefaultPins(SiteKindInfo kind)
        {
            if (kind == null) return new[] { "Visits", "Messages", "Settings" };
            if (kind.IsContent) return new[] { "Posts", "Comments", "Messages", "Categories", "Visits" };
            if (kind.IsCampus) return new[] { "Classes", "Enrollments", "Facilities", "Calendar", "Appointments", "Pos" };
            if (kind.IsBooking) return new[] { "Calendar", "Appointments", "Services", "Staff", "Messages" };
            if (kind.IsMenu) return new[] { "Products", "Categories", "MenuQr", "WaiterCalls", "Settings" };
            if (kind.IsDigital) return new[] { "Products", "Orders", "GrantAccess", "Customers", "Pos", "SaleInvoices" };
            return new[] { "Pos", "Orders", "Products", "SaleInvoices", "Customers", "Inventory" };
        }

        public const int MaxPins = 12;

        static bool IsActive(string current, string action, string[] also)
        {
            return string.Equals(current, action, StringComparison.OrdinalIgnoreCase)
                || (also != null && also.Any(a => string.Equals(current, a, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
