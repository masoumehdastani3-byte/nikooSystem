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
                return Error(401, authError);

            var companyCode = TenantStore.ResolveCompanyCode(Request, company);
            if (string.IsNullOrEmpty(companyCode) || TenantStore.GetTenant(companyCode)?.IsSubscriptionLocked() == true)
                return Error(404, "shop not found");

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
                return Error(400, "invalid json body");
            }

            try
            {
                return JsonText(TorobCatalogService.HandleRequest(companyCode, body, Request));
            }
            catch (TorobApiException ex)
            {
                return Error(400, ex.Message);
            }
        }

        private ActionResult Error(int status, string message)
        {
            Response.StatusCode = status;
            return JsonText(new TorobErrorResponse { Error = message });
        }

        private ContentResult JsonText(object value)
        {
            return Content(JsonConvert.SerializeObject(value), "application/json");
        }
    }
}
