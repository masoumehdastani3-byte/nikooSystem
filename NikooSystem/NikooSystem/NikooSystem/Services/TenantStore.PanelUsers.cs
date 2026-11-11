using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    public static partial class TenantStore
    {
        public static StorePanelUser GetPanelUser(string companyCode, int id) =>
            Find(NikooTables.PanelUsers, companyCode, id);

        public static StorePanelUser GetPanelUserByUsername(string companyCode, string username)
        {
            username = (username ?? "").Trim();
            if (username.Length == 0) return null;
            return All(NikooTables.PanelUsers, companyCode)
                .FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        }

        public static List<StorePanelUser> GetPanelUsers(string companyCode) =>
            All(NikooTables.PanelUsers, companyCode).OrderBy(u => u.DisplayName).ThenBy(u => u.Username).ToList();

        /// <summary>Active staff user matching username/password, or null.</summary>
        public static StorePanelUser AuthenticatePanelUser(string companyCode, string username, string password)
        {
            var user = GetPanelUserByUsername(companyCode, username);
            if (user == null || !user.IsActive) return null;
            if (user.Role == PanelRole.Owner) return null;
            if (!PasswordHelper.Verify(password, user.PasswordHash)) return null;
            return user;
        }

        /// <summary>Creates or updates a staff user. Returns an error, or null on success.</summary>
        public static string SavePanelUser(string companyCode, StorePanelUser user, string newPassword, string ownerUsername)
        {
            if (user == null) return "کاربر نامعتبر است.";
            user.Username = (user.Username ?? "").Trim();
            user.DisplayName = (user.DisplayName ?? "").Trim();
            if (user.Username.Length == 0) return "نام کاربری را بنویسید.";
            if (user.Username.Length > 40) return "نام کاربری حداکثر ۴۰ کاراکتر است.";
            if (user.DisplayName.Length == 0) return "نام نمایشی را بنویسید.";
            if (user.DisplayName.Length > 80) user.DisplayName = user.DisplayName.Substring(0, 80);
            if (!PanelRoleText.StaffRoles.Contains(user.Role))
                return "نقش را از میان مدیر، صندوقدار یا منشی انتخاب کنید.";

            if (string.Equals(user.Username, ownerUsername?.Trim(), StringComparison.OrdinalIgnoreCase))
                return "این نام کاربری متعلق به مالک سایت است؛ نام دیگری بگذارید.";

            var clash = GetPanelUserByUsername(companyCode, user.Username);
            if (clash != null && clash.Id != user.Id)
                return "نام کاربری «" + user.Username + "» قبلاً ثبت شده است.";

            var stored = user.Id > 0 ? GetPanelUser(companyCode, user.Id) : null;
            if (user.Id > 0 && stored == null) return "کاربر پیدا نشد.";

            var password = (newPassword ?? "").Trim();
            if (stored == null)
            {
                if (password.Length < 6) return "برای کاربر تازه، رمز حداقل ۶ کاراکتر بگذارید.";
                user.PasswordHash = PasswordHelper.Hash(password);
                user.CreatedAt = DateTime.Now;
            }
            else
            {
                user.PasswordHash = password.Length > 0
                    ? PasswordHelper.Hash(password)
                    : stored.PasswordHash;
                user.CreatedAt = stored.CreatedAt;
                user.DashboardPins = stored.DashboardPins;
                if (password.Length > 0 && password.Length < 6)
                    return "رمز جدید حداقل ۶ کاراکتر باشد؛ خالی بگذارید تا عوض نشود.";
            }

            Save(NikooTables.PanelUsers, companyCode, user);
            return null;
        }

        public static List<string> GetDashboardPins(string companyCode, int? panelUserId)
        {
            if (panelUserId.HasValue && panelUserId.Value > 0)
            {
                var user = GetPanelUser(companyCode, panelUserId.Value);
                return ParsePins(user?.DashboardPins);
            }
            var settings = GetSettings(companyCode);
            return ParsePins(settings?.OwnerDashboardPins);
        }

        public static void SaveDashboardPins(string companyCode, int? panelUserId, IEnumerable<string> pins)
        {
            var json = SerializePins(pins);
            if (panelUserId.HasValue && panelUserId.Value > 0)
            {
                var user = GetPanelUser(companyCode, panelUserId.Value);
                if (user == null) return;
                user.DashboardPins = json;
                Save(NikooTables.PanelUsers, companyCode, user);
                return;
            }
            var settings = GetSettings(companyCode);
            if (settings == null) return;
            settings.OwnerDashboardPins = json;
            SaveSettings(settings);
        }

        private static List<string> ParsePins(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);
                return (list ?? new List<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(Helpers.ShopAdminNav.MaxPins)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static string SerializePins(IEnumerable<string> pins)
        {
            var list = (pins ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(Helpers.ShopAdminNav.MaxPins)
                .ToList();
            return list.Count == 0 ? null : Newtonsoft.Json.JsonConvert.SerializeObject(list);
        }

        public static bool DeletePanelUser(string companyCode, int id) =>
            Remove(NikooTables.PanelUsers, companyCode, id);
    }
}
