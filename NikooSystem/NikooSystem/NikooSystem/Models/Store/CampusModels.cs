using System;
using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Models.Store
{
    /// <summary>How a class or a private lesson is held.</summary>
    public static class CampusDelivery
    {
        public const string InPerson = "inperson";
        public const string Online = "online";
        public const string Hybrid = "hybrid";

        public static string Normalize(string mode)
        {
            switch ((mode ?? "").Trim().ToLowerInvariant())
            {
                case Online: return Online;
                case Hybrid:
                case "both": return Hybrid;
                default: return InPerson;
            }
        }

        public static string Text(string mode)
        {
            switch (Normalize(mode))
            {
                case Online: return "آنلاین";
                case Hybrid: return "حضوری و آنلاین";
                default: return "حضوری";
            }
        }
    }

    /// <summary>A group class that runs on one weekday for a term (football school, language class…).</summary>
    public class StoreClass : IStoreEntity
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public int? InstructorId { get; set; }
        public string InstructorName { get; set; }
        /// <summary><see cref="CampusDelivery"/>.</summary>
        public string Mode { get; set; } = CampusDelivery.InPerson;
        public int Capacity { get; set; } = 12;
        public long Price { get; set; }
        /// <summary>0 = Saturday … 6 = Friday.</summary>
        public int Weekday { get; set; }
        public int StartMinute { get; set; } = 16 * 60;
        public int EndMinute { get; set; } = 17 * 60 + 30;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        /// <summary>Number of sessions in the term; 0 means it is not stated.</summary>
        public int Sessions { get; set; }
        public string Level { get; set; }
        public string AgeGroup { get; set; }
        public string Place { get; set; }
        /// <summary>Meeting link, shown to a confirmed student when the class is online.</summary>
        public string OnlineLink { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public bool IsSample { get; set; }

        public string WhenText =>
            (Weekday >= 0 && Weekday < BookingTime.DayNames.Length ? BookingTime.DayNames[Weekday] : "")
            + " " + BookingTime.Clock(StartMinute) + " تا " + BookingTime.Clock(EndMinute);

        public bool AcceptsEnrollment(DateTime today) =>
            IsActive && (!EndDate.HasValue || EndDate.Value.Date >= today.Date);
    }

    /// <summary>A student's seat in a group class. Status uses <see cref="AppointmentStatus"/>.</summary>
    public class StoreEnrollment : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public int ClassId { get; set; }
        public string ClassTitle { get; set; }
        public string CustomerName { get; set; }
        public string CustomerMobile { get; set; }
        public string CustomerEmail { get; set; }
        public string Notes { get; set; }
        public long Price { get; set; }
        public long PaidAmount { get; set; }
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
        public string Authority { get; set; }
        public long? RefId { get; set; }
        public string PaymentMessage { get; set; }
        public DateTime? PaidAt { get; set; }
        public string Source { get; set; } = "online";
        public bool IsSample { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CancelledAt { get; set; }

        public bool HoldsSeat(DateTime now) =>
            Status != AppointmentStatus.Cancelled
            && (Status != AppointmentStatus.PendingPayment || CreatedAt.AddMinutes(StoreAppointment.PaymentHoldMinutes) > now);
    }

    /// <summary>A court, hall or table rented by the hour (football pitch, ping-pong table…).</summary>
    public class StoreFacility : IStoreEntity
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public int SlotMinutes { get; set; } = 60;
        public long Price { get; set; }
        /// <summary>How many bookings may share one slot; 1 means the whole place is reserved.</summary>
        public int Capacity { get; set; } = 1;
        public string Place { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public bool IsSample { get; set; }
        public List<StoreWorkHours> Hours { get; set; } = new List<StoreWorkHours>();

        public IEnumerable<StoreWorkHours> HoursOn(int day) =>
            (Hours ?? new List<StoreWorkHours>()).Where(h => h.Day == day && h.EndMinute > h.StartMinute).OrderBy(h => h.StartMinute);
    }

    /// <summary>One hourly reservation of a <see cref="StoreFacility"/>.</summary>
    public class StoreFacilityBooking : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public int FacilityId { get; set; }
        public string FacilityTitle { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string CustomerName { get; set; }
        public string CustomerMobile { get; set; }
        public string CustomerEmail { get; set; }
        public string Notes { get; set; }
        public long Price { get; set; }
        public long PaidAmount { get; set; }
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
        public string Authority { get; set; }
        public long? RefId { get; set; }
        public string PaymentMessage { get; set; }
        public DateTime? PaidAt { get; set; }
        public string Source { get; set; } = "online";
        public bool IsSample { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CancelledAt { get; set; }

        public bool Blocks(DateTime now) =>
            Status != AppointmentStatus.Cancelled
            && (Status != AppointmentStatus.PendingPayment || CreatedAt.AddMinutes(StoreAppointment.PaymentHoldMinutes) > now);
    }
}
