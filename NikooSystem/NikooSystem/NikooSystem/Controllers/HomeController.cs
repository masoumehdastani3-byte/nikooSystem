using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var products = JsonStore.GetProducts().Where(p => p.IsActive).OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Id).ToList();
            return View(products);
        }

        public ActionResult About()
        {
            return View();
        }

        public ActionResult Contact()
        {
            return View(new ContactViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Contact(ContactViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            TempData["Success"] = "پیام شما با موفقیت ثبت شد. به‌زودی با شما تماس می‌گیریم.";
            return RedirectToAction("Contact");
        }

        public ActionResult Terms()
        {
            return View();
        }

        public ActionResult Privacy()
        {
            return View();
        }
    }
}
