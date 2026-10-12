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
    /// <summary>Booking sites: services, the booking wizard, the online deposit and the customer's appointment page.</summary>
    public partial class ShopController
    {
        private const string DateFormat = "yyyy-MM-dd";

        private ActionResult BookingHome()
        {
            ViewBag.Services = ActiveServices();
            ViewBag.Staff = ActiveStaff();
            return View("BookingHome");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Booking)]
        public ActionResult Services()
        {
            ViewBag.Staff = ActiveStaff();
            var services = ActiveServices();
            SeoSimple("خدمات و تعرفه‌های " + CurrentSettings.StoreName
                + (services.Any() ? ": " + string.Join("، ", services.Take(6).Select(s => s.Title)) : "")
                + " — رزرو آنلاین نوبت", "Services");
            return View(services);
        }

        [HttpGet]
        [ForKinds(SiteKinds.Booking)]
        public ActionResult Book(int? serviceId, int? staffId)
        {
            var services = ActiveServices();
            ViewBag.Services = services;
            ViewBag.Deposits = services.ToDictionary(s => s.Id, s => DepositFor(s.Price));
            ViewBag.Staff = ActiveStaff();
            ViewBag.ServiceId = serviceId;
            ViewBag.StaffId = staffId;
            SeoSimple("رزرو آنلاین نوبت " + CurrentSettings.StoreName + "؛ خدمت و روز و ساعت دلخواه را انتخاب کنید و کد پیگیری بگیرید.", "Book");
            return View();
        }

        /// <summary>The days with at least one free time, for the day chips of the wizard.</summary>
        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Booking)]
        public ActionResult BookingDays(int serviceId, int? staffId)
        {
            var service = ActiveService(serviceId);
            if (service == null) return Json(new { days = new object[0] }, JsonRequestBehavior.AllowGet);
            var today = DateTime.Today;
            var days = TenantStore.BookableDays(CompanyCode, service, staffId, CurrentSettings).Select(d => new
            {
                date = d.ToString(DateFormat, CultureInfo.InvariantCulture),
                day = d == today ? "امروز" : d == today.AddDays(1) ? "فردا" : BookingTime.DayNames[BookingTime.DayOf(d)],
                label = d.ToShamsiText()
            });
            return Json(new { days }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>The free start times of a day; with "any staff" each time lists the staff free then.</summary>
        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Booking)]
        public ActionResult BookingSlots(int serviceId, int? staffId, string date)
        {
            var service = ActiveService(serviceId);
            var day = ParseDate(date);
            if (service == null || !day.HasValue) return Json(new { slots = new object[0] }, JsonRequestBehavior.AllowGet);
            var slots = TenantStore.FreeSlots(CompanyCode, service, staffId, day.Value, CurrentSettings).Select(s => new
            {
                time = s.StartAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                label = BookingTime.Clock(s.StartAt),
                staff = s.StaffIds
            });
            return Json(new { slots }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking)]
        public async Task<ActionResult> Book(int? serviceId, int? staffId, string date, string time, string customerName,
            string customerMobile, string customerEmail, string notes, string website)
        {
            var back = new { serviceId, staffId };
            if (!string.IsNullOrEmpty(website)) return ToAction("Book");
            var service = serviceId.HasValue ? ActiveService(serviceId.Value) : null;
            if (service == null) return Fail("خدمت مورد نظر را انتخاب کنید.", "Book", back);
            var day = ParseDate(date);
            var minute = BookingTime.ParseClock(time);
            if (!day.HasValue || !minute.HasValue) return Fail("روز و ساعت نوبت را انتخاب کنید.", "Book", back);
            if (string.IsNullOrWhiteSpace(customerName)) return Fail("نام و نام خانوادگی را وارد کنید.", "Book", back);
            var mobile = BookingTime.NormalizeMobile(customerMobile);
            if (mobile == null) return Fail("شماره موبایل معتبر نیست؛ مثلاً ۰۹۱۲۱۲۳۴۵۶۷.", "Book", back);

            var start = day.Value.AddMinutes(minute.Value);
            var slot = TenantStore.FreeSlots(CompanyCode, service, staffId, day.Value, CurrentSettings).FirstOrDefault(s => s.StartAt == start);
            if (slot == null) return Fail("این ساعت دیگر خالی نیست؛ لطفاً ساعت دیگری انتخاب کنید.", "Book", back);

            var settings = CurrentSettings;
            var deposit = DepositFor(service.Price);
            var appointment = new StoreAppointment
            {
                ServiceId = service.Id,
                ServiceTitle = service.Title,
                StaffId = staffId ?? slot.StaffIds.First(),
                StartAt = start,
                EndAt = start.AddMinutes(service.DurationMinutes),
                Price = service.Price,
                Deposit = deposit,
                CustomerName = Clip(customerName, 80),
                CustomerMobile = mobile,
                CustomerEmail = Clip(customerEmail, 120),
                Notes = Clip(notes, 1000),
                Status = deposit > 0 ? AppointmentStatus.PendingPayment : AppointmentStatus.Confirmed,
                Source = "online"
            };
            var error = TenantStore.BookAppointment(CompanyCode, appointment, settings);
            if (error != null) return Fail(error, "Book", back);
            AllowAppointment(appointment.Code);

            if (deposit == 0)
            {
                Notifications.StoreAppointment(CompanyCode, appointment);
                return Ok("نوبت شما ثبت شد. کد پیگیری: " + appointment.Code, "Appointment", new { id = appointment.Code });
            }

            var callback = Request.Url.GetLeftPart(UriPartial.Authority) + "/Shop/" + CompanyCode + "/BookingCallback?id=" + appointment.Id;
            var pay = await Gateway().RequestPaymentAsync(
                deposit,
                "بیعانه نوبت " + appointment.Code + " - " + settings.StoreName,
                appointment.CustomerEmail,
                appointment.CustomerMobile,
                appointment.Id.ToString(),
                callback);
            if (!pay.Success)
            {
                CancelUnpaid(appointment, pay.Message);
                return Fail("اتصال به درگاه پرداخت انجام نشد: " + pay.Message, "Book", back);
            }
            appointment.Authority = pay.Authority;
            TenantStore.UpdateAppointment(CompanyCode, appointment);
            return Redirect(pay.PaymentUrl);
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Booking)]
        public async Task<ActionResult> BookingCallback(int? id, string Authority, string Status)
        {
            var appointment = id.HasValue ? TenantStore.GetAppointment(CompanyCode, id.Value) : null;
            if (appointment == null) return Fail("نوبت پیدا نشد.", "Track");
            if (appointment.PaidAt.HasValue)
                return ToAction("Appointment", new { id = appointment.Code });

            var verify = await Gateway().VerifyCallbackAsync(appointment.Authority, Authority, Status, appointment.Deposit);
            if (verify.Forged) return Fail("اطلاعات بازگشت از درگاه معتبر نبود.", "Track");
            if (!verify.Success)
            {
                CancelUnpaid(appointment, verify.Message);
                return Fail("پرداخت انجام نشد و نوبت ثبت نشد: " + verify.Message, "Book", new { serviceId = appointment.ServiceId });
            }
            TenantStore.MarkAppointmentPaid(CompanyCode, appointment.Id, verify.RefId, verify.Message);
            Notifications.StoreAppointment(CompanyCode, TenantStore.GetAppointment(CompanyCode, appointment.Id) ?? appointment);
            AllowAppointment(appointment.Code);
            return Ok("بیعانه پرداخت شد و نوبت شما قطعی است.", "Appointment", new { id = appointment.Code });
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Booking)]
        public ActionResult Track(string code)
        {
            ViewBag.Code = code;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking)]
        public ActionResult Track(string code, string mobile)
        {
            var appointment = TenantStore.GetAppointmentByCode(CompanyCode, code);
            var normalized = BookingTime.NormalizeMobile(mobile);
            if (appointment == null || normalized == null || appointment.CustomerMobile != normalized)
                return Fail("نوبتی با این کد پیگیری و شماره موبایل پیدا نشد.", "Track", new { code });
            AllowAppointment(appointment.Code);
            return ToAction("Appointment", new { id = appointment.Code });
        }

        /// <summary>The customer's appointment; open to whoever booked it or proved the mobile number on the tracking page.</summary>
        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Booking)]
        public ActionResult Appointment(string id)
        {
            var appointment = TenantStore.GetAppointmentByCode(CompanyCode, id);
            if (appointment == null || !MayView(appointment.Code)) return ToAction("Track", new { code = id });
            ViewBag.Service = TenantStore.GetService(CompanyCode, appointment.ServiceId);
            ViewBag.Staff = TenantStore.GetStaffMember(CompanyCode, appointment.StaffId);
            ViewBag.CanCancel = CustomerMayCancel(appointment);
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Booking)]
        public ActionResult CancelAppointment(string id)
        {
            var appointment = TenantStore.GetAppointmentByCode(CompanyCode, id);
            if (appointment == null || !MayView(appointment.Code)) return ToAction("Track", new { code = id });
            if (!CustomerMayCancel(appointment))
                return Fail("مهلت لغو آنلاین این نوبت گذشته است؛ لطفاً تماس بگیرید.", "Appointment", new { id = appointment.Code });
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelledAt = DateTime.Now;
            TenantStore.UpdateAppointment(CompanyCode, appointment);
            Notifications.StoreAppointmentCancelled(CompanyCode, appointment);
            return Ok(appointment.PaidAmount > 0
                ? "نوبت لغو شد. برای بازگشت بیعانه با ما تماس بگیرید."
                : "نوبت لغو شد.", "Appointment", new { id = appointment.Code });
        }

        private List<StoreService> ActiveServices()
        {
            var staff = ActiveStaff();
            return TenantStore.GetServices(CompanyCode).Where(s => s.IsActive && staff.Any(p => s.OfferedBy(p.Id))).ToList();
        }

        private List<StoreStaff> ActiveStaff() => TenantStore.GetStaff(CompanyCode).Where(s => s.IsActive).ToList();

        private StoreService ActiveService(int id)
        {
            var service = TenantStore.GetService(CompanyCode, id);
            return service != null && service.IsActive ? service : null;
        }

        /// <summary>The online deposit of a booking: a percent of the price, rounded up to 1,000 toman; 0 when payment is off.</summary>
        private long DepositFor(long price)
        {
            var s = CurrentSettings;
            if (!s.EnablePayment || s.BookingDepositPercent <= 0 || price <= 0 || !TenantProvisioningService.HasValidMerchant(s)) return 0;
            var deposit = (long)Math.Ceiling(price * Math.Min(100, s.BookingDepositPercent) / 100.0 / 1000) * 1000;
            return Math.Min(price, deposit);
        }

        private bool CustomerMayCancel(StoreAppointment a) =>
            CurrentSettings.BookingCancelHours > 0
            && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.PendingPayment)
            && a.StartAt > DateTime.Now.AddHours(CurrentSettings.BookingCancelHours);

        private void CancelUnpaid(StoreAppointment appointment, string message)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelledAt = DateTime.Now;
            appointment.PaymentMessage = message;
            TenantStore.UpdateAppointment(CompanyCode, appointment);
        }

        private string AppointmentsKey => "bk_codes_" + CompanyCode;

        private void AllowAppointment(string code)
        {
            var codes = Session[AppointmentsKey] as HashSet<string> ?? new HashSet<string>();
            codes.Add(code);
            Session[AppointmentsKey] = codes;
        }

        private bool MayView(string code) => (Session[AppointmentsKey] as HashSet<string>)?.Contains(code) == true;

        private static DateTime? ParseDate(string text)
        {
            DateTime date;
            return DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date.Date : (DateTime?)null;
        }
    }
}
