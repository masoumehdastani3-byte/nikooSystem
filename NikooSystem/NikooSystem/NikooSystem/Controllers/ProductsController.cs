using System.Linq;
using System.Web.Mvc;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Index(string category = null)
        {
            var products = JsonStore.GetProducts().Where(p => p.IsActive).AsEnumerable();
            if (!string.IsNullOrWhiteSpace(category))
                products = products.Where(p => p.Category == category);

            ViewBag.Category = category;
            ViewBag.Categories = JsonStore.GetProducts().Where(p => p.IsActive).Select(p => p.Category).Distinct().ToList();
            return View(products.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Title).ToList());
        }

        public ActionResult Details(int id)
        {
            var product = JsonStore.GetProduct(id);
            if (product == null || !product.IsActive)
                return HttpNotFound();

            var related = JsonStore.GetProducts()
                .Where(p => p.IsActive && p.Id != id && p.Category == product.Category)
                .Take(3)
                .ToList();
            ViewBag.Related = related;
            return View(product);
        }
    }
}
