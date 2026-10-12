using System;
using System.Web.Mvc;
using NikooSystem.Controllers;
using NikooSystem.Services;

namespace NikooSystem.Filters
{
    /// <summary>
    /// Counts the pages of the controller in the visit statistics (<see cref="VisitStats"/>): full pages shown to a GET
    /// request, on a store (its code from the store controllers) or on Nikoo's own site. The site's own managers are not counted.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CountVisitsAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            var http = filterContext.HttpContext;
            var request = http.Request;
            if (filterContext.IsChildAction || request.HttpMethod != "GET" || request.IsAjaxRequest()) return;
            if (!(filterContext.Result is ViewResult) || http.Response.StatusCode != 200) return;
            if (!string.IsNullOrEmpty(request.QueryString["previewTemplate"])) return;

            var session = http.Session;
            if (session?["AdminUser"] != null) return;

            var code = VisitStats.MainSite;
            if (filterContext.Controller is ShopBaseController)
            {
                code = filterContext.Controller.ViewBag.CompanyCode as string;
                if (string.IsNullOrEmpty(code) || session?["shop_admin_" + code] as bool? == true) return;
            }

            try { VisitStats.Record(http, code); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Visit was not counted: " + ex.Message); }
        }
    }
}
