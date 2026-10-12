using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Orders, customers and product reviews.</summary>
    public partial class ShopAdminController
    {
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Orders(OrderGridQuery query)
        {
            ViewBag.Query = query;
            return View(TenantStore.OrderGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult OrderDetails(int id)
        {
            var order = TenantStore.GetOrder(CompanyCode, id);
            if (order == null) return ToAction("Orders");
            ViewBag.Customer = TenantStore.GetCustomer(CompanyCode, order.CustomerId);
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult UpdateOrder(int id, StoreOrderStatus status, string trackingCode, string adminNote)
        {
            var order = TenantStore.SetOrderStatus(CompanyCode, id, status, trackingCode, adminNote);
            if (order == null) return ToAction("Orders");
            return Ok("سفارش " + order.OrderNumber + " به‌روز شد.", "OrderDetails", new { id });
        }

        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Customers(GridQuery query)
        {
            var page = TenantStore.CustomerGrid(CompanyCode, query);
            ViewBag.OrderCounts = TenantStore.OrderCounts(CompanyCode, page.Select(c => c.Id));
            return View(page);
        }

        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Reviews(ReviewGridQuery query)
        {
            var page = TenantStore.ReviewGrid(CompanyCode, query);
            ViewBag.Products = TenantStore.GetProductsByIds(CompanyCode, page.Select(r => r.ProductId)).ToDictionary(p => p.Id);
            ViewBag.Query = query;
            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult ToggleReview(int id, bool approved)
        {
            var review = TenantStore.GetReview(CompanyCode, id);
            if (review != null)
            {
                review.IsApproved = approved;
                TenantStore.UpdateReview(CompanyCode, review);
            }
            return BackTo("Reviews");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult DeleteReview(int id)
        {
            TenantStore.DeleteReview(CompanyCode, id);
            return OkBack("نظر حذف شد.", "Reviews");
        }
    }
}
