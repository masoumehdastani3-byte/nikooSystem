using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>
    /// Daily and monthly visits of the stores and of Nikoo's own site. Page views are counted in memory and written to
    /// SiteVisits once a minute (and before a report is read), so a visit never waits for the database.
    /// A visitor is told apart by a cookie holding the last day they were counted on that site.
    /// </summary>
    public static class VisitStats
    {
        /// <summary>The CompanyCode of Nikoo's own site.</summary>
        public const string MainSite = "";

        private const string CookiePrefix = "nv_";
        private static readonly TimeSpan FlushEvery = TimeSpan.FromMinutes(1);
        private static readonly PersianCalendar Shamsi = new PersianCalendar();
        private static readonly Regex Robots = new Regex(
            @"bot|crawl|spider|slurp|scrap|preview|monitor|lighthouse|headless|curl|wget|python|java/|httpclient|okhttp|go-http|facebookexternalhit|whatsapp|telegram",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly object PendingLock = new object();
        private static Dictionary<string, SiteVisitDay> _pending = new Dictionary<string, SiteVisitDay>();
        private static readonly object FlushLock = new object();
        private static Timer _timer;

        /// <summary>Counts a page view of <paramref name="companyCode"/>, unless it comes from a robot or a prefetch.</summary>
        public static void Record(HttpContextBase http, string companyCode)
        {
            var request = http.Request;
            if (IsRobot(request)) return;

            var today = DateTime.Today;
            var cookieName = CookiePrefix + (string.IsNullOrEmpty(companyCode) ? "site" : companyCode);
            var last = LastVisit(request.Cookies[cookieName]?.Value);
            var newToday = last != today;
            var newThisMonth = last == null || MonthKey(last.Value) != MonthKey(today);
            if (newToday)
            {
                http.Response.Cookies.Add(new HttpCookie(cookieName, today.ToString("yyyyMMdd", CultureInfo.InvariantCulture))
                {
                    HttpOnly = true,
                    Secure = request.IsSecureConnection,
                    SameSite = SameSiteMode.Lax,
                    Expires = today.AddDays(62)
                });
            }

            lock (PendingLock)
            {
                var key = (companyCode ?? MainSite) + "|" + today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                if (!_pending.TryGetValue(key, out var day))
                    _pending[key] = day = new SiteVisitDay { CompanyCode = companyCode ?? MainSite, Day = today };
                day.Views++;
                if (newToday) day.Visitors++;
                if (newThisMonth) day.MonthVisitors++;
                if (_timer == null) _timer = new Timer(_ => SafeFlush(), null, FlushEvery, FlushEvery);
            }
        }

        /// <summary>Writes the counted views to the database (also when the application stops). Counts that fail to be written are kept for the next try.</summary>
        public static void Flush()
        {
            lock (FlushLock)
            {
                Dictionary<string, SiteVisitDay> batch;
                lock (PendingLock)
                {
                    if (_pending.Count == 0) return;
                    batch = _pending;
                    _pending = new Dictionary<string, SiteVisitDay>();
                }

                var written = new List<string>();
                try
                {
                    foreach (var entry in batch)
                    {
                        Write(entry.Value);
                        written.Add(entry.Key);
                    }
                }
                finally
                {
                    if (written.Count < batch.Count)
                    {
                        lock (PendingLock)
                        {
                            foreach (var entry in batch.Where(e => !written.Contains(e.Key)))
                            {
                                if (!_pending.TryGetValue(entry.Key, out var day))
                                {
                                    _pending[entry.Key] = entry.Value;
                                    continue;
                                }
                                day.Views += entry.Value.Views;
                                day.Visitors += entry.Value.Visitors;
                                day.MonthVisitors += entry.Value.MonthVisitors;
                            }
                        }
                    }
                }
            }
        }

        private static void SafeFlush()
        {
            try { Flush(); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Visit stats were not saved: " + ex.Message); }
        }

        private static void Write(SiteVisitDay day)
        {
            NikooDb.Execute(
                "MERGE " + NikooDb.Table(NikooTables.SiteVisits.Name) + " WITH (HOLDLOCK) AS t "
                + "USING (SELECT @CompanyCode AS [CompanyCode], @Day AS [Day]) AS s ON t.[CompanyCode] = s.[CompanyCode] AND t.[Day] = s.[Day] "
                + "WHEN MATCHED THEN UPDATE SET [Views] = t.[Views] + @Views, [Visitors] = t.[Visitors] + @Visitors, [MonthVisitors] = t.[MonthVisitors] + @MonthVisitors "
                + "WHEN NOT MATCHED THEN INSERT ([CompanyCode], [Day], [Views], [Visitors], [MonthVisitors]) VALUES (@CompanyCode, @Day, @Views, @Visitors, @MonthVisitors);",
                new Row
                {
                    ["CompanyCode"] = day.CompanyCode,
                    ["Day"] = day.Day,
                    ["Views"] = day.Views,
                    ["Visitors"] = day.Visitors,
                    ["MonthVisitors"] = day.MonthVisitors
                });
        }

        // ---- Reports

        /// <summary>The report of one site; <paramref name="companyCode"/> null adds up all the customers' stores.</summary>
        public static VisitReport Report(string companyCode, string title = null)
        {
            SafeFlush();
            var today = DateTime.Today;
            var thisMonth = TenantStore.PersianMonthStart(today);
            var firstMonth = Shamsi.AddMonths(thisMonth, -11);

            var days = NikooDb.Select(
                "SELECT [Day], SUM([Views]), SUM([Visitors]), SUM([MonthVisitors]) FROM " + NikooDb.Table(NikooTables.SiteVisits.Name)
                + " WHERE [Day] >= @From AND " + (companyCode == null ? "[CompanyCode] <> ''" : "[CompanyCode] = @CompanyCode")
                + " GROUP BY [Day]",
                new Row { ["From"] = firstMonth, ["CompanyCode"] = companyCode ?? "" },
                r => new SiteVisitDay { Day = r.GetDateTime(0), Views = r.GetInt32(1), Visitors = r.GetInt32(2), MonthVisitors = r.GetInt32(3) })
                .ToDictionary(d => d.Day.Date);

            Func<DateTime, VisitCount> day = date =>
            {
                days.TryGetValue(date, out var d);
                return new VisitCount { Start = date, Label = date.ToShamsiText(), Views = d?.Views ?? 0, Visitors = d?.Visitors ?? 0 };
            };
            Func<DateTime, VisitCount> month = start =>
            {
                var end = Shamsi.AddMonths(start, 1);
                var inMonth = days.Values.Where(d => d.Day >= start && d.Day < end).ToList();
                return new VisitCount
                {
                    Start = start,
                    Label = MonthName(start),
                    Views = inMonth.Sum(d => d.Views),
                    Visitors = inMonth.Sum(d => d.MonthVisitors)
                };
            };

            return new VisitReport
            {
                Title = title,
                Today = day(today),
                Yesterday = day(today.AddDays(-1)),
                ThisMonth = month(thisMonth),
                LastMonth = month(Shamsi.AddMonths(thisMonth, -1)),
                Days = Enumerable.Range(0, 30).Select(i => day(today.AddDays(i - 29))).ToList(),
                Months = Enumerable.Range(0, 12).Select(i => month(Shamsi.AddMonths(firstMonth, i))).ToList()
            };
        }

        /// <summary>The customers' stores with visits this Persian month, most visited first.</summary>
        public static List<StoreVisitRow> StoreRanking()
        {
            SafeFlush();
            var today = DateTime.Today;
            var names = TenantStore.GetTenants().ToDictionary(t => t.CompanyCode, t => t.Name, StringComparer.OrdinalIgnoreCase);
            return NikooDb.Select(
                "SELECT [CompanyCode], SUM(CASE WHEN [Day] = @Today THEN [Views] ELSE 0 END), SUM([Views]), SUM([MonthVisitors]) FROM "
                + NikooDb.Table(NikooTables.SiteVisits.Name)
                + " WHERE [CompanyCode] <> '' AND [Day] >= @From GROUP BY [CompanyCode] ORDER BY SUM([Views]) DESC",
                new Row { ["Today"] = today, ["From"] = TenantStore.PersianMonthStart(today) },
                r => new StoreVisitRow
                {
                    CompanyCode = r.GetString(0),
                    TodayViews = r.GetInt32(1),
                    MonthViews = r.GetInt32(2),
                    MonthVisitors = r.GetInt32(3)
                })
                .Where(s => names.ContainsKey(s.CompanyCode))
                .Select(s =>
                {
                    s.Name = names[s.CompanyCode];
                    s.IsDemo = TenantStore.IsDemo(s.CompanyCode);
                    return s;
                })
                .ToList();
        }

        /// <summary>«مهر ۱۴۰۵»</summary>
        public static string MonthName(DateTime date)
        {
            var text = date.ToShamsiText();
            return text.Substring(text.IndexOf(' ') + 1);
        }

        private static bool IsRobot(HttpRequestBase request)
        {
            var agent = request.UserAgent;
            if (string.IsNullOrWhiteSpace(agent) || Robots.IsMatch(agent)) return true;
            var purpose = request.Headers["Sec-Purpose"] ?? request.Headers["Purpose"];
            return purpose != null && purpose.IndexOf("prefetch", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static DateTime? LastVisit(string value)
        {
            return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                ? day
                : (DateTime?)null;
        }

        private static int MonthKey(DateTime date) => Shamsi.GetYear(date) * 100 + Shamsi.GetMonth(date);
    }
}
