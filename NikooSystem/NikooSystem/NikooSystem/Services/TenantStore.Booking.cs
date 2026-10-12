using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>Services, staff, closed days and appointments of booking sites, and the free times offered to customers.</summary>
    public static partial class TenantStore
    {
        // ---- Services

        public static List<StoreService> GetServices(string companyCode) => All(NikooTables.Services, companyCode, "[SortOrder], [Id]");

        public static StoreService GetService(string companyCode, int id) => Find(NikooTables.Services, companyCode, id);

        public static StoreService SaveService(string companyCode, StoreService service) =>
            Save(NikooTables.Services, companyCode, service);

        public static bool DeleteService(string companyCode, int id) => Remove(NikooTables.Services, companyCode, id);

        public static int CountServices(string companyCode, bool? sample = null)
        {
            var filter = SqlFilter.ForStore(companyCode);
            if (sample.HasValue) filter.Eq("IsSample", sample.Value);
            return NikooDb.Count(NikooTables.Services, filter);
        }

        // ---- Staff

        public static List<StoreStaff> GetStaff(string companyCode) => All(NikooTables.Staff, companyCode, "[SortOrder], [Id]");

        public static StoreStaff GetStaffMember(string companyCode, int id) => Find(NikooTables.Staff, companyCode, id);

        public static StoreStaff SaveStaff(string companyCode, StoreStaff staff) => Save(NikooTables.Staff, companyCode, staff);

        /// <summary>
        /// Deletes a staff member with their leave and takes them off the services they offered. A service left with nobody
        /// is deactivated (an empty list would mean "everyone"); returns those services.
        /// </summary>
        public static List<StoreService> DeleteStaff(string companyCode, int id)
        {
            var orphaned = new List<StoreService>();
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                foreach (var service in NikooDb.List(NikooTables.Services, companyCode, tx: tx).Where(s => s.StaffIds != null && s.StaffIds.Contains(id)))
                {
                    service.StaffIds.Remove(id);
                    if (service.StaffIds.Count == 0)
                    {
                        service.IsActive = false;
                        orphaned.Add(service);
                    }
                    NikooDb.Update(NikooTables.Services, service, companyCode, tx);
                }
                foreach (var leave in NikooDb.List(NikooTables.TimeOffs, companyCode, tx: tx).Where(t => t.StaffId == id))
                    NikooDb.Delete(NikooTables.TimeOffs, NikooDb.Key(companyCode, leave.Id), tx);
                NikooDb.Delete(NikooTables.Staff, NikooDb.Key(companyCode, id), tx);
                tx.Commit();
            }
            return orphaned;
        }

        public static int CountStaff(string companyCode, bool? sample = null)
        {
            var filter = SqlFilter.ForStore(companyCode);
            if (sample.HasValue) filter.Eq("IsSample", sample.Value);
            return NikooDb.Count(NikooTables.Staff, filter);
        }

        // ---- Closed days and leave

        /// <summary>Closed days and leave from <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive).</summary>
        public static List<StoreTimeOff> GetTimeOffs(string companyCode, DateTime from, DateTime to)
        {
            var filter = SqlFilter.ForStore(companyCode).AtLeast("Date", from.Date).Where("t.[Date] < {0}", to.Date);
            return Every(NikooTables.TimeOffs, companyCode, filter, "t.[Date], t.[StartMinute]");
        }

        public static StoreTimeOff AddTimeOff(string companyCode, StoreTimeOff timeOff)
        {
            timeOff.Id = 0;
            timeOff.Date = timeOff.Date.Date;
            return Save(NikooTables.TimeOffs, companyCode, timeOff);
        }

        public static bool DeleteTimeOff(string companyCode, int id) => Remove(NikooTables.TimeOffs, companyCode, id);

        // ---- Appointments

        public static StoreAppointment GetAppointment(string companyCode, int id) => Find(NikooTables.Appointments, companyCode, id);

        public static StoreAppointment GetAppointmentByCode(string companyCode, string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            return code.Length == 0 ? null : FindBy(NikooTables.Appointments, companyCode, "Code", code);
        }

        public static bool UpdateAppointment(string companyCode, StoreAppointment appointment) =>
            Update(NikooTables.Appointments, companyCode, appointment);

        /// <summary>Appointments starting from <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive), by time.</summary>
        public static List<StoreAppointment> AppointmentsBetween(string companyCode, DateTime from, DateTime to, int? staffId = null)
        {
            var filter = SqlFilter.ForStore(companyCode).AtLeast("StartAt", from).Where("t.[StartAt] < {0}", to);
            if (staffId.HasValue) filter.Eq("StaffId", staffId.Value);
            return Every(NikooTables.Appointments, companyCode, filter, "t.[StartAt]");
        }

        /// <summary>Every row matching the filter, read page by page.</summary>
        private static List<T> Every<T>(EntityMap<T> map, string companyCode, SqlFilter filter, string orderBy) where T : class, new()
        {
            var rows = new List<T>();
            for (var page = 1; ; page++)
            {
                var chunk = Page(map, companyCode, filter, orderBy, page, PagedList<T>.MaxPageSize);
                rows.AddRange(chunk);
                if (!chunk.HasNext) return rows;
            }
        }

        private static readonly Dictionary<string, string> AppointmentSorts = new Dictionary<string, string>
        {
            { "when", "t.[StartAt]" },
            { "customer", "t.[CustomerName]" },
            { "service", "t.[ServiceTitle]" },
            { "staff", "t.[StaffName]" },
            { "price", "t.[Price]" },
            { "created", "t.[CreatedAt]" }
        };

        public static PagedList<StoreAppointment> AppointmentGrid(string companyCode, AppointmentGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Code", "CustomerName", "CustomerMobile", "ServiceTitle", "StaffName");
            if (query.Status.HasValue) filter.Eq("Status", query.Status.Value.ToString());
            if (query.StaffId.HasValue) filter.Eq("StaffId", query.StaffId.Value);
            if (query.ServiceId.HasValue) filter.Eq("ServiceId", query.ServiceId.Value);
            var today = DateTime.Today;
            switch (query.When)
            {
                case "today": filter.AtLeast("StartAt", today).Where("t.[StartAt] < {0}", today.AddDays(1)); break;
                case "upcoming": filter.AtLeast("StartAt", DateTime.Now); break;
                case "past": filter.Where("t.[StartAt] < {0}", DateTime.Now); break;
            }
            var descending = query.When != "upcoming" && query.When != "today";
            return Grid(NikooTables.Appointments, companyCode, query, filter, AppointmentSorts, "when", descending);
        }

        /// <summary>Today's appointments that are not cancelled; the badge next to «فهرست نوبت‌ها».</summary>
        public static int CountAppointmentsToday(string companyCode)
        {
            var today = DateTime.Today;
            return NikooDb.Count(NikooTables.Appointments, SqlFilter.ForStore(companyCode)
                .AtLeast("StartAt", today).Where("t.[StartAt] < {0}", today.AddDays(1))
                .In("Status", new[] { AppointmentStatus.Confirmed.ToString(), AppointmentStatus.Completed.ToString(), AppointmentStatus.PendingPayment.ToString() }));
        }

        public static int CountAppointments(string companyCode, DateTime from, DateTime to, params AppointmentStatus[] statuses)
        {
            var filter = SqlFilter.ForStore(companyCode).AtLeast("StartAt", from).Where("t.[StartAt] < {0}", to);
            if (statuses.Length > 0) filter.In("Status", statuses.Select(s => s.ToString()));
            return NikooDb.Count(NikooTables.Appointments, filter);
        }

        /// <summary>The price of the appointments done between the two times.</summary>
        public static long BookingRevenue(string companyCode, DateTime from, DateTime to)
        {
            var key = NikooDb.Key(companyCode);
            key["From"] = from;
            key["To"] = to;
            key["Done"] = AppointmentStatus.Completed.ToString();
            return NikooDb.Column<long>("SELECT ISNULL(SUM([Price]), 0) FROM " + Table(NikooTables.Appointments)
                + " WHERE [CompanyCode] = @CompanyCode AND [Status] = @Done AND [StartAt] >= @From AND [StartAt] < @To", key).FirstOrDefault();
        }

        /// <summary>The first day of the current Persian month.</summary>
        public static DateTime PersianMonthStart(DateTime date)
        {
            var pc = new PersianCalendar();
            return pc.ToDateTime(pc.GetYear(date), pc.GetMonth(date), 1, 0, 0, 0, 0);
        }

        /// <summary>
        /// Books the time if it is still free, all under the store's lock so two customers never get the same time.
        /// <paramref name="checkHours"/> false lets the owner book outside working hours (e.g. an extra patient), but never
        /// on top of another appointment. Returns the error message, or null once saved (the appointment then has its Id and Code).
        /// </summary>
        public static string BookAppointment(string companyCode, StoreAppointment appointment, StoreSettings settings, bool checkHours = true)
        {
            lock (LockFor(companyCode))
            {
                var staff = GetStaffMember(companyCode, appointment.StaffId);
                if (staff == null || !staff.IsActive) return "این متخصص در دسترس نیست.";
                var day = appointment.StartAt.Date;
                var taken = AppointmentsBetween(companyCode, day, day.AddDays(1), staff.Id);
                var now = DateTime.Now;
                if (taken.Any(a => a.Blocks(now) && a.StartAt < appointment.EndAt && appointment.StartAt < a.EndAt))
                    return "این ساعت همین حالا رزرو شد؛ لطفاً ساعت دیگری انتخاب کنید.";
                if (checkHours)
                {
                    var closed = GetTimeOffs(companyCode, day, day.AddDays(1));
                    var start = Minutes(appointment.StartAt);
                    var end = start + (int)(appointment.EndAt - appointment.StartAt).TotalMinutes;
                    var free = FreeIntervals(staff, day, closed, new List<StoreAppointment>(), now);
                    if (!free.Any(f => f.Key <= start && end <= f.Value))
                        return "این ساعت خارج از زمان کاری متخصص یا در روز تعطیل است.";
                }

                appointment.Id = 0;
                appointment.Code = NewAppointmentCode(companyCode);
                appointment.StaffName = staff.Name;
                Save(NikooTables.Appointments, companyCode, appointment, (a, stored) => a.CreatedAt = DateTime.Now);
                return null;
            }
        }

        /// <summary>Records the online deposit; the appointment is confirmed.</summary>
        public static void MarkAppointmentPaid(string companyCode, int id, long? refId, string message)
        {
            lock (LockFor(companyCode))
            {
                var a = GetAppointment(companyCode, id);
                if (a == null || a.PaidAt.HasValue) return;
                var now = DateTime.Now;
                var clash = a.Status == AppointmentStatus.Cancelled
                    || AppointmentsBetween(companyCode, a.StartAt.Date, a.StartAt.Date.AddDays(1), a.StaffId)
                        .Any(o => o.Id != a.Id && o.Blocks(now) && o.StartAt < a.EndAt && a.StartAt < o.EndAt);
                a.Status = AppointmentStatus.Confirmed;
                a.PaidAmount = a.Deposit;
                a.RefId = refId;
                a.PaidAt = now;
                a.PaymentMessage = message;
                a.CancelledAt = null;
                if (clash)
                    a.AdminNote = ((a.AdminNote ?? "") + " پرداخت پس از پایان مهلت نگه‌داری انجام شد و این ساعت با نوبت دیگری تداخل دارد؛ با مشتری هماهنگ کنید.").Trim();
                UpdateAppointment(companyCode, a);
            }
        }

        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private static string NewAppointmentCode(string companyCode)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                for (var attempt = 0; ; attempt++)
                {
                    var bytes = new byte[8];
                    rng.GetBytes(bytes);
                    var code = new string(bytes.Select(b => CodeChars[b % CodeChars.Length]).ToArray());
                    if (attempt > 5 || GetAppointmentByCode(companyCode, code) == null) return code;
                }
            }
        }

        // ---- Free times

        /// <summary>
        /// The days from today on which <paramref name="service"/> has at least one free time, with or without a chosen staff
        /// member, within the booking window of the site.
        /// </summary>
        public static List<DateTime> BookableDays(string companyCode, StoreService service, int? staffId, StoreSettings settings)
        {
            var now = DateTime.Now;
            var from = now.Date;
            var to = from.AddDays(Math.Max(1, Math.Min(90, settings.BookingDaysAhead)) + 1);
            var staff = EligibleStaff(companyCode, service, staffId);
            if (!staff.Any()) return new List<DateTime>();
            var appointments = AppointmentsBetween(companyCode, from, to);
            var closed = GetTimeOffs(companyCode, from, to);
            var days = new List<DateTime>();
            for (var d = from; d < to; d = d.AddDays(1))
                if (ComputeSlots(service, staff, d, appointments, closed, settings, now).Any()) days.Add(d);
            return days;
        }

        /// <summary>The free start times of <paramref name="service"/> on <paramref name="date"/>.</summary>
        public static List<BookingSlot> FreeSlots(string companyCode, StoreService service, int? staffId, DateTime date, StoreSettings settings)
        {
            var now = DateTime.Now;
            date = date.Date;
            var last = now.Date.AddDays(Math.Max(1, Math.Min(90, settings.BookingDaysAhead)));
            if (date < now.Date || date > last) return new List<BookingSlot>();
            var staff = EligibleStaff(companyCode, service, staffId);
            if (!staff.Any()) return new List<BookingSlot>();
            return ComputeSlots(service, staff, date,
                AppointmentsBetween(companyCode, date, date.AddDays(1)), GetTimeOffs(companyCode, date, date.AddDays(1)), settings, now);
        }

        /// <summary>Active staff offering the service (only <paramref name="staffId"/> when given).</summary>
        public static List<StoreStaff> EligibleStaff(string companyCode, StoreService service, int? staffId = null)
        {
            return GetStaff(companyCode)
                .Where(s => s.IsActive && service.OfferedBy(s.Id) && (!staffId.HasValue || s.Id == staffId.Value))
                .ToList();
        }

        private static List<BookingSlot> ComputeSlots(StoreService service, List<StoreStaff> staff, DateTime date,
            List<StoreAppointment> appointments, List<StoreTimeOff> closed, StoreSettings settings, DateTime now)
        {
            var step = Math.Max(5, settings.BookingSlotMinutes);
            var duration = Math.Max(5, service.DurationMinutes);
            var earliest = now.AddMinutes(Math.Max(0, settings.BookingMinNoticeMinutes));
            var slots = new SortedDictionary<int, BookingSlot>();
            foreach (var person in staff)
            {
                var busy = appointments.Where(a => a.StaffId == person.Id && a.StartAt.Date == date).ToList();
                foreach (var free in FreeIntervals(person, date, closed, busy, now))
                {
                    var hours = person.HoursOn(BookingTime.DayOf(date)).FirstOrDefault(h => h.StartMinute <= free.Key && free.Key < h.EndMinute);
                    var origin = hours?.StartMinute ?? free.Key;
                    // start times keep the rhythm of the working day (9:00, 9:30…) even after a booking of odd length
                    var first = origin + (int)Math.Ceiling((free.Key - origin) / (double)step) * step;
                    for (var start = first; start + duration <= free.Value; start += step)
                    {
                        var at = date.AddMinutes(start);
                        if (at < earliest) continue;
                        if (!slots.TryGetValue(start, out var slot))
                            slots[start] = slot = new BookingSlot { StartAt = at };
                        slot.StaffIds.Add(person.Id);
                    }
                }
            }
            return slots.Values.ToList();
        }

        /// <summary>
        /// The staff member's working intervals on the day (minutes from midnight) minus closed hours and the given
        /// appointments that still hold their time.
        /// </summary>
        private static List<KeyValuePair<int, int>> FreeIntervals(StoreStaff staff, DateTime date, List<StoreTimeOff> closed,
            List<StoreAppointment> appointments, DateTime now)
        {
            var intervals = staff.HoursOn(BookingTime.DayOf(date)).Select(h => new KeyValuePair<int, int>(h.StartMinute, h.EndMinute)).ToList();
            var cuts = closed
                .Where(c => c.Date.Date == date.Date && (!c.StaffId.HasValue || c.StaffId == staff.Id))
                .Select(c => c.IsWholeDay() ? new KeyValuePair<int, int>(0, 24 * 60) : new KeyValuePair<int, int>(c.StartMinute.Value, c.EndMinute.Value))
                .Concat(appointments
                    .Where(a => a.StaffId == staff.Id && a.Blocks(now))
                    .Select(a => new KeyValuePair<int, int>(Minutes(a.StartAt), Minutes(a.StartAt) + (int)(a.EndAt - a.StartAt).TotalMinutes)));
            foreach (var cut in cuts)
            {
                var next = new List<KeyValuePair<int, int>>();
                foreach (var i in intervals)
                {
                    if (cut.Value <= i.Key || cut.Key >= i.Value) { next.Add(i); continue; }
                    if (cut.Key > i.Key) next.Add(new KeyValuePair<int, int>(i.Key, cut.Key));
                    if (cut.Value < i.Value) next.Add(new KeyValuePair<int, int>(cut.Value, i.Value));
                }
                intervals = next;
            }
            return intervals;
        }

        private static int Minutes(DateTime time) => time.Hour * 60 + time.Minute;
    }
}
