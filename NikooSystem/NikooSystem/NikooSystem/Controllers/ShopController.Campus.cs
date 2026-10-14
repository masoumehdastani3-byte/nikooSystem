using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Academy and sports-club sites: group classes, private lessons and hourly courts.</summary>
    public partial class ShopController
    {
        private ActionResult CampusHome()
        {
            ViewBag.Classes = ActiveClasses();
            ViewBag.Facilities = ActiveFacilities();
            ViewBag.Services = ActiveServices();
            ViewBag.Staff = ActiveStaff();
            return View("CampusHome");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult Classes()
        {
            ViewBag.Title = "کلاس‌ها و دوره‌ها";
            return View(ActiveClasses());
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult Class(int? id)
        {
            var item = id.HasValue ? ActiveClass(id.Value) : null;
            if (item == null) return ToAction("Classes");
            ViewBag.Title = item.Title;
            ViewBag.Taken = TenantStore.SeatsTaken(CompanyCode, item.Id);
            ViewBag.Instructor = item.InstructorId.HasValue ? TenantStore.GetStaffMember(CompanyCode, item.InstructorId.Value) : null;
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public async Task<ActionResult> Enroll(int? classId, string customerName, string customerMobile, string customerEmail, string notes, string website)
        {
            var back = new { id = classId };
            if (!string.IsNullOrEmpty(website)) return ToAction("Classes");
            var item = classId.HasValue ? ActiveClass(classId.Value) : null;
            if (item == null) return Fail("کلاس مورد نظر پیدا نشد.", "Classes");
            if (string.IsNullOrWhiteSpace(customerName)) return Fail("نام و نام خانوادگی را وارد کنید.", "Class", back);
            var mobile = BookingTime.NormalizeMobile(customerMobile);
            if (mobile == null) return Fail("شماره موبایل معتبر نیست؛ مثلاً ۰۹۱۲۱۲۳۴۵۶۷.", "Class", back);

            var due = Payable(item.Price);
            var enrollment = new StoreEnrollment
            {
                ClassId = item.Id,
                CustomerName = Clip(customerName, 80),
                CustomerMobile = mobile,
                CustomerEmail = Clip(customerEmail, 120),
                Notes = Clip(notes, 1000),
                Status = due > 0 ? AppointmentStatus.PendingPayment : AppointmentStatus.Confirmed,
                Source = "online"
            };
            var error = TenantStore.Enroll(CompanyCode, enrollment);
            if (error != null) return Fail(error, "Class", back);
            AllowAppointment(enrollment.Code);
            if (due == 0)
            {
                Notifications.StoreEnrollment(CompanyCode, enrollment);
                return Ok("ثبت‌نام شما انجام شد. کد پیگیری: " + enrollment.Code, "Enrollment", new { id = enrollment.Code });
            }

            var callback = Request.Url.GetLeftPart(UriPartial.Authority) + "/Shop/" + CompanyCode + "/ClassCallback?id=" + enrollment.Id;
            var pay = await Gateway().RequestPaymentAsync(due, "شهریه " + enrollment.Code + " - " + CurrentSettings.StoreName,
                enrollment.CustomerEmail, enrollment.CustomerMobile, enrollment.Id.ToString(), callback);
            if (!pay.Success)
            {
                CancelHold(enrollment, pay.Message);
                return Fail("اتصال به درگاه پرداخت انجام نشد: " + pay.Message, "Class", back);
            }
            enrollment.Authority = pay.Authority;
            TenantStore.UpdateEnrollment(CompanyCode, enrollment);
            return Redirect(pay.PaymentUrl);
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Campus)]
        public async Task<ActionResult> ClassCallback(int? id, string Authority, string Status)
        {
            var enrollment = id.HasValue ? TenantStore.GetEnrollment(CompanyCode, id.Value) : null;
            if (enrollment == null) return Fail("ثبت‌نام پیدا نشد.", "CampusTrack");
            if (enrollment.PaidAt.HasValue) return ToAction("Enrollment", new { id = enrollment.Code });
            var verify = await Gateway().VerifyCallbackAsync(enrollment.Authority, Authority, Status, enrollment.Price);
            if (verify.Forged) return Fail("اطلاعات بازگشت از درگاه معتبر نبود.", "CampusTrack");
            if (!verify.Success)
            {
                CancelHold(enrollment, verify.Message);
                return Fail("پرداخت انجام نشد و ثبت‌نام قطعی نشد: " + verify.Message, "Class", new { id = enrollment.ClassId });
            }
            TenantStore.MarkEnrollmentPaid(CompanyCode, enrollment.Id, verify.RefId, verify.Message);
            var saved = TenantStore.GetEnrollment(CompanyCode, enrollment.Id) ?? enrollment;
            Notifications.StoreEnrollment(CompanyCode, saved);
            AllowAppointment(saved.Code);
            return Ok("شهریه پرداخت شد و ثبت‌نام شما قطعی است.", "Enrollment", new { id = saved.Code });
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult Enrollment(string id)
        {
            var enrollment = TenantStore.GetEnrollmentByCode(CompanyCode, id);
            if (enrollment == null || !MayView(enrollment.Code)) return ToAction("CampusTrack", new { code = id });
            ViewBag.Class = TenantStore.GetClass(CompanyCode, enrollment.ClassId);
            ViewBag.CanCancel = MayCancelEnrollment(enrollment);
            return View(enrollment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult CancelEnrollment(string id)
        {
            var enrollment = TenantStore.GetEnrollmentByCode(CompanyCode, id);
            if (enrollment == null || !MayView(enrollment.Code)) return ToAction("CampusTrack");
            if (!MayCancelEnrollment(enrollment)) return Fail("لغو این ثبت‌نام دیگر ممکن نیست.", "Enrollment", new { id });
            enrollment.Status = AppointmentStatus.Cancelled;
            enrollment.CancelledAt = DateTime.Now;
            TenantStore.UpdateEnrollment(CompanyCode, enrollment);
            Notifications.StoreEnrollmentCancelled(CompanyCode, enrollment);
            return Ok("ثبت‌نام لغو شد.", "Enrollment", new { id });
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult Facilities()
        {
            ViewBag.Title = "رزرو سالن و زمین";
            return View(ActiveFacilities());
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult Facility(int? id, string date)
        {
            var facility = id.HasValue ? ActiveFacility(id.Value) : null;
            if (facility == null) return ToAction("Facilities");
            var day = ParseDate(date) ?? NextOpenDay(facility);
            ViewBag.Title = facility.Title;
            ViewBag.Day = day;
            ViewBag.Days = OpenDays(facility);
            ViewBag.Slots = day.HasValue ? TenantStore.FacilitySlots(CompanyCode, facility, day.Value, CurrentSettings) : new List<DateTime>();
            return View(facility);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public async Task<ActionResult> BookFacility(int? facilityId, string date, string time, string customerName, string customerMobile, string customerEmail, string notes, string website)
        {
            var back = new { id = facilityId, date };
            if (!string.IsNullOrEmpty(website)) return ToAction("Facilities");
            var facility = facilityId.HasValue ? ActiveFacility(facilityId.Value) : null;
            if (facility == null) return Fail("سالن مورد نظر پیدا نشد.", "Facilities");
            var day = ParseDate(date);
            var minute = BookingTime.ParseClock(time);
            if (!day.HasValue || !minute.HasValue) return Fail("روز و ساعت را انتخاب کنید.", "Facility", back);
            if (string.IsNullOrWhiteSpace(customerName)) return Fail("نام و نام خانوادگی را وارد کنید.", "Facility", back);
            var mobile = BookingTime.NormalizeMobile(customerMobile);
            if (mobile == null) return Fail("شماره موبایل معتبر نیست؛ مثلاً ۰۹۱۲۱۲۳۴۵۶۷.", "Facility", back);

            var start = day.Value.AddMinutes(minute.Value);
            if (!TenantStore.FacilitySlots(CompanyCode, facility, day.Value, CurrentSettings).Contains(start))
                return Fail("این ساعت دیگر خالی نیست؛ لطفاً ساعت دیگری انتخاب کنید.", "Facility", back);

            var due = Payable(facility.Price);
            var booking = new StoreFacilityBooking
            {
                FacilityId = facility.Id,
                StartAt = start,
                EndAt = start.AddMinutes(Math.Max(15, facility.SlotMinutes)),
                Price = facility.Price,
                CustomerName = Clip(customerName, 80),
                CustomerMobile = mobile,
                CustomerEmail = Clip(customerEmail, 120),
                Notes = Clip(notes, 1000),
                Status = due > 0 ? AppointmentStatus.PendingPayment : AppointmentStatus.Confirmed,
                Source = "online"
            };
            var error = TenantStore.BookFacility(CompanyCode, booking, CurrentSettings);
            if (error != null) return Fail(error, "Facility", back);
            AllowAppointment(booking.Code);
            if (due == 0)
            {
                Notifications.StoreFacilityBooking(CompanyCode, booking);
                return Ok("رزرو شما ثبت شد. کد پیگیری: " + booking.Code, "FacilityReservation", new { id = booking.Code });
            }

            var callback = Request.Url.GetLeftPart(UriPartial.Authority) + "/Shop/" + CompanyCode + "/FacilityCallback?id=" + booking.Id;
            var pay = await Gateway().RequestPaymentAsync(due, "رزرو " + booking.Code + " - " + CurrentSettings.StoreName,
                booking.CustomerEmail, booking.CustomerMobile, booking.Id.ToString(), callback);
            if (!pay.Success)
            {
                CancelHold(booking, pay.Message);
                return Fail("اتصال به درگاه پرداخت انجام نشد: " + pay.Message, "Facility", back);
            }
            booking.Authority = pay.Authority;
            TenantStore.UpdateFacilityBooking(CompanyCode, booking);
            return Redirect(pay.PaymentUrl);
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Campus)]
        public async Task<ActionResult> FacilityCallback(int? id, string Authority, string Status)
        {
            var booking = id.HasValue ? TenantStore.GetFacilityBooking(CompanyCode, id.Value) : null;
            if (booking == null) return Fail("رزرو پیدا نشد.", "CampusTrack");
            if (booking.PaidAt.HasValue) return ToAction("FacilityReservation", new { id = booking.Code });
            var verify = await Gateway().VerifyCallbackAsync(booking.Authority, Authority, Status, booking.Price);
            if (verify.Forged) return Fail("اطلاعات بازگشت از درگاه معتبر نبود.", "CampusTrack");
            if (!verify.Success)
            {
                CancelHold(booking, verify.Message);
                return Fail("پرداخت انجام نشد و رزرو ثبت نشد: " + verify.Message, "Facility", new { id = booking.FacilityId });
            }
            TenantStore.MarkFacilityBookingPaid(CompanyCode, booking.Id, verify.RefId, verify.Message);
            var saved = TenantStore.GetFacilityBooking(CompanyCode, booking.Id) ?? booking;
            Notifications.StoreFacilityBooking(CompanyCode, saved);
            AllowAppointment(saved.Code);
            return Ok("پرداخت شد و رزرو شما قطعی است.", "FacilityReservation", new { id = saved.Code });
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult FacilityReservation(string id)
        {
            var booking = TenantStore.GetFacilityBookingByCode(CompanyCode, id);
            if (booking == null || !MayView(booking.Code)) return ToAction("CampusTrack", new { code = id });
            ViewBag.CanCancel = CustomerMayCancelFacility(booking);
            return View(booking);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult CancelFacility(string id)
        {
            var booking = TenantStore.GetFacilityBookingByCode(CompanyCode, id);
            if (booking == null || !MayView(booking.Code)) return ToAction("CampusTrack");
            if (!CustomerMayCancelFacility(booking)) return Fail("لغو این رزرو دیگر ممکن نیست.", "FacilityReservation", new { id });
            booking.Status = AppointmentStatus.Cancelled;
            booking.CancelledAt = DateTime.Now;
            TenantStore.UpdateFacilityBooking(CompanyCode, booking);
            Notifications.StoreFacilityBookingCancelled(CompanyCode, booking);
            return Ok("رزرو لغو شد.", "FacilityReservation", new { id });
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult CampusTrack(string code)
        {
            ViewBag.Code = code;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult CampusTrack(string code, string mobile)
        {
            var normalized = BookingTime.NormalizeMobile(mobile);
            if (normalized == null) return Fail("شماره موبایل معتبر نیست.", "CampusTrack", new { code });
            var enrollment = TenantStore.GetEnrollmentByCode(CompanyCode, code);
            if (enrollment != null && enrollment.CustomerMobile == normalized)
            {
                AllowAppointment(enrollment.Code);
                return ToAction("Enrollment", new { id = enrollment.Code });
            }
            var booking = TenantStore.GetFacilityBookingByCode(CompanyCode, code);
            if (booking != null && booking.CustomerMobile == normalized)
            {
                AllowAppointment(booking.Code);
                return ToAction("FacilityReservation", new { id = booking.Code });
            }
            var appointment = TenantStore.GetAppointmentByCode(CompanyCode, code);
            if (appointment != null && appointment.CustomerMobile == normalized)
            {
                AllowAppointment(appointment.Code);
                return ToAction("Appointment", new { id = appointment.Code });
            }
            return Fail("موردی با این کد پیگیری و شماره موبایل پیدا نشد.", "CampusTrack", new { code });
        }

        private List<StoreClass> ActiveClasses() =>
            TenantStore.GetClasses(CompanyCode).Where(c => c.IsActive).ToList();

        private StoreClass ActiveClass(int id)
        {
            var item = TenantStore.GetClass(CompanyCode, id);
            return item != null && item.IsActive ? item : null;
        }

        private List<StoreFacility> ActiveFacilities() =>
            TenantStore.GetFacilities(CompanyCode).Where(f => f.IsActive).ToList();

        private StoreFacility ActiveFacility(int id)
        {
            var item = TenantStore.GetFacility(CompanyCode, id);
            return item != null && item.IsActive ? item : null;
        }

        /// <summary>The full price when the store can take online payment; otherwise the booking is confirmed without payment.</summary>
        private long Payable(long price)
        {
            var s = CurrentSettings;
            if (!s.EnablePayment || price <= 0 || !TenantProvisioningService.HasValidMerchant(s)) return 0;
            return price;
        }

        private bool MayCancelEnrollment(StoreEnrollment enrollment)
        {
            if (enrollment.Status != AppointmentStatus.Confirmed && enrollment.Status != AppointmentStatus.PendingPayment) return false;
            var item = TenantStore.GetClass(CompanyCode, enrollment.ClassId);
            return item == null || !item.EndDate.HasValue || item.EndDate.Value.Date >= DateTime.Today;
        }

        private bool CustomerMayCancelFacility(StoreFacilityBooking booking) =>
            CurrentSettings.BookingCancelHours > 0
            && (booking.Status == AppointmentStatus.Confirmed || booking.Status == AppointmentStatus.PendingPayment)
            && booking.StartAt > DateTime.Now.AddHours(CurrentSettings.BookingCancelHours);

        private void CancelHold(StoreEnrollment enrollment, string message)
        {
            enrollment.Status = AppointmentStatus.Cancelled;
            enrollment.CancelledAt = DateTime.Now;
            enrollment.PaymentMessage = message;
            TenantStore.UpdateEnrollment(CompanyCode, enrollment);
        }

        private void CancelHold(StoreFacilityBooking booking, string message)
        {
            booking.Status = AppointmentStatus.Cancelled;
            booking.CancelledAt = DateTime.Now;
            booking.PaymentMessage = message;
            TenantStore.UpdateFacilityBooking(CompanyCode, booking);
        }

        private DateTime? NextOpenDay(StoreFacility facility)
        {
            return OpenDays(facility).Select(d => ParseDate(d.Key)).FirstOrDefault(d => d.HasValue);
        }

        private List<KeyValuePair<string, string>> OpenDays(StoreFacility facility)
        {
            var days = new List<KeyValuePair<string, string>>();
            var ahead = Math.Max(1, Math.Min(90, CurrentSettings.BookingDaysAhead));
            for (var i = 0; i < ahead; i++)
            {
                var day = DateTime.Today.AddDays(i);
                if (!facility.HoursOn(BookingTime.DayOf(day)).Any()) continue;
                days.Add(new KeyValuePair<string, string>(
                    day.ToString(DateFormat, CultureInfo.InvariantCulture),
                    BookingTime.DayNames[BookingTime.DayOf(day)] + " " + day.ToShamsiText()));
            }
            return days;
        }
    }
}
