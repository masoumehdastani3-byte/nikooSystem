using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Models.Store;

namespace NikooSystem.Helpers
{
    public static class StoreMenuHelper
    {
        public static IEnumerable<StoreMenuItem> Roots(IEnumerable<StoreMenuItem> menus, string location = "main")
        {
            return (menus ?? Enumerable.Empty<StoreMenuItem>())
                .Where(m => m.IsActive && m.Location == location && m.ParentId == null)
                .OrderBy(m => m.SortOrder);
        }

        public static IEnumerable<StoreMenuItem> Children(IEnumerable<StoreMenuItem> menus, int parentId)
        {
            return (menus ?? Enumerable.Empty<StoreMenuItem>())
                .Where(m => m.IsActive && m.ParentId == parentId)
                .OrderBy(m => m.SortOrder);
        }
    }
}
