using System;
using System.Globalization;
using System.Linq;
using NikooSystem.Models;
using NikooSystem.Models.Store;

namespace NikooSystem.Helpers
{
    /// <summary>
    /// یک سال مالی شمسی: از روز اول ماه شروع تا روز قبل از همان ماه در سال بعد
    /// (پیش‌فرض فروردین تا اسفند).
    /// </summary>
    public sealed class FiscalPeriod
    {
        public static readonly string[] MonthNames =
        {
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        };

        private static readonly PersianCalendar Shamsi = new PersianCalendar();

        public FiscalPeriod(int startYear, DateTime start, DateTime end)
        {
            StartYear = startYear;
            Start = start.Date;
            End = end.Date;
        }

        /// <summary>سال شمسیِ روز شروع سال مالی.</summary>
        public int StartYear { get; }

        public DateTime Start { get; }
        public DateTime End { get; }

        public string Label => ("سال مالی " + StartYear.ToString(CultureInfo.InvariantCulture)).ToFaDigits();

        public string RangeText =>
            (Start.ToShamsi() + " تا " + End.ToShamsi()).ToFaDigits();

        public static int NormalizeMonth(int month) =>
            month < 1 || month > 12 ? 1 : month;

        public static FiscalPeriod OfYear(int startYear, int startMonth)
        {
            startMonth = NormalizeMonth(startMonth);
            var start = Shamsi.ToDateTime(startYear, startMonth, 1, 0, 0, 0, 0);
            var next = Shamsi.ToDateTime(startYear + 1, startMonth, 1, 0, 0, 0, 0);
            return new FiscalPeriod(startYear, start, next.AddDays(-1));
        }

        public static FiscalPeriod For(DateTime day, int startMonth)
        {
            startMonth = NormalizeMonth(startMonth);
            var year = Shamsi.GetYear(day);
            var month = Shamsi.GetMonth(day);
            var startYear = month >= startMonth ? year : year - 1;
            return OfYear(startYear, startMonth);
        }

        public static FiscalPeriod Current(StoreSettings settings) =>
            For(DateTime.Today, settings?.FiscalStartMonth ?? 1);

        public static bool IsClosed(StoreSettings settings, int startYear)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.ClosedFiscalYears)) return false;
            var key = startYear.ToString(CultureInfo.InvariantCulture);
            return settings.ClosedFiscalYears.Split(new[] { ',', '،', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(x => string.Equals(x.Trim(), key, StringComparison.Ordinal));
        }

        public static void MarkClosed(StoreSettings settings, int startYear)
        {
            if (settings == null || IsClosed(settings, startYear)) return;
            var key = startYear.ToString(CultureInfo.InvariantCulture);
            settings.ClosedFiscalYears = string.IsNullOrWhiteSpace(settings.ClosedFiscalYears)
                ? key
                : settings.ClosedFiscalYears.Trim().TrimEnd(',', '،') + "," + key;
        }

        public static void MarkOpen(StoreSettings settings, int startYear)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.ClosedFiscalYears)) return;
            var key = startYear.ToString(CultureInfo.InvariantCulture);
            var kept = settings.ClosedFiscalYears
                .Split(new[] { ',', '،', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0 && !string.Equals(x, key, StringComparison.Ordinal))
                .ToList();
            settings.ClosedFiscalYears = kept.Count == 0 ? null : string.Join(",", kept);
        }

        /// <summary>
        /// When the user has not chosen a range (and has not asked for all dates), fills From/To with the current fiscal year.
        /// </summary>
        public static bool ApplyDefault(GridQuery query, StoreSettings settings)
        {
            if (query == null) return false;
            if (string.Equals(query.Scope, "all", StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(query.From) || !string.IsNullOrEmpty(query.To)) return false;
            var period = Current(settings);
            query.From = period.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            query.To = period.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }
    }
}
