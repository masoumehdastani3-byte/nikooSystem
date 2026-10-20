using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Models;

namespace NikooSystem.Helpers
{
    /// <summary>A drop-down filter of an admin table toolbar; the empty option shows everything.</summary>
    public class GridSelect
    {
        public GridSelect(string name, string allLabel, IEnumerable<KeyValuePair<string, string>> options)
        {
            Name = name;
            AllLabel = allLabel;
            Options = options.ToList();
        }

        public string Name { get; }
        public string AllLabel { get; }
        public List<KeyValuePair<string, string>> Options { get; }
    }

    /// <summary>
    /// Search box, sortable column headers and pager of the admin tables. They work on the query string of the current
    /// page (q, sort, dir, page, size and any filters), so the server does the searching, sorting and paging.
    /// </summary>
    public static class GridHtml
    {
        private static readonly CultureInfo Fa = CultureInfo.GetCultureInfo("fa-IR");

        public static string ToFa(this int number) => number.ToString("N0", Fa);

        /// <summary>The current URL with some query values replaced; a null value removes the key.</summary>
        public static string QueryUrl(this HtmlHelper html, IDictionary<string, object> changes)
        {
            var request = html.ViewContext.HttpContext.Request;
            var query = HttpUtility.ParseQueryString(request.Url.Query);
            foreach (var change in changes)
            {
                var value = change.Value == null ? null : System.Convert.ToString(change.Value, CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(value)) query.Remove(change.Key);
                else query[change.Key] = value;
            }
            var text = query.ToString();
            return request.Url.AbsolutePath + (text.Length > 0 ? "?" + text : "");
        }

        private static string QueryUrl(HtmlHelper html, string key, object value, params string[] reset)
        {
            var changes = reset.ToDictionary(r => r, r => (object)null);
            changes[key] = value;
            return html.QueryUrl(changes);
        }

        /// <summary>A table header that sorts by <paramref name="key"/>; a second click reverses the order.</summary>
        public static MvcHtmlString SortHeader(this HtmlHelper html, string key, string label)
        {
            var query = html.ViewContext.HttpContext.Request.QueryString;
            var active = string.Equals(query["sort"], key, System.StringComparison.OrdinalIgnoreCase);
            var desc = active && string.Equals(query["dir"], "desc", System.StringComparison.OrdinalIgnoreCase);
            var th = new TagBuilder("th") { InnerHtml = SortAnchor(html, key, label).ToString() };
            if (active) th.MergeAttribute("aria-sort", desc ? "descending" : "ascending");
            return MvcHtmlString.Create(th.ToString());
        }

        /// <summary>The same sort control as <see cref="SortHeader"/>, for lists that are not a table (the gallery).</summary>
        public static MvcHtmlString SortLink(this HtmlHelper html, string key, string label) =>
            MvcHtmlString.Create(SortAnchor(html, key, label).ToString());

        private static TagBuilder SortAnchor(HtmlHelper html, string key, string label)
        {
            var query = html.ViewContext.HttpContext.Request.QueryString;
            var active = string.Equals(query["sort"], key, System.StringComparison.OrdinalIgnoreCase);
            var desc = active && string.Equals(query["dir"], "desc", System.StringComparison.OrdinalIgnoreCase);
            var link = new TagBuilder("a");
            link.AddCssClass("grid-sort" + (active ? " is-active" : ""));
            link.MergeAttribute("href", html.QueryUrl(new Dictionary<string, object> { ["sort"] = key, ["dir"] = active && !desc ? "desc" : "asc", ["page"] = null }));
            link.MergeAttribute("title", "مرتب‌سازی بر اساس " + label);
            link.InnerHtml = html.Encode(label) + " <i class=\"bi " + (!active ? "bi-arrow-down-up" : desc ? "bi-sort-down" : "bi-sort-up") + "\"></i>";
            return link;
        }

        /// <summary>The search box and filters above a table, sent to the server as a GET form.</summary>
        public static MvcHtmlString GridToolbar(this HtmlHelper html, string placeholder, params GridSelect[] selects)
        {
            var query = html.ViewContext.HttpContext.Request.QueryString;
            var own = new HashSet<string>(selects.Select(s => s.Name).Concat(new[] { "q", "page" }), System.StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            sb.Append("<form class=\"grid-toolbar\" method=\"get\" role=\"search\">");
            foreach (var key in query.AllKeys.Where(k => k != null && !own.Contains(k)))
                sb.Append(Hidden(key, query[key]));

            sb.Append("<div class=\"grid-search\"><i class=\"bi bi-search\"></i>");
            sb.Append("<input type=\"search\" name=\"q\" value=\"" + html.AttributeEncode(query["q"]) + "\" placeholder=\"" + html.AttributeEncode(placeholder) + "\" aria-label=\"" + html.AttributeEncode(placeholder) + "\" />");
            sb.Append("</div>");

            foreach (var select in selects)
            {
                var current = query[select.Name] ?? "";
                sb.Append("<select class=\"form-select grid-filter\" name=\"" + html.AttributeEncode(select.Name) + "\" onchange=\"this.form.submit()\" aria-label=\"" + html.AttributeEncode(select.AllLabel) + "\">");
                sb.Append("<option value=\"\">" + html.Encode(select.AllLabel) + "</option>");
                foreach (var option in select.Options)
                    sb.Append("<option value=\"" + html.AttributeEncode(option.Key) + "\"" + (option.Key == current ? " selected" : "") + ">" + html.Encode(option.Value) + "</option>");
                sb.Append("</select>");
            }

            sb.Append("<button class=\"btn-store-sm\" type=\"submit\"><i class=\"bi bi-search\"></i> جستجو</button>");
            if (!string.IsNullOrEmpty(query["q"]) || selects.Any(s => !string.IsNullOrEmpty(query[s.Name])))
            {
                var clear = selects.Select(s => s.Name).Concat(new[] { "q", "page" }).ToDictionary(k => k, k => (object)null);
                sb.Append("<a class=\"grid-clear\" href=\"" + html.AttributeEncode(html.QueryUrl(clear)) + "\"><i class=\"bi bi-x-circle\"></i> حذف فیلتر</a>");
            }
            sb.Append("</form>");
            return MvcHtmlString.Create(sb.ToString());
        }

        /// <summary>Row count, page links and page size under a table.</summary>
        /// <param name="sizes">False on pages whose action has a fixed page size.</param>
        public static MvcHtmlString Pager(this HtmlHelper html, IPagedList list, bool sizes = true)
        {
            var sb = new StringBuilder("<div class=\"grid-footer\">");
            if (list.TotalCount == 0)
            {
                sb.Append("<span class=\"grid-summary\">موردی پیدا نشد</span></div>");
                return MvcHtmlString.Create(sb.ToString());
            }

            var first = (list.Page - 1) * list.PageSize + 1;
            var last = System.Math.Min(list.TotalCount, list.Page * list.PageSize);
            sb.Append("<span class=\"grid-summary\">نمایش " + first.ToFa() + " تا " + last.ToFa() + " از " + list.TotalCount.ToFa() + " ردیف</span>");

            if (list.TotalPages > 1)
            {
                sb.Append("<nav class=\"grid-pager\" aria-label=\"صفحه‌بندی\">");
                sb.Append(PageLink(html, list.Page - 1, "<i class=\"bi bi-chevron-right\"></i>", list.Page <= 1, "صفحه قبل"));
                var shownGap = false;
                for (var i = 1; i <= list.TotalPages; i++)
                {
                    if (i == 1 || i == list.TotalPages || System.Math.Abs(i - list.Page) <= 2)
                    {
                        shownGap = false;
                        sb.Append(i == list.Page
                            ? "<span class=\"is-current\" aria-current=\"page\">" + i.ToFa() + "</span>"
                            : PageLink(html, i, i.ToFa(), false, "صفحه " + i.ToFa()));
                    }
                    else if (!shownGap)
                    {
                        shownGap = true;
                        sb.Append("<span class=\"gap\">…</span>");
                    }
                }
                sb.Append(PageLink(html, list.Page + 1, "<i class=\"bi bi-chevron-left\"></i>", !list.HasNext, "صفحه بعد"));
                sb.Append("</nav>");
            }

            if (sizes)
            {
                sb.Append("<label class=\"grid-size\">تعداد در صفحه <select onchange=\"location.href=this.value\">");
                foreach (var size in GridQuery.PageSizes)
                {
                    var url = QueryUrl(html, "size", size, "page");
                    sb.Append("<option value=\"" + html.AttributeEncode(url) + "\"" + (size == list.PageSize ? " selected" : "") + ">" + size.ToFa() + "</option>");
                }
                sb.Append("</select></label>");
            }
            sb.Append("</div>");
            return MvcHtmlString.Create(sb.ToString());
        }

        private static string PageLink(HtmlHelper html, int page, string inner, bool disabled, string label)
        {
            if (disabled) return "<span class=\"is-disabled\" aria-hidden=\"true\">" + inner + "</span>";
            return "<a href=\"" + html.AttributeEncode(QueryUrl(html, "page", page > 1 ? (object)page : null)) + "\" aria-label=\"" + label + "\">" + inner + "</a>";
        }

        private static string Hidden(string name, string value)
        {
            return "<input type=\"hidden\" name=\"" + HttpUtility.HtmlAttributeEncode(name) + "\" value=\"" + HttpUtility.HtmlAttributeEncode(value) + "\" />";
        }

        /// <summary>Options for a <see cref="GridSelect"/> from any list.</summary>
        public static IEnumerable<KeyValuePair<string, string>> Options<T>(IEnumerable<T> items, System.Func<T, object> value, System.Func<T, string> text)
        {
            return items.Select(i => new KeyValuePair<string, string>(System.Convert.ToString(value(i), CultureInfo.InvariantCulture), text(i)));
        }
    }
}
