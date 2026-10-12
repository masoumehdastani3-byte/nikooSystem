using System.Web;
using System.Web.Mvc;

namespace NikooSystem.Filters
{
    public class AdminAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            return httpContext.Session != null && httpContext.Session["AdminUser"] != null;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            var url = "~/Admin/Login";
            if (request.HttpMethod == "GET" && !request.IsAjaxRequest())
                url += "?returnUrl=" + HttpUtility.UrlEncode(request.RawUrl);
            filterContext.Result = new RedirectResult(url);
        }
    }
}
