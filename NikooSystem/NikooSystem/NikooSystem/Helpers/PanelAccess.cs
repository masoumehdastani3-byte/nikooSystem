using System;
using System.Collections.Generic;
using NikooSystem.Models.Store;

namespace NikooSystem.Helpers
{
    /// <summary>Fixed allowlists for ShopAdmin actions by panel role (MVP; no custom permissions).</summary>
    public static class PanelAccess
    {
        private static readonly HashSet<string> Shared = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Index", "TutorialIndex", "MyDashboard", "SaveDashboardPins",
            "Support", "NewTicket", "Ticket", "ReplyTicket", "TicketImage", "CloseTicket"
        };

        private static readonly HashSet<string> OwnerOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PanelUsers", "EditPanelUser", "DeletePanelUser"
        };

        private static readonly HashSet<string> Cashier = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Orders", "OrderDetails", "UpdateOrder", "GrantAccess", "Customers",
            "Reviews", "ToggleReview", "DeleteReview",
            "Messages", "Message", "DeleteMessage",
            "Pos", "SaleInvoices", "EditSaleInvoice", "DeleteSaleInvoice", "PrintSaleInvoice",
            "Payments", "EditPayment", "DeletePayment",
            "Inventory", "SaveGoods", "AdjustStock", "AdjustGoods",
            "Parties", "EditParty", "DeleteParty",
            "AccountingGuide", "Books",
            "Returns", "ReturnDetails", "DecideReturn", "ShippingGuide",
            "Appointments", "AppointmentDetails", "CollectAppointmentPayment",
            "WaiterCalls", "CloseWaiterCall", "MenuQr"
        };

        private static readonly HashSet<string> Receptionist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Calendar", "Appointments", "AppointmentDetails", "CollectAppointmentPayment",
            "UpdateAppointment", "SetAppointmentStatus", "NewAppointment",
            "TimeOff", "AddTimeOff", "AddOfficialHolidays", "DeleteTimeOff",
            "Customers", "Messages", "Message", "DeleteMessage",
            "Enrollments", "ManualEnroll", "SetEnrollmentStatus",
            "FacilityBookings", "ManualFacilityBooking", "SetFacilityBookingStatus",
            "CampusGuide",
            "WaiterCalls", "CloseWaiterCall", "MenuQr"
        };

        public static bool Allows(PanelRole role, string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return false;
            if (Shared.Contains(action)) return true;
            if (role == PanelRole.Owner) return true;
            if (role == PanelRole.Manager) return !OwnerOnly.Contains(action);
            if (role == PanelRole.Cashier) return Cashier.Contains(action);
            if (role == PanelRole.Receptionist) return Receptionist.Contains(action);
            return false;
        }

        public static bool IsOwner(PanelRole role) => role == PanelRole.Owner;
    }
}
