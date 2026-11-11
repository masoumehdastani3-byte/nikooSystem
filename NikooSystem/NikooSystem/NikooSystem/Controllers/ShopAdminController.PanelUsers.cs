using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Owner-only management of staff panel logins.</summary>
    public partial class ShopAdminController
    {
        public ActionResult PanelUsers()
        {
            return View(TenantStore.GetPanelUsers(CompanyCode));
        }

        [HttpGet]
        public ActionResult EditPanelUser(int? id)
        {
            var user = id.HasValue
                ? TenantStore.GetPanelUser(CompanyCode, id.Value)
                : new StorePanelUser { Role = PanelRole.Cashier, IsActive = true };
            if (id.HasValue && user == null) return ToAction("PanelUsers");
            ViewBag.Roles = PanelRoleText.StaffRoles;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditPanelUser(StorePanelUser model, string newPassword)
        {
            ViewBag.Roles = PanelRoleText.StaffRoles;
            var error = TenantStore.SavePanelUser(CompanyCode, model, newPassword, CurrentSettings.AdminUsername);
            if (error != null)
            {
                Fail(error);
                return View(model);
            }
            return Ok("کاربر «" + model.DisplayName + "» ذخیره شد.", "PanelUsers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeletePanelUser(int id)
        {
            if (CurrentPanelUserId == id)
                return Fail("نمی‌توانید کاربر واردشده را حذف کنید.", "PanelUsers");
            TenantStore.DeletePanelUser(CompanyCode, id);
            return Ok("کاربر حذف شد.", "PanelUsers");
        }
    }
}
