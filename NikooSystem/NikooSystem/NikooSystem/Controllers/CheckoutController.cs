using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ZarinPalService _zarinPal = new ZarinPalService();

        [HttpGet]
        public ActionResult Buy(int productId)
        {
            var product = JsonStore.GetProduct(productId);
            if (product == null || !product.IsActive)
                return HttpNotFound();

            var model = new CheckoutViewModel { Product = product };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Buy(int productId, CheckoutViewModel model)
        {
            var product = JsonStore.GetProduct(productId);
            if (product == null || !product.IsActive)
                return HttpNotFound();

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

            order = JsonStore.AddOrder(order);

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
                JsonStore.UpdateOrder(order);
                TempData["Error"] = payment.Message;
                return View(model);
            }

            order.Authority = payment.Authority;
            JsonStore.UpdateOrder(order);
            return Redirect(payment.PaymentUrl);
        }

        [HttpGet]
        public async Task<ActionResult> Callback(int? orderId, string Authority, string Status)
        {
            if (orderId == null)
                return RedirectToAction("Failed");

            var order = JsonStore.GetOrder(orderId.Value);
            if (order == null)
                return RedirectToAction("Failed");

            if (!string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = OrderStatus.Failed;
                order.PaymentMessage = "پرداخت توسط کاربر لغو شد یا ناموفق بود.";
                JsonStore.UpdateOrder(order);
                return RedirectToAction("Failed", new { id = order.Id });
            }

            var authority = Authority ?? order.Authority;
            var verify = await _zarinPal.VerifyPaymentAsync(authority, order.Amount);
            if (!verify.Success)
            {
                order.Status = OrderStatus.Failed;
                order.PaymentMessage = verify.Message;
                JsonStore.UpdateOrder(order);
                return RedirectToAction("Failed", new { id = order.Id });
            }

            order.Status = OrderStatus.Paid;
            order.RefId = verify.RefId;
            order.PaidAt = DateTime.Now;
            order.PaymentMessage = verify.Message;
            JsonStore.UpdateOrder(order);

            return RedirectToAction("Success", new { id = order.Id });
        }

        public ActionResult Success(int id)
        {
            var order = JsonStore.GetOrder(id);
            if (order == null) return HttpNotFound();
            return View(order);
        }

        public ActionResult Failed(int? id)
        {
            Order order = null;
            if (id.HasValue)
                order = JsonStore.GetOrder(id.Value);
            return View(order);
        }
    }
}
