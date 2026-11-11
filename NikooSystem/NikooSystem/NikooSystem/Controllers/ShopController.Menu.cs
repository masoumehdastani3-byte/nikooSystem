using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public partial class ShopController
    {
        private ActionResult MenuHome()
        {
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode).Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
            ViewBag.Products = TenantStore.GetProducts(CompanyCode).Where(p => p.IsActive).OrderBy(p => p.Id).ToList();
            ViewBag.Table = CleanTable(Request["table"]);
            ViewBag.MenuUrl = Seo.StoreUrl(Tenant, Request);
            return View("Menu");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Menu)]
        public ActionResult CallWaiter(string table, string note)
        {
            var label = CleanTable(table);
            if (label == null)
                return Json(new { ok = false, message = "شماره میز را از ۱ تا ۴۰ وارد کنید." });
            note = (note ?? "").Trim();
            if (note.Length > 200) note = note.Substring(0, 200);
            TenantStore.AddWaiterCall(CompanyCode, new WaiterCall { TableLabel = label, Note = note });
            return Json(new { ok = true, message = "درخواست برای گارسون فرستاده شد." });
        }

        private static string CleanTable(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            const string persian = "۰۱۲۳۴۵۶۷۸۹";
            var digits = new string(raw.Trim().Select(c =>
            {
                var index = persian.IndexOf(c);
                if (index >= 0) return (char)('0' + index);
                return c >= '0' && c <= '9' ? c : '\0';
            }).Where(c => c != '\0').ToArray());
            int number;
            if (!int.TryParse(digits, out number) || number < 1 || number > 40) return null;
            return number.ToString();
        }
    }
}
