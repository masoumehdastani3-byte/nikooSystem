using System;
using System.Globalization;
using System.Web.Mvc;
using NikooSystem.Models;

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
    }
}
