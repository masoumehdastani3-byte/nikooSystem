using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    [NoIndex]
    public class CheckoutController : Controller
    {
        private readonly ZarinPalService _zarinPal = new ZarinPalService();

        [HttpGet]
        public ActionResult Buy(int productId, string company = null)
        {
            var product = SiteStore.GetProduct(productId);
            if (product == null || !product.IsActive)
                return HttpNotFound();

            var store = SubscribedStore(company);
            if (store != null)
                return RedirectToAction("Subscription", "ShopAdmin", new { company = store.CompanyCode });
            return View(new CheckoutViewModel { Product = product });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Buy(int productId, CheckoutViewModel model, string company = null)
        {
            var product = SiteStore.GetProduct(productId);
            if (product == null || !product.IsActive)
                return HttpNotFound();

            var store = SubscribedStore(company);
            if (store != null)
                return RedirectToAction("Subscription", "ShopAdmin", new { company = store.CompanyCode });

            model.Product = product;
            if (!ModelState.IsValid)
                return View(model);

            var order = new Order
            {
                ProductId = product.Id,
                ProductTitle = product.Title,
                Amount = product.Price,
                CustomerName = model.CustomerName,
                CustomerMobile = model.CustomerMobile,
                CustomerEmail = model.CustomerEmail,
                CustomerAddress = model.CustomerAddress,
                Notes = model.Notes,
                Status = OrderStatus.PendingPayment
            };

            order = SiteStore.AddOrder(order);

            var payment = await _zarinPal.RequestPaymentAsync(
                order.Amount,
                "درخواست دمو " + order.ProductTitle + " - سفارش " + order.OrderNumber,
                order.CustomerEmail,
                order.CustomerMobile,
                order.Id.ToString());

            if (!payment.Success)
            {
                order.Status = OrderStatus.Failed;
                order.PaymentMessage = payment.Message;
                SiteStore.UpdateOrder(order);
                TempData["Error"] = payment.Message;
                return View(model);
            }

            order.Authority = payment.Authority;
            SiteStore.UpdateOrder(order);
            return Redirect(payment.PaymentUrl);
        }

        [HttpGet]
        public async Task<ActionResult> Callback(int? orderId, string Authority, string Status)
        {
            var order = orderId.HasValue ? SiteStore.GetOrder(orderId.Value) : null;
            if (order == null)
                return RedirectToAction("Failed");
            if (order.Status == OrderStatus.Paid)
                return RedirectToAction("Success", new { id = order.Id });

            var verify = await _zarinPal.VerifyCallbackAsync(order.Authority, Authority, Status, order.Amount);
            if (verify.Forged)
                return RedirectToAction("Failed");
            if (!verify.Success)
            {
                order.Status = OrderStatus.Failed;
                order.PaymentMessage = verify.Message;
                SiteStore.UpdateOrder(order);
                return RedirectToAction("Failed", new { id = order.Id });
            }

            order.Status = OrderStatus.Paid;
            order.RefId = verify.RefId;
            order.PaidAt = DateTime.Now;
            order.PaymentMessage = verify.Message;
            SiteStore.UpdateOrder(order);

            if (!string.IsNullOrEmpty(order.CompanyCode))
                Subscriptions.Apply(order.Id);
            Notifications.SiteOrder(SiteStore.GetOrder(order.Id) ?? order);

            return RedirectToAction("Success", new { id = order.Id });
        }

        /// <summary>A customer's store (not a demo): its subscription is paid from the store's panel.</summary>
        private static Models.Store.TenantInfo SubscribedStore(string company)
        {
            var tenant = TenantStore.GetTenant(company);
            return tenant != null && !TenantStore.IsDemo(tenant.CompanyCode) ? tenant : null;
        }

        public ActionResult Success(int id)
        {
            var order = SiteStore.GetOrder(id);
            if (order == null) return HttpNotFound();
            return View(order);
        }

        public ActionResult Failed(int? id)
        {
            Order order = null;
            if (id.HasValue)
                order = SiteStore.GetOrder(id.Value);
            return View(order);
        }
    }
}
