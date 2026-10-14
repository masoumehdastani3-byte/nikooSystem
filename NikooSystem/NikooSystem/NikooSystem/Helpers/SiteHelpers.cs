using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Models;
using NikooSystem.Models.Store;

namespace NikooSystem.Helpers
{
    public static class SiteHelpers
    {
        public static string ToToman(this long amount)
        {
            return amount.ToString("N0", CultureInfo.GetCultureInfo("fa-IR")) + " تومان";
        }

        private static readonly PersianCalendar Shamsi = new PersianCalendar();

        /// <summary>تاریخ شمسی به شکل ۱۴۰۵/۰۷/۱۸</summary>
        public static string ToShamsi(this DateTime date)
        {
            return string.Format("{0:0000}/{1:00}/{2:00}", Shamsi.GetYear(date), Shamsi.GetMonth(date), Shamsi.GetDayOfMonth(date));
        }

        /// <summary>تاریخ شمسی فشرده با ارقام لاتین برای شماره‌ها، مثل 14050718</summary>
        public static string ToShamsiCompact(this DateTime date)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0000}{1:00}{2:00}", Shamsi.GetYear(date), Shamsi.GetMonth(date), Shamsi.GetDayOfMonth(date));
        }

        private static readonly string[] ShamsiMonths =
        {
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        };

        /// <summary>تاریخ شمسی خوانا مثل «۱۸ مهر ۱۴۰۵»</summary>
        public static string ToShamsiText(this DateTime date)
        {
            return (Shamsi.GetDayOfMonth(date) + " " + ShamsiMonths[Shamsi.GetMonth(date) - 1] + " " + Shamsi.GetYear(date)).ToFaDigits();
        }

        /// <summary>ارقام لاتین متن را به ارقام فارسی تبدیل می‌کند.</summary>
        public static string ToFaDigits(this string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (chars[i] >= '0' && chars[i] <= '9') chars[i] = (char)('۰' + (chars[i] - '0'));
            return new string(chars);
        }

        /// <summary>حجم فایل مثل «۲٫۴ مگابایت»</summary>
        public static string ToFileSize(this long bytes)
        {
            if (bytes < 1024) return (bytes + " بایت").ToFaDigits();
            if (bytes < 1024 * 1024) return (Math.Round(bytes / 1024.0) + " کیلوبایت").ToFaDigits();
            return (Math.Round(bytes / 1048576.0, 1).ToString(CultureInfo.InvariantCulture).Replace('.', '٫') + " مگابایت").ToFaDigits();
        }

        public static string StatusText(this OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.PendingPayment: return "در انتظار پرداخت";
                case OrderStatus.Paid: return "پرداخت شده";
                case OrderStatus.Failed: return "ناموفق";
                case OrderStatus.Processing: return "در حال انجام";
                case OrderStatus.Completed: return "تکمیل شده";
                case OrderStatus.Cancelled: return "لغو شده";
                default: return status.ToString();
            }
        }

        public static string StatusBadgeClass(this OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.Paid:
                case OrderStatus.Completed: return "bg-success";
                case OrderStatus.Failed:
                case OrderStatus.Cancelled: return "bg-danger";
                case OrderStatus.Processing: return "bg-info";
                default: return "bg-warning text-dark";
            }
        }

        public static string StatusText(this StoreOrderStatus status)
        {
            switch (status)
            {
                case StoreOrderStatus.PendingPayment: return "در انتظار پرداخت";
                case StoreOrderStatus.Paid: return "پرداخت شده";
                case StoreOrderStatus.Failed: return "ناموفق";
                case StoreOrderStatus.Processing: return "در حال پردازش";
                case StoreOrderStatus.Shipped: return "ارسال شده";
                case StoreOrderStatus.Completed: return "تکمیل شده";
                case StoreOrderStatus.Cancelled: return "لغو شده";
                default: return status.ToString();
            }
        }

        public static string StatusBadgeClass(this StoreOrderStatus status)
        {
            switch (status)
            {
                case StoreOrderStatus.Paid:
                case StoreOrderStatus.Completed: return "bg-success";
                case StoreOrderStatus.Failed:
                case StoreOrderStatus.Cancelled: return "bg-danger";
                case StoreOrderStatus.Processing:
                case StoreOrderStatus.Shipped: return "bg-info";
                default: return "bg-warning text-dark";
            }
        }

        public static string HubStatusText(this HubPaymentStatus status)
        {
            switch (status)
            {
                case HubPaymentStatus.Pending: return "در انتظار پرداخت";
                case HubPaymentStatus.Paid: return "پرداخت شده";
                case HubPaymentStatus.Failed: return "ناموفق";
                case HubPaymentStatus.Expired: return "منقضی";
                default: return status.ToString();
            }
        }

        public static string HubStatusBadge(this HubPaymentStatus status)
        {
            switch (status)
            {
                case HubPaymentStatus.Paid: return "bg-success";
                case HubPaymentStatus.Failed: return "bg-danger";
                case HubPaymentStatus.Expired: return "bg-secondary";
                default: return "bg-warning text-dark";
            }
        }

        /// <summary>
        /// Plain text typed in the admin as HTML: blank lines separate paragraphs, lines starting with "-" become a
        /// bulleted list and lines starting with "##" become headings (with ids part-1, part-2… in the order of
        /// <see cref="TextHeadings"/>). Everything is encoded, so the text cannot carry markup.
        /// </summary>
        public static MvcHtmlString TextBlocks(this HtmlHelper html, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return MvcHtmlString.Empty;
            var sb = new System.Text.StringBuilder();
            var inList = false;
            var headings = 0;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                var bullet = line.StartsWith("-") || line.StartsWith("•");
                if (bullet && !inList) { sb.Append("<ul>"); inList = true; }
                if (!bullet && inList) { sb.Append("</ul>"); inList = false; }
                if (line.Length == 0) continue;
                var heading = Heading(line);
                if (bullet) sb.Append("<li>").Append(html.Encode(line.Substring(1).Trim())).Append("</li>");
                else if (heading != null) sb.Append("<h2 id=\"part-").Append(++headings).Append("\">").Append(html.Encode(heading)).Append("</h2>");
                else sb.Append("<p>").Append(html.Encode(line)).Append("</p>");
            }
            if (inList) sb.Append("</ul>");
            return MvcHtmlString.Create(sb.ToString());
        }

        /// <summary>The "##" headings of a <see cref="TextBlocks"/> text, in order.</summary>
        public static List<string> TextHeadings(string text) =>
            (text ?? "").Replace("\r", "").Split('\n').Select(l => Heading(l.Trim())).Where(h => h != null).ToList();

        private static string Heading(string line) =>
            line.StartsWith("##") && line.TrimStart('#').Trim().Length > 0 ? line.TrimStart('#').Trim() : null;

        /// <summary>Multi-line text with its line breaks kept (encoded).</summary>
        public static MvcHtmlString Lines(this HtmlHelper html, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return MvcHtmlString.Empty;
            return MvcHtmlString.Create(string.Join("<br>", text.Replace("\r", "").Split('\n').Select(l => html.Encode(l.Trim()))));
        }

        public static MvcHtmlString StatusBadge(this HtmlHelper html, OrderStatus status) =>
            Badge(status.StatusBadgeClass(), status.StatusText());

        public static MvcHtmlString StatusBadge(this HtmlHelper html, StoreOrderStatus status) =>
            Badge(status.StatusBadgeClass(), status.StatusText());

        private static MvcHtmlString Badge(string cssClass, string text)
        {
            var span = new TagBuilder("span");
            span.AddCssClass("badge " + cssClass);
            span.SetInnerText(text);
            return MvcHtmlString.Create(span.ToString());
        }

        /// <summary>The one-time success and error messages set by the previous request (TempData "Ok" / "Error").</summary>
        public static MvcHtmlString FlashMessages(this HtmlHelper html, string wrapperClass = null)
        {
            var output = "";
            foreach (var flash in new[] { new { Key = "Ok", Css = "alert-success" }, new { Key = "Error", Css = "alert-danger" } })
            {
                var message = html.ViewContext.TempData[flash.Key] as string;
                if (string.IsNullOrEmpty(message)) continue;
                var alert = new TagBuilder("div");
                alert.AddCssClass("alert " + flash.Css);
                alert.SetInnerText(message);
                if (string.IsNullOrEmpty(wrapperClass))
                {
                    output += alert.ToString();
                    continue;
                }
                var wrapper = new TagBuilder("div") { InnerHtml = alert.ToString() };
                wrapper.AddCssClass(wrapperClass);
                output += wrapper.ToString();
            }
            return MvcHtmlString.Create(output);
        }
    }
}
