using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>«قالب دموها»: the template each product's demo site opens with.</summary>
    public partial class AdminController
    {
        [HttpGet]
        public ActionResult DemoTemplates()
        {
            var products = SiteStore.GetProducts();
            var demos = TenantStore.DemoBySlug
                .Select(d =>
                {
                    var type = StoreTypes.Get(d.Key);
                    var settings = TenantStore.GetSettings(d.Value);
                    return new DemoTemplateRow
                    {
                        Slug = d.Key,
                        CompanyCode = d.Value,
                        Type = type,
                        Template = StoreTemplates.Get(settings?.LayoutStyle),
                        TemplateCount = StoreTemplates.ForKind(type.Kind).Count(),
                        Products = products.Where(p => string.Equals(p.DemoSlug, d.Key, StringComparison.OrdinalIgnoreCase)).ToList()
                    };
                })
                .ToList();
            return View(demos);
        }

        [HttpGet]
        public ActionResult DemoTemplate(string slug)
        {
            string company;
            if (!TenantStore.DemoBySlug.TryGetValue(slug ?? "", out company)) return HttpNotFound();
            var type = StoreTypes.Get(slug);
            ViewBag.Type = type;
            ViewBag.Slug = type.Id;
            ViewBag.CompanyCode = company;
            ViewBag.Kind = SiteKinds.Get(type.Kind);
            return View((object)TenantStore.GetSettings(company)?.LayoutStyle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DemoTemplate(string slug, string layoutStyle)
        {
            string company;
            if (!TenantStore.DemoBySlug.TryGetValue(slug ?? "", out company)) return HttpNotFound();
            var type = StoreTypes.Get(slug);
            var template = StoreTemplates.ForKind(type.Kind).FirstOrDefault(t => t.Id == layoutStyle);
            var settings = TenantStore.GetSettings(company);
            if (template == null || settings == null)
            {
                TempData["Error"] = "این قالب برای دموی «" + type.Name + "» نیست.";
                return RedirectToAction("DemoTemplate", new { slug });
            }

            settings.LayoutStyle = template.Id;
            settings.PrimaryColor = template.PrimaryColor;
            settings.SecondaryColor = template.SecondaryColor;
            TenantStore.SaveSettings(settings);
            TempData["Ok"] = "دموی «" + type.Name + "» از این به بعد با قالب «" + template.Name + "» باز می‌شود.";
            return RedirectToAction("DemoTemplates");
        }
    }
}

namespace NikooSystem.Models
{
    /// <summary>A demo site on «قالب دموها».</summary>
    public sealed class DemoTemplateRow
    {
        public string Slug { get; set; }
        public string CompanyCode { get; set; }
        public StoreTypeInfo Type { get; set; }
        public StoreTemplateInfo Template { get; set; }
        public int TemplateCount { get; set; }
        /// <summary>The main site's products whose «مشاهده دمو» opens this demo.</summary>
        public List<Product> Products { get; set; }
    }
}
