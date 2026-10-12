using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// The quick-manage popup beside dropdowns (<see cref="AdminHtml.QuickManage"/>): lists, adds, renames and deletes the
    /// items of a list without leaving the form. Lists: "categories", "collections", "menus" and "attr-{id}" (the values
    /// of a select-type attribute). Every call answers with the whole list so the page can refresh its controls.
    /// </summary>
    public partial class ShopAdminController
    {
        private const int QuickTitleMax = 120;

        public sealed class QuickItem
        {
            public string Id { get; set; }
            public string Title { get; set; }
            /// <summary>Text of the item in dropdowns, e.g. "— Shirts" for a sub-category.</summary>
            public string Label { get; set; }
            public string ParentId { get; set; }
            public int Depth { get; set; }
            public string Note { get; set; }
            /// <summary>Added to the delete confirmation.</summary>
            public string Warn { get; set; }
            public string EditUrl { get; set; }
        }

        [HttpGet]
        public ActionResult QuickList(string list)
        {
            return QuickResult(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult QuickSave(string list, string id, string title, int? parentId, string scope)
        {
            title = (title ?? "").Trim();
            if (title.Length == 0) return QuickResult(list, error: "عنوان را بنویسید.");
            if (title.Length > QuickTitleMax) title = title.Substring(0, QuickTitleMax);
            int.TryParse(id, out var numericId);

            switch (QuickListName(list, out var attributeId))
            {
                case "categories": return QuickSaveCategory(list, numericId, title, parentId);
                case "collections": return QuickSaveCollection(list, numericId, title);
                case "menus": return QuickSaveMenu(list, numericId, title, parentId, scope);
                case "attr": return QuickSaveOption(list, attributeId, id, title);
                default: return QuickResult(list);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult QuickDelete(string list, string id)
        {
            int.TryParse(id, out var numericId);
            switch (QuickListName(list, out var attributeId))
            {
                case "categories":
                    var category = TenantStore.GetCategory(CompanyCode, numericId);
                    if (category == null) return QuickResult(list, error: "این مورد قبلاً حذف شده است.");
                    TenantStore.DeleteCategory(CompanyCode, numericId);
                    return QuickResult(list, "«" + category.Title + "» حذف شد.");
                case "collections":
                    var collection = TenantStore.GetCollection(CompanyCode, numericId);
                    if (collection == null) return QuickResult(list, error: "این مورد قبلاً حذف شده است.");
                    RemoveCollection(numericId);
                    return QuickResult(list, "«" + collection.Title + "» حذف شد.");
                case "menus":
                    var menu = TenantStore.GetMenu(CompanyCode, numericId);
                    if (menu == null) return QuickResult(list, error: "این مورد قبلاً حذف شده است.");
                    TenantStore.DeleteMenu(CompanyCode, numericId);
                    return QuickResult(list, "«" + menu.Title + "» حذف شد.");
                case "attr":
                    var def = TenantStore.GetAttributeDef(CompanyCode, attributeId);
                    var options = OptionsOf(def);
                    if (def == null || !options.Remove(id ?? "")) return QuickResult(list, error: "این مورد قبلاً حذف شده است.");
                    def.Options = string.Join(",", options);
                    TenantStore.SaveAttributeDef(CompanyCode, def);
                    return QuickResult(list, "«" + id + "» حذف شد.");
                default:
                    return QuickResult(list);
            }
        }

        private ActionResult QuickSaveCategory(string list, int id, string title, int? parentId)
        {
            var all = TenantStore.GetCategories(CompanyCode);
            var category = id > 0 ? all.FirstOrDefault(c => c.Id == id) : new StoreCategory { IsActive = true };
            if (category == null) return QuickResult(list, error: "این مورد حذف شده است.");
            var parent = QuickParent(all, parentId, id);
            if (all.Any(c => c.Id != id && c.ParentId == parent && SameTitle(c.Title, title)))
                return QuickResult(list, error: "«" + title + "» از قبل وجود دارد.");

            if (id == 0) category.SortOrder = NextSort(all.Where(c => c.ParentId == parent).Select(c => c.SortOrder));
            category.Title = title;
            category.ParentId = parent;
            TenantStore.SaveCategory(CompanyCode, category);
            return QuickSaved(list, id, category.Id, title);
        }

        private ActionResult QuickSaveCollection(string list, int id, string title)
        {
            if (!Kind.Sells) return QuickResult(list);
            var all = TenantStore.GetCollections(CompanyCode);
            var collection = id > 0 ? all.FirstOrDefault(c => c.Id == id) : new StoreCollection { IsActive = true, SortOrder = NextSort(all.Select(c => c.SortOrder)) };
            if (collection == null) return QuickResult(list, error: "این مورد حذف شده است.");
            if (all.Any(c => c.Id != id && SameTitle(c.Title, title)))
                return QuickResult(list, error: "«" + title + "» از قبل وجود دارد.");

            collection.Title = title;
            var tagged = id > 0
                ? TenantStore.GetProducts(CompanyCode).Where(p => p.InCollection(id)).Select(p => p.Id).ToList()
                : new List<int>();
            TenantStore.SaveCollection(CompanyCode, collection, tagged);
            return QuickSaved(list, id, collection.Id, title);
        }

        /// <summary>New menu items link nowhere ("#") until their address is set on the full edit page; they sit in the menu of their parent, else of <paramref name="scope"/>.</summary>
        private ActionResult QuickSaveMenu(string list, int id, string title, int? parentId, string scope)
        {
            var all = TenantStore.GetMenus(CompanyCode);
            var menu = id > 0 ? all.FirstOrDefault(m => m.Id == id) : new StoreMenuItem { IsActive = true, Url = "#", LinkType = "custom" };
            if (menu == null) return QuickResult(list, error: "این مورد حذف شده است.");
            var parent = QuickParent(all, parentId, id);
            if (parent.HasValue) menu.Location = all.First(m => m.Id == parent.Value).Location;
            else if (id == 0) menu.Location = scope == "footer" ? "footer" : "main";

            if (id == 0) menu.SortOrder = NextSort(all.Where(m => m.ParentId == parent && m.Location == menu.Location).Select(m => m.SortOrder));
            menu.Title = title;
            menu.ParentId = parent;
            TenantStore.SaveMenu(CompanyCode, menu);
            return QuickSaved(list, id, menu.Id, title);
        }

        /// <summary>Values are the ids of their own items, so renaming one also renames it on the products that use it.</summary>
        private ActionResult QuickSaveOption(string list, int attributeId, string oldValue, string value)
        {
            var def = Kind.Sells ? TenantStore.GetAttributeDef(CompanyCode, attributeId) : null;
            if (def == null) return QuickResult(list);
            if (value.Contains(",")) return QuickResult(list, error: "مقدار نباید ویرگول (,) داشته باشد.");

            var options = OptionsOf(def);
            var renaming = !string.IsNullOrEmpty(oldValue);
            if (options.Any(o => o != oldValue && SameTitle(o, value)))
                return QuickResult(list, error: "«" + value + "» از قبل وجود دارد.");
            if (renaming)
            {
                var index = options.IndexOf(oldValue);
                if (index < 0) return QuickResult(list, error: "این مورد حذف شده است.");
                options[index] = value;
            }
            else options.Add(value);

            def.Options = string.Join(",", options);
            def.InputType = "select";
            TenantStore.SaveAttributeDef(CompanyCode, def);
            if (!renaming) return QuickResult(list, "«" + value + "» اضافه شد.", value);

            var products = TenantStore.RenameAttributeValue(CompanyCode, attributeId, oldValue, value);
            var result = QuickData(list, "«" + oldValue + "» به «" + value + "» تغییر کرد" + (products > 0 ? " (در " + products + " محصول)." : "."));
            result["renamed"] = new { from = oldValue, to = value };
            return Json(result);
        }

        private ActionResult QuickSaved(string list, int oldId, int newId, string title)
        {
            return oldId > 0
                ? QuickResult(list, "«" + title + "» ذخیره شد.")
                : QuickResult(list, "«" + title + "» اضافه شد.", newId.ToString());
        }

        private ActionResult QuickResult(string list, string message = null, string created = null, string error = null)
        {
            return Json(QuickData(list, message, created, error), JsonRequestBehavior.AllowGet);
        }

        private Dictionary<string, object> QuickData(string list, string message = null, string created = null, string error = null)
        {
            var name = QuickListName(list, out var attributeId);
            var items = QuickItems(name, attributeId);
            if (items == null) error = "این فهرست برای سایت شما وجود ندارد.";
            return new Dictionary<string, object>
            {
                ["ok"] = error == null,
                ["error"] = error,
                ["message"] = message,
                ["created"] = created,
                ["tree"] = name == "categories" || name == "menus",
                ["hint"] = name == "menus" ? "آیتم تازه به «#» لینک می‌شود؛ آدرسش را از «ویرایش کامل» تعیین کنید." : null,
                ["items"] = items ?? new List<QuickItem>()
            };
        }

        private List<QuickItem> QuickItems(string name, int attributeId)
        {
            switch (name)
            {
                case "categories":
                    var counts = TenantStore.ItemCountsByCategory(CompanyCode, Kind.IsContent);
                    var noun = Kind.IsContent ? "مطلب" : Kind.IsDigital ? "دوره یا کتاب" : "محصول";
                    return TreeHelper.Flatten(TenantStore.GetCategories(CompanyCode)).Select(r =>
                    {
                        counts.TryGetValue(r.Node.Id, out var count);
                        return new QuickItem
                        {
                            Id = r.Node.Id.ToString(),
                            Title = r.Node.Title,
                            Label = r.Node.TreeTitle(),
                            ParentId = r.Node.ParentId?.ToString(),
                            Depth = r.Depth,
                            Note = Join(count > 0 ? count + " " + noun : null, r.Node.IsActive ? null : "غیرفعال"),
                            Warn = count > 0 ? count + " " + noun + " در این گروه هست که بدون گروه می‌ماند." : null,
                            EditUrl = Url.Action("EditCategory", "ShopAdmin", new { company = CompanyCode, id = r.Node.Id })
                        };
                    }).ToList();

                case "collections":
                    if (!Kind.Sells) return null;
                    var tagged = TenantStore.ProductCountsByCollection(CompanyCode);
                    return TenantStore.GetCollections(CompanyCode).Select(c =>
                    {
                        tagged.TryGetValue(c.Id, out var count);
                        return new QuickItem
                        {
                            Id = c.Id.ToString(),
                            Title = c.Title,
                            Label = c.Title + (c.IsActive ? "" : " (غیرفعال)"),
                            Note = Join(count > 0 ? count + " محصول" : null, c.IsActive ? null : "غیرفعال"),
                            Warn = count > 0 ? "برچسب از " + count + " محصول برداشته می‌شود." : null,
                            EditUrl = Url.Action("EditCollection", "ShopAdmin", new { company = CompanyCode, id = c.Id })
                        };
                    }).ToList();

                case "menus":
                    var menus = TenantStore.GetMenus(CompanyCode).OrderBy(m => m.Location == "main" ? 0 : 1);
                    return TreeHelper.Flatten(menus).Select(r => new QuickItem
                    {
                        Id = r.Node.Id.ToString(),
                        Title = r.Node.Title,
                        Label = r.Node.TreeTitle(),
                        ParentId = r.Node.ParentId?.ToString(),
                        Depth = r.Depth,
                        Note = Join(r.Node.Location == "footer" ? "فوتر" : "منوی اصلی", r.Node.IsActive ? null : "غیرفعال"),
                        EditUrl = Url.Action("EditMenu", "ShopAdmin", new { company = CompanyCode, id = r.Node.Id })
                    }).ToList();

                case "attr":
                    var def = Kind.Sells ? TenantStore.GetAttributeDef(CompanyCode, attributeId) : null;
                    if (def == null) return null;
                    var editUrl = Url.Action("EditAttribute", "ShopAdmin", new { company = CompanyCode, id = def.Id });
                    return OptionsOf(def).Select(o => new QuickItem
                    {
                        Id = o,
                        Title = o,
                        Label = o,
                        Warn = "محصولاتی که این مقدار را دارند آن را نگه می‌دارند ولی دیگر در فهرست انتخاب نیست.",
                        EditUrl = editUrl
                    }).ToList();

                default:
                    return null;
            }
        }

        /// <summary>"attr-12" gives "attr" and 12; other names come back as they are.</summary>
        private static string QuickListName(string list, out int attributeId)
        {
            attributeId = 0;
            list = list ?? "";
            if (list.StartsWith("attr-", StringComparison.Ordinal) && int.TryParse(list.Substring(5), out attributeId)) return "attr";
            return list;
        }

        /// <summary>Only top-level items can be parents (as on the full edit pages), and never the item itself.</summary>
        private static int? QuickParent<T>(IEnumerable<T> all, int? parentId, int id) where T : ITreeNode
        {
            if (!parentId.HasValue || parentId.Value == id) return null;
            return all.Any(n => n.Id == parentId.Value && !n.ParentId.HasValue) ? parentId : null;
        }

        private static List<string> OptionsOf(StoreAttributeDef def) =>
            (def?.Options ?? "").Split(',').Select(o => o.Trim()).Where(o => o.Length > 0).Distinct().ToList();

        private static int NextSort(IEnumerable<int> sorts) => sorts.DefaultIfEmpty(0).Max() + 1;

        private static bool SameTitle(string a, string b) => string.Equals((a ?? "").Trim(), b, StringComparison.OrdinalIgnoreCase);

        private static string Join(params string[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
    }
}
