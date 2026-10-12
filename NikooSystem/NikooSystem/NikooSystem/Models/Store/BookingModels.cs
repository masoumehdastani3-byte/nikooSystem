using System;
using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Models.Store
{
    /// <summary>A service customers book on a booking site (a visit, a haircut, a consultation…).</summary>
    public class StoreService : IStoreEntity
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public int DurationMinutes { get; set; } = 30;
        public long Price { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public bool IsSample { get; set; }
        /// <summary>The staff who offer the service; empty means every staff member.</summary>
        public List<int> StaffIds { get; set; } = new List<int>();

        public bool OfferedBy(int staffId) => StaffIds == null || StaffIds.Count == 0 || StaffIds.Contains(staffId);
    }

    /// <summary>A doctor, stylist or consultant whose time is booked.</summary>
    public class StoreStaff : IStoreEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        /// <summary>Speciality or role shown under the name (e.g. «متخصص پوست»).</summary>
        public string Title { get; set; }
        public string Bio { get; set; }
        public string PhotoUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public bool IsSample { get; set; }
        /// <summary>Weekly working hours; a day may have more than one interval (e.g. morning and evening).</summary>
        public List<StoreWorkHours> Hours { get; set; } = new List<StoreWorkHours>();

        public IEnumerable<StoreWorkHours> HoursOn(int day) =>
            (Hours ?? new List<StoreWorkHours>()).Where(h => h.Day == day && h.EndMinute > h.StartMinute).OrderBy(h => h.StartMinute);
    }

    /// <summary>One working interval of a weekday, in minutes from midnight.</summary>
    public class StoreWorkHours
    {
        /// <summary>0 = Saturday … 6 = Friday (see <see cref="BookingTime.DayOf"/>).</summary>
        public int Day { get; set; }
        public int StartMinute { get; set; }
        public int EndMinute { get; set; }
    }

    /// <summary>A closed day or hours: for the whole site (holiday) when StaffId is null, else one person's leave.</summary>
    public class StoreTimeOff : IStoreEntity
    {
        public int Id { get; set; }
        public int? StaffId { get; set; }
        public DateTime Date { get; set; }
        /// <summary>Both null: the whole day.</summary>
        public int? StartMinute { get; set; }
        public int? EndMinute { get; set; }
        public string Reason { get; set; }

        public bool IsWholeDay() => !StartMinute.HasValue || !EndMinute.HasValue;
    }

    public enum AppointmentStatus
    {
        /// <summary>Waiting for the online deposit; holds the time for a few minutes.</summary>
        PendingPayment,
        Confirmed,
        Completed,
        Cancelled,
        NoShow
    }

    public class StoreAppointment : IStoreEntity
    {
        public int Id { get; set; }
        /// <summary>Tracking code given to the customer; with the mobile number it opens the appointment page.</summary>
        public string Code { get; set; }
        public int ServiceId { get; set; }
        public string ServiceTitle { get; set; }
        public int StaffId { get; set; }
        public string StaffName { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public long Price { get; set; }
        /// <summary>The part of the price paid online when booking (0 when no deposit is taken).</summary>
        public long Deposit { get; set; }
        public long PaidAmount { get; set; }
        public string CustomerName { get; set; }
        public string CustomerMobile { get; set; }
        public string CustomerEmail { get; set; }
        public string Notes { get; set; }
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
        public string Authority { get; set; }
        public long? RefId { get; set; }
        public string PaymentMessage { get; set; }
        public DateTime? PaidAt { get; set; }
        /// <summary>Private note of the owner; never shown to the customer.</summary>
        public string AdminNote { get; set; }
        /// <summary>online | admin</summary>
        public string Source { get; set; } = "online";
        public bool IsSample { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CancelledAt { get; set; }

        /// <summary>Minutes an unpaid online booking keeps its time before others can take it.</summary>
        public const int PaymentHoldMinutes = 15;

        /// <summary>Takes the staff member's time: every booking except cancelled ones and unpaid ones whose hold ran out.</summary>
        public bool Blocks(DateTime now) =>
            Status != AppointmentStatus.Cancelled
            && (Status != AppointmentStatus.PendingPayment || CreatedAt.AddMinutes(PaymentHoldMinutes) > now);

        public bool IsUpcoming(DateTime now) => Status == AppointmentStatus.Confirmed && StartAt > now;
    }

    /// <summary>A free start time offered to the customer, with the staff members free at that time.</summary>
    public class BookingSlot
    {
        public DateTime StartAt { get; set; }
        public List<int> StaffIds { get; set; } = new List<int>();
    }

    public class AppointmentGridQuery : GridQuery
    {
        public AppointmentStatus? Status { get; set; }
        public int? StaffId { get; set; }
        public int? ServiceId { get; set; }
        /// <summary>today | upcoming | past</summary>
        public string When { get; set; }
    }

    /// <summary>Weekdays and times of the Iranian week (Saturday first), shared by the booking pages.</summary>
    public static class BookingTime
    {
        public static readonly string[] DayNames = { "شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه" };

        /// <summary>0 = Saturday … 6 = Friday.</summary>
        public static int DayOf(DateTime date) => ((int)date.DayOfWeek + 1) % 7;

        /// <summary>The Saturday on or before <paramref name="date"/>.</summary>
        public static DateTime WeekStart(DateTime date) => date.Date.AddDays(-DayOf(date));

        /// <summary>Minutes from midnight as «۰۹:۳۰».</summary>
        public static string Clock(int minutes) => ((minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00")).ToFaDigitsLocal();

        public static string Clock(DateTime time) => Clock(time.Hour * 60 + time.Minute);

        /// <summary>«09:30» → 570; null when the text is not a time of day.</summary>
        public static int? ParseClock(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var parts = text.Trim().Split(':');
            int h, m;
            if (parts.Length != 2 || !int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m)) return null;
            if (h < 0 || h > 24 || m < 0 || m > 59 || (h == 24 && m > 0)) return null;
            return h * 60 + m;
        }

        /// <summary>An Iranian mobile number as 09xxxxxxxxx (Persian digits, spaces and +98 accepted), or null when it is not one.</summary>
        public static string NormalizeMobile(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var digits = new string(text.Select(c =>
                c >= '۰' && c <= '۹' ? (char)('0' + (c - '۰')) :
                c >= '٠' && c <= '٩' ? (char)('0' + (c - '٠')) : c).Where(char.IsDigit).ToArray());
            if (digits.StartsWith("0098")) digits = "0" + digits.Substring(4);
            else if (digits.StartsWith("98") && digits.Length == 12) digits = "0" + digits.Substring(2);
            else if (digits.Length == 10 && digits.StartsWith("9")) digits = "0" + digits;
            return digits.Length == 11 && digits.StartsWith("09") ? digits : null;
        }

        /// <summary>«۹۰ دقیقه» / «۱ ساعت و ۳۰ دقیقه».</summary>
        public static string Duration(int minutes)
        {
            if (minutes < 60) return (minutes + " دقیقه").ToFaDigitsLocal();
            var text = minutes / 60 + " ساعت";
            if (minutes % 60 > 0) text += " و " + minutes % 60 + " دقیقه";
            return text.ToFaDigitsLocal();
        }

        public static string StatusText(this AppointmentStatus status)
        {
            switch (status)
            {
                case AppointmentStatus.PendingPayment: return "در انتظار پرداخت";
                case AppointmentStatus.Confirmed: return "تأیید شده";
                case AppointmentStatus.Completed: return "انجام شده";
                case AppointmentStatus.Cancelled: return "لغو شده";
                default: return "مراجعه نکرد";
            }
        }

        public static string StatusBadgeClass(this AppointmentStatus status)
        {
            switch (status)
            {
                case AppointmentStatus.PendingPayment: return "bg-warning text-dark";
                case AppointmentStatus.Confirmed: return "bg-primary";
                case AppointmentStatus.Completed: return "bg-success";
                case AppointmentStatus.Cancelled: return "bg-secondary";
                default: return "bg-danger";
            }
        }

        private static string ToFaDigitsLocal(this string text)
        {
            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (chars[i] >= '0' && chars[i] <= '9') chars[i] = (char)('۰' + (chars[i] - '0'));
            return new string(chars);
        }
    }
}
