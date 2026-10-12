using System.Text;
using System.Web.Mvc;
using System.Web.Routing;

namespace NikooSystem.Helpers
{
    /// <summary>Markup shared by the store admin pages. Links and forms target the current controller and store.</summary>
    public static class AdminHtml
    {
        /// <summary>Page title with an optional "add new" button on the opposite side.</summary>
        public static MvcHtmlString PageHeader(this HtmlHelper html, string title, string newLabel = null, string newAction = null)
        {
            var heading = new TagBuilder("h2");
            heading.SetInnerText(title);
            var inner = heading.ToString();
            if (!string.IsNullOrEmpty(newLabel))
            {
                var button = new TagBuilder("a");
                button.AddCssClass("btn-store");
                button.MergeAttribute("href", StoreUrl(html, newAction));
                button.SetInnerText(newLabel);
                inner += button.ToString();
            }
            var header = new TagBuilder("div") { InnerHtml = inner };
            header.AddCssClass("d-flex justify-content-between mb-3");
            return MvcHtmlString.Create(header.ToString());
        }

        /// <summary>
        /// A labelled checkbox for a bool property. The hidden "false" after it is what gets posted when the box is
        /// unticked, so model binding sees false instead of keeping the default.
        /// </summary>
        public static MvcHtmlString BoolField(this HtmlHelper html, string name, bool value, string label)
        {
            var box = new TagBuilder("input");
            box.MergeAttribute("type", "checkbox");
            box.MergeAttribute("name", name);
            box.MergeAttribute("value", "true");
            if (value) box.MergeAttribute("checked", "checked");

            var wrapper = new TagBuilder("label") { InnerHtml = box.ToString(TagRenderMode.SelfClosing) + " " + html.Encode(label) };

            var fallback = new TagBuilder("input");
            fallback.MergeAttribute("type", "hidden");
            fallback.MergeAttribute("name", name);
            fallback.MergeAttribute("value", "false");
            return MvcHtmlString.Create(wrapper.ToString() + fallback.ToString(TagRenderMode.SelfClosing));
        }

        /// <summary>Save button and a link back to the list page.</summary>
        public static MvcHtmlString FormActions(this HtmlHelper html, string backAction, string saveText = "ذخیره")
        {
            var save = new TagBuilder("button");
            save.AddCssClass("btn-store");
            save.MergeAttribute("type", "submit");
            save.SetInnerText(saveText);

            var back = new TagBuilder("a");
            back.AddCssClass("btn-outline-store");
            back.MergeAttribute("href", StoreUrl(html, backAction));
            back.SetInnerText("بازگشت");
            return MvcHtmlString.Create(save + "\n" + back);
        }

        public static MvcHtmlString EditLink(this HtmlHelper html, string action, int id, string title = "ویرایش")
        {
            return html.IconLink(StoreUrl(html, action, new { id }), "bi-pencil-square", title, "is-edit");
        }

        /// <summary>A round icon button linking to <paramref name="url"/>; the title is its tooltip and accessible name.</summary>
        public static MvcHtmlString IconLink(this HtmlHelper html, string url, string icon, string title, string tone = null, bool newTab = false)
        {
            var link = new TagBuilder("a") { InnerHtml = Icon(icon) };
            link.AddCssClass("icon-btn" + (tone == null ? "" : " " + tone));
            link.MergeAttribute("href", url);
            link.MergeAttribute("title", title);
            link.MergeAttribute("aria-label", title);
            if (newTab)
            {
                link.MergeAttribute("target", "_blank");
                link.MergeAttribute("rel", "noopener");
            }
            return MvcHtmlString.Create(link.ToString());
        }

        public static MvcHtmlString DeleteButton(this HtmlHelper html, string action, int id, string confirm = "حذف شود؟")
        {
            return html.IconPost(action, new { id }, "bi-trash", "حذف", "is-delete", confirm);
        }

        /// <summary>A round icon button posting <paramref name="fields"/> to an action of the current store.</summary>
        public static MvcHtmlString IconPost(this HtmlHelper html, string action, object fields, string icon, string title, string tone = null, string confirm = null)
        {
            var button = new TagBuilder("button") { InnerHtml = Icon(icon) };
            button.MergeAttribute("type", "submit");
            button.AddCssClass("icon-btn" + (tone == null ? "" : " " + tone));
            button.MergeAttribute("title", title);
            button.MergeAttribute("aria-label", title);
            return PostForm(html, action, fields, button.ToString(), confirm);
        }

        /// <summary>
        /// A one-button form posting <paramref name="fields"/> (as hidden inputs) to an action of the current store,
        /// with the anti-forgery token and an optional confirmation question.
        /// </summary>
        public static MvcHtmlString PostButton(this HtmlHelper html, string action, object fields, string text, string buttonClass, string confirm = null)
        {
            var button = new TagBuilder("button");
            button.MergeAttribute("type", "submit");
            button.AddCssClass(buttonClass);
            button.SetInnerText(text);
            return PostForm(html, action, fields, button.ToString(), confirm);
        }

        private const string QuickManageKey = "NikooSystem.QuickManage";

        /// <summary>
        /// The button that opens the quick-manage popup, where the items of <paramref name="list"/> are added, renamed and
        /// deleted without leaving the form. Put it in a .qm-field next to the dropdown (or checkbox list) marked
        /// data-qm="<paramref name="list"/>"; every such control on the page is refreshed after a change.
        /// <paramref name="scopeSelector"/> names a field whose value is sent along (e.g. the menu a new item goes in).
        /// </summary>
        public static MvcHtmlString QuickManage(this HtmlHelper html, string list, string title, string scopeSelector = null)
        {
            html.ViewContext.HttpContext.Items[QuickManageKey] = true;
            var tip = "افزودن، ویرایش یا حذف " + title;
            var button = new TagBuilder("button") { InnerHtml = Icon("bi-gear") };
            button.MergeAttribute("type", "button");
            button.AddCssClass("icon-btn is-edit qm-btn");
            button.MergeAttribute("data-qm-open", list);
            button.MergeAttribute("data-qm-title", title);
            if (scopeSelector != null) button.MergeAttribute("data-qm-scope", scopeSelector);
            button.MergeAttribute("title", tip);
            button.MergeAttribute("aria-label", tip);
            return MvcHtmlString.Create(button.ToString());
        }

        /// <summary>Whether the page rendered a <see cref="QuickManage"/> button, so the layout adds the popup.</summary>
        public static bool UsesQuickManage(this HtmlHelper html) => html.ViewContext.HttpContext.Items.Contains(QuickManageKey);

        private static string Icon(string icon) => "<i class=\"bi " + icon + "\" aria-hidden=\"true\"></i>";

        private static MvcHtmlString PostForm(HtmlHelper html, string action, object fields, string buttonHtml, string confirm)
        {
            var inner = new StringBuilder(html.AntiForgeryToken().ToString());
            foreach (var field in new RouteValueDictionary(fields))
            {
                var input = new TagBuilder("input");
                input.MergeAttribute("type", "hidden");
                input.MergeAttribute("name", field.Key);
                input.MergeAttribute("value", System.Convert.ToString(field.Value));
                inner.Append(input.ToString(TagRenderMode.SelfClosing));
            }
            inner.Append(buttonHtml);

            var form = new TagBuilder("form") { InnerHtml = inner.ToString() };
            form.MergeAttribute("action", StoreUrl(html, action));
            form.MergeAttribute("method", "post");
            form.AddCssClass("d-inline");
            if (!string.IsNullOrEmpty(confirm))
                form.MergeAttribute("onsubmit", "return confirm(" + System.Web.HttpUtility.JavaScriptStringEncode(confirm, true) + ");");
            return MvcHtmlString.Create(form.ToString());
        }

        private static string StoreUrl(HtmlHelper html, string action, object routeValues = null)
        {
            var values = new RouteValueDictionary(routeValues) { ["company"] = html.ViewData["CompanyCode"] };
            var controller = (string)html.ViewContext.RouteData.Values["controller"];
            return new UrlHelper(html.ViewContext.RequestContext).Action(action, controller, values);
        }
    }
}
