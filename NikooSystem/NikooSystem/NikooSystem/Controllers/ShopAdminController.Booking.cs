using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Booking sites: the calendar, appointments, services, staff with their working hours, and closed days.</summary>
    public partial class ShopAdminController
    {
        private const string BookingDateFormat = "yyyy-MM-dd";

        // ---- Calendar

        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult Calendar(string start, int? staffId)
        {
            KeepDemoAppointmentsFresh();
            var weekStart = BookingTime.WeekStart(ParseBookingDate(start) ?? DateTime.Today);
            var weekEnd = weekStart.AddDays(7);
            ViewBag.WeekStart = weekStart;
            ViewBag.StaffId = staffId;
            ViewBag.Staff = TenantStore.GetStaff(CompanyCode);
            ViewBag.TimeOffs = TenantStore.GetTimeOffs(CompanyCode, weekStart, weekEnd)
                .Where(t => !staffId.HasValue || !t.StaffId.HasValue || t.StaffId == staffId).ToList();
            return View(TenantStore.AppointmentsBetween(CompanyCode, weekStart, weekEnd, staffId)
                .Where(a => a.Status != AppointmentStatus.Cancelled).ToList());
        }

        // ---- Appointments

        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult Appointments(AppointmentGridQuery query)
        {
            KeepDemoAppointmentsFresh();
            ViewBag.Staff = TenantStore.GetStaff(CompanyCode);
            ViewBag.Services = TenantStore.GetServices(CompanyCode);
            ViewBag.Query = query;
            return View(TenantStore.AppointmentGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult AppointmentDetails(int id)
        {
            var appointment = TenantStore.GetAppointment(CompanyCode, id);
            if (appointment == null) return ToAction("Appointments");
            ViewBag.History = TenantStore.AppointmentGrid(CompanyCode, new AppointmentGridQuery { Q = appointment.CustomerMobile, Size = 20 })
                .Where(a => a.Id != id && a.CustomerMobile == appointment.CustomerMobile).ToList();
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult UpdateAppointment(int id, AppointmentStatus status, string adminNote)
        {
            var appointment = TenantStore.GetAppointment(CompanyCode, id);
            if (appointment == null) return ToAction("Appointments");
            SetStatus(appointment, status);
            appointment.AdminNote = Clip(adminNote, 1000);
            TenantStore.UpdateAppointment(CompanyCode, appointment);
            return Ok("نوبت ذخیره شد.", "AppointmentDetails", new { id });
        }

        /// <summary>The quick status buttons of the appointment list and calendar.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult SetAppointmentStatus(int id, AppointmentStatus status, string back = null)
        {
            var appointment = TenantStore.GetAppointment(CompanyCode, id);
            if (appointment != null)
            {
                SetStatus(appointment, status);
                TenantStore.UpdateAppointment(CompanyCode, appointment);
                Ok("نوبت «" + appointment.CustomerName + "»: " + status.StatusText() + ".");
            }
            return BackTo(back == "Calendar" || back == "Index" ? back : "Appointments");
        }

        private static void SetStatus(StoreAppointment appointment, AppointmentStatus status)
        {
            if (appointment.Status == status) return;
            appointment.Status = status;
            appointment.CancelledAt = status == AppointmentStatus.Cancelled ? DateTime.Now : (DateTime?)null;
        }

        [HttpGet]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult NewAppointment(string date, string time, int? staffId, int? serviceId)
        {
            ViewBag.Services = TenantStore.GetServices(CompanyCode).Where(s => s.IsActive).ToList();
            ViewBag.Staff = TenantStore.GetStaff(CompanyCode).Where(s => s.IsActive).ToList();
            ViewBag.Days = BookingDayOptions(-7, Math.Max(CurrentSettings.BookingDaysAhead, 60));
            ViewBag.Date = ParseBookingDate(date)?.ToString(BookingDateFormat, CultureInfo.InvariantCulture)
                ?? DateTime.Today.ToString(BookingDateFormat, CultureInfo.InvariantCulture);
            ViewBag.Time = BookingTime.ParseClock(time).HasValue ? time : null;
            ViewBag.StaffId = staffId;
            ViewBag.ServiceId = serviceId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult NewAppointment(int? serviceId, int? staffId, string date, string time, string customerName,
            string customerMobile, string notes, bool outsideHours = false)
        {
            var back = new { date, time, staffId, serviceId };
            var service = serviceId.HasValue ? TenantStore.GetService(CompanyCode, serviceId.Value) : null;
            if (service == null) return Fail("خدمت را انتخاب کنید.", "NewAppointment", back);
            if (!staffId.HasValue) return Fail("متخصص را انتخاب کنید.", "NewAppointment", back);
            if (!service.OfferedBy(staffId.Value)) return Fail("این متخصص خدمت «" + service.Title + "» را ارائه نمی‌دهد.", "NewAppointment", back);
            var day = ParseBookingDate(date);
            var minute = BookingTime.ParseClock(time);
            if (!day.HasValue || !minute.HasValue) return Fail("روز و ساعت را وارد کنید.", "NewAppointment", back);
            if (string.IsNullOrWhiteSpace(customerName)) return Fail("نام مراجع را وارد کنید.", "NewAppointment", back);
            var mobile = BookingTime.NormalizeMobile(customerMobile);
            if (mobile == null) return Fail("شماره موبایل معتبر نیست؛ مثلاً ۰۹۱۲۱۲۳۴۵۶۷.", "NewAppointment", back);

            var start = day.Value.AddMinutes(minute.Value);
            var appointment = new StoreAppointment
            {
                ServiceId = service.Id,
                ServiceTitle = service.Title,
                StaffId = staffId.Value,
                StartAt = start,
                EndAt = start.AddMinutes(service.DurationMinutes),
                Price = service.Price,
                CustomerName = Clip(customerName, 80),
                CustomerMobile = mobile,
                Notes = Clip(notes, 1000),
                Status = start < DateTime.Now ? AppointmentStatus.Completed : AppointmentStatus.Confirmed,
                Source = "admin"
            };
            var error = TenantStore.BookAppointment(CompanyCode, appointment, CurrentSettings, !outsideHours);
            if (error != null)
                return Fail(error + (outsideHours ? "" : " اگر می‌خواهید خارج از ساعت کاری نوبت بدهید، گزینه آن را بزنید."), "NewAppointment", back);
            return Ok("نوبت " + appointment.CustomerName + " ثبت شد. کد پیگیری: " + appointment.Code, "AppointmentDetails", new { id = appointment.Id });
        }

        // ---- Services

        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult Services(GridQuery query)
        {
            var rows = TenantStore.GetServices(CompanyCode).AsEnumerable();
            if (!string.IsNullOrWhiteSpace(query.Q))
                rows = rows.Where(s => (s.Title ?? "").IndexOf(query.Q.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            ViewBag.Staff = TenantStore.GetStaff(CompanyCode);
            return View(PagedList<StoreService>.From(rows, query.Page, query.PageSize));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult EditService(int? id)
        {
            var service = id.HasValue
                ? TenantStore.GetService(CompanyCode, id.Value)
                : new StoreService { DurationMinutes = CurrentSettings.BookingSlotMinutes, SortOrder = TenantStore.CountServices(CompanyCode) + 1 };
            if (service == null) return ToAction("Services");
            ViewBag.Staff = TenantStore.GetStaff(CompanyCode);
            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult EditService(StoreService model, HttpPostedFileBase imageFile, bool removeImage = false)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Fail("عنوان خدمت الزامی است.", "EditService", model.Id > 0 ? new { id = model.Id } : null);
            var existing = model.Id > 0 ? TenantStore.GetService(CompanyCode, model.Id) : null;
            if (model.Id > 0 && existing == null) return ToAction("Services");

            model.ImageUrl = removeImage ? null : existing?.ImageUrl;
            try
            {
                var image = ImageService.SaveProductImage(CompanyCode, imageFile);
                if (image != null) model.ImageUrl = image.LargeUrl;
            }
            catch (InvalidOperationException ex)
            {
                Fail(ex.Message);
            }

            var staffIds = new HashSet<int>(TenantStore.GetStaff(CompanyCode).Select(s => s.Id));
            model.Title = Clip(model.Title, 150);
            model.Description = Clip(model.Description, 1000);
            model.DurationMinutes = Math.Max(5, Math.Min(480, model.DurationMinutes));
            model.Price = Math.Max(0, model.Price);
            model.Mode = Kind.IsCampus ? CampusDelivery.Normalize(model.Mode) : existing?.Mode;
            model.StaffIds = (model.StaffIds ?? new List<int>()).Where(staffIds.Contains).Distinct().ToList();
            model.IsSample = false;
            model = TenantStore.SaveService(CompanyCode, model);
            return Ok("خدمت «" + model.Title + "» ذخیره شد.", "Services");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult DeleteService(int id)
        {
            TenantStore.DeleteService(CompanyCode, id);
            return OkBack("خدمت حذف شد. نوبت‌های گرفته‌شده برای آن باقی می‌مانند.", "Services");
        }

        // ---- Staff and working hours

        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult Staff(GridQuery query)
        {
            var rows = TenantStore.GetStaff(CompanyCode).AsEnumerable();
            if (!string.IsNullOrWhiteSpace(query.Q))
                rows = rows.Where(s => ((s.Name ?? "") + " " + (s.Title ?? "")).IndexOf(query.Q.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            ViewBag.Services = TenantStore.GetServices(CompanyCode);
            return View(PagedList<StoreStaff>.From(rows, query.Page, query.PageSize));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult EditStaff(int? id)
        {
            var staff = id.HasValue
                ? TenantStore.GetStaffMember(CompanyCode, id.Value)
                : new StoreStaff { SortOrder = TenantStore.CountStaff(CompanyCode) + 1, Hours = DefaultWeek() };
            if (staff == null) return ToAction("Staff");
            return View(staff);
        }

        /// <summary>Saturday to Wednesday 9–17 and Thursday 9–13: the starting week of a new staff member.</summary>
        private static List<StoreWorkHours> DefaultWeek() =>
            Enumerable.Range(0, 5).Select(d => new StoreWorkHours { Day = d, StartMinute = 9 * 60, EndMinute = 17 * 60 })
                .Concat(new[] { new StoreWorkHours { Day = 5, StartMinute = 9 * 60, EndMinute = 13 * 60 } })
                .ToList();

        /// <summary>Working intervals per weekday from the hours editor: fields h{day}s{n} and h{day}e{n}, n = 1 or 2.</summary>
        private List<StoreWorkHours> PostedHours(out string error)
        {
            error = null;
            var hours = new List<StoreWorkHours>();
            for (var day = 0; day < 7; day++)
            {
                if (Request.Form["off" + day] == "true") continue;
                for (var n = 1; n <= 2; n++)
                {
                    var start = BookingTime.ParseClock(Request.Form["h" + day + "s" + n]);
                    var end = BookingTime.ParseClock(Request.Form["h" + day + "e" + n]);
                    if (!start.HasValue && !end.HasValue) continue;
                    if (!start.HasValue || !end.HasValue || end.Value <= start.Value)
                    {
                        error = "ساعت کاری " + BookingTime.DayNames[day] + " درست نیست؛ ساعت پایان باید بعد از شروع باشد.";
                        continue;
                    }
                    hours.Add(new StoreWorkHours { Day = day, StartMinute = start.Value, EndMinute = end.Value });
                }
                var today = hours.Where(h => h.Day == day).OrderBy(h => h.StartMinute).ToList();
                if (today.Count == 2 && today[1].StartMinute < today[0].EndMinute)
                    error = "دو بازه کاری " + BookingTime.DayNames[day] + " روی هم افتاده‌اند.";
            }
            return hours;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult EditStaff(StoreStaff model, HttpPostedFileBase photoFile, bool removePhoto = false)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                return Fail("نام الزامی است.", "EditStaff", model.Id > 0 ? new { id = model.Id } : null);
            var existing = model.Id > 0 ? TenantStore.GetStaffMember(CompanyCode, model.Id) : null;
            if (model.Id > 0 && existing == null) return ToAction("Staff");

            string hoursError;
            var hours = PostedHours(out hoursError);
            if (hoursError != null)
                return Fail(hoursError + " تغییرات ذخیره نشد.", "EditStaff", model.Id > 0 ? new { id = model.Id } : null);

            model.PhotoUrl = removePhoto ? null : existing?.PhotoUrl;
            try
            {
                var photo = ImageService.SaveProductImage(CompanyCode, photoFile);
                if (photo != null) model.PhotoUrl = photo.LargeUrl;
            }
            catch (InvalidOperationException ex)
            {
                Fail(ex.Message);
            }

            model.Name = Clip(model.Name, 80);
            model.Title = Clip(model.Title, 120);
            model.Bio = Clip(model.Bio, 1000);
            model.Hours = hours;
            model.IsSample = false;
            model = TenantStore.SaveStaff(CompanyCode, model);
            return Ok("«" + model.Name + "» ذخیره شد." + (hours.Any() ? "" : " هیچ ساعت کاری ندارد، پس نوبت آنلاین برایش باز نمی‌شود."), "Staff");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult DeleteStaff(int id)
        {
            var orphaned = TenantStore.DeleteStaff(CompanyCode, id);
            return OkBack(orphaned.Any()
                ? "حذف شد. این خدمت‌ها کسی را نداشتند و غیرفعال شدند: " + string.Join("، ", orphaned.Select(s => s.Title))
                : "حذف شد. نوبت‌های قبلی او در فهرست می‌مانند.", "Staff");
        }

        // ---- Closed days and leave

        [HttpGet]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult TimeOff()
        {
            ViewBag.Staff = TenantStore.GetStaff(CompanyCode);
            ViewBag.Days = BookingDayOptions(0, 180);
            return View(TenantStore.GetTimeOffs(CompanyCode, DateTime.Today, DateTime.Today.AddYears(1)));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult AddTimeOff(int? staffId, string date, string startTime, string endTime, string reason, bool wholeDay = true)
        {
            var day = ParseBookingDate(date);
            if (!day.HasValue) return Fail("روز را انتخاب کنید.", "TimeOff");
            if (staffId.HasValue && TenantStore.GetStaffMember(CompanyCode, staffId.Value) == null) staffId = null;
            var timeOff = new StoreTimeOff { StaffId = staffId, Date = day.Value, Reason = Clip(reason, 200) };
            if (!wholeDay)
            {
                var start = BookingTime.ParseClock(startTime);
                var end = BookingTime.ParseClock(endTime);
                if (!start.HasValue || !end.HasValue || end.Value <= start.Value)
                    return Fail("ساعت شروع و پایان را درست وارد کنید.", "TimeOff");
                timeOff.StartMinute = start;
                timeOff.EndMinute = end;
            }
            TenantStore.AddTimeOff(CompanyCode, timeOff);

            var from = day.Value.AddMinutes(timeOff.StartMinute ?? 0);
            var to = day.Value.AddMinutes(timeOff.EndMinute ?? 24 * 60);
            var now = DateTime.Now;
            var clashes = TenantStore.AppointmentsBetween(CompanyCode, day.Value, day.Value.AddDays(1), staffId)
                .Count(a => a.Blocks(now) && a.StartAt < to && from < a.EndAt);
            return Ok(clashes > 0
                ? "ثبت شد. " + clashes.ToString().ToFaDigits() + " نوبت در این بازه از قبل گرفته شده است؛ آن‌ها را از تقویم ببینید و با مراجعان هماهنگ کنید."
                : "ثبت شد؛ در این بازه نوبت آنلاین گرفته نمی‌شود.", "TimeOff");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking, SiteKinds.Campus)]
        public ActionResult DeleteTimeOff(int id)
        {
            TenantStore.DeleteTimeOff(CompanyCode, id);
            return OkBack("حذف شد.", "TimeOff");
        }

        // ---- Helpers

        /// <summary>The sample appointments of a demo follow the calendar, so visitors always see a busy week.</summary>
        private void KeepDemoAppointmentsFresh()
        {
            if (TenantStore.IsDemo(CompanyCode)) TenantStore.RefreshSampleAppointments(CompanyCode);
        }

        /// <summary>Days from today + <paramref name="from"/> to today + <paramref name="to"/>, with Persian labels, for date selects.</summary>
        private static List<KeyValuePair<string, string>> BookingDayOptions(int from, int to)
        {
            var today = DateTime.Today;
            return Enumerable.Range(from, to - from + 1).Select(offset =>
            {
                var d = today.AddDays(offset);
                var prefix = offset == 0 ? "امروز، " : offset == 1 ? "فردا، " : "";
                return new KeyValuePair<string, string>(d.ToString(BookingDateFormat, CultureInfo.InvariantCulture),
                    prefix + BookingTime.DayNames[BookingTime.DayOf(d)] + " " + d.ToShamsiText());
            }).ToList();
        }

        private static DateTime? ParseBookingDate(string text)
        {
            DateTime date;
            return DateTime.TryParseExact(text, BookingDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date.Date : (DateTime?)null;
        }
    }
}
