using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>Group classes, enrollments and hourly facility bookings of academy and sports-club sites.</summary>
    public static partial class TenantStore
    {
        public static List<StoreClass> GetClasses(string companyCode) => All(NikooTables.Classes, companyCode, "[SortOrder], [Id]");

        public static StoreClass GetClass(string companyCode, int id) => Find(NikooTables.Classes, companyCode, id);

        public static StoreClass SaveClass(string companyCode, StoreClass item) => Save(NikooTables.Classes, companyCode, item);

        public static bool DeleteClass(string companyCode, int id) => Remove(NikooTables.Classes, companyCode, id);

        public static int CountClasses(string companyCode, bool? sample = null)
        {
            var filter = SqlFilter.ForStore(companyCode);
            if (sample.HasValue) filter.Eq("IsSample", sample.Value);
            return NikooDb.Count(NikooTables.Classes, filter);
        }

        public static List<StoreEnrollment> GetEnrollments(string companyCode) =>
            All(NikooTables.Enrollments, companyCode, "[CreatedAt] DESC");

        public static StoreEnrollment GetEnrollment(string companyCode, int id) => Find(NikooTables.Enrollments, companyCode, id);

        public static StoreEnrollment GetEnrollmentByCode(string companyCode, string code) =>
            string.IsNullOrWhiteSpace(code) ? null : FindBy(NikooTables.Enrollments, companyCode, "Code", code.Trim());

        public static List<StoreEnrollment> EnrollmentsOf(string companyCode, int classId) =>
            Every(NikooTables.Enrollments, companyCode, SqlFilter.ForStore(companyCode).Eq("ClassId", classId), "t.[CreatedAt]");

        public static int CountEnrollmentsToday(string companyCode)
        {
            var today = DateTime.Today;
            return NikooDb.Count(NikooTables.Enrollments, SqlFilter.ForStore(companyCode)
                .AtLeast("CreatedAt", today).Where("t.[CreatedAt] < {0}", today.AddDays(1))
                .In("Status", new[] { AppointmentStatus.Confirmed.ToString(), AppointmentStatus.PendingPayment.ToString() }));
        }

        /// <summary>Seats still free in the class, counting unpaid holds that have not expired.</summary>
        public static int SeatsTaken(string companyCode, int classId)
        {
            var now = DateTime.Now;
            return EnrollmentsOf(companyCode, classId).Count(e => e.HoldsSeat(now));
        }

        /// <summary>
        /// Takes a seat if the class is open and not full. Returns the error, or null once saved
        /// (the enrollment then has its Id and Code).
        /// </summary>
        public static string Enroll(string companyCode, StoreEnrollment enrollment)
        {
            lock (LockFor(companyCode))
            {
                var item = GetClass(companyCode, enrollment.ClassId);
                if (item == null || !item.AcceptsEnrollment(DateTime.Today)) return "ثبت‌نام این کلاس بسته است.";
                var now = DateTime.Now;
                var taken = EnrollmentsOf(companyCode, item.Id).Where(e => e.HoldsSeat(now)).ToList();
                if (item.Capacity > 0 && taken.Count >= item.Capacity) return "ظرفیت این کلاس تکمیل شده است.";
                if (taken.Any(e => e.CustomerMobile == enrollment.CustomerMobile))
                    return "با این شماره موبایل قبلاً در این کلاس ثبت‌نام شده است.";

                enrollment.Id = 0;
                enrollment.ClassTitle = item.Title;
                enrollment.Price = item.Price;
                enrollment.Code = NewTrackingCode(c => GetEnrollmentByCode(companyCode, c) != null);
                Save(NikooTables.Enrollments, companyCode, enrollment, (e, stored) => e.CreatedAt = DateTime.Now);
                return null;
            }
        }

        public static void MarkEnrollmentPaid(string companyCode, int id, long? refId, string message)
        {
            lock (LockFor(companyCode))
            {
                var e = GetEnrollment(companyCode, id);
                if (e == null || e.PaidAt.HasValue) return;
                var now = DateTime.Now;
                var item = GetClass(companyCode, e.ClassId);
                var taken = item == null ? 0 : EnrollmentsOf(companyCode, item.Id).Count(o => o.Id != e.Id && o.HoldsSeat(now));
                var full = item != null && item.Capacity > 0 && taken >= item.Capacity;
                e.Status = AppointmentStatus.Confirmed;
                e.PaidAmount = e.Price;
                e.RefId = refId;
                e.PaidAt = now;
                e.PaymentMessage = message;
                e.CancelledAt = null;
                if (full)
                    e.Notes = ((e.Notes ?? "") + " پرداخت پس از تکمیل ظرفیت انجام شد؛ با دانشجو هماهنگ کنید.").Trim();
                Update(NikooTables.Enrollments, companyCode, e);
            }
        }

        public static bool UpdateEnrollment(string companyCode, StoreEnrollment enrollment) =>
            Update(NikooTables.Enrollments, companyCode, enrollment);

        public static List<StoreFacility> GetFacilities(string companyCode) => All(NikooTables.Facilities, companyCode, "[SortOrder], [Id]");

        public static StoreFacility GetFacility(string companyCode, int id) => Find(NikooTables.Facilities, companyCode, id);

        public static StoreFacility SaveFacility(string companyCode, StoreFacility item) => Save(NikooTables.Facilities, companyCode, item);

        public static bool DeleteFacility(string companyCode, int id) => Remove(NikooTables.Facilities, companyCode, id);

        public static int CountFacilities(string companyCode, bool? sample = null)
        {
            var filter = SqlFilter.ForStore(companyCode);
            if (sample.HasValue) filter.Eq("IsSample", sample.Value);
            return NikooDb.Count(NikooTables.Facilities, filter);
        }

        public static List<StoreFacilityBooking> GetFacilityBookings(string companyCode) =>
            All(NikooTables.FacilityBookings, companyCode, "[StartAt] DESC");

        public static StoreFacilityBooking GetFacilityBooking(string companyCode, int id) => Find(NikooTables.FacilityBookings, companyCode, id);

        public static StoreFacilityBooking GetFacilityBookingByCode(string companyCode, string code) =>
            string.IsNullOrWhiteSpace(code) ? null : FindBy(NikooTables.FacilityBookings, companyCode, "Code", code.Trim());

        public static List<StoreFacilityBooking> FacilityBookingsBetween(string companyCode, DateTime from, DateTime to, int? facilityId = null)
        {
            var filter = SqlFilter.ForStore(companyCode).AtLeast("StartAt", from).Where("t.[StartAt] < {0}", to);
            if (facilityId.HasValue) filter.Eq("FacilityId", facilityId.Value);
            return Every(NikooTables.FacilityBookings, companyCode, filter, "t.[StartAt]");
        }

        public static int CountFacilityBookingsToday(string companyCode)
        {
            var today = DateTime.Today;
            return NikooDb.Count(NikooTables.FacilityBookings, SqlFilter.ForStore(companyCode)
                .AtLeast("StartAt", today).Where("t.[StartAt] < {0}", today.AddDays(1))
                .In("Status", new[] { AppointmentStatus.Confirmed.ToString(), AppointmentStatus.Completed.ToString(), AppointmentStatus.PendingPayment.ToString() }));
        }

        /// <summary>Free start times of one facility on a day, one slot apart.</summary>
        public static List<DateTime> FacilitySlots(string companyCode, StoreFacility facility, DateTime day, StoreSettings settings)
        {
            day = day.Date;
            var now = DateTime.Now;
            var earliest = now.AddMinutes(Math.Max(0, settings.BookingMinNoticeMinutes));
            var latest = DateTime.Today.AddDays(Math.Max(1, settings.BookingDaysAhead));
            if (day < DateTime.Today || day >= latest) return new List<DateTime>();
            var step = Math.Max(15, facility.SlotMinutes);
            var taken = FacilityBookingsBetween(companyCode, day, day.AddDays(1), facility.Id);
            var closed = GetTimeOffs(companyCode, day, day.AddDays(1)).Where(t => t.StaffId == null).ToList();
            var slots = new List<DateTime>();
            foreach (var hours in facility.HoursOn(BookingTime.DayOf(day)))
            {
                for (var minute = hours.StartMinute; minute + step <= hours.EndMinute; minute += step)
                {
                    var start = day.AddMinutes(minute);
                    var end = start.AddMinutes(step);
                    if (start < earliest || FacilityClosed(start, end, closed)) continue;
                    var used = taken.Count(b => b.Blocks(now) && b.StartAt < end && start < b.EndAt);
                    if (used < Math.Max(1, facility.Capacity)) slots.Add(start);
                }
            }
            return slots;
        }

        /// <summary>
        /// Reserves the facility when the slot is still free. <paramref name="checkHours"/> false lets the owner book
        /// outside opening hours, but never over capacity. Returns the error, or null once saved.
        /// </summary>
        public static string BookFacility(string companyCode, StoreFacilityBooking booking, StoreSettings settings, bool checkHours = true)
        {
            lock (LockFor(companyCode))
            {
                var facility = GetFacility(companyCode, booking.FacilityId);
                if (facility == null || !facility.IsActive) return "این سالن در دسترس نیست.";
                var now = DateTime.Now;
                if (booking.StartAt < now) return "این ساعت گذشته است.";
                var taken = FacilityBookingsBetween(companyCode, booking.StartAt.Date, booking.StartAt.Date.AddDays(1), facility.Id);
                var used = taken.Count(b => b.Blocks(now) && b.StartAt < booking.EndAt && booking.StartAt < b.EndAt);
                if (used >= Math.Max(1, facility.Capacity)) return "این ساعت همین حالا رزرو شد؛ لطفاً ساعت دیگری انتخاب کنید.";
                if (checkHours)
                {
                    var start = Minutes(booking.StartAt);
                    var end = Minutes(booking.EndAt);
                    var open = facility.HoursOn(BookingTime.DayOf(booking.StartAt)).Any(h => h.StartMinute <= start && end <= h.EndMinute);
                    var closed = GetTimeOffs(companyCode, booking.StartAt.Date, booking.StartAt.Date.AddDays(1));
                    if (!open || FacilityClosed(booking.StartAt, booking.EndAt, closed.Where(t => t.StaffId == null)))
                        return "این ساعت خارج از زمان باز بودن سالن یا در روز تعطیل است.";
                    if (booking.StartAt < now.AddMinutes(Math.Max(0, settings.BookingMinNoticeMinutes)))
                        return "برای این ساعت دیگر دیر شده است؛ ساعت دیگری انتخاب کنید.";
                }

                booking.Id = 0;
                booking.FacilityTitle = facility.Title;
                booking.Code = NewTrackingCode(c => GetFacilityBookingByCode(companyCode, c) != null);
                Save(NikooTables.FacilityBookings, companyCode, booking, (b, stored) => b.CreatedAt = DateTime.Now);
                return null;
            }
        }

        public static void MarkFacilityBookingPaid(string companyCode, int id, long? refId, string message)
        {
            lock (LockFor(companyCode))
            {
                var b = GetFacilityBooking(companyCode, id);
                if (b == null || b.PaidAt.HasValue) return;
                var now = DateTime.Now;
                var clash = b.Status == AppointmentStatus.Cancelled
                    || FacilityBookingsBetween(companyCode, b.StartAt.Date, b.StartAt.Date.AddDays(1), b.FacilityId)
                        .Count(o => o.Id != b.Id && o.Blocks(now) && o.StartAt < b.EndAt && b.StartAt < o.EndAt)
                        >= Math.Max(1, GetFacility(companyCode, b.FacilityId)?.Capacity ?? 1);
                b.Status = AppointmentStatus.Confirmed;
                b.PaidAmount = b.Price;
                b.RefId = refId;
                b.PaidAt = now;
                b.PaymentMessage = message;
                b.CancelledAt = null;
                if (clash)
                    b.Notes = ((b.Notes ?? "") + " پرداخت پس از پایان مهلت نگه‌داری انجام شد و این ساعت با رزرو دیگری تداخل دارد؛ با مشتری هماهنگ کنید.").Trim();
                Update(NikooTables.FacilityBookings, companyCode, b);
            }
        }

        public static bool UpdateFacilityBooking(string companyCode, StoreFacilityBooking booking) =>
            Update(NikooTables.FacilityBookings, companyCode, booking);

        private static bool FacilityClosed(DateTime start, DateTime end, IEnumerable<StoreTimeOff> offs)
        {
            var startMin = Minutes(start);
            var endMin = Minutes(end);
            return offs.Any(off => off.Date.Date == start.Date && (off.IsWholeDay()
                || (off.StartMinute.HasValue && off.EndMinute.HasValue && off.StartMinute.Value < endMin && startMin < off.EndMinute.Value)));
        }

        private static string NewTrackingCode(Func<string, bool> taken)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                for (var attempt = 0; ; attempt++)
                {
                    var bytes = new byte[8];
                    rng.GetBytes(bytes);
                    var code = new string(bytes.Select(b => CodeChars[b % CodeChars.Length]).ToArray());
                    if (attempt > 5 || !taken(code)) return code;
                }
            }
        }
    }
}
