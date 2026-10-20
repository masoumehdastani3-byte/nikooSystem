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
    /// <summary>The owner's pages for group classes, enrollments and hourly facilities.</summary>
    public partial class ShopAdminController
    {
        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult Classes(GridQuery query)
        {
            return View(query.Slice(TenantStore.GetClasses(CompanyCode),
                c => c.Title + " " + c.InstructorName + " " + c.Place,
                new Dictionary<string, Func<StoreClass, object>>
                {
                    ["title"] = c => c.Title,
                    ["when"] = c => c.Weekday * 10000 + c.StartMinute,
                    ["mode"] = c => c.Mode,
                    ["capacity"] = c => c.Capacity,
                    ["price"] = c => c.Price,
                    ["status"] = c => c.IsActive,
                    ["sort"] = c => c.SortOrder
                }, "sort"));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult EditClass(int? id)
        {
            var item = id.HasValue ? TenantStore.GetClass(CompanyCode, id.Value) : new StoreClass
            {
                Capacity = 12,
                StartMinute = 16 * 60,
                EndMinute = 17 * 60 + 30,
                Sessions = 12,
                SortOrder = TenantStore.CountClasses(CompanyCode) + 1
            };
            if (item == null) return ToAction("Classes");
            ViewBag.Staff = TenantStore.GetStaff(CompanyCode);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult EditClass(StoreClass model, string startTime, string endTime, string startDate, string endDate,
            HttpPostedFileBase imageFile, bool removeImage = false)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Fail("عنوان کلاس الزامی است.", "EditClass", model.Id > 0 ? new { id = model.Id } : null);
            var existing = model.Id > 0 ? TenantStore.GetClass(CompanyCode, model.Id) : null;
            if (model.Id > 0 && existing == null) return ToAction("Classes");
            var start = BookingTime.ParseClock(startTime);
            var end = BookingTime.ParseClock(endTime);
            if (!start.HasValue || !end.HasValue || end.Value <= start.Value)
                return Fail("ساعت شروع و پایان کلاس را درست وارد کنید.", "EditClass", model.Id > 0 ? new { id = model.Id } : null);

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

            var staff = model.InstructorId.HasValue ? TenantStore.GetStaffMember(CompanyCode, model.InstructorId.Value) : null;
            model.InstructorId = staff?.Id;
            model.InstructorName = staff?.Name;
            model.Title = Clip(model.Title, 150);
            model.Summary = Clip(model.Summary, 300);
            model.Description = Clip(model.Description, 2000);
            model.Level = Clip(model.Level, 40);
            model.AgeGroup = Clip(model.AgeGroup, 40);
            model.Place = Clip(model.Place, 120);
            model.OnlineLink = Clip(model.OnlineLink, 300);
            model.Mode = CampusDelivery.Normalize(model.Mode);
            model.Capacity = Math.Max(1, Math.Min(500, model.Capacity));
            model.Price = Math.Max(0, model.Price);
            model.Weekday = Math.Max(0, Math.Min(6, model.Weekday));
            model.StartMinute = start.Value;
            model.EndMinute = end.Value;
            model.StartDate = ParseBookingDate(startDate);
            model.EndDate = ParseBookingDate(endDate);
            model.Sessions = Math.Max(0, Math.Min(200, model.Sessions));
            model.IsSample = false;
            model = TenantStore.SaveClass(CompanyCode, model);
            return Ok("کلاس «" + model.Title + "» ذخیره شد.", "Classes");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult DeleteClass(int id)
        {
            TenantStore.DeleteClass(CompanyCode, id);
            return OkBack("کلاس حذف شد. ثبت‌نام‌های قبلی در فهرست می‌مانند.", "Classes");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult Enrollments(GridQuery query, string status = null)
        {
            ViewBag.Classes = TenantStore.GetClasses(CompanyCode);
            var rows = TenantStore.GetEnrollments(CompanyCode).AsEnumerable();
            AppointmentStatus parsed;
            if (Enum.TryParse(status, true, out parsed)) rows = rows.Where(e => e.Status == parsed);
            return View(query.Slice(rows,
                e => e.Code + " " + e.ClassTitle + " " + e.CustomerName + " " + e.CustomerMobile,
                new Dictionary<string, Func<StoreEnrollment, object>>
                {
                    ["code"] = e => e.Code,
                    ["class"] = e => e.ClassTitle,
                    ["name"] = e => e.CustomerName,
                    ["price"] = e => e.PaidAmount > 0 ? e.PaidAmount : e.Price,
                    ["status"] = e => e.Status,
                    ["date"] = e => e.CreatedAt
                }, "date", true));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult ManualEnroll(int classId, string customerName, string customerMobile)
        {
            var item = TenantStore.GetClass(CompanyCode, classId);
            if (item == null) return Fail("کلاس را انتخاب کنید.", "Enrollments");
            if (string.IsNullOrWhiteSpace(customerName)) return Fail("نام دانشجو را وارد کنید.", "Enrollments");
            var mobile = BookingTime.NormalizeMobile(customerMobile);
            if (mobile == null) return Fail("شماره موبایل معتبر نیست.", "Enrollments");
            var enrollment = new StoreEnrollment
            {
                ClassId = item.Id,
                CustomerName = Clip(customerName, 80),
                CustomerMobile = mobile,
                Status = AppointmentStatus.Confirmed,
                Source = "admin"
            };
            var error = TenantStore.Enroll(CompanyCode, enrollment);
            if (error != null) return Fail(error, "Enrollments");
            NikooSystem.Services.Notifications.StoreEnrollment(CompanyCode, enrollment);
            return Ok("ثبت‌نام «" + enrollment.CustomerName + "» با کد " + enrollment.Code + " ثبت شد.", "Enrollments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult SetEnrollmentStatus(int id, AppointmentStatus status)
        {
            var enrollment = TenantStore.GetEnrollment(CompanyCode, id);
            if (enrollment == null) return ToAction("Enrollments");
            if (status != AppointmentStatus.Confirmed && status != AppointmentStatus.Cancelled && status != AppointmentStatus.Completed)
                return ToAction("Enrollments");
            enrollment.Status = status;
            enrollment.CancelledAt = status == AppointmentStatus.Cancelled ? DateTime.Now : (DateTime?)null;
            TenantStore.UpdateEnrollment(CompanyCode, enrollment);
            return OkBack("وضعیت ثبت‌نام به‌روز شد.", "Enrollments");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult Facilities(GridQuery query)
        {
            return View(query.Slice(TenantStore.GetFacilities(CompanyCode),
                f => f.Title + " " + f.Place,
                new Dictionary<string, Func<StoreFacility, object>>
                {
                    ["title"] = f => f.Title,
                    ["slot"] = f => f.SlotMinutes,
                    ["capacity"] = f => f.Capacity,
                    ["price"] = f => f.Price,
                    ["status"] = f => f.IsActive,
                    ["sort"] = f => f.SortOrder
                }, "sort"));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult EditFacility(int? id)
        {
            var item = id.HasValue ? TenantStore.GetFacility(CompanyCode, id.Value) : new StoreFacility
            {
                SlotMinutes = 60,
                Capacity = 1,
                SortOrder = TenantStore.CountFacilities(CompanyCode) + 1
            };
            if (item == null) return ToAction("Facilities");
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult EditFacility(StoreFacility model, HttpPostedFileBase imageFile, bool removeImage = false)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return Fail("نام سالن الزامی است.", "EditFacility", model.Id > 0 ? new { id = model.Id } : null);
            var existing = model.Id > 0 ? TenantStore.GetFacility(CompanyCode, model.Id) : null;
            if (model.Id > 0 && existing == null) return ToAction("Facilities");
            string hoursError;
            var hours = PostedHours(out hoursError);
            if (hoursError != null)
                return Fail(hoursError + " تغییرات ذخیره نشد.", "EditFacility", model.Id > 0 ? new { id = model.Id } : null);

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

            model.Title = Clip(model.Title, 150);
            model.Description = Clip(model.Description, 1000);
            model.Place = Clip(model.Place, 120);
            model.SlotMinutes = Math.Max(15, Math.Min(240, model.SlotMinutes));
            model.Capacity = Math.Max(1, Math.Min(200, model.Capacity));
            model.Price = Math.Max(0, model.Price);
            model.Hours = hours;
            model.IsSample = false;
            model = TenantStore.SaveFacility(CompanyCode, model);
            return Ok("«" + model.Title + "» ذخیره شد." + (hours.Any() ? "" : " هیچ ساعت کاری ندارد، پس رزرو آنلاین برایش باز نمی‌شود."), "Facilities");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult DeleteFacility(int id)
        {
            TenantStore.DeleteFacility(CompanyCode, id);
            return OkBack("سالن حذف شد. رزروهای قبلی در فهرست می‌مانند.", "Facilities");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult FacilityBookings(GridQuery query, string status = null)
        {
            ViewBag.Facilities = TenantStore.GetFacilities(CompanyCode);
            ViewBag.Days = BookingDayOptions(0, Math.Max(CurrentSettings.BookingDaysAhead, 30));
            var rows = TenantStore.GetFacilityBookings(CompanyCode).AsEnumerable();
            AppointmentStatus parsed;
            if (Enum.TryParse(status, true, out parsed)) rows = rows.Where(b => b.Status == parsed);
            return View(query.Slice(rows,
                b => b.Code + " " + b.FacilityTitle + " " + b.CustomerName + " " + b.CustomerMobile,
                new Dictionary<string, Func<StoreFacilityBooking, object>>
                {
                    ["start"] = b => b.StartAt,
                    ["facility"] = b => b.FacilityTitle,
                    ["name"] = b => b.CustomerName,
                    ["price"] = b => b.Price,
                    ["status"] = b => b.Status
                }, "start", true));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult ManualFacilityBooking(int facilityId, string date, string time, string customerName, string customerMobile)
        {
            var facility = TenantStore.GetFacility(CompanyCode, facilityId);
            if (facility == null) return Fail("سالن را انتخاب کنید.", "FacilityBookings");
            var day = ParseBookingDate(date);
            var minute = BookingTime.ParseClock(time);
            if (!day.HasValue || !minute.HasValue) return Fail("روز و ساعت را انتخاب کنید.", "FacilityBookings");
            if (string.IsNullOrWhiteSpace(customerName)) return Fail("نام را وارد کنید.", "FacilityBookings");
            var mobile = BookingTime.NormalizeMobile(customerMobile);
            if (mobile == null) return Fail("شماره موبایل معتبر نیست.", "FacilityBookings");
            var start = day.Value.AddMinutes(minute.Value);
            var booking = new StoreFacilityBooking
            {
                FacilityId = facility.Id,
                StartAt = start,
                EndAt = start.AddMinutes(Math.Max(15, facility.SlotMinutes)),
                Price = facility.Price,
                CustomerName = Clip(customerName, 80),
                CustomerMobile = mobile,
                Status = AppointmentStatus.Confirmed,
                Source = "admin"
            };
            var error = TenantStore.BookFacility(CompanyCode, booking, CurrentSettings, false);
            if (error != null) return Fail(error, "FacilityBookings");
            NikooSystem.Services.Notifications.StoreFacilityBooking(CompanyCode, booking);
            return Ok("رزرو با کد " + booking.Code + " ثبت شد.", "FacilityBookings");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult SetFacilityBookingStatus(int id, AppointmentStatus status)
        {
            var booking = TenantStore.GetFacilityBooking(CompanyCode, id);
            if (booking == null) return ToAction("FacilityBookings");
            if (status != AppointmentStatus.Confirmed && status != AppointmentStatus.Cancelled
                && status != AppointmentStatus.Completed && status != AppointmentStatus.NoShow)
                return ToAction("FacilityBookings");
            booking.Status = status;
            booking.CancelledAt = status == AppointmentStatus.Cancelled ? DateTime.Now : (DateTime?)null;
            TenantStore.UpdateFacilityBooking(CompanyCode, booking);
            return OkBack("وضعیت رزرو به‌روز شد.", "FacilityBookings");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Campus)]
        public ActionResult CampusGuide()
        {
            return View();
        }
    }
}
