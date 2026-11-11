using System;
using System.ComponentModel.DataAnnotations;

namespace NikooSystem.Models.Store
{
    /// <summary>Role of a signed-in panel user. Owner is the Settings credential, not a PanelUsers row.</summary>
    public enum PanelRole
    {
        Owner = 0,
        Manager = 1,
        Cashier = 2,
        Receptionist = 3
    }

    /// <summary>A staff login for the store panel (beside the owner account in Settings).</summary>
    public class StorePanelUser : IStoreEntity
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "نام کاربری را بنویسید.")]
        [StringLength(40, ErrorMessage = "نام کاربری حداکثر ۴۰ کاراکتر است.")]
        public string Username { get; set; }

        public string PasswordHash { get; set; }

        [Required(ErrorMessage = "نام نمایشی را بنویسید.")]
        [StringLength(80, ErrorMessage = "نام حداکثر ۸۰ کاراکتر است.")]
        public string DisplayName { get; set; }

        /// <summary>Manager, Cashier or Receptionist — never Owner.</summary>
        public PanelRole Role { get; set; } = PanelRole.Cashier;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>JSON array of ShopAdmin action names pinned on this user's personal dashboard.</summary>
        public string DashboardPins { get; set; }
    }

    public static class PanelRoleText
    {
        public static string Label(this PanelRole role)
        {
            switch (role)
            {
                case PanelRole.Owner: return "مالک";
                case PanelRole.Manager: return "مدیر";
                case PanelRole.Cashier: return "صندوقدار";
                case PanelRole.Receptionist: return "منشی";
                default: return role.ToString();
            }
        }

        /// <summary>Roles that can be assigned to PanelUsers rows.</summary>
        public static readonly PanelRole[] StaffRoles = { PanelRole.Manager, PanelRole.Cashier, PanelRole.Receptionist };
    }
}
