using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web;
using NikooSystem.Models.Store;
using Newtonsoft.Json;

namespace NikooSystem.Services
{
    /// <summary>اطلاع‌رسانی به webhook ترب پس از تغییر محصول — api.torob.com/update/webhook/v1/</summary>
    public static class TorobWebhookService
    {
        private const string WebhookUrl = "https://api.torob.com/update/webhook/v1/";

        public static void NotifyProductChanged(string companyCode, StoreProduct product)
        {
            if (product == null || !product.IsActive) return;

            var settings = TenantStore.GetSettings(companyCode);
            if (settings == null || !settings.EnableTorob) return;
            if (string.IsNullOrWhiteSpace(settings.TorobWebhookBearerToken)) return;

            var tenant = TenantStore.GetTenant(companyCode);
            HttpRequestBase request = HttpContext.Current?.Request != null
                ? new HttpRequestWrapper(HttpContext.Current.Request)
                : null;
            var baseUrl = TorobCatalogService.GetPublicBaseUrl(settings, tenant, companyCode, request);
            var pageUrl = TorobCatalogService.BuildProductPageUrl(baseUrl, companyCode, product.Slug, tenant);
            var pageUnique = TorobCatalogService.PageUnique(companyCode, product.Id);

            NotifyBatch(settings.TorobWebhookBearerToken, new[]
            {
                new { page_url = pageUrl, page_unique = pageUnique }
            });
        }

        private static void NotifyBatch(string bearerToken, IEnumerable<object> items)
        {
            try
            {
                var payload = JsonConvert.SerializeObject(new { items });
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    client.Headers[HttpRequestHeader.Authorization] = "Bearer " + bearerToken.Trim();
                    client.Encoding = Encoding.UTF8;
                    client.UploadString(WebhookUrl, "POST", payload);
                }
            }
            catch
            {
                // webhook اختیاری است؛ خطا نباید ذخیره محصول را مختل کند
            }
        }
    }
}
