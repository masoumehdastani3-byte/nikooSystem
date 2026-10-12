using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>«اشتراک و تمدید»: the site's subscription, renewing it for 3, 6 or 12 months, and its payments.</summary>
    public partial class ShopAdminController
    {
        private static readonly HashSet<string> OpenActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Login", "Logout", "Subscription", "RenewSubscription", "SubscriptionPaid",
            "Support", "NewTicket", "Ticket", "ReplyTicket", "TicketImage", "CloseTicket"
        };

        protected override bool OpenWhenClosed(string action) => OpenActions.Contains(action);

        /// <summary>A closed site's panel opens on the renewal page; support stays reachable.</summary>
        protected override ActionResult WhenClosed() => ToAction("Subscription");

        [HttpGet]
        public ActionResult Subscription()
        {
            var product = Subscriptions.ProductFor(Tenant);
            ViewBag.IsDemo = TenantStore.IsDemo(CompanyCode);
            ViewBag.Product = product;
            ViewBag.Plans = Subscriptions.Plans(product);
            ViewBag.RenewFrom = Subscriptions.RenewFrom(Tenant);
            ViewBag.Payments = SiteStore.OrdersForStore(CompanyCode);
            return View(Tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RenewSubscription(int months)
        {
            if (TenantStore.IsDemo(CompanyCode))
                return Fail("سایت‌های نمونه اشتراک ندارند؛ در سایت خودتان از همین صفحه اشتراک را تمدید کنید.", "Subscription");

            var order = Subscriptions.StartRenewal(Tenant, months);
            if (order == null) return Fail("این دوره اشتراک پیدا نشد.", "Subscription");

            var callback = Url.Action("SubscriptionPaid", "ShopAdmin", new { company = CompanyCode }, Request.Url.Scheme);
            var pay = await new ZarinPalService().RequestPaymentAsync(
                order.Amount,
                "اشتراک " + Subscriptions.PeriodName(months) + " سایت " + (CurrentSettings?.StoreName ?? CompanyCode) + " - سفارش " + order.OrderNumber,
                Tenant.OwnerEmail,
                Tenant.OwnerMobile,
                order.Id.ToString(),
                callback);
            if (!pay.Success)
            {
                order.Status = OrderStatus.Failed;
                order.PaymentMessage = pay.Message;
                SiteStore.UpdateOrder(order);
                return Fail("اتصال به درگاه پرداخت انجام نشد: " + pay.Message, "Subscription");
            }
            order.Authority = pay.Authority;
            SiteStore.UpdateOrder(order);
            return Redirect(pay.PaymentUrl);
        }

        /// <summary>The gateway's return; open without the panel session so a lost session cannot lose a payment.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> SubscriptionPaid(int? orderId, string Authority, string Status)
        {
            var order = orderId.HasValue ? SiteStore.GetOrder(orderId.Value) : null;
            if (order == null || order.Months <= 0 || !string.Equals(order.CompanyCode, CompanyCode, StringComparison.OrdinalIgnoreCase))
                return Fail("پرداخت اشتراک پیدا نشد.", "Subscription");
            if (order.Status == OrderStatus.Paid)
                return Ok(Renewed(order), "Subscription");

            var verify = await new ZarinPalService().VerifyCallbackAsync(order.Authority, Authority, Status, order.Amount);
            if (verify.Forged) return Fail("اطلاعات بازگشت از درگاه معتبر نبود.", "Subscription");
            if (!verify.Success)
            {
                order.Status = OrderStatus.Failed;
                order.PaymentMessage = verify.Message;
                SiteStore.UpdateOrder(order);
                return Fail("پرداخت انجام نشد و اشتراک تمدید نشد: " + verify.Message, "Subscription");
            }

            order.Status = OrderStatus.Paid;
            order.RefId = verify.RefId;
            order.PaidAt = DateTime.Now;
            order.PaymentMessage = verify.Message;
            SiteStore.UpdateOrder(order);
            if (Subscriptions.Apply(order.Id))
                NikooSystem.Services.Notifications.SiteOrder(SiteStore.GetOrder(order.Id) ?? order);
            return Ok("پرداخت انجام شد. " + Renewed(SiteStore.GetOrder(order.Id) ?? order), "Subscription");
        }

        private static string Renewed(Order order)
        {
            return "اشتراک " + Subscriptions.PeriodName(order.Months) + " ثبت شد"
                + (order.SubscriptionEndsAt.HasValue ? " و سایت شما تا " + order.SubscriptionEndsAt.Value.ToShamsiText() + " فعال است." : ".");
        }
    }
}
