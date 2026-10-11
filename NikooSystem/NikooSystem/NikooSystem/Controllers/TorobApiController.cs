using System.IO;
using System.Text;
using System.Web.Mvc;
using NikooSystem.Models.Torob;
using NikooSystem.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NikooSystem.Controllers
{
    public class TorobApiController : Controller
    {
        [HttpPost]
        public ActionResult Products(string company)
        {
            Response.ContentType = "application/json; charset=utf-8";

            if (!TorobTokenValidator.TryValidate(Request, out var authError))
            {
                Response.StatusCode = 401;
                return Content(JsonConvert.SerializeObject(new TorobErrorResponse { Error = authError }), "application/json");
            }

            var companyCode = ResolveCompanyCode(company);
            if (string.IsNullOrEmpty(companyCode))
            {
                Response.StatusCode = 404;
                return Content(JsonConvert.SerializeObject(new TorobErrorResponse { Error = "shop not found" }), "application/json");
            }

            JObject body;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream, Encoding.UTF8))
                {
                    var json = reader.ReadToEnd();
                    body = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                }
            }
            catch
            {
                Response.StatusCode = 400;
                return Content(JsonConvert.SerializeObject(new TorobErrorResponse { Error = "invalid json body" }), "application/json");
            }

            try
            {
                var result = TorobCatalogService.HandleRequest(companyCode, body, Request);
                return Content(JsonConvert.SerializeObject(result), "application/json");
            }
            catch (TorobApiException ex)
            {
                Response.StatusCode = 400;
                return Content(JsonConvert.SerializeObject(new TorobErrorResponse { Error = ex.Message }), "application/json");
            }
        }

        private string ResolveCompanyCode(string routeCompany)
        {
            if (!string.IsNullOrWhiteSpace(routeCompany))
            {
                var t = TenantStore.GetTenant(routeCompany);
                return t?.CompanyCode;
            }

            var byHost = TenantStore.ResolveByHost(Request.Url?.Host);
            return byHost?.CompanyCode;
        }
    }
}
