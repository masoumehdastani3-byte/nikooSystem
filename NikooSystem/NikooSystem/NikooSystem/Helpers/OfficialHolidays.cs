using System;
using System.Collections.Generic;
using System.Globalization;

namespace NikooSystem.Helpers
{
    /// <summary>
    /// Public holidays of Iran. Fixed solar dates apply every year. Lunar dates follow the
    /// official calendar of the Calendar Center (مؤسسه ژئوفیزیک) for years listed here:
    /// <see cref="HijriCalendar"/> is one or two days away from that calendar, and the gap is not constant.
    /// </summary>
    public static class OfficialHolidays
    {
        public sealed class Day
        {
            public DateTime Date { get; set; }
            public string Name { get; set; }
        }

        private static readonly PersianCalendar Shamsi = new PersianCalendar();

        /// <summary>Month * 100 + day → name. Solar holidays, used when a year has no full official list.</summary>
        private static readonly Dictionary<int, string> Solar = new Dictionary<int, string>
        {
            { 101, "عید نوروز" },
            { 102, "عید نوروز" },
            { 103, "عید نوروز" },
            { 104, "عید نوروز" },
            { 112, "روز جمهوری اسلامی" },
            { 113, "روز طبیعت" },
            { 314, "رحلت امام خمینی" },
            { 315, "قیام ۱۵ خرداد" },
            { 1122, "پیروزی انقلاب اسلامی" },
            { 1229, "ملی شدن صنعت نفت" }
        };

        /// <summary>
        /// Official calendar of 1405 (تقویم رسمی کشور، مرکز تقویم مؤسسه ژئوفیزیک).
        /// Days that carry two occasions keep both names.
        /// </summary>
        private static readonly Dictionary<int, string> Year1405 = new Dictionary<int, string>
        {
            { 101, "عید نوروز و عید فطر" },
            { 102, "عید نوروز و تعطیل عید فطر" },
            { 103, "عید نوروز" },
            { 104, "عید نوروز" },
            { 112, "روز جمهوری اسلامی" },
            { 113, "روز طبیعت" },
            { 125, "شهادت امام جعفر صادق" },
            { 306, "عید قربان" },
            { 314, "رحلت امام خمینی و عید غدیر" },
            { 315, "قیام ۱۵ خرداد" },
            { 403, "تاسوعای حسینی" },
            { 404, "عاشورای حسینی" },
            { 513, "اربعین حسینی" },
            { 521, "رحلت پیامبر و شهادت امام حسن مجتبی" },
            { 522, "شهادت امام رضا" },
            { 530, "شهادت امام حسن عسکری" },
            { 608, "میلاد پیامبر و امام جعفر صادق" },
            { 822, "شهادت حضرت فاطمه" },
            { 1002, "ولادت امام علی" },
            { 1016, "مبعث" },
            { 1104, "ولادت امام زمان" },
            { 1122, "پیروزی انقلاب اسلامی" },
            { 1209, "شهادت امام علی" },
            { 1219, "عید فطر" },
            { 1220, "تعطیل عید فطر" },
            { 1229, "ملی شدن صنعت نفت" }
        };

        private static readonly Dictionary<int, Dictionary<int, string>> Years = new Dictionary<int, Dictionary<int, string>>
        {
            { 1405, Year1405 }
        };

        /// <summary>The official holiday on <paramref name="date"/>, or null when that day is an ordinary working day.</summary>
        public static string Name(DateTime date)
        {
            Dictionary<int, string> year;
            var days = Years.TryGetValue(Shamsi.GetYear(date), out year) ? year : Solar;
            string name;
            return days.TryGetValue(Shamsi.GetMonth(date) * 100 + Shamsi.GetDayOfMonth(date), out name) ? name : null;
        }

        /// <summary>Same as <see cref="Name(DateTime)"/> for a Gregorian <c>yyyy-MM-dd</c> value posted by the day select.</summary>
        public static string Name(string gregorianDate)
        {
            DateTime date;
            return DateTime.TryParseExact(gregorianDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                ? Name(date)
                : null;
        }

        /// <summary>Official holidays from <paramref name="from"/> inclusive to <paramref name="to"/> exclusive.</summary>
        public static List<Day> Between(DateTime from, DateTime to)
        {
            var list = new List<Day>();
            for (var date = from.Date; date < to.Date; date = date.AddDays(1))
            {
                var name = Name(date);
                if (name != null) list.Add(new Day { Date = date, Name = name });
            }
            return list;
        }
    }
}
